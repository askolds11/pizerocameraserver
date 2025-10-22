using picamerasserver.Database.Models;
using picamerasserver.PiZero;
using picamerasserver.PiZero.GetAlive;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.Ntp;
using picamerasserver.PiZero.SendPicture;
using picamerasserver.PiZero.Sync;

namespace picamerasserver.Components.Components.NewPicture;

public class SharedState : IDisposable
{
    private readonly PiZeroManager _piZeroManager;
    private readonly IGetAliveManager _getAliveManager;
    private readonly INtpManager _ntpManager;
    private readonly ISyncManager _syncManager;
    private readonly ChangeListener _changeListener;
    private readonly ISendPictureSetManager _sendPictureSetManager;
    private readonly IUploadManager _uploadToServer;

    public SharedState(
        PiZeroManager piZeroManager,
        IGetAliveManager getAliveManager,
        ChangeListener changeListener, INtpManager ntpManager, ISyncManager syncManager, IUploadManager uploadToServer,
        ISendPictureSetManager sendPictureSetManager)
    {
        _piZeroManager = piZeroManager;
        _getAliveManager = getAliveManager;
        _changeListener = changeListener;
        _ntpManager = ntpManager;
        _syncManager = syncManager;
        _uploadToServer = uploadToServer;
        _sendPictureSetManager = sendPictureSetManager;

        _changeListener.OnPingChange += OnChange;
        _changeListener.OnNtpChange += OnChange;
        _changeListener.OnSyncChange += OnChange;
        _changeListener.OnPictureSetChange += OnChangeGuid;
    }

    public void Dispose()
    {
        _changeListener.OnPingChange -= OnChange;
        _changeListener.OnNtpChange -= OnChange;
        _changeListener.OnSyncChange -= OnChange;
        _changeListener.OnPictureSetChange -= OnChangeGuid;

        GC.SuppressFinalize(this);
    }

    public event Func<Task>? OnChange;

    private async Task OnChangeGuid(Guid _)
    {
        if (OnChange != null)
        {
            await OnChange();
        }
    }

    // Fields
    private PictureSetModel? _pictureSet;
    private bool _alived = false;
    private bool _ntpSynced = false;
    private bool _syncedFrames = false;

    // Properties
    public PictureSetModel? PictureSet
    {
        get => _pictureSet;
        set
        {
            _pictureSet = value;
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Have cameras been checked if they are working?
    /// </summary>
    public bool Alived
    {
        get => _alived;
        set
        {
            _alived = value;
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Have cameras been ntp synced?
    /// </summary>
    public bool NtpSynced
    {
        get => _ntpSynced;
        set
        {
            _ntpSynced = value;
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Have cameras been ntp synced?
    /// </summary>
    public bool SyncedFrames
    {
        get => _syncedFrames;
        set
        {
            _syncedFrames = value;
            OnChange?.Invoke();
        }
    }

    public bool AnyActive => PingActive || NtpActive || SyncActive || SendSetActive || UploadActive;

    /// <summary>
    /// Is a ping operation ongoing?
    /// </summary>
    public bool PingActive => _getAliveManager.PingActive;

    /// <summary>
    /// Is a ntp sync operation ongoing?
    /// </summary>
    public bool NtpActive => _ntpManager.NtpActive;

    /// <summary>
    /// Is a sync operation ongoing?
    /// </summary>
    public bool SyncActive => _syncManager.SyncActive;

    /// <summary>
    /// Is a send operation ongoing?
    /// </summary>
    public bool SendSetActive => _sendPictureSetManager.SendSetActive;

    /// <summary>
    /// Is an upload operation ongoing?
    /// </summary>
    public bool UploadActive => _uploadToServer.UploadActive;

    /// <summary>
    /// Is the indicator alive?
    /// </summary>
    public bool IndicatorAlive => _piZeroManager.PiZeroIndicator is { Pingable: true, Status: not null };

    /// <summary>
    /// Is the indicator ntp synced?
    /// </summary>
    public bool IndicatorNtped => _piZeroManager.PiZeroIndicator.NtpRequest is PiZeroNtpRequest.Success;

    /// <summary>
    /// Count of cameras that are alive
    /// </summary>
    public int AliveCount =>
        _piZeroManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });
}