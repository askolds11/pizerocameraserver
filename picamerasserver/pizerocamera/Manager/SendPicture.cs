using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    /// <summary>
    /// Request to send picture
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    public async Task RequestSendPicture(Guid uuid)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var options = _optionsMonitor.CurrentValue;

        var pictureRequest = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        if (pictureRequest == null)
        {
            throw new Exception("Picture request not found");
        }


        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.CameraTopic)
            .WithPayload(Json.Serialize(sendPictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var publishResult = await _mqttClient.PublishAsync(message);

        var expectedCams = pictureRequest.CameraPictures
            .Where(x => x.CameraPictureStatus == CameraPictureStatus.SavedOnDevice)
            .Select(x => x.CameraId)
            .Select(x => PiZeroCameras[x]);

        foreach (var piZeroCamera in expectedCams)
        {
            // ignore not working cameras
            if (piZeroCamera.Pingable == null || piZeroCamera.Status == null)
            {
                continue;
            }

            // First because it exists based on previous
            var dbItem = pictureRequest.CameraPictures.First(x => x.CameraId == piZeroCamera.Id);
            dbItem.CameraPictureStatus = publishResult.IsSuccess
                ? CameraPictureStatus.RequestedSend
                : CameraPictureStatus.FailedToRequestSend;
            piDbContext.Update(dbItem);
        }

        await piDbContext.SaveChangesAsync();
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();
    }

    /// <summary>
    /// Request to send picture for individual camera
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    /// <param name="cameraId">Camera id of wanted camera</param>
    public async Task RequestSendPictureIndividual(Guid uuid, string cameraId)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var options = _optionsMonitor.CurrentValue;

        var pictureRequest = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        if (pictureRequest == null)
        {
            throw new Exception("Picture request not found");
        }

        var cameraPicture = pictureRequest.CameraPictures
            .FirstOrDefault(x => x.ReceivedSaved != null && x.CameraId == cameraId);
        // Can't do anything if valid picture does not exist.
        if (cameraPicture == null)
        {
            return;
        }

        var piZeroCamera = PiZeroCameras[cameraId];
        // Can't do anything if unreachable
        if (piZeroCamera.Pingable == null || piZeroCamera.Status == null)
        {
            return;
        }

        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic($"{options.CameraTopic}/{cameraId}")
            .WithPayload(Json.Serialize(sendPictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var publishResult = await _mqttClient.PublishAsync(message);


        // First because it exists based on previous
        cameraPicture.CameraPictureStatus = publishResult.IsSuccess
            ? CameraPictureStatus.RequestedSend
            : CameraPictureStatus.FailedToRequestSend;
        piDbContext.Update(cameraPicture);

        await piDbContext.SaveChangesAsync();
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();
    }

    public async Task ResponseSendPicture(MqttApplicationMessage message, CameraResponse.SendPicture sendPicture)
    {
        var timeNow = DateTimeOffset.Now;
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var id = message.Topic.Split('/').Last();

        var successWrapper = sendPicture.Response;
        // todo: value bad
        var uuid = successWrapper.Value.Uuid;

        // todo: async, uuid
        var dbItem = piDbContext.CameraPictures.FirstOrDefault(x => x.PictureRequestId == uuid && x.CameraId == id);
        // TODO: Really null check?
        if (dbItem == null)
        {
            dbItem = new CameraPictureModel { CameraId = id, PictureRequestId = uuid };
            piDbContext.Add(dbItem);
        }

        // if (response.IsSuccess)
        // {
        // var successWrapper = response.Value;
        if (successWrapper.Success)
        {
            if (successWrapper.Value is SendPictureResponse.PictureSent)
            {
                dbItem.ReceivedSent = timeNow;
            }

            (dbItem.CameraPictureStatus, dbItem.StatusMessage) = successWrapper.Value switch
            {
                SendPictureResponse.PictureSent => (CameraPictureStatus.Success, null),
                _ => (CameraPictureStatus.Unknown, "Unknown Success")
            };
        }
        else
        {
            (dbItem.CameraPictureStatus, dbItem.StatusMessage) = successWrapper.Value switch
            {
                SendPictureResponse.Failure failure => failure switch
                {
                    SendPictureResponse.Failure.Failed failed => (CameraPictureStatus.Failed, "Failed"),
                    SendPictureResponse.Failure.PictureFailedToRead pictureFailedToSave => (
                        CameraPictureStatus.PictureFailedToRead, pictureFailedToSave.Message),
                    SendPictureResponse.Failure.PictureFailedToSend pictureFailedToSend => (
                        CameraPictureStatus.PictureFailedToSend, pictureFailedToSend.Message),
                    _ => throw new ArgumentOutOfRangeException(nameof(failure))
                },
                _ => (CameraPictureStatus.Unknown, "Unknown failure")
            };
        }

        // }
        // else
        // {
        //     piZeroCamera.TakePictureRequest = new PiZeroCameraTakePictureRequest.Unknown(response.Error.ToString());
        // }
        await piDbContext.SaveChangesAsync();
        OnPictureChange?.Invoke(uuid);
        OnChange?.Invoke();
        await Task.Yield();
    }
}