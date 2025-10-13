using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.Ntp;

namespace picamerasserver.Components.Components;

public partial class NtpRequestTooltip2 : ComponentBase
{
    [Parameter, EditorRequired] public required PiZeroNtpRequest NtpRequest { get; set; }
    [Parameter, EditorRequired] public required float? LastNtpOffsetMillis { get; set; }
    [Parameter, EditorRequired] public required float? LastNtpErrorMillis { get; set; }
    [Parameter, EditorRequired] public required string DeviceId { get; set; }
    [Inject] protected INtpManager NtpManager { get; init; } = null!;

    private (string header, string? message, float? offset, float? error) GetContent()
    {
        return NtpRequest switch
        {
            null => ("Nothing", null, null, null),
            PiZeroNtpRequest.Cancelled _ => ("Cancelled", null, null, null),
            PiZeroNtpRequest.Failure.Failed failed => ("Failed on device", failed.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToParseJson failedToParseJson => ("Failed json",
                failedToParseJson.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToParseRegex failedToParseRegex => ("Failed regex",
                failedToParseRegex.Message, null, null),
            PiZeroNtpRequest.Failure.FailedToRequest failedToRequest => ("Failed request",
                failedToRequest.Message, null, null),
            PiZeroNtpRequest.Requested => ("Requested", null, null, null),
            PiZeroNtpRequest.Success success => ("Success", success.Message, LastNtpOffsetMillis,
                LastNtpErrorMillis),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    private async Task RequestSync()
    {
        await NtpManager.RequestNtpSyncIndividual(DeviceId);
    }
}