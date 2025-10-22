using System.Text.Json;
using MQTTnet;
using picamerasserver.Database.Models;
using picamerasserver.PiZero.Responses;

namespace picamerasserver.PiZero.SendPicture;

public partial class SendPicture
{
    /// <inheritdoc />
    public async Task ResponseSendPicture(
        MqttApplicationMessage message,
        DateTimeOffset messageReceived,
        CameraResponse.SendPicture sendPicture,
        string id
    )
    {
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();

        var successWrapper = sendPicture.Response;
        var uuid = successWrapper.Value.Uuid;
        // Get the database item, create if it doesn't exist
        var dbItem = piDbContext.CameraPictures.FirstOrDefault(x => x.PictureRequestId == uuid && x.CameraId == id);
        if (dbItem == null)
        {
            dbItem = new CameraPictureModel { CameraId = id, PictureRequestId = uuid };
            piDbContext.Add(dbItem);
        }

        // Send received signal so that next pictures can be sent
        if (successWrapper.Value is SendPictureResponse.PictureSent or SendPictureResponse.Failure)
        {
            if (_sendPictureChannels.TryGetValue(uuid, out var channel))
            {
                channel.Writer.TryWrite(id);
            }
        }

        if (successWrapper.Success)
        {
            if (successWrapper.Value is SendPictureResponse.PictureSent)
            {
                dbItem.ReceivedSent = messageReceived;
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
                var directory = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, uuid.ToString());
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
                        var metadata = Json.TryDeserializeWithOptions<Metadata>(json, logger, jsonOptions);
                        if (metadata.IsSuccess)
                        {
                            var val = metadata.Value;
                            dbItem.SensorTimestamp = val.SensorTimestamp;
                            // TODO: Once all picamera2 is updated to 0.5.2, use nanoseconds
                            try
                            {
                                dbItem.PictureTaken = val.FrameWallClock != null
                                    ? DateTimeOffset.FromUnixTimeMilliseconds(val.FrameWallClock.Value / 1000)
                                    : null;
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                dbItem.PictureTaken = val.FrameWallClock != null
                                    ? DateTimeOffset.FromUnixTimeMilliseconds(val.FrameWallClock.Value / 1_000_000)
                                    : null;
                            }
                            
                            dbItem.FocusFoM = val.FocusFoM;
                            dbItem.AnalogueGain = val.AnalogueGain;
                            dbItem.DigitalGain = val.DigitalGain;
                            dbItem.ExposureTime = val.ExposureTime;
                            dbItem.ColourTemperature = val.ColourTemperature;
                            dbItem.Lux = val.Lux;
                            dbItem.FrameDuration = val.FrameDuration;
                            dbItem.AeState = val.AeState;
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
                    SendPictureResponse.Failure.Failed failed => (CameraPictureStatus.Failed, failed.Message),
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
        changeListener.UpdatePicture(uuid);
    }
}