namespace picamerasserver.pizerocamera;

public class PiZeroIndicatorStatus
{
    public required string Version { get; set; }
    public string? IpAddress { get; set; }
}

public class PiZeroIndicator
{
    public static string Id => "IND";
    public PiZeroIndicatorStatus? Status { get; set; }

    // Pending requests
    public PiZeroNtpRequest? NtpRequest { get; set; }
    public DateTimeOffset? LastNtpSync { get; set; }
    public float? LastNtpOffsetMillis { get; set; }
    public float? LastNtpErrorMillis { get; set; }

    public bool? Pingable { get; set; }
}