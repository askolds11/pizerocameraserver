using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _sendPictureChannels = new();
    
    /// <summary>
    /// Get list of cameras based on criteria for sending pictures
    /// </summary>
    /// <param name="pictureRequest">PictureRequest with cameras</param>
    /// <param name="requirePing">Should camera be pingable</param>
    /// <param name="requireDeviceStatus">Should camera have status</param>
    /// <param name="requireStatus">Should the camera's picture's status be valid</param>
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

    /// <inheritdoc />
    public async Task RequestSendPictureAll(Guid uuid)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        // Get the request
        var pictureRequest = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        if (pictureRequest == null)
        {
            throw new Exception("Picture request not found");
        }

        // Make message
        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic($"{options.CameraTopic}")
            .WithPayload(Json.Serialize(sendPictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var expectedCams = GetSendableCameras(
            pictureRequest,
            requirePing: false,
            requireDeviceStatus: false,
            requireStatus: false
        ).ToList();
        if (expectedCams.Count == 0)
        {
            return;
        }

        // Send message to column
        var publishResult = await _mqttClient.PublishAsync(message);

        // Update statuses
        foreach (var (dbItem, _) in expectedCams)
        {
            // First because it exists based on previous
            dbItem.CameraPictureStatus = publishResult.IsSuccess
                ? CameraPictureStatus.RequestedSend
                : CameraPictureStatus.FailedToRequestSend;
            piDbContext.Update(dbItem);
        }

        await piDbContext.SaveChangesAsync();
        // Update UI
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();
    }

    /// <inheritdoc />
    public async Task RequestSendPictureColumns(Guid uuid, int columnDelayMillis)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        // Get the request
        var pictureRequest = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        if (pictureRequest == null)
        {
            throw new Exception("Picture request not found");
        }

        // Make message
        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("temp")
            .WithPayload(Json.Serialize(sendPictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var expectedCams = GetSendableCameras(
            pictureRequest,
            requirePing: false,
            requireDeviceStatus: false,
            requireStatus: false
        ).ToList();

        var columns = Enumerable.Range('A', 16).Select(c => ((char)c).ToString()).ToList();
        foreach (var column in columns)
        {
            var expectedColumnCams = expectedCams
                .Where(x => x.dictItem.Id.StartsWith(column))
                .ToList();

            // If column has no useful cameras, skip it
            if (expectedColumnCams.Count == 0)
            {
                continue;
            }

            // Send message to column
            message.Topic = $"{options.CameraTopic}/{column}";
            var publishResult = await _mqttClient.PublishAsync(message);

            // Update statuses
            foreach (var (dbItem, _) in expectedColumnCams)
            {
                // First because it exists based on previous
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
                await Task.Delay(columnDelayMillis);
            }
        }
    }

    /// <inheritdoc />
    public async Task RequestSendPictureChannels(Guid uuid, int maxConcurrentUploads)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();
        var options = _optionsMonitor.CurrentValue;

        // Get the request
        var pictureRequest = await piDbContext.PictureRequests
            .Include(x => x.CameraPictures)
            .FirstOrDefaultAsync(x => x.Uuid == uuid);
        if (pictureRequest == null)
        {
            throw new Exception("Picture request not found");
        }

        // Create message
        CameraRequest sendPictureRequest = new CameraRequest.SendPicture(
            uuid
        );
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("temp")
            .WithPayload(Json.Serialize(sendPictureRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        // Create channel to receive events
        var channel = Channel.CreateUnbounded<string>();
        if (!_sendPictureChannels.TryAdd(uuid, channel))
        {
            throw new Exception("Failed to create channel");
        }

        // Cameras that need to be sent requests as a queue
        var unsentCameras = new Queue<(CameraPictureModel dbItem, PiZeroCamera dictItem)>(GetSendableCameras(
            pictureRequest,
            requirePing: false,
            requireDeviceStatus: false,
            requireStatus: false
        ));

        // Start first transfers (maxConcurrent)
        for (var i = 0; i < maxConcurrentUploads; i++)
        {
            await SendMessage(piDbContext);
            // Stop early if there are no more cameras available
            if (unsentCameras.Count == 0)
            {
                channel.Writer.Complete();
                break;
            }
        }

        await piDbContext.SaveChangesAsync();
        // Update UI
        // TODO: Broken
        OnPictureChange?.Invoke(uuid);
        await Task.Yield();

        // Check that there are cameras that need to be sent requests
        if (unsentCameras.Count > 0)
        {
            // Wait for message
            while (await channel.Reader.WaitToReadAsync())
            {
                // Pop message
                channel.Reader.TryRead(out _);

                await SendMessage(piDbContext);

                await piDbContext.SaveChangesAsync();
                // Update UI
                OnPictureChange?.Invoke(uuid);
                await Task.Yield();

                // If no more cameras, complete channel and break out
                // Need break, because according to docs, Read could still run, if it's quick enough.
                if (unsentCameras.Count == 0)
                {
                    channel.Writer.Complete();
                    break;
                }
            }
        }

        // Remove channel
        _sendPictureChannels.TryRemove(uuid, out _);
        return;

        // Send message to a camera
        async Task SendMessage(PiDbContext localPiDbContext)
        {
            var (dbItem, piZeroCamera) = unsentCameras.Dequeue();

            message.Topic = $"{options.CameraTopic}/{piZeroCamera.Id}";
            var publishResult = await _mqttClient.PublishAsync(message);

            // Update statuses
            dbItem.CameraPictureStatus = publishResult.IsSuccess
                ? CameraPictureStatus.RequestedSend
                : CameraPictureStatus.FailedToRequestSend;
            localPiDbContext.Update(dbItem);
        }
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
        // Get database item, create if doesn't exist
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
}