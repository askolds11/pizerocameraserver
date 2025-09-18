using MQTTnet;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.TakePicture;

public partial class TakePicture
{
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

        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();

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

        // Inform channels
        switch (successWrapper.Value)
        {
            case TakePictureResponse.PictureTaken:
            {
                if (_takePictureChannels.TryGetValue(uuid, out var channel))
                {
                    channel.Writer.TryWrite(id);
                }

                break;
            }
            case TakePictureResponse.PictureSavedOnDevice:
            {
                if (_savePictureChannels.TryGetValue(uuid, out var channel))
                {
                    channel.Writer.TryWrite(id);
                }

                break;
            }
            default:
            {
                if (_takePictureChannels.TryGetValue(uuid, out var takeChannel))
                {
                    takeChannel.Writer.TryWrite(id);
                }

                if (_savePictureChannels.TryGetValue(uuid, out var saveChannel))
                {
                    saveChannel.Writer.TryWrite(id);
                }

                break;
            }
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
        changeListener.UpdatePicture(uuid);
    }
}