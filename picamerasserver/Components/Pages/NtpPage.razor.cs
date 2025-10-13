using System.Drawing;
using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Ntp;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.Components.Pages;

public partial class NtpPage : ComponentBase, IDisposable
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;
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
        var offsets = PiZeroCameraManager.PiZeroCameras.Values
            .Where(x => x.LastNtpOffsetMillis != null)
            .Select(x => Math.Abs((float)x.LastNtpOffsetMillis!))
            .ToList();

        return offsets.Count == 0 ? null : offsets;
    }

    private List<float>? GetErrors()
    {
        var errors = PiZeroCameraManager.PiZeroCameras.Values
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
    
    private Color ColorTransform(string id)
    {
        var piZeroCamera = PiZeroCameraManager.PiZeroCameras[id];
        return piZeroCamera.NtpRequest switch
        {
            null => Color.FromArgb(0x00, 0x00, 0x00),
            PiZeroNtpRequest.Failure.Failed _ => Color.FromArgb(0xff, 0x00, 0x00),
            PiZeroNtpRequest.Failure.FailedToParseJson _ => Color.FromArgb(0xff, 0x00, 0x00),
            PiZeroNtpRequest.Failure.FailedToParseRegex _ => Color.FromArgb(0xff, 0x00, 0x00),
            PiZeroNtpRequest.Failure.FailedToRequest _ => Color.FromArgb(0xff, 0x00, 0x00),
            PiZeroNtpRequest.Requested _ => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroNtpRequest.Success _ => Color.FromArgb(0x00, 0xFF, 0x00),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}