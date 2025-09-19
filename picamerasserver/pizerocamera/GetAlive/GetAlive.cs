using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Update;

namespace picamerasserver.pizerocamera.GetAlive;

public interface IGetAliveManager
{
    /// <summary>
    /// Is a ping operation ongoing?
    /// </summary>
    bool PingActive { get; }

    /// <summary>
    /// Pings all cameras to check their network status.
    /// </summary>
    /// <param name="timeoutMillis">Timeout after which a ping is considered failed</param>
    Task Ping(int? timeoutMillis = null );

    /// <summary>
    /// Request Pis to send their status.
    /// </summary>
    Task GetStatus();

    /// <summary>
    /// Handle a Status response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="id">Camera's id</param>
    Task ResponseGetStatus(MqttApplicationMessage message, string id);

    /// <summary>
    /// Cancels the ongoing ping operation
    /// </summary>
    Task CancelPing();
}

public partial class GetAlive(
    PiZeroCameraManager piZeroCameraManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IUpdateManager updateManager,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    ILogger<GetAlive> logger
) : IGetAliveManager
{
    /// <inheritdoc />
    public bool PingActive { get; private set; }
    private readonly SemaphoreSlim _pingSemaphore = new(1, 1);
    private CancellationTokenSource? _pingCancellationTokenSource;

    /// <inheritdoc />
    public async Task CancelPing()
    {
        if (_pingCancellationTokenSource != null)
        {
            await _pingCancellationTokenSource.CancelAsync();
        }
    }
}