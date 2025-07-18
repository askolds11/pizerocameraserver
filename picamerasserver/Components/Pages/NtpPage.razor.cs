using Microsoft.AspNetCore.Components;
using picamerasserver.Components.Components;
using picamerasserver.mqtt;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Pages;

public partial class NtpPage : ComponentBase
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
    
    private string ColorTransform(PiZeroCamera piZeroCamera)
    {
        return piZeroCamera.NtpRequest switch
        {
            null => "#000000",
            PiZeroCameraNtpRequest.FailedToRequest failedToRequest => "#FF0000",
            PiZeroCameraNtpRequest.Failure failure => "#550000",
            PiZeroCameraNtpRequest.Requested requested => "#555500",
            PiZeroCameraNtpRequest.Success success => "#00FF00",
            PiZeroCameraNtpRequest.Unknown unknown => "#0000FF",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}