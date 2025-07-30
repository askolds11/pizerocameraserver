using System.Drawing;
using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Pages;

public partial class NtpPage : ComponentBase, IDisposable
{
    [Inject]
    protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;

    private async Task RequestNtpSync()
    {
        await PiZeroCameraManager.RequestNtpSync(null);
    }
    
    private void OnGlobalChanged()
    {
        InvokeAsync(StateHasChanged);
    }
    
    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnChange += OnGlobalChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnChange -= OnGlobalChanged;
    }
    
    private Color ColorTransform(string id)
    {
        var piZeroCamera = PiZeroCameraManager.PiZeroCameras[id];
        return piZeroCamera.NtpRequest switch
        {
            null => Color.FromArgb(0x00, 0x00, 0x00),
            PiZeroCameraNtpRequest.FailedToRequest failedToRequest => Color.FromArgb(0xFF, 0x00, 0x00),
            PiZeroCameraNtpRequest.Failure failure => Color.FromArgb(0x55, 0x00, 0x00),
            PiZeroCameraNtpRequest.Requested requested => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroCameraNtpRequest.Success success => Color.FromArgb(0x00, 0xFF, 0x00),
            PiZeroCameraNtpRequest.Unknown unknown => Color.FromArgb(0x00, 0x00, 0xFF),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}