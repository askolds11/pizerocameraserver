using System.Text.Json;
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

        var expectedCams = pictureRequest.CameraPictures
            .Where(x => x.CameraPictureStatus == CameraPictureStatus.SavedOnDevice)
            .Select(x => x.CameraId)
            .Select(x => PiZeroCameras[x])
            .ToList();

        var columns = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();

        foreach (var column in columns)
        {
            var expectedColumnCams = expectedCams
                .Where(x => x is { Pingable: true, Status: not null })
                .Where(x => x.Id.StartsWith(column))
                .ToList();

            // If column has no useful cameras, skip it
            if (expectedColumnCams.Count == 0)
            {
                continue;
            }

            // Send message to column
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic($"{options.CameraTopic}/{column}")
                .WithPayload(Json.Serialize(sendPictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            var publishResult = await _mqttClient.PublishAsync(message);

            // Update statuses
            foreach (var piZeroCamera in expectedColumnCams)
            {
                // First because it exists based on previous
                var dbItem = pictureRequest.CameraPictures.First(x => x.CameraId == piZeroCamera.Id);
                dbItem.CameraPictureStatus = publishResult.IsSuccess
                    ? CameraPictureStatus.RequestedSend
                    : CameraPictureStatus.FailedToRequestSend;
                piDbContext.Update(dbItem);
            }

            await piDbContext.SaveChangesAsync();
            // Update UI
            OnPictureChange?.Invoke(uuid);
            await Task.Yield();

            // Allow time for the cameras to send
            if (column != columns.Last())
            {
                await Task.Delay(10000);
            }
        }
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
    public async Task ResponseSendPicture(
        MqttApplicationMessage message,
        CameraResponse.SendPicture sendPicture,
        string id
    )
    {
        var timeNow = DateTimeOffset.Now;
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var successWrapper = sendPicture.Response;
        var uuid = successWrapper.Value.Uuid;
        var dbItem = piDbContext.CameraPictures.FirstOrDefault(x => x.PictureRequestId == uuid && x.CameraId == id);
        if (dbItem == null)
        {
            dbItem = new CameraPictureModel { CameraId = id, PictureRequestId = uuid };
            piDbContext.Add(dbItem);
        }
        
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
            
            // Fill data on picture taken
            // TODO: Generate filename in one
            // var metadataFileName = image.FileName.Split('.').First() + "_metadata.json";
            if (successWrapper.Value is SendPictureResponse.PictureSent)
            {
                var directory = Path.Combine(_dirOptionsMonitor.CurrentValue.UploadDirectory, uuid.ToString());
                if (Directory.Exists(directory))
                {
                    var filePath = Path.Combine(directory, $"{uuid}_{id}_metadata.json");
                    if (File.Exists(filePath))
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var jsonOptions = new JsonSerializerOptions(Json.GetDefaultOptions())
                        {
                            PropertyNamingPolicy = null,
                            Converters = { new MetadataConverter() }
                        };
                        var metadata = Json.TryDeserializeWithOptions<Metadata>(json, _logger, jsonOptions);
                        if (metadata.IsSuccess)
                        {
                            var val = metadata.Value;
                            dbItem.SensorTimestamp = val.SensorTimestamp;
                            dbItem.PictureTaken = val.FrameWallClock != null ? DateTimeOffset.FromUnixTimeMilliseconds(val.FrameWallClock.Value / 1000) : null;
                            dbItem.FocusFoM = val.FocusFoM;
                            dbItem.AnalogueGain = val.AnalogueGain;
                            dbItem.DigitalGain = val.DigitalGain;
                            dbItem.ExposureTime = val.ExposureTime;
                            dbItem.ColourTemperature = val.ColourTemperature;
                            dbItem.Lux = val.Lux;
                        }
                    }
                }
            }
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
        
        await piDbContext.SaveChangesAsync();
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();
    }
}