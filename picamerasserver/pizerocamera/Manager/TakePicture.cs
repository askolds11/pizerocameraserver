using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    public event Action<Guid>? OnPictureChange;

    /// <summary>
    /// Creates request model and MQTT message for take picture request
    /// </summary>
    /// <param name="futureMillis">How far in the future to take the photo</param>
    /// <returns>PictureRequest model and MQTT message</returns>
    private static (PictureRequestModel pictureRequest, MqttApplicationMessage message) CreateRequestModels(
        int futureMillis)
    {
        var uuid = Guid.CreateVersion7();
        var currentTime = DateTimeOffset.Now;
        var pictureTime = currentTime.AddMilliseconds(futureMillis);

        var pictureRequest = new PictureRequestModel
        {
            Uuid = uuid,
            RequestTime = currentTime,
            PictureTime = pictureTime
        };

        CameraRequest takePictureRequest = new CameraRequest.TakePicture(
            pictureTime.ToUnixTimeMilliseconds(),
            uuid
        );

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("temp")
            .WithPayload(Json.Serialize(takePictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        return (pictureRequest, message);
    }

    /// <summary>
    /// Get list of cameras based on criteria for taking pictures
    /// </summary>
    /// <param name="requirePing">Should camera be pingable</param>
    /// <param name="requireDeviceStatus">Should camera have status</param>
    /// <returns>A collection of cameras that a request can be sent to</returns>
    private IEnumerable<PiZeroCamera> GetTakeableCameras(
        bool requirePing = true,
        bool requireDeviceStatus = true
    )
    {
        // Cameras to send request to
        return PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null);
    }

    /// <inheritdoc />
    public async Task<PictureRequestModel> RequestTakePictureAll(int futureMillis)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        var (pictureRequest, message) = CreateRequestModels(futureMillis);

        // must save request to db before requests
        piDbContext.PictureRequests.Add(pictureRequest);
        await piDbContext.SaveChangesAsync();

        var expectedCams = GetTakeableCameras(
            requirePing: false,
            requireDeviceStatus: false
        ).ToList();


        // Send message
        message.Topic = options.CameraTopic;
        var publishResult = await _mqttClient.PublishAsync(message);

        // Add to database
        var dbItems = expectedCams.Select(x => new CameraPictureModel
        {
            CameraId = x.Id, PictureRequestId = pictureRequest.Uuid,
            CameraPictureStatus = publishResult.IsSuccess
                ? CameraPictureStatus.Requested
                : CameraPictureStatus.FailedToRequest
        });

        piDbContext.AddRange(dbItems);
        await piDbContext.SaveChangesAsync();

        await piDbContext.Entry(pictureRequest).Collection(x => x.CameraPictures).LoadAsync();
        return pictureRequest;
    }

    /// <inheritdoc />
    /// TODO: implement form part for millis
    public async Task<PictureRequestModel> RequestTakePictureColumns(int futureMillis, int columnDelayMillis)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        var (pictureRequest, message) = CreateRequestModels(futureMillis);

        // must save request to db before requests
        piDbContext.PictureRequests.Add(pictureRequest);
        await piDbContext.SaveChangesAsync();

        var expectedCams = GetTakeableCameras(
            requirePing: false,
            requireDeviceStatus: false
        ).ToList();

        var columns = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();
        foreach (var column in columns)
        {
            var expectedColumnCams = expectedCams
                .Where(x => x.Id.StartsWith(column))
                .ToList();

            // TODO: Probably delete - maybe we get lucky and get a response anyways

            // If column has no useful cameras, skip it
            // if (expectedColumnCams.Count == 0)
            // {
            //     continue;
            // }

            // Send message to column
            message.Topic = $"{options.CameraTopic}/{column}";
            var publishResult = await _mqttClient.PublishAsync(message);

            // Add to database
            var dbItems = expectedColumnCams.Select(x => new CameraPictureModel
            {
                CameraId = x.Id, PictureRequestId = pictureRequest.Uuid,
                CameraPictureStatus = publishResult.IsSuccess
                    ? CameraPictureStatus.Requested
                    : CameraPictureStatus.FailedToRequest
            });
            piDbContext.AddRange(dbItems);

            // Have to save now, as request was already made.
            await piDbContext.SaveChangesAsync();

            // Allow time for the cameras to send
            if (column != columns.Last())
            {
                await Task.Delay(columnDelayMillis);
            }
        }

        await piDbContext.Entry(pictureRequest).Collection(x => x.CameraPictures).LoadAsync();
        return pictureRequest;
    }

    /// <inheritdoc />
    public async Task ResponseTakePicture(
        MqttApplicationMessage message,
        DateTimeOffset messageReceived,
        CameraResponse.TakePicture takePicture,
        string id
    )
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var successWrapper = takePicture.Response;
        var uuid = successWrapper.Value.Uuid;
        var dbItem = piDbContext.CameraPictures.FirstOrDefault(x => x.PictureRequestId == uuid && x.CameraId == id);
        if (dbItem == null)
        {
            dbItem = new CameraPictureModel { CameraId = id, PictureRequestId = uuid };
            piDbContext.Add(dbItem);
        }

        if (successWrapper.Success)
        {
            if (successWrapper.Value is TakePictureResponse.PictureTaken pictureTaken)
            {
                dbItem.ReceivedTaken = messageReceived;
                dbItem.MonotonicTime = pictureTaken.MonotonicTime;
            }
            else if (successWrapper.Value is TakePictureResponse.PictureSavedOnDevice)
            {
                dbItem.ReceivedSaved = messageReceived;
            }

            (dbItem.CameraPictureStatus, dbItem.StatusMessage) = successWrapper.Value switch
            {
                TakePictureResponse.PictureTaken =>
                    (CameraPictureStatus.Taken, null),
                TakePictureResponse.PictureSavedOnDevice =>
                    (CameraPictureStatus.SavedOnDevice, null),
                _ => (CameraPictureStatus.Unknown, "Unknown Success")
            };
        }
        else
        {
            (dbItem.CameraPictureStatus, dbItem.StatusMessage) = successWrapper.Value switch
            {
                TakePictureResponse.Failure failure => failure switch
                {
                    TakePictureResponse.Failure.Failed failed => (CameraPictureStatus.Failed, failed.Message),
                    TakePictureResponse.Failure.PictureFailedToSave pictureFailedToSave => (
                        CameraPictureStatus.PictureFailedToSave, pictureFailedToSave.Message),
                    TakePictureResponse.Failure.PictureFailedToSchedule pictureFailedToSchedule => (
                        CameraPictureStatus.PictureFailedToSchedule, pictureFailedToSchedule.Message),
                    TakePictureResponse.Failure.PictureFailedToTake pictureFailedToTake => (
                        CameraPictureStatus.PictureFailedToTake, pictureFailedToTake.Message),
                    _ => throw new ArgumentOutOfRangeException(nameof(failure))
                },
                _ => (CameraPictureStatus.Unknown, "Unknown failure")
            };
        }

        await piDbContext.SaveChangesAsync();
        OnPictureChange?.Invoke(uuid);
    }
}