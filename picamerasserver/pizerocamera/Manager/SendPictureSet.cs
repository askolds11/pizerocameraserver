using Microsoft.EntityFrameworkCore;

namespace picamerasserver.pizerocamera.manager;

public interface ISendPictureSetManager
{
    /// <summary>
    /// Is a send operation ongoing?
    /// </summary>
    bool SendSetActive { get; }

    /// <summary>
    /// Makes requests to send picture set's pictures from cameras to server. <br />
    /// Makes requests to <paramref name="maxConcurrentUploads"/> cameras at once.
    /// </summary>
    /// <param name="uuid">Uuid of PictureSet</param>
    /// <param name="maxConcurrentUploads">How many uploads to do at once</param>
    Task RequestSendPictureSet(Guid uuid, int maxConcurrentUploads = 3);

    /// <summary>
    /// Cancels the ongoing send picture set operation
    /// </summary>
    Task CancelSendSet();
}

public partial class PiZeroCameraManager : ISendPictureSetManager
{
    public event Func<Guid, Task>? OnPictureSetChange;
    
    public bool SendSetActive { get; private set; }
    private readonly SemaphoreSlim _sendSetSemaphore = new(1, 1);
    private CancellationTokenSource? _sendSetCancellationTokenSource;
    
    /// <inheritdoc />
    public async Task RequestSendPictureSet(Guid uuid, int maxConcurrentUploads)
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
        
        await using var piDbContext = await _dbContextFactory.CreateDbContextAsync(_sendSetCancellationTokenSource.Token);
        var pictureSet = await piDbContext.PictureSets
            .Include(x => x.PictureRequests.Where(y => y.IsActive))
            .FirstOrDefaultAsync(x => x.Uuid == uuid, _sendSetCancellationTokenSource.Token);

        try
        {
            OnPictureChange += PictureToPictureSetChange;
            SendSetActive = true;

            if (pictureSet == null)
            {
                throw new Exception("Picture set not found");
            }

            foreach (var pictureRequest in pictureSet.PictureRequests)
            {
                await RequestSendPictureChannels(pictureRequest.Uuid, maxConcurrentUploads);
                _sendSetCancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

            // Update UI
            if (OnPictureSetChange != null)
            {
                await OnPictureSetChange(uuid);
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            OnPictureChange -= PictureToPictureSetChange;
            _sendSetCancellationTokenSource.Dispose();
            _sendSetCancellationTokenSource = null;
            // Release semaphore
            _sendSetSemaphore.Release();
            // Update UI
            SendSetActive = false;
            if (OnPictureSetChange != null)
            {
                await OnPictureSetChange(uuid);
                await Task.Yield();
            }
        }

        return;

        async Task PictureToPictureSetChange(Guid pictureUId)
        {
            if (pictureSet?.PictureRequests.Any(x => x.Uuid == pictureUId) == true)
            {
                if (OnPictureSetChange != null)
                {
                    await OnPictureSetChange(uuid);
                    await Task.Yield();
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task CancelSendSet()
    {
        if (_sendCancellationTokenSource != null)
        {
            await _sendCancellationTokenSource.CancelAsync();
        }
        if (_sendSetCancellationTokenSource != null)
        {
            await _sendSetCancellationTokenSource.CancelAsync();
        }

        if (SendSetActive)
        {
            await CancelCameraTasks();
        }
    }
}