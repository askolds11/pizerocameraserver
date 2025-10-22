using Microsoft.AspNetCore.Components;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.Sync;
using picamerasserver.PiZero.SyncReceiver;

namespace picamerasserver.Components.Pages;

public partial class Sync : ComponentBase, IDisposable
{
    [Inject] protected PiZeroManager PiZeroManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected ISyncManager SyncManager { get; init; } = null!;
    [Inject] protected SyncPayloadService SyncPayloadService { get; init; } = null!;

    private SyncPayload? SyncPayload = null;
    
    private bool SyncActive => SyncManager.SyncActive;

    private int SyncedCount =>
        PiZeroManager.PiZeroCameras.Values.Count(x => x.SyncStatus is SyncStatus.Success { SyncReady: true });

    private int AliveCount => PiZeroManager.PiZeroCameras.Values.Count(x => x is { Pingable: true, Status: not null });

    private List<long>? GetTimeTillSync()
    {
        var unsyncedTimes = PiZeroManager.PiZeroCameras.Values
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
        try
        {
            await SyncManager.GetSyncStatus();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CancelSync()
    {
        await SyncManager.CancelSyncStatus();
    }

    private void RefreshSync()
    {
        SyncPayload = SyncPayloadService.GetLatest();
    }

    private async Task OnSyncChanged()
    {
        await InvokeAsync(() =>
        {
            UpdateTooltipTransformSync();
            StateHasChanged();
        });
    }
    
    private async Task OnChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        ChangeListener.OnSyncChange += OnSyncChanged;
        ChangeListener.OnPingChange += OnChanged;

        SyncPayload = SyncPayloadService.GetLatest();

        UpdateTooltipTransformSync();
    }

    public void Dispose()
    {
        ChangeListener.OnSyncChange -= OnSyncChanged;
        ChangeListener.OnPingChange -= OnChanged;
        GC.SuppressFinalize(this);
    }
}