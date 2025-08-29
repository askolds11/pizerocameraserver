using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public interface ITakePictureManager
{
    /// <summary>
    /// Makes requests to take pictures. <br />
    /// Makes requests to all cameras at once.
    /// </summary>
    /// <param name="pictureRequestType">Type of picture</param>
    /// <param name="pictureSetUId">UUID of the picture set, if exists</param>
    /// <param name="futureMillis">How far in the future to take the photo</param>
    /// <returns>The resulting request model with cameras</returns>
    Task<PictureRequestModel> RequestTakePictureAll(PictureRequestType pictureRequestType = PictureRequestType.Other,
        Guid? pictureSetUId = null, int futureMillis = 1000);

    /// <summary>
    /// Handle a TakePicture response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="messageReceived">Time when the message was received</param>
    /// <param name="takePicture">Deserialized TakePicture</param>
    /// <param name="id">Camera's id</param>
    /// <exception cref="ArgumentOutOfRangeException">Unknown failure type</exception>
    Task ResponseTakePicture(MqttApplicationMessage message, DateTimeOffset messageReceived,
        CameraResponse.TakePicture takePicture, string id);
}

public partial class PiZeroCameraManager : ITakePictureManager
{
    public event Action<Guid>? OnPictureChange;

    /// <summary>
    /// Creates request model and MQTT message for take picture request
    /// </summary>
    /// <remarks>Message is not set!</remarks>
    /// <param name="futureMillis">How far in the future to take the photo</param>
    /// <param name="pictureRequestType">Type for picture</param>
    /// <param name="pictureSetUId">UUID for a picture set</param>
    /// <returns>PictureRequest model and MQTT message</returns>
    private static (PictureRequestModel pictureRequest, MqttApplicationMessage message) CreateRequestModels(
        int futureMillis,
        PictureRequestType pictureRequestType,
        Guid? pictureSetUId
    )
    {
        var uuid = Guid.CreateVersion7();
        var currentTime = DateTimeOffset.UtcNow;
        var pictureTime = currentTime.AddMilliseconds(futureMillis);

        var pictureRequest = new PictureRequestModel
        {
            Uuid = uuid,
            RequestTime = currentTime,
            PictureTime = pictureTime,
            PictureRequestType = pictureRequestType,
            IsActive = true,
            PictureSetId = pictureSetUId
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
    /// Get a list of cameras based on criteria for taking pictures
    /// </summary>
    /// <param name="requirePing">Should a camera be pingable?</param>
    /// <param name="requireDeviceStatus">Should a camera have status?</param>
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
    public async Task<PictureRequestModel> RequestTakePictureAll(PictureRequestType pictureRequestType,
        Guid? pictureSetUId, int futureMillis)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        var (pictureRequest, message) = CreateRequestModels(futureMillis, pictureRequestType, pictureSetUId);

        // must save request to db before requests
        piDbContext.PictureRequests.Add(pictureRequest);
        await piDbContext.SaveChangesAsync();

        var expectedCams = GetTakeableCameras(
            requirePing: true,
            requireDeviceStatus: true
        ).ToList();


        // Send the message
        message.Topic = options.CameraTopic;
        var publishResult = await _mqttClient.PublishAsync(message);

        // Add to the database
        var dbItems = expectedCams.Select(x => new CameraPictureModel
        {
            CameraId = x.Id, PictureRequestId = pictureRequest.Uuid,
            CameraPictureStatus = publishResult.IsSuccess
                ? CameraPictureStatus.Requested
                : CameraPictureStatus.FailedToRequest,
            Requested = DateTimeOffset.UtcNow,
            NtpErrorMillis = x.LastNtpErrorMillis
        });

        piDbContext.AddRange(dbItems);
        await piDbContext.SaveChangesAsync();

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
        var successWrapper = takePicture.Response;
        var uuid = successWrapper.Value.Uuid;
        
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var dbItem = piDbContext.CameraPictures.FirstOrDefault(x => x.PictureRequestId == uuid && x.CameraId == id);
        // Just in case create a database item if it does not exist
        if (dbItem == null)
        {
            dbItem = new CameraPictureModel
                { CameraId = id, PictureRequestId = uuid };
            piDbContext.Add(dbItem);
        }

        // Handle times
        switch (successWrapper.Value)
        {
            case TakePictureResponse.PictureTaken pictureTaken:
                dbItem.PictureRequestReceived =
                    DateTimeOffset.FromUnixTimeMilliseconds(pictureTaken.MessageReceivedNanos / 1000000);
                dbItem.WaitTimeNanos = pictureTaken.WaitTimeNanos;
                break;
            case TakePictureResponse.Failure.PictureFailedToSchedule failedToSchedule:
                dbItem.PictureRequestReceived =
                    DateTimeOffset.FromUnixTimeMilliseconds(failedToSchedule.MessageReceivedNanos / 1000000);
                dbItem.WaitTimeNanos = failedToSchedule.WaitTimeNanos;
                break;
            case TakePictureResponse.Failure.PictureFailedToTake failedToTake:
                dbItem.PictureRequestReceived =
                    DateTimeOffset.FromUnixTimeMilliseconds(failedToTake.MessageReceivedNanos / 1000000);
                dbItem.WaitTimeNanos = failedToTake.WaitTimeNanos;
                break;
        }

        if (successWrapper.Success)
        {
            switch (successWrapper.Value)
            {
                case TakePictureResponse.PictureTaken pictureTaken:
                    dbItem.ReceivedTaken = messageReceived;
                    dbItem.MonotonicTime = pictureTaken.MonotonicTime;
                    break;
                case TakePictureResponse.PictureSavedOnDevice:
                    dbItem.ReceivedSaved = messageReceived;
                    break;
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