using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.PiZero;
using picamerasserver.PiZero.GetAlive;
using picamerasserver.PiZero.Manager;

namespace picamerasserver.Components.Components.NewPicture;

public partial class GetAliveTab : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected PiZeroManager PiZeroManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected IGetAliveManager GetAliveManager { get; init; } = null!;

    private PictureSetModel? PictureSet => SharedState.PictureSet;

    private bool PingActive => SharedState.PingActive;
    private bool AnyActive => SharedState.AnyActive;

    private bool Alived
    {
        get => SharedState.Alived;
        set => SharedState.Alived = value;
    }

    private bool IndicatorAlive => SharedState.IndicatorAlive;

    private int PingedCount => PiZeroManager.PiZeroCameras.Values.Count(x => x.Pingable == true);
    private int StatusCount => PiZeroManager.PiZeroCameras.Values.Count(x => x.Status != null);

    private int AliveCount => SharedState.AliveCount;
    private int TotalCount => PiZeroManager.PiZeroCameras.Count;

    private async Task GetAlive()
    {
        Alived = false;

        try
        {
            var task1 = GetAliveManager.Ping();
            var task2 = GetAliveManager.GetStatus();
            await Task.WhenAll(task1, task2);
            Alived = true;
        }
        catch (OperationCanceledException)
        {
            Alived = false;
        }
    }

    private async Task CancelGetAlive()
    {
        await GetAliveManager.CancelPing();
    }

    private void OverrideGetAlive()
    {
        Alived = true;
    }

    private async Task OnChange()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        SharedState.OnChange += OnChange;
        ChangeListener.OnPingChange += OnChange;
    }

    public void Dispose()
    {
        SharedState.OnChange -= OnChange;
        ChangeListener.OnPingChange += OnChange;
        GC.SuppressFinalize(this);
    }
}