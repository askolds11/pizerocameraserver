namespace picamerasserver.pizerocamera;

/// <summary>
/// Class for listening to changes and emitting changes.
/// </summary>
public class ChangeListener
{
    /// <summary>
    /// Called when a ping is changed.
    /// </summary>
    public event Func<Task>? OnPingChange;
    /// <summary>
    /// Called when a ntp is changed.
    /// </summary>
    public event Func<Task>? OnNtpChange;
    /// <summary>
    /// Called when a picture set is changed.
    /// </summary>
    public event Func<Guid, Task>? OnPictureSetChange;
    /// <summary>
    /// Called when a picture request is changed.
    /// </summary>
    public event Func<Guid, Task>? OnPictureChange;
    /// <summary>
    /// Called when an update is changed.
    /// </summary>
    public event Func<Task>? OnUpdateChange;
    /// <summary>
    /// Called when a sync status is changed.
    /// </summary>
    public event Func<Task>? OnSyncChange;
    
    public void UpdatePing()
    {
        UpdateEvent(OnPingChange);
    }
    
    public void UpdateNtp()
    {
        UpdateEvent(OnNtpChange);
    }
    
    public void UpdateUpdate()
    {
        UpdateEvent(OnUpdateChange);
    }
    
    public void UpdateSync()
    {
        UpdateEvent(OnSyncChange);
    }
    
    public void UpdatePictureSet(Guid pictureSetUuid)
    {
        Func<Task>? func = OnPictureSetChange == null ? null : () => OnPictureSetChange.Invoke(pictureSetUuid);
        UpdateEvent(func);
    }
    
    public void UpdatePicture(Guid pictureRequestUuid)
    {
        Func<Task>? func = OnPictureChange == null ? null : () => OnPictureChange.Invoke(pictureRequestUuid);
        UpdateEvent(func);
    }

    /// <summary>
    /// Executes a function on a separate thread if the function is not null.
    /// </summary>
    /// <param name="func"></param>
    private static void UpdateEvent(Func<Task>? func)
    {
        if (func != null)
        {
            Task.Run(async () =>
            {
                await func.Invoke();
                await Task.Yield();
            });
        }
    }
}