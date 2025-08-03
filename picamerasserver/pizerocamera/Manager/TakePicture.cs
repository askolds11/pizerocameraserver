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
    /// Request to take a picture
    /// </summary>
    public async Task<PictureRequestModel> RequestTakePicture()
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var uuid = Guid.CreateVersion7();
        var currentTime = DateTimeOffset.Now;
        // todo: changeable in form
        var pictureTime = currentTime.AddMilliseconds(2000);

        var pictureRequest = new PictureRequestModel
        {
            Uuid = uuid,
            RequestTime = currentTime,
            PictureTime = pictureTime
        };

        piDbContext.PictureRequests.Add(pictureRequest);
        // must save before requests
        await piDbContext.SaveChangesAsync();

        var options = _optionsMonitor.CurrentValue;

        CameraRequest takePictureRequest = new CameraRequest.TakePicture(
            pictureTime.ToUnixTimeMilliseconds(),
            uuid);

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.CameraTopic)
            .WithPayload(Json.Serialize(takePictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var publishResult = await _mqttClient.PublishAsync(message);

        foreach (var piZeroCamera in PiZeroCameras.Values)
        {
            // ignore not working cameras
            if (piZeroCamera.Pingable == null || piZeroCamera.Status == null)
            {
                continue;
            }

            var dbItem = new CameraPictureModel
            {
                CameraId = piZeroCamera.Id, PictureRequestId = uuid,
                CameraPictureStatus = publishResult.IsSuccess
                    ? CameraPictureStatus.Requested
                    : CameraPictureStatus.FailedToRequest
            };
            piDbContext.Add(dbItem);
        }

        await piDbContext.SaveChangesAsync();
        await piDbContext.Entry(pictureRequest).Collection(x => x.CameraPictures).LoadAsync();
        return pictureRequest;
    }

    public async Task ResponseTakePicture(MqttApplicationMessage message, CameraResponse.TakePicture takePicture)
    {
        var timeNow = DateTimeOffset.Now;
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var id = message.Topic.Split('/').Last();

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
            if (successWrapper.Value is TakePictureResponse.PictureTaken)
            {
                dbItem.ReceivedTaken = timeNow;
            }
            else if (successWrapper.Value is TakePictureResponse.PictureSavedOnDevice)
            {
                dbItem.ReceivedSaved = timeNow;
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
                    TakePictureResponse.Failure.Failed failed => (CameraPictureStatus.Failed, "Failed"),
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