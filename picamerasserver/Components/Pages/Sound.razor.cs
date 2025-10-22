using Microsoft.AspNetCore.Components;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;

namespace picamerasserver.Components.Pages;

public partial class Sound : ComponentBase, IDisposable
{
    [Inject] protected SoundManager SoundManager { get; init; } = null!;
    [Inject] protected PiZeroManager PiZeroManager { get; init; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;

    private bool IndicatorAlive => PiZeroManager.PiZeroIndicator is { Pingable: true, Status: not null };

    private async Task TestSignal()
    {
        await SoundManager.SendSignal();
    }
    
    private async Task OnChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        ChangeListener.OnPingChange += OnChanged;
    }

    public void Dispose()
    {
        ChangeListener.OnPingChange -= OnChanged;
        GC.SuppressFinalize(this);
    }
    
    
}