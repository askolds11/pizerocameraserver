using System.Diagnostics;
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
            uuid
        );

        // cameras, which are online
        var expectedCams = PiZeroCameras.Values
            .Where(x => x is { Pingable: true, Status: not null })
            .ToList();

        var stopwatch = Stopwatch.StartNew();
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
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic($"{options.CameraTopic}/{column}")
                .WithPayload(Json.Serialize(takePictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            var publishResult = await _mqttClient.PublishAsync(message);

            // Add to database
            var dbItems = expectedColumnCams.Select(x => new CameraPictureModel
            {
                CameraId = x.Id, PictureRequestId = uuid,
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
                // todo: changeable in form
                await Task.Delay(50);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Sending \"Take Picture\" took {StopwatchElapsedMilliseconds} ms, avg {ElapsedMilliseconds} ms per column",
            stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds / 16);

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
            if (successWrapper.Value is TakePictureResponse.PictureTaken pictureTaken)
            {
                dbItem.ReceivedTaken = timeNow;
                dbItem.MonotonicTime = pictureTaken.MonotonicTime;
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