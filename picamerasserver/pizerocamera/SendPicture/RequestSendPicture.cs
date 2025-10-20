using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.Settings;

namespace picamerasserver.pizerocamera.SendPicture;

public partial class SendPicture
{
    /// <summary>
    /// Get a list of cameras based on criteria for sending pictures
    /// </summary>
    /// <param name="pictureRequest">PictureRequest with cameras</param>
    /// <param name="requirePing">Should the cameras be pingable?</param>
    /// <param name="requireDeviceStatus">Should the cameras have status?</param>
    /// <param name="requireStatus">Should the camera's picture's status be valid?</param>
    /// <param name="requireTaken">Should the camera's picture be taken?</param>
    /// <returns>A collection of PiZeroCamera id's meeting the criteria.</returns>
    private IEnumerable<string> GetSendableCameras(
        PictureRequestModel pictureRequest,
        bool requirePing = true,
        bool requireDeviceStatus = true,
        bool requireStatus = true,
        bool requireTaken = true
    )
    {
        var allowedStatuses = new[]
        {
            CameraPictureStatus.SavedOnDevice, CameraPictureStatus.FailedToRequestSend, CameraPictureStatus.FailureSend,
            CameraPictureStatus.PictureFailedToRead, CameraPictureStatus.PictureFailedToSend,
            CameraPictureStatus.CancelledSend
        };
        // Cameras to send request to
        return pictureRequest.CameraPictures
            .Where(x => !requireTaken || x.ReceivedTaken != null)
            .Where(x => !requireStatus || x.CameraPictureStatus != null &&
                allowedStatuses.Contains((CameraPictureStatus)x.CameraPictureStatus)
            )
            .Select(x => (dbItem: x, dictItem: piZeroCameraManager.PiZeroCameras[x.CameraId]))
            .Where(x => !requirePing || x.dictItem.Pingable == true)
            .Where(x => !requireDeviceStatus || x.dictItem.Status != null)
            .Select(x => x.dbItem.CameraId);
    }

    /// <summary>
    /// Generic set up for a request to send pictures. <br />
    /// Sets up channel, cancellation, try-catch, etc.
    /// </summary>
    /// <param name="uuid"></param>
    /// <param name="tryBlock"></param>
    private async Task RequestSendPictureTryBlock(
        Guid uuid,
        Func<(CancellationTokenSource cts,
            PiDbContext dbContext,
            List<string> unsentCameraIds,
            MqttOptions options,
            Channel<string> channel,
            int maxConcurrentUploads),
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

        List<string>? unsentCameras = null;
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync(_sendCancellationTokenSource.Token);
        try
        {
            SendActive = true;

            var maxConcurrentUploads = (await settingsService.GetAsync<Setting.MaxConcurrentSend>()).Value.Value;
            var options = optionsMonitor.CurrentValue;

            // Get the request
            var pictureRequest = await piDbContext.PictureRequests
                .Include(x => x.CameraPictures)
                .AsNoTracking()
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
            await tryBlock((_sendCancellationTokenSource, piDbContext, unsentCameras, options, channel, maxConcurrentUploads));
        }
        catch (OperationCanceledException)
        {
            if (unsentCameras is { Count: > 0 })
            {
                await piDbContext.CameraPictures
                    .Where(x => x.PictureRequestId == uuid && unsentCameras.Contains(x.CameraId))
                    .ExecuteUpdateAsync(x => x.SetProperty(
                            b => b.CameraPictureStatus, CameraPictureStatus.CancelledSend),
                        CancellationToken.None
                    );
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
            changeListener.UpdatePicture(uuid);
        }
    }

    /// <inheritdoc />
    public async Task RequestSendPictureChannels(Guid uuid)
    {
        await RequestSendPictureTryBlock(uuid, async context =>
        {
            var (cts, piDbContext, unsentCameras, options, channel, maxConcurrentUploads) = context;

            // Queue for sending requests
            var cameraQueue = new Queue<string>(unsentCameras);

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
            
            // Update UI
            changeListener.UpdatePicture(uuid);

            // Wait for messages
            while (await channel.Reader.WaitToReadAsync(cts.Token))
            {
                // Pop message
                if (channel.Reader.TryRead(out var cameraId))
                {
                    var camera = unsentCameras.First(x => x == cameraId);
                    unsentCameras.Remove(camera);
                }

                // Send request if there are cameras left to send
                if (cameraQueue.Count > 0)
                {
                    await SendMessage(piDbContext);
                    // Update UI
                    changeListener.UpdatePicture(uuid);
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
                var cameraId = cameraQueue.Dequeue();

                message.Topic = $"{options.CameraTopic}/{cameraId}";
                var publishResult = await mqttClient.PublishAsync(message, cts.Token);

                // Update statuses
                await localPiDbContext.CameraPictures
                    .Where(x => x.PictureRequestId == uuid && x.CameraId == cameraId)
                    .ExecuteUpdateAsync(x => x.SetProperty(
                            b => b.CameraPictureStatus, publishResult.IsSuccess
                                ? CameraPictureStatus.RequestedSend
                                : CameraPictureStatus.FailedToRequestSend),
                        CancellationToken.None
                    );
            }
        });
    }

    /// <inheritdoc />
    public async Task RequestSendPictureIndividual(Guid uuid, string cameraId)
    {
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();

        var options = optionsMonitor.CurrentValue;

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

        var piZeroCamera = piZeroCameraManager.PiZeroCameras[cameraId];
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

        var publishResult = await mqttClient.PublishAsync(message);


        // First because it exists based on previous
        cameraPicture.CameraPictureStatus = publishResult.IsSuccess
            ? CameraPictureStatus.RequestedSend
            : CameraPictureStatus.FailedToRequestSend;
        piDbContext.Update(cameraPicture);

        await piDbContext.SaveChangesAsync();
        changeListener.UpdatePicture(uuid);
    }
}