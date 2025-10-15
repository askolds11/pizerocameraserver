using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Database;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.SendPicture;

/// <summary>
/// Interface for sending pictures.
/// </summary>
public interface ISendPictureManager
{
    /// <summary>
    /// Is a send operation ongoing?
    /// </summary>
    bool SendActive { get; }

    /// <summary>
    /// Makes requests to send pictures from cameras to server. <br />
    /// Makes requests to <paramref name="maxConcurrentUploads"/> cameras at once.
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    /// <param name="maxConcurrentUploads">How many uploads to do at once</param>
    Task RequestSendPictureChannels(Guid uuid, int maxConcurrentUploads = 3);

    /// <summary>
    /// Request to send picture for individual camera
    /// </summary>
    /// <param name="uuid">Uuid of PictureRequest</param>
    /// <param name="cameraId">Camera id of wanted camera</param>
    Task RequestSendPictureIndividual(Guid uuid, string cameraId);

    /// <summary>
    /// Handle a SendPicture response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="messageReceived">Time when the message was received</param>
    /// <param name="sendPicture">Deserialized SendPicture</param>
    /// <param name="id">Camera's id</param>
    /// <exception cref="ArgumentOutOfRangeException">Unknown failure type</exception>
    Task ResponseSendPicture(MqttApplicationMessage message, DateTimeOffset messageReceived,
        CameraResponse.SendPicture sendPicture, string id);

    /// <summary>
    /// Cancels the ongoing send operation
    /// </summary>
    Task CancelSend();
}

public partial class SendPicture(
    PiZeroCameraManager piZeroCameraManager,
    ChangeListener changeListener,
    IMqttClient mqttClient,
    IOptionsMonitor<MqttOptions> optionsMonitor,
    IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor,
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<SendPicture> logger
) : ISendPictureManager, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _sendPictureChannels = new();

    /// <inheritdoc />
    public bool SendActive { get; private set; }

    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private CancellationTokenSource? _sendCancellationTokenSource;

    /// <inheritdoc />
    public async Task CancelSend()
    {
        if (_sendCancellationTokenSource != null)
        {
            await _sendCancellationTokenSource.CancelAsync();
        }

        if (SendActive)
        {
            await piZeroCameraManager.CancelCameraTasks();
        }
    }

    public void Dispose()
    {
        _sendSemaphore.Dispose();
        _sendCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}