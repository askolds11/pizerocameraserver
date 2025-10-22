using Microsoft.AspNetCore.Components;
using picamerasserver.PiZero.GetAlive;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.Ntp;
using picamerasserver.PiZero.SendPicture;
using picamerasserver.PiZero.Sync;
using picamerasserver.PiZero.TakePicture;

namespace picamerasserver.Components.Components.NewPicture;

public partial class CancelTab : ComponentBase
{
    [Inject] protected ISendPictureSetManager SendPictureSetManager { get; init; } = null!;
    [Inject] protected ITakePictureManager TakePictureManager { get; init; } = null!;
    [Inject] protected IGetAliveManager GetAliveManager { get; init; } = null!;
    [Inject] protected INtpManager NtpManager { get; init; } = null!;
    [Inject] protected ISyncManager SyncManager { get; init; } = null!;
    [Inject] protected IUploadManager UploadToServer { get; init; } = null!;
    
    private async Task CancelTakePic()
    {
        await TakePictureManager.CancelTakePicture();
    }

    private async Task CancelSend()
    {
        await SendPictureSetManager.CancelSendSet();
    }

    private async Task CancelNtpSync()
    {
        await NtpManager.CancelNtpSync();
    }

    private async Task CancelPing()
    {
        await GetAliveManager.CancelPing();
    }

    private async Task CancelUpload()
    {
        await UploadToServer.CancelUpload();
    }

    private async Task CancelSync()
    {
        await SyncManager.CancelSyncStatus();
    }
}