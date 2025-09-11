using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public interface ISendPictureManager
{
    /// <summary>
    /// Is a send operation ongoing?
    /// </summary>
    bool SendActive { get; }

    /// <summary>
    /// Makes requests to send pictures from cameras to server. <br />
    /// Makes requests to <paramref name="maxConcurrentUploads"/> cameras at once.
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    /// <param name="maxConcurrentUploads">How many uploads to do at once</param>
    Task RequestSendPictureChannels(Guid uuid, int maxConcurrentUploads = 3);

    /// <summary>
    /// Request to send picture for individual camera
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    /// <param name="cameraId">Camera id of wanted camera</param>
    Task RequestSendPictureIndividual(Guid uuid, string cameraId);

    /// <summary>
    /// Handle a SendPicture response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="messageReceived">Time when the message was received</param>
    /// <param name="sendPicture">Deserialized SendPicture</param>
    /// <param name="id">Camera's id</param>
    /// <exception cref="ArgumentOutOfRangeException">Unknown failure type</exception>
    Task ResponseSendPicture(MqttApplicationMessage message, DateTimeOffset messageReceived,
        CameraResponse.SendPicture sendPicture, string id);

    /// <summary>
    /// Cancels the ongoing send operation
    /// </summary>
    Task CancelSend();
}

