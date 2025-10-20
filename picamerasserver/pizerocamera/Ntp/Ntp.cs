using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.Settings;

namespace picamerasserver.pizerocamera.Ntp;

public interface INtpManager
{
    /// <summary>
    /// Is an ntp operation ongoing?
    /// </summary>
    bool NtpActive { get; }

    /// <summary>
    /// Makes requests to all Pis to sync time.
    /// </summary>
    /// <param name="ntpRequest">Type of NTP sync method to use</param>
    /// <returns>Result whether the sync was successful</returns>
    Task<Result> RequestNtpSync(NtpRequest ntpRequest);

    /// <summary>
    /// Request a single Pi to sync its time.
    /// </summary>
    Task RequestNtpSyncIndividual(string cameraId);

    /// <summary>
    /// Handle an Ntp response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="id">Camera's or Indicator's id</param>
    Task ResponseNtpSync(MqttApplicationMessage message, string id);

    /// <summary>
    /// Cancels the ongoing ntp operation
    /// </summary>
    Task CancelNtpSync();
}

public partial class Ntp(
    PiZeroCameraManager piZeroCameraManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    ILogger<Ntp> logger,
    SettingsService settingsService
) : INtpManager, IDisposable
{
    private Channel<string>? _ntpChannel;

    /// <inheritdoc />
    public bool NtpActive { get; private set; }

    private readonly SemaphoreSlim _ntpSemaphore = new(1, 1);
    private CancellationTokenSource? _ntpCancellationTokenSource;

    /// <inheritdoc />
    public async Task CancelNtpSync()
    {
        if (_ntpCancellationTokenSource != null)
        {
            await _ntpCancellationTokenSource.CancelAsync();
        }

        if (NtpActive)
        {
            await piZeroCameraManager.CancelCameraTasks();
        }
    }

    public void Dispose()
    {
        _ntpSemaphore.Dispose();
        _ntpCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}