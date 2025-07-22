using System.Drawing;
using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.Requests;

namespace picamerasserver.Components.Pages;

public partial class CameraPage : ComponentBase, IDisposable
{
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; set; } = null!;

    private Guid _workUuid = Guid.CreateVersion7();

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
        _workUuid = Guid.CreateVersion7();
        await PiZeroCameraManager.RequestTakePicture(null, _workUuid);
    }

    private async Task SendPicture()
    {
        await PiZeroCameraManager.RequestSendPicture(null, _workUuid);
    }

    private async Task OnSwitchMode(CameraRequest cameraRequest)
    {
        // await MqttStuff.SetCameraMode(cameraRequest);
        await Task.Delay(2000);
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

    private Color ColorTransform(PiZeroCamera piZeroCamera)
    {
        return piZeroCamera.TakePictureRequest switch
        {
            null => Color.FromArgb(0x00, 0x00, 0x00),
            PiZeroCameraTakePictureRequest.FailedToRequest => Color.FromArgb(0xFF, 0x00, 0x00),
            PiZeroCameraTakePictureRequest.Failure => Color.FromArgb(0x55, 0x00, 0x00),
            PiZeroCameraTakePictureRequest.Requested => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroCameraTakePictureRequest.SavedOnDevice => Color.FromArgb(0x00, 0x55, 0x00),
            PiZeroCameraTakePictureRequest.Success => Color.FromArgb(0x00, 0xFF, 0x00),
            PiZeroCameraTakePictureRequest.Unknown => Color.FromArgb(0x00, 0x00, 0xFF),
            PiZeroCameraTakePictureRequest.Taken => Color.FromArgb(0x00, 0x55, 0x55),
            PiZeroCameraTakePictureRequest.RequestedSend => Color.FromArgb(0x55, 0x55, 0x00),
            PiZeroCameraTakePictureRequest.FailedToRequestSend => Color.FromArgb(0x55, 0x00, 0x00),
            PiZeroCameraTakePictureRequest.FailureSend => Color.FromArgb(0x99, 0x00, 0x00),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}