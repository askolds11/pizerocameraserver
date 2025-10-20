using System.Collections.Concurrent;
using System.Threading.Channels;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Responses;
using picamerasserver.pizerocamera.syncreceiver;
using picamerasserver.Settings;

namespace picamerasserver.pizerocamera.TakePicture;

/// <summary>
/// Interface for taking pictures.
/// </summary>
public interface ITakePictureManager
{
    /// <summary>
    /// Is a take picture operation ongoing?
    /// </summary>
    bool TakePictureActive { get; }

    /// <summary>
    /// Makes requests to take pictures. <br />
    /// Makes requests to all cameras at once.
    /// </summary>
    /// <param name="pictureRequestType">Type of picture</param>
    /// <param name="pictureSetUId">UUID of the picture set, if exists</param>
    /// <returns>The resulting request model with cameras</returns>
    Task<Result<PictureRequestModel>> RequestTakePictureAll(
        PictureRequestType pictureRequestType = PictureRequestType.Other,
        Guid? pictureSetUId = null);

    /// <summary>
    /// Handle a TakePicture response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="messageReceived">Time when the message was received</param>
    /// <param name="takePicture">Deserialized TakePicture</param>
    /// <param name="id">Camera's id</param>
    /// <exception cref="ArgumentOutOfRangeException">Unknown failure type</exception>
    Task ResponseTakePicture(MqttApplicationMessage message, DateTimeOffset messageReceived,
        CameraResponse.TakePicture takePicture, string id);

    /// <summary>
    /// Cancels the ongoing take picture operation
    /// </summary>
    Task CancelTakePicture();
}

public partial class TakePicture(
    PiZeroCameraManager piZeroCameraManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<TakePicture> logger,
    SyncPayloadService syncPayloadService,
    SettingsService settingsService
) : ITakePictureManager, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _takePictureChannels = new();
    private readonly ConcurrentDictionary<Guid, Channel<string>> _savePictureChannels = new();

    /// <inheritdoc />
    public bool TakePictureActive { get; private set; }

    private readonly SemaphoreSlim _takePictureSemaphore = new(1, 1);
    private CancellationTokenSource? _takePictureCancellationTokenSource;

    /// <inheritdoc />
    public async Task CancelTakePicture()
    {
        if (TakePictureActive)
        {
            await piZeroCameraManager.CancelCameraTasks();
        }

        if (_takePictureCancellationTokenSource != null)
        {
            await _takePictureCancellationTokenSource.CancelAsync();
        }
    }

    public void Dispose()
    {
        _takePictureSemaphore.Dispose();
        _takePictureCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}