using System.Net.NetworkInformation;
using MQTTnet;
using MQTTnet.Protocol;

namespace picamerasserver.pizerocamera.GetAlive;

public partial class GetAlive
{
    private async Task PingSingleCamera(string id, TimeSpan timespan, CancellationToken cancellationToken)
    {
        var piZeroCamera = piZeroCameraManager.PiZeroCameras[id];

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
            logger.LogError(ex, "Failed to ping PiZeroCamera {Id}", id);
        }

        cancellationToken.ThrowIfCancellationRequested();
        changeListener.UpdatePing();
    }
    
    private async Task PingSingleIndicator(TimeSpan timespan, CancellationToken cancellationToken)
    {
        var piZeroIndicator = piZeroCameraManager.PiZeroIndicator;

        using var ping = new Ping();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // BUG: .local at end does not work?
            var result = await ping.SendPingAsync(
                $"pizero{PiZeroIndicator.Id}",
                timespan,
                null,
                null,
                cancellationToken
            );
            piZeroIndicator.Pingable = result.Status == IPStatus.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            piZeroIndicator.Pingable = false;
            logger.LogError(ex, "Failed to ping PiZeroIndicator");
        }

        cancellationToken.ThrowIfCancellationRequested();
        changeListener.UpdatePing();
    }

    /// <inheritdoc />
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
            changeListener.UpdatePing();

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var piZeroCamera in piZeroCameraManager.PiZeroCameras.Values)
            {
                piZeroCamera.Pingable = null;
            }
            piZeroCameraManager.PiZeroIndicator.Pingable = null;

            changeListener.UpdatePing();

            // if timeout provided, use that, else infinite
            // BUG: TimeSpan.FromMilliseconds() always times out with values greater than or equal to 2147483100
            var timespan = timeoutMillis == null
                ? TimeSpan.FromMinutes(10)
                : TimeSpan.FromMilliseconds((int)timeoutMillis);

            await PingSingleIndicator(timespan, cancellationToken);
            var tasks = piZeroCameraManager.PiZeroCameras.Keys
                .Select(id => PingSingleCamera(id, timespan, cancellationToken));
            await Task.WhenAll(tasks);
        }
        finally
        {
            _pingCancellationTokenSource.Dispose();
            _pingCancellationTokenSource = null;

            _pingSemaphore.Release();

            PingActive = false;

            changeListener.UpdatePing();
        }
    }

    /// <inheritdoc />
    public async Task GetStatus()
    {
        foreach (var piZeroCamera in piZeroCameraManager.PiZeroCameras.Values)
        {
            piZeroCamera.Status = null;
        }
        piZeroCameraManager.PiZeroIndicator.Status = null;

        changeListener.UpdatePing();

        var options = optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.StatusTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        await mqttClient.PublishAsync(message);

        changeListener.UpdatePing();
    }
}