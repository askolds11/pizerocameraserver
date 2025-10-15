using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.Sync;

public interface ISyncManager
{
    /// <summary>
    /// Is a "get sync" operation ongoing?
    /// </summary>
    bool SyncActive { get; }

    /// <summary>
    /// Checks sync status for all Pis.
    /// </summary>
    /// <returns>Result whether the request was successful</returns>
    Task<Result> GetSyncStatus();

    /// <summary>
    /// Handle a sync status response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="syncStatus">Deserialized SyncStatus</param>
    /// <param name="id">Camera's id</param>
    Task ResponseSyncStatus(MqttApplicationMessage message, CameraResponse.SyncStatus syncStatus, string id);

    /// <summary>
    /// Cancels the ongoing "get sync"" operation
    /// </summary>
    Task CancelSyncStatus();
}

public partial class Sync(
    PiZeroCameraManager piZeroCameraManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    ILogger<Sync> logger
) : ISyncManager, IDisposable
{
    private Channel<string>? _syncChannel;

    /// <inheritdoc />
    public bool SyncActive { get; private set; }

    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private CancellationTokenSource? _syncCancellationTokenSource;

    /// <inheritdoc />
    public async Task CancelSyncStatus()
    {
        if (_syncCancellationTokenSource != null)
        {
            await _syncCancellationTokenSource.CancelAsync();
        }

        if (SyncActive)
        {
            await piZeroCameraManager.CancelCameraTasks();
        }
    }

    public void Dispose()
    {
        _syncSemaphore.Dispose();
        _syncCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}