public partial class PiZeroCameraManager : ISendPictureManager
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _sendPictureChannels = new();

    public bool SendActive { get; private set; }
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private CancellationTokenSource? _sendCancellationTokenSource;

    /// <summary>
    /// Get a list of cameras based on criteria for sending pictures
    /// </summary>
    /// <param name="pictureRequest">PictureRequest with cameras</param>
    /// <param name="requirePing">Should the cameras be pingable?</param>
    /// <param name="requireDeviceStatus">Should the cameras have status?</param>
    /// <param name="requireStatus">Should the camera's picture's status be valid?</param>
    /// <returns>A collection of tuples, where each tuple contains a database camera picture model and a PiZeroCamera instance meeting the criteria.</returns>
    private IEnumerable<(CameraPictureModel dbItem, PiZeroCamera dictItem)> GetSendableCameras(
        PictureRequestModel pictureRequest,
        bool requirePing = true,
        bool requireDeviceStatus = true,
        bool requireStatus = true
    )
    {
        var allowedStatuses = new[]
        {
            CameraPictureStatus.SavedOnDevice, CameraPictureStatus.FailedToRequestSend, CameraPictureStatus.FailureSend,
            CameraPictureStatus.PictureFailedToRead, CameraPictureStatus.PictureFailedToSend
        };
        // Cameras to send request to
        return pictureRequest.CameraPictures
            .Where(x => !requireStatus || x.CameraPictureStatus != null &&
                allowedStatuses.Contains((CameraPictureStatus)x.CameraPictureStatus)
            )
            .Select(x => (dbItem: x, dictItem: PiZeroCameras[x.CameraId]))
            .Where(x => !requirePing || x.dictItem.Pingable == true)
            .Where(x => !requireDeviceStatus || x.dictItem.Status != null);
    }

    /// <summary>
    /// Generic set up for a request to send pictures. <br />
    /// Sets up channel, cancellation, try-catch, etc.
    /// </summary>
    /// <param name="uuid"></param>
    /// <param name="tryBlock"></param>
    private async Task RequestSendPictureTryBlock(
        Guid uuid,
        Func<
            (CancellationTokenSource cts,
            PiDbContext dbContext,
            List<(CameraPictureModel dbItem, PiZeroCamera dictItem)> unsentCameras,
            MqttOptions options,
            Channel<string> channel ),
            Task> tryBlock
    )
    {
        // Another send operation is already running
        if (!await _sendSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return;
        }

        // Just in case, cancel existing sends (there shouldn't be any)
        if (_sendCancellationTokenSource != null)
        {
            await _sendCancellationTokenSource.CancelAsync();
        }

        _sendCancellationTokenSource?.Dispose();
        _sendCancellationTokenSource = new CancellationTokenSource();

        List<(CameraPictureModel dbItem, PiZeroCamera dictItem)>? unsentCameras = null;
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync(_sendCancellationTokenSource.Token);
        try
        {
            SendActive = true;

            var options = _optionsMonitor.CurrentValue;

            // Get the request
            var pictureRequest = await piDbContext.PictureRequests
                .Include(x => x.CameraPictures)
                .FirstOrDefaultAsync(x => x.Uuid == uuid, _sendCancellationTokenSource.Token);
            if (pictureRequest == null)
            {
                throw new Exception("Picture request not found");
            }

            unsentCameras = GetSendableCameras(
                pictureRequest,
                requirePing: true,
                requireDeviceStatus: true,
                requireStatus: true
            ).ToList();
            if (unsentCameras.Count == 0)
            {
                return;
            }

            // Create a channel to receive events
            var channel = Channel.CreateUnbounded<string>();
            if (!_sendPictureChannels.TryAdd(uuid, channel))
            {
                throw new Exception("Failed to create channel");
            }

            // Execute the custom logic provided by `tryBlock`
            await tryBlock((_sendCancellationTokenSource, piDbContext, unsentCameras, options, channel));
        }
        catch (OperationCanceledException)
        {
            if (unsentCameras is { Count: > 0 })
            {
                foreach (var (dbItem, _) in unsentCameras)
                {
                    dbItem.CameraPictureStatus = CameraPictureStatus.Cancelled;
                    piDbContext.Update(dbItem);
                }

                await piDbContext.SaveChangesAsync();
            }
        }
        finally
        {
            // Remove the channel and clean up
            _sendPictureChannels.TryRemove(uuid, out _);

            _sendCancellationTokenSource.Dispose();
            _sendCancellationTokenSource = null;
            // Release semaphore
            _sendSemaphore.Release();
            // Update UI
            SendActive = false;
            OnPictureChange?.Invoke(uuid);
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async Task RequestSendPictureChannels(Guid uuid, int maxConcurrentUploads)
    {
        await RequestSendPictureTryBlock(uuid, async context =>
        {
            var (cts, piDbContext, unsentCameras, options, channel) = context;

            // Queue for sending requests
            var cameraQueue = new Queue<(CameraPictureModel dbItem, PiZeroCamera dictItem)>(unsentCameras);

            // Make a message
            CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
                uuid
            );
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic("temp")
                .WithPayload(Json.Serialize(sendPictureRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            // Start first transfers (maxConcurrent)
            for (var i = 0; i < maxConcurrentUploads; i++)
            {
                await SendMessage(piDbContext);
                // Stop early if there are no more cameras available
                if (cameraQueue.Count == 0)
                {
                    break;
                }
            }

            await piDbContext.SaveChangesAsync(cts.Token);
            // Update UI
            OnPictureChange?.Invoke(uuid);
            await Task.Yield();
            
            // Wait for messages
            while (await channel.Reader.WaitToReadAsync(cts.Token))
            {
                // Pop message
                if (channel.Reader.TryRead(out var cameraId))
                {
                    var camera = unsentCameras.First(x => x.dbItem.CameraId == cameraId);
                    unsentCameras.Remove(camera);
                }

                // Send request if there are cameras left to send
                if (cameraQueue.Count > 0)
                {
                    await SendMessage(piDbContext);
                    
                    await piDbContext.SaveChangesAsync(cts.Token);
                    // Update UI
                    OnPictureChange?.Invoke(uuid);
                    await Task.Yield();
                }

                // If no more cameras, complete the channel and break out
                // Need break because, according to docs, Read could still run, if it's quick enough.
                if (unsentCameras.Count == 0)
                {
                    channel.Writer.Complete();
                    break;
                }
            }

            return;

            // Send a message to a camera
            async Task SendMessage(PiDbContext localPiDbContext)
            {
                var (dbItem, piZeroCamera) = cameraQueue.Dequeue();

                message.Topic = $"{options.CameraTopic}/{piZeroCamera.Id}";
                var publishResult = await _mqttClient.PublishAsync(message, cts.Token);

                // Update statuses
                dbItem.CameraPictureStatus = publishResult.IsSuccess
                    ? CameraPictureStatus.RequestedSend
                    : CameraPictureStatus.FailedToRequestSend;
                localPiDbContext.Update(dbItem);
            }
        });
    }

    /// <inheritdoc />
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
        // Can't do anything if a valid picture does not exist.
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

    /// <inheritdoc />
    public async Task ResponseSendPicture(
        MqttApplicationMessage message,
        DateTimeOffset messageReceived,
        CameraResponse.SendPicture sendPicture,
        string id
    )
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

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
                            dbItem.PictureTaken = val.FrameWallClock != null
                                ? DateTimeOffset.FromUnixTimeMilliseconds(val.FrameWallClock.Value / 1000)
                                : null;
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
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();
    }

    /// <inheritdoc />
    public async Task CancelSend()
    {
        if (_sendCancellationTokenSource != null)
        {
            await _sendCancellationTokenSource.CancelAsync();
        }

        if (SendActive)
        {
            await CancelCameraTasks();
        }
    }
}