using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;

namespace picamerasserver.Components.Components;

public partial class SyncRequestTooltip : ComponentBase
{
    [Parameter, EditorRequired] public required PiZeroCamera PiZeroCamera { get; set; }

    private (string header, string? message, bool? success, long? timer) GetContent()
    {
        return PiZeroCamera.SyncStatus switch
        {
            null => ("Nothing", null, null, null),
            SyncStatus.Cancelled _ => ("Cancelled", null, null, null),
            SyncStatus.Failure.Failed failed => ("Failed", failed.Message, null, null),
            SyncStatus.Failure.FailedToRequest failedToRequest => ("Failed request", failedToRequest.Message, null,
                null),
            SyncStatus.Requested _ => ("Requested", null, null, null),
            SyncStatus.Success success => ("Success", null, success.SyncReady, success.SyncTiming / 1000),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}