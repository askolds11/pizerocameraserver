using System.Threading.Channels;
using MQTTnet;
using MQTTnet.Protocol;

namespace picamerasserver.pizerocamera.Update;

public partial class Update
{
    /// <summary>
    /// Get a list of cameras based on criteria for updating
    /// </summary>
    /// <param name="requirePing">Should the cameras be pingable?</param>
    /// <param name="requireDeviceStatus">Should the cameras have status?</param>
    /// <returns>A collection of PiZeroCamera instances meeting the criteria.</returns>
    private IEnumerable<PiZeroCamera> GetUpdateableCameras(
        bool requirePing = true,
        bool requireDeviceStatus = true
    )
    {
        return piZeroCameraManager.PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null);
    }

    /// <inheritdoc />
    public async Task RequestUpdateChannels(int maxConcurrentUploads)
    {
        // Another update operation is already running
        if (!await _updateSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return;
        }

        // No version
        var activeVersion = await GetActiveVersion();
        if (activeVersion == null)
        {
            return;
        }

        // Just in case, cancel existing updates (there shouldn't be any)
        if (_updateCancellationTokenSource != null)
        {
            await _updateCancellationTokenSource.CancelAsync();
        }

        _updateCancellationTokenSource?.Dispose();
        _updateCancellationTokenSource = new CancellationTokenSource();

        List<PiZeroCamera>? unsentCameras = null;
        await using var piDbContext =
            await dbContextFactory.CreateDbContextAsync(_updateCancellationTokenSource.Token);

        try
        {
            UpdateActive = true;

            var options = optionsMonitor.CurrentValue;

            unsentCameras = GetUpdateableCameras(requirePing: true, requireDeviceStatus: true).ToList();

            if (unsentCameras.Count == 0)
            {
                return;
            }

            // Create a channel to receive events
            _updateChannel = Channel.CreateUnbounded<string>();

            // Queue for sending requests
            var cameraQueue = new Queue<PiZeroCamera>(unsentCameras);

            // Set updated time
            activeVersion.UpdatedTime = DateTimeOffset.UtcNow;
            piDbContext.Update(activeVersion);
            await piDbContext.SaveChangesAsync();

            // Make a message
            var message = new MqttApplicationMessageBuilder()
                // .WithContentType("application/json")
                .WithTopic("temp")
                .WithPayload(activeVersion.Version)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            // Start first updates (maxConcurrent)
            for (var i = 0; i < maxConcurrentUploads; i++)
            {
                await SendMessage(_updateCancellationTokenSource);
                // Stop early if there are no more cameras available
                if (cameraQueue.Count == 0)
                {
                    break;
                }
            }

            // Update UI
            changeListener.UpdateUpdate();
            await Task.Yield();

            // Wait for messages
            while (await _updateChannel.Reader.WaitToReadAsync(_updateCancellationTokenSource.Token))
            {
                // Pop message
                if (_updateChannel.Reader.TryRead(out var cameraId))
                {
                    var camera = unsentCameras.First(x => x.Id == cameraId);
                    unsentCameras.Remove(camera);
                }

                // Send request if there are cameras left to update
                if (cameraQueue.Count > 0)
                {
                    await SendMessage(_updateCancellationTokenSource);
                    // Update UI
                    changeListener.UpdateUpdate();
                    await Task.Yield();
                }

                // If no more cameras, complete the channel and break out
                // Need break because, according to docs, Read could still run, if it's quick enough.
                if (unsentCameras.Count == 0)
                {
                    _updateChannel.Writer.Complete();
                    break;
                }
            }

            return;

            // Send a message to a camera
            async Task SendMessage(CancellationTokenSource cts)
            {
                var piZeroCamera = cameraQueue.Dequeue();

                message.Topic = $"{options.UpdateTopic}/{piZeroCamera.Id}";
                var publishResult = await mqttClient.PublishAsync(message, cts.Token);

                // Update statuses
                piZeroCamera.UpdateRequest = publishResult.IsSuccess
                    ? new PiZeroCameraUpdateRequest.Requested()
                    : new PiZeroCameraUpdateRequest.Failure.FailedToRequest();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Remove the channel and clean up
            _updateChannel = null;
            if (unsentCameras is { Count: > 0 })
            {
                foreach (var unsentCamera in unsentCameras)
                {
                    unsentCamera.UpdateRequest = new PiZeroCameraUpdateRequest.Cancelled();
                }
            }

            _updateCancellationTokenSource.Dispose();
            _updateCancellationTokenSource = null;
            // Release semaphore
            _updateSemaphore.Release();
            // Update UI
            UpdateActive = false;
            changeListener.UpdateUpdate();
            await Task.Yield();
        }
    }
}