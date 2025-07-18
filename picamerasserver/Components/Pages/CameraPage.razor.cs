using Microsoft.AspNetCore.Components;
using picamerasserver.Components.Components;
using picamerasserver.mqtt;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.manager.Requests;

namespace picamerasserver.Components.Pages;

public partial class CameraPage : ComponentBase
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;

    private Guid _cacheBuster = Guid.CreateVersion7();

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
    
    private async Task TakePicture()
    {
        await PiZeroCameraManager.RequestTakePicture(null);
    }
    
    private async Task OnSwitchMode(CameraRequest cameraRequest)
    {
        // await MqttStuff.SetCameraMode(cameraRequest);
        await Task.Delay(2000);
        _cacheBuster = Guid.CreateVersion7();
    }

    private async Task OnSubmitConfig()
    {
        // var controls = new CameraRequest.SetControls();
        // controls.NoiseReductionMode = NoiseReductionModeEnum.Off;
        // await MqttStuff.SetConfig(controls);
    }

    private async Task OnGetConfig()
    {
        // await MqttStuff.SetConfig(new CameraRequest.GetControlLimits());
    }

    private string ColorTransform(PiZeroCamera piZeroCamera)
    {
        return piZeroCamera.TakePictureRequest switch
        {
            null => "#000000",
            PiZeroCameraTakePictureRequest.FailedToRequest => "#FF0000",
            PiZeroCameraTakePictureRequest.Failure => "#550000",
            PiZeroCameraTakePictureRequest.Requested => "#555500",
            PiZeroCameraTakePictureRequest.SavedOnDevice => "#005555",
            PiZeroCameraTakePictureRequest.Success => "#00FF00",
            PiZeroCameraTakePictureRequest.Unknown => "#0000FF",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}