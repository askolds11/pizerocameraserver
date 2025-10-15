using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.GetAlive;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Components.NewPicture;

public partial class GetAliveTab : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required SharedState SharedState { get; set; }

    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
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

    private int PingedCount => PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.Pingable == true);
    private int StatusCount => PiZeroCameraManager.PiZeroCameras.Values.Count(x => x.Status != null);

    private int AliveCount => SharedState.AliveCount;
    private int TotalCount => PiZeroCameraManager.PiZeroCameras.Count;

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