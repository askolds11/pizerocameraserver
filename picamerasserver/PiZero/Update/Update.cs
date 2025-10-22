using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using picamerasserver.PiZero.Manager;

namespace picamerasserver.PiZero.Update;

public interface IUpdateManager
{
    /// <summary>
    /// Is an update operation ongoing?
    /// </summary>
    bool UpdateActive { get; }

    /// <summary>
    /// Gets the currently active version for the Pis.
    /// </summary>
    /// <returns>Active version of the Pis</returns>
    Task<UpdateModel?> GetActiveVersion();

    /// <summary>
    /// Makes requests to all Pis to update
    /// </summary>
    /// <param name="maxConcurrentUploads">Maximum concurrent update downloads at once</param>
    Task RequestUpdateChannels(int maxConcurrentUploads);

    /// <summary>
    /// Handle an update response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="id">Camera's id</param>
    Task ResponseUpdate(MqttApplicationMessage message, string id);

    /// <summary>
    /// Cancels the ongoing update operation
    /// </summary>
    Task CancelUpdate();
}

public partial class Update(
    PiZeroManager piZeroManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<Update> logger
) : IUpdateManager, IDisposable
{
    private Channel<string>? _updateChannel;

    /// <inheritdoc />
    public bool UpdateActive { get; private set; }

    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private CancellationTokenSource? _updateCancellationTokenSource;

    /// <inheritdoc />
    public async Task<UpdateModel?> GetActiveVersion()
    {
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();
        return await piDbContext.Updates.FirstOrDefaultAsync(x => x.IsCurrent);
    }

    /// <inheritdoc />
    public async Task CancelUpdate()
    {
        if (_updateCancellationTokenSource != null)
        {
            await _updateCancellationTokenSource.CancelAsync();
        }

        if (UpdateActive)
        {
            await piZeroManager.CancelCameraTasks();
        }
    }

    public void Dispose()
    {
        _updateSemaphore.Dispose();
        _updateCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}