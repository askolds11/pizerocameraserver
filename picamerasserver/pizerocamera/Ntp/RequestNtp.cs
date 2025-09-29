using System.Threading.Channels;
using CSharpFunctionalExtensions;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.pizerocamera.Ntp;

public partial class Ntp
{
    /// <summary>
    /// Get a list of cameras based on criteria for syncing ntp
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
        return piZeroCameraManager.PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null)
            .OrderBy(x => x.Id);
    }

    /// <inheritdoc />
    public async Task<Result> RequestNtpSync(NtpRequest ntpRequest, int maxConcurrentSyncs)
    {
        // Another ntp operation is already running
        if (!await _ntpSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return Result.Failure("Another ntp operation is already running");
        }

        // Just in case, cancel existing ntp operations (there shouldn't be any)
        if (_ntpCancellationTokenSource != null)
        {
            await _ntpCancellationTokenSource.CancelAsync();
        }

        _ntpCancellationTokenSource?.Dispose();
        _ntpCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _ntpCancellationTokenSource.Token;

        List<PiZeroCamera>? unsyncedCameras = null;
        try
        {
            NtpActive = true;
            _ntpChannel = Channel.CreateUnbounded<string>();

            var options = optionsMonitor.CurrentValue;


            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic("temp")
                .WithPayload(Json.Serialize(ntpRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            unsyncedCameras = GetSyncableCameras().ToList();
            var cameraQueue = new Queue<PiZeroCamera>(unsyncedCameras);

            // Reset previous NTP data
            foreach (var piZeroCamera in piZeroCameraManager.PiZeroCameras.Values)
            {
                piZeroCamera.LastNtpErrorMillis = null;
                piZeroCamera.LastNtpOffsetMillis = null;
                piZeroCamera.LastNtpSync = null;
                piZeroCamera.NtpRequest = null;
            }

            async Task SendMessage()
            {
                var piZeroCamera = cameraQueue.Dequeue();

                message.Topic = $"{options.NtpTopic}/{piZeroCamera.Id}";
                var publishResult = await mqttClient.PublishAsync(message, cancellationToken);

                piZeroCamera.NtpRequest = publishResult.IsSuccess
                    ? new PiZeroCameraNtpRequest.Requested()
                    : new PiZeroCameraNtpRequest.Failure.FailedToRequest(publishResult.ReasonString);
            }

            // Start first transfers (maxConcurrent)
            for (var i = 0; i < maxConcurrentSyncs; i++)
            {
                await SendMessage();
                // Stop early if there are no more cameras available
                if (cameraQueue.Count == 0)
                {
                    break;
                }
            }

            changeListener.UpdateNtp();

            // Wait for messages
            while (await _ntpChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                // Pop message
                if (_ntpChannel.Reader.TryRead(out var cameraId))
                {
                    var camera = unsyncedCameras.FirstOrDefault(x => x.Id == cameraId);
                    if (camera != null)
                    {
                        unsyncedCameras.Remove(camera);
                    }
                }

                // Send a request if there are cameras left to sync
                if (cameraQueue.Count > 0)
                {
                    await SendMessage();

                    changeListener.UpdateNtp();
                }

                // If no more cameras, complete the channel and break out
                // Need break because, according to docs, Read could still run, if it's quick enough.
                if (unsyncedCameras.Count == 0)
                {
                    _ntpChannel.Writer.Complete();
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
                    piZeroCamera.NtpRequest =
                        new PiZeroCameraNtpRequest.Cancelled();
                    piZeroCamera.LastNtpErrorMillis = null;
                    piZeroCamera.LastNtpOffsetMillis = null;
                    piZeroCamera.LastNtpSync = null;
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
                    piZeroCamera.NtpRequest =
                        new PiZeroCameraNtpRequest.Failure.FailedToRequest(e.ToString());
                    piZeroCamera.LastNtpErrorMillis = null;
                    piZeroCamera.LastNtpOffsetMillis = null;
                    piZeroCamera.LastNtpSync = null;
                }
            }

            return Result.Failure(e.ToString());
        }
        finally
        {
            _ntpChannel = null;

            _ntpCancellationTokenSource.Dispose();
            _ntpCancellationTokenSource = null;
            // Release semaphore
            _ntpSemaphore.Release();
            // Update UI
            NtpActive = false;
            changeListener.UpdateNtp();
            await Task.Yield();
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task RequestNtpSyncIndividual(string cameraId)
    {
        var options = optionsMonitor.CurrentValue;

        var piZeroCamera = piZeroCameraManager.PiZeroCameras[cameraId];

        if (piZeroCamera.Pingable != true || piZeroCamera.Status == null)
        {
            return;
        }

        NtpRequest ntpRequest = new NtpRequest.Step();

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic($"{options.NtpTopic}/{cameraId}")
            .WithPayload(Json.Serialize(ntpRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var publishResult = await mqttClient.PublishAsync(message);

        piZeroCamera.LastNtpErrorMillis = null;
        piZeroCamera.LastNtpOffsetMillis = null;
        piZeroCamera.LastNtpSync = null;

        piZeroCamera.NtpRequest = publishResult.IsSuccess
            ? new PiZeroCameraNtpRequest.Requested()
            : new PiZeroCameraNtpRequest.Failure.FailedToRequest(publishResult.ReasonString);

        changeListener.UpdateNtp();
    }
}