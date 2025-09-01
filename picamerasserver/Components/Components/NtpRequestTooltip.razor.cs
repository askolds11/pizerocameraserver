using Microsoft.AspNetCore.Components;
using picamerasserver.pizerocamera;

namespace picamerasserver.Components.Components;

public partial class NtpRequestTooltip : ComponentBase
{
    [Parameter, EditorRequired] public required PiZeroCamera PiZeroCamera { get; set; }

    private (string header, string? message, float? offset, float? error) GetContent()
    {
        return PiZeroCamera.NtpRequest switch
        {
            null => ("Nothing", null, null, null),
            PiZeroCameraNtpRequest.Cancelled _ => ("Cancelled", null, null, null),
            PiZeroCameraNtpRequest.Failure.Failed failed => ("Failed on device", failed.Message, null, null),
            PiZeroCameraNtpRequest.Failure.FailedToParseJson failedToParseJson => ("Failed json",
                failedToParseJson.Message, null, null),
            PiZeroCameraNtpRequest.Failure.FailedToParseRegex failedToParseRegex => ("Failed regex",
                failedToParseRegex.Message, null, null),
            PiZeroCameraNtpRequest.Failure.FailedToRequest failedToRequest => ("Failed request",
                failedToRequest.Message, null, null),
            PiZeroCameraNtpRequest.Requested => ("Requested", null, null, null),
            PiZeroCameraNtpRequest.Success success => ("Success", success.Message, PiZeroCamera.LastNtpOffsetMillis,
                PiZeroCamera.LastNtpErrorMillis),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}