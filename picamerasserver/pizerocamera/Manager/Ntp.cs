using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using CSharpFunctionalExtensions;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    private Channel<string>? _ntpChannel;

    public bool NtpActive { get; private set; }
    private readonly SemaphoreSlim _ntpSemaphore = new(1, 1);
    private CancellationTokenSource? _ntpCancellationTokenSource;

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
        return PiZeroCameras.Values
            .Where(x => !requirePing || x.Pingable == true)
            .Where(x => !requireDeviceStatus || x.Status != null)
            .OrderBy(x => x.Id);
    }

    /// <summary>
    /// Request a NTP time sync.
    /// </summary>
    public async Task<Result> RequestNtpSync(NtpRequest ntpRequest, int maxConcurrentSyncs = 1)
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

            var options = _optionsMonitor.CurrentValue;


            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic("temp")
                .WithPayload(Json.Serialize(ntpRequest))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            unsyncedCameras = GetSyncableCameras().ToList();
            var cameraQueue = new Queue<PiZeroCamera>(unsyncedCameras);

            async Task SendMessage()
            {
                var piZeroCamera = cameraQueue.Dequeue();

                piZeroCamera.LastNtpErrorMillis = null;
                piZeroCamera.LastNtpOffsetMillis = null;
                piZeroCamera.LastNtpSync = null;

                message.Topic = $"{options.NtpTopic}/{piZeroCamera.Id}";
                var publishResult = await _mqttClient.PublishAsync(message, cancellationToken);

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

            _changeListener.UpdateNtp();

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

                    _changeListener.UpdateNtp();
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
            _logger.LogError(e, "NTP Error");

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
            _changeListener.UpdateNtp();
            await Task.Yield();
        }

        return Result.Success();
    }

    public async Task ResponseNtpSync(MqttApplicationMessage message, string id)
    {
        _ntpChannel?.Writer.TryWrite(id);

        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<string>>(text, _logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Success(successWrapper.Value);
                const string pattern = @"(?:^CLOCK:.*?\\n)?(.*?)\s([-+]?\d+\.\d+)\s\+\/-\s(\d+\.\d+)";
                var match = Regex.Match(successWrapper.Value, pattern, RegexOptions.Multiline);

                if (match.Success)
                {
                    var timestamp = match.Groups[1].Value;
                    var offset = match.Groups[2].Value;
                    var error = match.Groups[3].Value;

                    var date = DateTimeOffset.ParseExact(
                        timestamp,
                        "yyyy-MM-dd HH:mm:ss.FFFFFF (zzz)",
                        CultureInfo.InvariantCulture
                    );
                    var offsetSeconds = float.Parse(offset, CultureInfo.InvariantCulture);
                    var errorSeconds = float.Parse(error, CultureInfo.InvariantCulture);

                    piZeroCamera.LastNtpSync = date;
                    piZeroCamera.LastNtpOffsetMillis = offsetSeconds * 1000;
                    piZeroCamera.LastNtpErrorMillis = errorSeconds * 1000;
                }
                else
                {
                    piZeroCamera.NtpRequest =
                        new PiZeroCameraNtpRequest.Failure.FailedToParseRegex(successWrapper.Value);
                }
            }
            else
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure.Failed(successWrapper.Value);
            }
        }
        else
        {
            piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure.FailedToParseJson(response.Error.ToString());
        }

        _changeListener.UpdateNtp();
    }

    public async Task CancelNtpSync()
    {
        if (_ntpCancellationTokenSource != null)
        {
            await _ntpCancellationTokenSource.CancelAsync();
        }

        if (NtpActive)
        {
            await CancelCameraTasks();
        }
    }

    public async Task RequestNtpSyncIndividual(string cameraId)
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var options = _optionsMonitor.CurrentValue;

        var piZeroCamera = PiZeroCameras[cameraId];

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

        var publishResult = await _mqttClient.PublishAsync(message);

        piZeroCamera.LastNtpErrorMillis = null;
        piZeroCamera.LastNtpOffsetMillis = null;
        piZeroCamera.LastNtpSync = null;

        piZeroCamera.NtpRequest = publishResult.IsSuccess
            ? new PiZeroCameraNtpRequest.Requested()
            : new PiZeroCameraNtpRequest.Failure.FailedToRequest(publishResult.ReasonString);

        _changeListener.UpdateNtp();
    }
}