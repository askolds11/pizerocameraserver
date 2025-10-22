using Microsoft.AspNetCore.Components;
using picamerasserver.PiZero;
using picamerasserver.PiZero.Ntp;

namespace picamerasserver.Components.Components.StatusTable.Tooltip;

public partial class NtpRequestTooltip : ComponentBase
{
    [Parameter, EditorRequired] public required PiZeroCamera PiZeroCamera { get; set; }
    [Inject] protected INtpManager NtpManager { get; init; } = null!;

    private (string header, string? message, float? offset, float? error) GetContent()
    {
        return PiZeroCamera.NtpRequest switch
        {
            null => ("Nothing", null, null, null),
            PiZeroNtpRequest.Cancelled => ("Cancelled", null, null, null),
            PiZeroNtpRequest.Failure.Failed failed => ("Failed on device", failed.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToParseJson failedToParseJson => ("Failed json",
                failedToParseJson.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToParseRegex failedToParseRegex => ("Failed regex",
                failedToParseRegex.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToRequest failedToRequest => ("Failed request",
                failedToRequest.Message, null, null),
            PiZeroNtpRequest.Requested => ("Requested", null, null, null),
            PiZeroNtpRequest.Success success => ("Success", success.Message, PiZeroCamera.LastNtpOffsetMillis,
                PiZeroCamera.LastNtpErrorMillis),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    private async Task RequestSync()
    {
        await NtpManager.RequestNtpSyncIndividual(PiZeroCamera.Id);
    }
}