using Microsoft.AspNetCore.Components;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Manager;
using picamerasserver.PiZero.Ntp;
using picamerasserver.PiZero.Requests;

namespace picamerasserver.Components.Pages;

public partial class NtpPage : ComponentBase, IDisposable
{
    [Inject] protected PiZeroManager PiZeroManager { get; set; } = null!;
    [Inject] protected ChangeListener ChangeListener { get; init; } = null!;
    [Inject] protected INtpManager NtpManager { get; init; } = null!;

    private async Task RequestNtpSyncStep()
    {
        try
        {
            await NtpManager.RequestNtpSync(new NtpRequest.Step());
        }
        catch (OperationCanceledException)
        {
            
        }
    }
    
    private async Task RequestNtpSyncSlew()
    {
        try
        {
            await NtpManager.RequestNtpSync(new NtpRequest.Slew());
        }
        catch (OperationCanceledException)
        {
            
        }
        
    }
    
    private List<float>? GetOffsets()
    {
        var offsets = PiZeroManager.PiZeroCameras.Values
            .Where(x => x.LastNtpOffsetMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpOffsetMillis!))
            .ToList();

        return offsets.Count == 0 ? null : offsets;
    }

    private List<float>? GetErrors()
    {
        var errors = PiZeroManager.PiZeroCameras.Values
            .Where(x => x.LastNtpErrorMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpErrorMillis!))
            .ToList();

        return errors.Count == 0 ? null : errors;
    }

    private float? MinOffset => GetOffsets()?.Min();
    private float? MaxOffset => GetOffsets()?.Max();
    private float? MinError => GetErrors()?.Min();
    private float? MaxError => GetErrors()?.Max();
    
    private async Task OnGlobalChanged()
    {
        await InvokeAsync(() =>
        {
            UpdateTooltipTransform();
            StateHasChanged();
        });
    }
    
    protected override void OnInitialized()
    {
        ChangeListener.OnNtpChange += OnGlobalChanged;
    }

    public void Dispose()
    {
        ChangeListener.OnNtpChange -= OnGlobalChanged;
        GC.SuppressFinalize(this);
    }
}