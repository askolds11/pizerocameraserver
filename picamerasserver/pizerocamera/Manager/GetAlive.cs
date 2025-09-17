using System.Net.NetworkInformation;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    public bool PingActive { get; private set; }
    private readonly SemaphoreSlim _pingSemaphore = new(1, 1);
    private CancellationTokenSource? _pingCancellationTokenSource;

    private async Task PingSingle(string id, TimeSpan timespan, CancellationToken cancellationToken)
    {
        var piZeroCamera = PiZeroCameras[id];

        using var ping = new Ping();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // BUG: .local at end does not work?
            var result = await ping.SendPingAsync(
                $"pizero{id}",
                timespan,
                null,
                null,
                cancellationToken
            );
            piZeroCamera.Pingable = result.Status == IPStatus.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            piZeroCamera.Pingable = false;
            _logger.LogError(ex, "Failed to ping PiZeroCamera {Id}", id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _changeListener.UpdatePing();
    }

    public async Task Ping(
        int? timeoutMillis = null
    )
    {
        if (!await _pingSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return; // Another ping operation is already running
        }

        // Just in case, cancel existing ping operations (there shouldn't be any)
        if (_pingCancellationTokenSource != null)
        {
            await _pingCancellationTokenSource.CancelAsync();
        }

        _pingCancellationTokenSource?.Dispose();
        _pingCancellationTokenSource = new CancellationTokenSource();

        var cancellationToken = _pingCancellationTokenSource.Token;

        try
        {
            PingActive = true;
            _changeListener.UpdatePing();

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var piZeroCamera in PiZeroCameras.Values)
            {
                piZeroCamera.Pingable = null;
            }

            _changeListener.UpdatePing();

            // if timeout provided, use that, else infinite
            // BUG: TimeSpan.FromMilliseconds() always times out with values greater than or equal to 2147483100
            var timespan = timeoutMillis == null
                ? TimeSpan.FromMinutes(10)
                : TimeSpan.FromMilliseconds((int)timeoutMillis);

            var tasks = PiZeroCameras.Keys
                .Select(id => PingSingle(id, timespan, cancellationToken));
            await Task.WhenAll(tasks);
        }
        finally
        {
            _pingCancellationTokenSource.Dispose();
            _pingCancellationTokenSource = null;

            _pingSemaphore.Release();

            PingActive = false;

            _changeListener.UpdatePing();
        }
    }

    public async Task GetStatus()
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        foreach (var piZeroCamera in PiZeroCameras.Values)
        {
            piZeroCamera.Status = null;
        }

        _changeListener.UpdatePing();

        var options = _optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.StatusTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        await _mqttClient.PublishAsync(message);

        _changeListener.UpdatePing();
    }

    public async Task ResponseGetStatus(MqttApplicationMessage message, string id)
    {
        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<StatusResponse>(text, _logger);
        if (statusResponse.IsFailure)
        {
            piZeroCamera.Status = null;
            _logger.LogError(statusResponse.Error, "Failed to parse status response");
        }
        else
        {
            var successWrapper = statusResponse.Value;
            piZeroCamera.Status = successWrapper.Success ? successWrapper.Value : null;

            if (successWrapper.Success)
            {
                var activeVersion = (await GetActiveVersion())?.Version;
                if (activeVersion == successWrapper.Value.Version)
                {
                    piZeroCamera.UpdateRequest = new PiZeroCameraUpdateRequest.Success();
                }
                else
                {
                    piZeroCamera.UpdateRequest = new PiZeroCameraUpdateRequest.Failure.VersionMismatch();
                }

                _changeListener.UpdateUpdate();
            }
        }

        _changeListener.UpdatePing();
    }

    public async Task CancelPing()
    {
        if (_pingCancellationTokenSource != null)
        {
            await _pingCancellationTokenSource.CancelAsync();
        }
    }
}