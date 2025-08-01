using Microsoft.AspNetCore.Components;
using picamerasserver.Database.Models;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.Components.Components;

public partial class PictureRequestTooltip : ComponentBase
{
    [Parameter, EditorRequired] public required CameraPictureModel CameraPicture { get; set; }
    
    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;
    
    private (string header, string? message) GetContent()
    {
        return CameraPicture.CameraPictureStatus switch
        {
            CameraPictureStatus.Requested => ("Requested", CameraPicture.StatusMessage),
            CameraPictureStatus.FailedToRequest => ("Failed to request", CameraPicture.StatusMessage),
            CameraPictureStatus.Taken => ("Picture taken", CameraPicture.StatusMessage),
            CameraPictureStatus.SavedOnDevice => ("Saved on device", CameraPicture.StatusMessage),
            CameraPictureStatus.Failed => ("Failed", CameraPicture.StatusMessage),
            CameraPictureStatus.PictureFailedToSave => ("Failed to save", CameraPicture.StatusMessage),
            CameraPictureStatus.PictureFailedToSchedule => ("Failed to schedule", CameraPicture.StatusMessage),
            CameraPictureStatus.PictureFailedToTake => ("Failed to take", CameraPicture.StatusMessage),
            CameraPictureStatus.RequestedSend => ("Requested send", CameraPicture.StatusMessage),
            CameraPictureStatus.FailedToRequestSend => ("Failed to request send", CameraPicture.StatusMessage),
            CameraPictureStatus.FailureSend => ("Failed to send 1", CameraPicture.StatusMessage),
            CameraPictureStatus.Success => ("Successfully sent", CameraPicture.StatusMessage),
            CameraPictureStatus.Unknown => ("Unknown", CameraPicture.StatusMessage),
            CameraPictureStatus.PictureFailedToRead => ("Failed to read when sending", CameraPicture.StatusMessage),
            CameraPictureStatus.PictureFailedToSend => ("Failed to send 2", CameraPicture.StatusMessage),
            null => ("Nothing", CameraPicture.StatusMessage),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private async Task RequestSend()
    {
        await PiZeroCameraManager.RequestSendPictureIndividual(CameraPicture.PictureRequestId, CameraPicture.CameraId);
    }
}