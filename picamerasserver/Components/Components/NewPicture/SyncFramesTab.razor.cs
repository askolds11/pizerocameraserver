using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Sync;

namespace picamerasserver.Components.Components.NewPicture;

public partial class SyncFramesTab : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected ISyncManager SyncManager { get; init; } = null!;

    private PictureSetModel? PictureSet => SharedState.PictureSet;

    private bool SyncActive => SharedState.SyncActive;
    private bool AnyActive => SharedState.AnyActive;

    private bool Alived => SharedState.Alived;
    private bool NtpSynced => SharedState.NtpSynced;

    private bool SyncedFrames
    {
        get => SharedState.SyncedFrames;
        set => SharedState.SyncedFrames = value;
    }

    private int SyncedCount =>
        PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.SyncStatus is SyncStatus.Success { SyncReady: true });

    private int AliveCount => SharedState.AliveCount;

    private List<long>? GetTimeTillSync()
    {
        var unsyncedTimes = PiZeroCameraManager.PiZeroCameras.Values
            .Select(x => x.SyncStatus)
            .Where(x => x is SyncStatus.Success { SyncReady: false })
            .Select(x => (SyncStatus.Success)x!)
            .Select(x => x.SyncTiming / 1000)
            .ToList();

        return unsyncedTimes.Count == 0 ? null : unsyncedTimes;
    }

    private float? TimeTillSync => GetTimeTillSync()?.Max();

    private async Task RequestSync()
    {
        SyncedFrames = false;

        try
        {
            await SyncManager.GetSyncStatus();
            SyncedFrames = true;
        }
        catch (OperationCanceledException)
        {
            SyncedFrames = false;
        }
    }

    private async Task CancelSync()
    {
        await SyncManager.CancelSyncStatus();
    }

    private void OverrideSync()
    {
        SyncedFrames = true;
    }

    private async Task OnSyncChanged()
    {
        await InvokeAsync(() =>
        {
            UpdateTooltipTransformSync();
            StateHasChanged();
        });
    }

    private async Task OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        SharedState.OnChange += OnChange;
        ChangeListener.OnSyncChange += OnSyncChanged;

        UpdateTooltipTransformSync();
    }

    public void Dispose()
    {
        SharedState.OnChange -= OnChange;
        ChangeListener.OnSyncChange -= OnSyncChanged;
        GC.SuppressFinalize(this);
    }
}