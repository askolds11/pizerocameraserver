using System.Threading.Channels;
using CSharpFunctionalExtensions;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.PiZero.Requests;

namespace picamerasserver.PiZero.Sync;

public partial class Sync
{
    /// <summary>
    /// Get a list of cameras based on criteria for syncing frames
    /// </summary>
    /// <param name="requirePing">Should the cameras be pingable?</param>
    /// <param name="requireDeviceStatus">Should the cameras have status?</param>
    /// <returns>A collection of PiZeroCamera instances meeting the criteria.</returns>
    private IEnumerable<PiZeroCamera> GetSyncableCameras(
        bool requirePing = true,
        bool requireDeviceStatus = true
    )
    {
        // Cameras to send request to
        return piZeroManager.PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null)
            .OrderBy(x => x.Id);
    }

    /// <inheritdoc />
    public async Task<Result> GetSyncStatus()
    {
        // Another ntp operation is already running
        if (!await _syncSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return Result.Failure("Another get sync operation is already running");
        }

        // Just in case, cancel existing ntp operations (there shouldn't be any)
        if (_syncCancellationTokenSource != null)
        {
            await _syncCancellationTokenSource.CancelAsync();
        }

        _syncCancellationTokenSource?.Dispose();
        _syncCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _syncCancellationTokenSource.Token;

        List<PiZeroCamera>? unsyncedCameras = null;
        try
        {
            SyncActive = true;
            _syncChannel = Channel.CreateUnbounded<string>();

            var options = optionsMonitor.CurrentValue;

            CameraRequest request = new CameraRequest.GetSyncStatus();

            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic(options.CameraTopic)
                .WithPayload(Json.Serialize(request))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            unsyncedCameras = GetSyncableCameras().ToList();

            // Reset previous NTP data
            foreach (var piZeroCamera in piZeroManager.PiZeroCameras.Values)
            {
                piZeroCamera.SyncStatus = null;
            }

            var publishResult = await mqttClient.PublishAsync(message, cancellationToken);

            foreach (var piZeroCamera in unsyncedCameras)
            {
                piZeroCamera.SyncStatus = publishResult.IsSuccess
                    ? new SyncStatus.Requested()
                    : new SyncStatus.Failure.FailedToRequest(publishResult.ReasonString);
            }

            changeListener.UpdateSync();

            // Wait for messages
            while (await _syncChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                // Pop message
                if (_syncChannel.Reader.TryRead(out var cameraId))
                {
                    var camera = unsyncedCameras.FirstOrDefault(x => x.Id == cameraId);
                    if (camera != null)
                    {
                        unsyncedCameras.Remove(camera);
                    }
                }

                changeListener.UpdateSync();

                // If no more cameras, complete the channel and break out
                // Need break because, according to docs, Read could still run, if it's quick enough.
                if (unsyncedCameras.Count == 0)
                {
                    _syncChannel.Writer.Complete();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (unsyncedCameras != null)
            {
                foreach (var piZeroCamera in unsyncedCameras)
                {
                    piZeroCamera.SyncStatus = new SyncStatus.Cancelled();
                }
            }

            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "NTP Error");

            if (unsyncedCameras != null)
            {
                foreach (var piZeroCamera in unsyncedCameras)
                {
                    piZeroCamera.SyncStatus =
                        new SyncStatus.Failure.FailedToRequest(e.ToString());
                }
            }

            return Result.Failure(e.ToString());
        }
        finally
        {
            _syncChannel = null;

            _syncCancellationTokenSource.Dispose();
            _syncCancellationTokenSource = null;
            // Release semaphore
            _syncSemaphore.Release();
            // Update UI
            SyncActive = false;
            changeListener.UpdateSync();
            await Task.Yield();
        }

        return Result.Success();
    }
}