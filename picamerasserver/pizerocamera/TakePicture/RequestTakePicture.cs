using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.pizerocamera.TakePicture;

public partial class TakePicture
{
    /// <summary>
    /// Creates request model and MQTT message for take picture request
    /// </summary>
    /// <remarks>Message is not set!</remarks>
    /// <param name="futureMillis">How far in the future to take the photo</param>
    /// <param name="pictureRequestType">Type for picture</param>
    /// <param name="pictureSetUId">UUID for a picture set</param>
    /// <returns>PictureRequest model and MQTT message</returns>
    private (PictureRequestModel pictureRequest, MqttApplicationMessage message) CreateRequestModels(
        int futureMillis,
        PictureRequestType pictureRequestType,
        Guid? pictureSetUId
    )
    {
        var latestSync = syncPayloadService.GetLatest();
        
        var uuid = Guid.CreateVersion7();
        var currentTime = DateTimeOffset.UtcNow;

        DateTimeOffset pictureTime;
        if (latestSync != null)
        {
            // If the packets are synced, make the picture time a bit after a frame's start
            // Sync info
            var frameTimestampMicros = latestSync.Value.WallClockFrameTimestamp;
            var frameDurationMicros = latestSync.Value.FrameDuration;
            
            // Planned time
            var futureTimeMicros = (ulong) currentTime.AddMilliseconds(futureMillis).ToUnixTimeMilliseconds() * 1_000;
            
            // Get the difference between planned time and current frame start time
            var diff = futureTimeMicros > frameTimestampMicros ? futureTimeMicros - frameTimestampMicros : (ulong) futureMillis * 1_000;
            
            // Number of frames such that their total time is at least as long as the difference
            var n = (long)Math.Ceiling(diff / (double)frameDurationMicros);
            
            // Get the closest frame start time to the planned time
            var closestMicros = frameTimestampMicros + (ulong)(n * frameDurationMicros);

            // Create the DateTimeOffset. Add extra time to be a bit after the frame start time.
            var epochTime = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            pictureTime = epochTime.AddTicks((long) closestMicros * 10 + 5_000 * 10);
        }
        else
        {
            // If no sync info, just add the time
            pictureTime = currentTime.AddMilliseconds(futureMillis);
        }

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
        bool requirePing,
        bool requireDeviceStatus
    )
    {
        // Cameras to send request to
        return piZeroCameraManager.PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null);
    }

    /// <inheritdoc />
    public async Task<Result<PictureRequestModel>> RequestTakePictureAll(PictureRequestType pictureRequestType,
        Guid? pictureSetUId, int futureMillis)
    {
        // Another take picture operation is already running
        if (!await _takePictureSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return Result.Failure<PictureRequestModel>("Another take picture operation is already running");
        }

        // Just in case, cancel existing take picture operations (there shouldn't be any)
        if (_takePictureCancellationTokenSource != null)
        {
            await _takePictureCancellationTokenSource.CancelAsync();
        }

        _takePictureCancellationTokenSource?.Dispose();
        _takePictureCancellationTokenSource = new CancellationTokenSource();

        var cts = _takePictureCancellationTokenSource;
        var cancellationToken = _takePictureCancellationTokenSource.Token;

        bool? success = null;
        PictureRequestModel? pictureRequest = null;
        List<CameraPictureModel>? cameraPictureModels = null;
        try
        {
            TakePictureActive = true;
            await using var piDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var options = optionsMonitor.CurrentValue;

            (pictureRequest, var message) = CreateRequestModels(futureMillis, pictureRequestType, pictureSetUId);

            // must save request to db before requests
            piDbContext.PictureRequests.Add(pictureRequest);
            await piDbContext.SaveChangesAsync(cancellationToken);

            var expectedCams = GetTakeableCameras(
                requirePing: true,
                requireDeviceStatus: true
            ).ToList();

            // Create channels
            var takeChannel = Channel.CreateUnbounded<string>();
            if (!_takePictureChannels.TryAdd(pictureRequest.Uuid, takeChannel))
            {
                throw new Exception("Failed to create take picture channel");
            }

            var sendChannel = Channel.CreateUnbounded<string>();
            if (!_savePictureChannels.TryAdd(pictureRequest.Uuid, sendChannel))
            {
                throw new Exception("Failed to create send picture channel");
            }

            // Send the message
            message.Topic = options.CameraTopic;
            var publishResult = await mqttClient.PublishAsync(message, cancellationToken);
            success = publishResult.IsSuccess;

            // Add to the database
            cameraPictureModels = expectedCams.Select(x => new CameraPictureModel
            {
                CameraId = x.Id, PictureRequestId = pictureRequest.Uuid,
                CameraPictureStatus = publishResult.IsSuccess
                    ? CameraPictureStatus.Requested
                    : CameraPictureStatus.FailedToRequest,
                Requested = DateTimeOffset.UtcNow,
                NtpErrorMillis = x.LastNtpErrorMillis
            }).ToList();

            piDbContext.AddRange(cameraPictureModels);
            // Not cancellable if the message is successfully sent.
            await piDbContext.SaveChangesAsync(CancellationToken.None);

            await piDbContext
                .Entry(pictureRequest)
                .Collection(x => x.CameraPictures)
                .LoadAsync(publishResult.IsSuccess ? CancellationToken.None : cancellationToken);

            // Handle cancellation inside the task
            _ = Task.Run(
                async () =>
                {
                    await HandleResponses(
                        takeChannel,
                        sendChannel,
                        pictureRequest,
                        _takePictureSemaphore,
                        cts
                    );
                },
                CancellationToken.None
            );
        }
        // No finally block as disposing only needs to happen here if before the "background" task is run
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                logger.LogError(e, "Failed to take picture");
            }

            var isCancelled = e is OperationCanceledException;
            // Remove the channels
            if (pictureRequest != null)
            {
                _takePictureChannels.TryRemove(pictureRequest.Uuid, out _);
                _savePictureChannels.TryRemove(pictureRequest.Uuid, out _);
            }

            // Update statuses
            await using var piDbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            if (pictureRequest != null && cameraPictureModels is { Count: > 0 })
            {
                // Update using ExecuteUpdate so that other changes are not lost
                var updateableIds = cameraPictureModels.Select(x => x.CameraId).ToList();
                await piDbContext.CameraPictures
                    .Where(x => x.PictureRequestId == pictureRequest.Uuid && updateableIds.Contains(x.CameraId))
                    .ExecuteUpdateAsync(x => x.SetProperty(
                        b => b.CameraPictureStatus, isCancelled
                            ? CameraPictureStatus.Cancelled
                            : CameraPictureStatus.FailedToRequest),
                        CancellationToken.None
                    );
            }

            _takePictureCancellationTokenSource.Dispose();
            _takePictureCancellationTokenSource = null;
            _takePictureSemaphore.Release();
            TakePictureActive = false;

            return success switch
            {
                // Message was sent, bad
                true => Result.Failure<PictureRequestModel>(isCancelled
                    ? "Take picture operation was cancelled, but the message was sent"
                    : "Message was sent, but something failed"),
                // Message was not sent
                null => Result.Failure<PictureRequestModel>(isCancelled
                    ? "Take picture operation was cancelled, message was not sent"
                    : "Take picture operation failed, message was not sent"),
                // Message failed to send, have to set IsActive to false, if saved to db
                false => Result.Failure<PictureRequestModel>(isCancelled
                    ? "Take picture operation was cancelled, message failed to send"
                    : "Take picture operation failed, message failed to send")
            };
        }

        if (pictureSetUId != null)
        {
            // No point updating the picture, unless there is a picture set, because it does not exist elsewhere yet
            changeListener.UpdatePicture(pictureRequest.Uuid);
            changeListener.UpdatePictureSet((Guid)pictureSetUId);
        }

        return pictureRequest;
    }

    /// <summary>
    /// Handles responses for taking and saving pictures
    /// </summary>
    /// <param name="takeChannel">Channel for taken pictures</param>
    /// <param name="saveChannel">Channel for saved pictures</param>
    /// <param name="semaphore">Semaphore for taking pictures</param>
    /// <param name="cts">Cancellation token source</param>
    /// <param name="pictureRequest">Corresponding picture request</param>
    private async Task HandleResponses(
        Channel<string> takeChannel,
        Channel<string> saveChannel,
        PictureRequestModel pictureRequest,
        SemaphoreSlim semaphore,
        CancellationTokenSource cts
    )
    {
        List<string> takeCameras = [];
        List<string> saveCameras = [];
        try
        {
            // Do ToList separately, so that functions get separate instances
            var cameras = pictureRequest.CameraPictures.Select(x => x.CameraId);
            // ReSharper disable once PossibleMultipleEnumeration
            takeCameras = cameras.ToList();
            // ReSharper disable once PossibleMultipleEnumeration
            saveCameras = cameras.ToList();
            var takeTask = HandleChannelResponses(takeChannel, takeCameras, cts.Token);
            var saveTask = HandleChannelResponses(saveChannel, saveCameras, cts.Token);
            await Task.WhenAll(takeTask, saveTask);
        }
        finally
        {
            // var isCancelled = e is OperationCanceledException;
            // Remove the channels
            _takePictureChannels.TryRemove(pictureRequest.Uuid, out _);
            _savePictureChannels.TryRemove(pictureRequest.Uuid, out _);

            // Update statuses
            await using var piDbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            var cancelledCameraIds = takeCameras.Union(saveCameras).ToList();
            if (cancelledCameraIds is { Count: > 0 })
            {
                // Update using ExecuteUpdate so that other changes are not lost
                await piDbContext.CameraPictures
                    .Where(x => x.PictureRequestId == pictureRequest.Uuid && cancelledCameraIds.Contains(x.CameraId))
                    .ExecuteUpdateAsync(x => x.SetProperty(
                            b => b.CameraPictureStatus, CameraPictureStatus.Cancelled),
                        CancellationToken.None
                    );
            }

            cts.Dispose();
            _takePictureCancellationTokenSource = null;
            semaphore.Release();
            TakePictureActive = false;

            // Update UI
            if (pictureRequest.PictureSetId != null)
            {
                changeListener.UpdatePictureSet((Guid)pictureRequest.PictureSetId);
            }

            changeListener.UpdatePicture(pictureRequest.Uuid);
        }
    }

    /// <summary>
    /// Handles responses for a channel until no more responses are expected or cancellation.
    /// </summary>
    /// <param name="channel">Channel for responses</param>
    /// <param name="sentCameras">Cameras that responses are expected for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private static async Task HandleChannelResponses(
        Channel<string> channel,
        List<string> sentCameras,
        CancellationToken cancellationToken
    )
    {
        // Wait for messages
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            // Pop message
            if (channel.Reader.TryRead(out var cameraId))
            {
                // Use FirstOrDefault in case a non-pinged camera responds
                var camera = sentCameras.FirstOrDefault(x => x == cameraId);
                if (camera != null)
                {
                    sentCameras.Remove(camera);
                }
            }

            // If no more cameras, complete the channel and break out
            // Need break because, according to docs, Read could still run, if it's quick enough.
            if (sentCameras.Count == 0)
            {
                channel.Writer.Complete();
                return;
            }
        }
    }
}