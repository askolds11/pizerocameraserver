using System.Net.NetworkInformation;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    public event Action? OnChangePing;
    public bool PingActive { get; private set; }
    private readonly SemaphoreSlim _pingSemaphore = new(1, 1);

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
        // update listeners
        OnChangePing?.Invoke();
        await Task.Yield();
    }

    public async Task Ping(
        int? timeoutMillis = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await _pingSemaphore.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return; // Another ping operation is already running
        }

        try
        {
            PingActive = true;
            OnChangePing?.Invoke();
            await Task.Yield();

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var piZeroCamera in PiZeroCameras.Values)
            {
                piZeroCamera.Pingable = null;
            }

            OnChangePing?.Invoke();
            await Task.Yield();

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
            PingActive = false;
            _pingSemaphore.Release();
            OnChangePing?.Invoke();
            await Task.Yield();
        }
    }

    public async Task GetStatus()
    {
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync();

        var options = _optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.StatusTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        await _mqttClient.PublishAsync(message);

        OnChangePing?.Invoke();
    }

    public void ResponseGetStatus(MqttApplicationMessage message)
    {
        var id = message.Topic.Split('/').Last();
        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<StatusResponse>(text, _logger);
        if (statusResponse.IsFailure)
        {
            piZeroCamera.Status = null;
            _logger.LogError(statusResponse.Error, "Failed to parse status response");
        }

        var successWrapper = statusResponse.Value;

        piZeroCamera.Status = successWrapper.Success ? successWrapper.Value : null;

        OnChangePing?.Invoke();
    }
}