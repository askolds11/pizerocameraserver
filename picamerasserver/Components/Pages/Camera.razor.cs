using Microsoft.AspNetCore.Components;
using picamerasserver.Components.Components;
using picamerasserver.mqtt;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.Components.Pages;

public partial class Camera : ComponentBase
{
    [Inject]
    protected MqttStuff MqttStuff { get; set; } = null!;

    private Guid _cacheBuster = Guid.CreateVersion7();

    private async Task OnSwitchMode(CameraRequest cameraRequest)
    {
        await MqttStuff.SetCameraMode(cameraRequest);
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
        await MqttStuff.SetConfig(new CameraRequest.GetControlLimits());
    }
    
    private string ColorTransform(Something something)
    {
        return "#00FF00";
    }
}