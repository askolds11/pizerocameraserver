using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using picamerasserver.pizerocamera.manager;
using Color = System.Drawing.Color;

namespace picamerasserver.Components.Components;

public partial class PreviewDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    // [CascadingParameter]
    [Parameter, EditorRequired] public required string CameraId { get; set; }

    [Inject] protected PiZeroCameraManager PiZeroCameraManager { get; init; } = null!;

    private string PreviewStreamUrl => $"http://pizero{CameraId}.local:8000/stream.mjpg";

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        var currCol = CameraId[0];
        var currRow = CameraId[1] - '0';
        char newCol;
        int newRow;
        switch (args.Key)
        {
            case "ArrowDown":
                newCol = currCol;
                newRow = currRow is 1 ? currRow : currRow - 1;
                break;
            case "ArrowUp":
                newCol = currCol;
                newRow = currRow is 6 ? currRow : currRow + 1;
                break;
            case "ArrowLeft":
                newCol = currCol is 'A' ? currCol : (char)(currCol - 1);
                newRow = currRow;
                break;
            case "ArrowRight":
                newCol = currCol is 'P' ? currCol : (char)(currCol + 1);
                newRow = currRow;
                break;
            default:
                return;
        }

        var newCameraId = string.Concat(newCol, newRow);
        CameraId = newCameraId;
    }

    private Color ColorTransform(string cameraId)
    {
        var piZeroCamera = PiZeroCameraManager.PiZeroCameras[cameraId];

        // if match
        if (cameraId == CameraId)
        {
            return piZeroCamera.Status != null ? Color.FromArgb(0x00, 0xFF, 0x00) : Color.FromArgb(0xFF, 0x00, 0x00);
        }


        // not match
        return piZeroCamera.Status != null ? Color.FromArgb(0x55, 0x55, 0x55) : Color.FromArgb(0x00, 0x00, 0x00);
    }
}