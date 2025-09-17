using System.Collections.Concurrent;
using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
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
    Task<Result<PictureRequestModel>> RequestTakePictureAll(
        PictureRequestType pictureRequestType = PictureRequestType.Other,
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

    /// <summary>
    /// Cancels the ongoing take picture operation
    /// </summary>
    Task CancelTakePicture();
}

public partial class PiZeroCameraManager : ITakePictureManager
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _takePictureChannels = new();
    private readonly ConcurrentDictionary<Guid, Channel<string>> _savePictureChannels = new();
    public bool TakePictureActive { get; private set; }
    private readonly SemaphoreSlim _takePictureSemaphore = new(1, 1);
    private CancellationTokenSource? _takePictureCancellationTokenSource;

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
            await using var piDbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var options = _optionsMonitor.CurrentValue;

            (pictureRequest, var message) = CreateRequestModels(futureMillis, pictureRequestType, pictureSetUId);

            // must save request to db before requests
            piDbContext.PictureRequests.Add(pictureRequest);
            await piDbContext.SaveChangesAsync(cancellationToken);

            var expectedCams = GetTakeableCameras(
                requirePing: true,
                requireDeviceStatus: true
            ).ToList();

            // Create channels
            // TODO: Throw and return in exception if channels cannot be created
            var takeChannel = Channel.CreateUnbounded<string>();
            if (!_takePictureChannels.TryAdd(pictureRequest.Uuid, takeChannel))
            {
                return Result.Failure<PictureRequestModel>("Failed to create take picture channel");
            }

            var sendChannel = Channel.CreateUnbounded<string>();
            if (!_savePictureChannels.TryAdd(pictureRequest.Uuid, sendChannel))
            {
                return Result.Failure<PictureRequestModel>("Failed to create save picture channel");
            }

            // Send the message
            message.Topic = options.CameraTopic;
            var publishResult = await _mqttClient.PublishAsync(message, cancellationToken);
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
            await piDbContext.SaveChangesAsync(publishResult.IsSuccess ? CancellationToken.None : cancellationToken);

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
                _logger.LogError(e, "Failed to take picture");
            }

            var isCancelled = e is OperationCanceledException;
            // Remove the channels
            if (pictureRequest != null)
            {
                _takePictureChannels.TryRemove(pictureRequest.Uuid, out _);
                _savePictureChannels.TryRemove(pictureRequest.Uuid, out _);
            }

            // Update statuses
            await using var piDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            if (cameraPictureModels is { Count: > 0 })
            {
                foreach (var dbItem in cameraPictureModels)
                {
                    dbItem.CameraPictureStatus = isCancelled
                        ? CameraPictureStatus.Cancelled
                        : CameraPictureStatus.FailedToRequest;
                    piDbContext.Update(dbItem);
                }

                await piDbContext.SaveChangesAsync(CancellationToken.None);
            }

            // Set IsActive to false if saved to db
            if (pictureRequest != null)
            {
                await piDbContext.PictureRequests.Where(x => x.Uuid == pictureRequest.Uuid)
                    .ExecuteUpdateAsync(x => x.SetProperty(
                        b => b.IsActive,
                        false), cancellationToken: CancellationToken.None);
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
            _changeListener.UpdatePicture(pictureRequest.Uuid);
            _changeListener.UpdatePictureSet((Guid)pictureSetUId);
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
            await using var piDbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
            var cancelledCameraIds = takeCameras.Union(saveCameras).ToList();
            var cancelledCameras = pictureRequest.CameraPictures
                .Where(x => cancelledCameraIds.Contains(x.CameraId))
                .ToList();
            if (cancelledCameras is { Count: > 0 })
            {
                foreach (var dbItem in cancelledCameras)
                {
                    dbItem.CameraPictureStatus = CameraPictureStatus.Cancelled;
                    piDbContext.Update(dbItem);
                }

                await piDbContext.SaveChangesAsync(CancellationToken.None);

                // Set IsActive to false if saved to db
                await piDbContext.PictureRequests.Where(x => x.Uuid == pictureRequest.Uuid)
                    .ExecuteUpdateAsync(x => x.SetProperty(
                        b => b.IsActive,
                        false), cancellationToken: CancellationToken.None);
            }

            cts.Dispose();
            _takePictureCancellationTokenSource = null;
            semaphore.Release();
            TakePictureActive = false;

            // Update UI
            if (pictureRequest.PictureSetId != null)
            {
                _changeListener.UpdatePictureSet((Guid)pictureRequest.PictureSetId);
            }

            _changeListener.UpdatePicture(pictureRequest.Uuid);
        }
    }

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
        _changeListener.UpdatePicture(uuid);
    }

    /// <inheritdoc />
    public async Task CancelTakePicture()
    {
        if (TakePictureActive)
        {
            await CancelCameraTasks();
        }

        if (_takePictureCancellationTokenSource != null)
        {
            await _takePictureCancellationTokenSource.CancelAsync();
        }
    }
}