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
        await PiZeroCameraManager.RequestNtpSync();
    }
    
    private void OnGlobalChanged()
    {
        InvokeAsync(() =>
        {
            UpdateTooltipTransform();
            StateHasChanged();
        });
    }
    
    protected override void OnInitialized()
    {
        PiZeroCameraManager.OnNtpChange += OnGlobalChanged;
    }

    public void Dispose()
    {
        PiZeroCameraManager.OnNtpChange -= OnGlobalChanged;
        GC.SuppressFinalize(this);
    }
    
    private Color ColorTransform(string id)
    {
        var piZeroCamera = PiZeroCameraManager.PiZeroCameras[id];
        return piZeroCamera.NtpRequest switch
        {
            null => Color.FromArgb(0x00, 0x00, 0x00),
            PiZeroCameraNtpRequest.Failure _ => Color.FromArgb(0xff, 0x00, 0x00),
            PiZeroCameraNtpRequest.Requested _ => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroCameraNtpRequest.Success _ => Color.FromArgb(0x00, 0xFF, 0x00),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}