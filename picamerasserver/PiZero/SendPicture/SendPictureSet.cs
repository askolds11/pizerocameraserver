using Microsoft.EntityFrameworkCore;
using picamerasserver.Database;

namespace picamerasserver.PiZero.SendPicture;

public interface ISendPictureSetManager
{
    /// <summary>
    /// Is a send operation ongoing?
    /// </summary>
    bool SendSetActive { get; }

    /// <summary>
    /// Makes requests to send picture set's pictures from cameras to server. <br />
    /// </summary>
    /// <param name="uuid">Uuid of PictureSet</param>
    Task RequestSendPictureSet(Guid uuid);

    /// <summary>
    /// Cancels the ongoing send picture set operation
    /// </summary>
    Task CancelSendSet();
}

public class SendPictureSet(
    ISendPictureManager sendPictureManager,
    ChangeListener changeListener,
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<SendPictureSet> logger
) : ISendPictureSetManager, IDisposable
{
    /// <inheritdoc />
    public bool SendSetActive { get; private set; }
    private readonly SemaphoreSlim _sendSetSemaphore = new(1, 1);
    private CancellationTokenSource? _sendSetCancellationTokenSource;
    
    /// <inheritdoc />
    public async Task RequestSendPictureSet(Guid uuid)
    {
        // Another send set operation is already running
        if (!await _sendSetSemaphore.WaitAsync(TimeSpan.Zero))
        {
            return;
        }

        // Just in case, cancel existing sends (there shouldn't be any)
        if (_sendSetCancellationTokenSource != null)
        {
            await _sendSetCancellationTokenSource.CancelAsync();
        }

        _sendSetCancellationTokenSource?.Dispose();
        _sendSetCancellationTokenSource = new CancellationTokenSource();
        
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync(_sendSetCancellationTokenSource.Token);
        var pictureSet = await piDbContext.PictureSets
            .Include(x => x.PictureRequests.Where(y => y.IsActive))
            .FirstOrDefaultAsync(x => x.Uuid == uuid, _sendSetCancellationTokenSource.Token);

        try
        {
            changeListener.OnPictureChange += PictureToPictureSetChange;
            SendSetActive = true;

            if (pictureSet == null)
            {
                throw new Exception("Picture set not found");
            }

            foreach (var pictureRequest in pictureSet.PictureRequests)
            {
                await sendPictureManager.RequestSendPictureChannels(pictureRequest.Uuid);
                _sendSetCancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

            // Update UI
            changeListener.UpdatePictureSet(uuid);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            changeListener.OnPictureChange -= PictureToPictureSetChange;
            _sendSetCancellationTokenSource.Dispose();
            _sendSetCancellationTokenSource = null;
            // Release semaphore
            _sendSetSemaphore.Release();
            // Update UI
            SendSetActive = false;
            changeListener.UpdatePictureSet(uuid);
        }

        return;

        // Callback to update the picture set based on individual picture request changes
        Task PictureToPictureSetChange(Guid pictureUId)
        {
            if (pictureSet?.PictureRequests.Any(x => x.Uuid == pictureUId) == true)
            {
                changeListener.UpdatePictureSet(uuid);
            }
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public async Task CancelSendSet()
    {
        var tasks = new List<Task>();
        if (_sendSetCancellationTokenSource != null)
        {
            tasks.Add(_sendSetCancellationTokenSource.CancelAsync());
        }
        
        if (sendPictureManager.SendActive)
        {
            tasks.Add(sendPictureManager.CancelSend());
        }
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        _sendSetSemaphore.Dispose();
        _sendSetCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}