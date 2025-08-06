using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera;

public enum PiZeroCameraCameraMode
{
    Still,
    Video
}

public abstract record PiZeroCameraNtpRequest
{
    public sealed record Requested : PiZeroCameraNtpRequest;

    public sealed record Success(string Message) : PiZeroCameraNtpRequest;

    public abstract record Failure(Guid Uuid) : PiZeroCameraNtpRequest
    {
        public sealed record FailedToRequest(string Message) : PiZeroCameraNtpRequest;
        public sealed record FailedToParseRegex(string Message) : PiZeroCameraNtpRequest;
        public sealed record FailedToParseJson(string Message) : PiZeroCameraNtpRequest;
        public sealed record Failed(string Message) : PiZeroCameraNtpRequest;
    }
}

public class PiZeroCameraStatus
{
    public required string Version { get; set; }
    public string? IpAddress { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PiZeroCameraCameraMode CameraMode { get; set; }
}

public class PiZeroCamera
{
    public required string Id { get; set; }
    public PiZeroCameraStatus? Status { get; set; }

    // Pending requests
    public PiZeroCameraCameraMode? PendingCameraMode { get; set; }
    public PiZeroCameraNtpRequest? NtpRequest { get; set; }
    public DateTimeOffset? LastNtpSync { get; set; }
    public float? LastNtpOffsetMillis { get; set; }
    public float? LastNtpErrorMillis { get; set; }

    public bool? Pingable { get; set; }
}