using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera;

public enum PiZeroCameraCameraMode
{
    Still,
    Video
}

public abstract record PiZeroNtpRequest
{
    public sealed record Requested : PiZeroNtpRequest;

    public sealed record Success(string Message) : PiZeroNtpRequest;
    public sealed record Cancelled : PiZeroNtpRequest;

    public abstract record Failure : PiZeroNtpRequest
    {
        public sealed record FailedToRequest(string Message) : Failure;
        public sealed record FailedToParseRegex(string Message) : Failure;
        public sealed record FailedToParseJson(string Message) : Failure;
        public sealed record Failed(string Message) : Failure;
    }
}

public abstract record PiZeroCameraUpdateRequest
{
    public sealed record Requested : PiZeroCameraUpdateRequest;
    public sealed record Downloading : PiZeroCameraUpdateRequest;
    public sealed record Downloaded : PiZeroCameraUpdateRequest;
    public sealed record Success : PiZeroCameraUpdateRequest;
    public sealed record UnknownSuccess : PiZeroCameraUpdateRequest;
    public sealed record Cancelled : PiZeroCameraUpdateRequest;

    public abstract record Failure : PiZeroCameraUpdateRequest
    {
        public sealed record FailedToRequest : Failure;
        public sealed record Failed(string Message) : Failure;
        public sealed record UnknownFailure : Failure;
        public sealed record VersionMismatch : Failure;
    }
}

public class PiZeroCameraStatus
{
    public required string Version { get; set; }
    public string? IpAddress { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PiZeroCameraCameraMode CameraMode { get; set; }
}

public abstract record SyncStatus
{
    public sealed record Requested : SyncStatus;
    public sealed record Cancelled : SyncStatus;
    public sealed record Success(bool SyncReady, long SyncTiming) : SyncStatus;

    public abstract record Failure : SyncStatus
    {
        public sealed record Failed(string Message) : Failure;
        public sealed record FailedToRequest(string Message) : Failure;
    }
}

public class PiZeroCamera
{
    public required string Id { get; set; }
    public PiZeroCameraStatus? Status { get; set; }

    // Pending requests
    public PiZeroCameraCameraMode? PendingCameraMode { get; set; }
    public PiZeroNtpRequest? NtpRequest { get; set; }
    public PiZeroCameraUpdateRequest? UpdateRequest { get; set; }
    public DateTimeOffset? LastNtpSync { get; set; }
    public float? LastNtpOffsetMillis { get; set; }
    public float? LastNtpErrorMillis { get; set; }
    public SyncStatus? SyncStatus { get; set; }

    public bool? Pingable { get; set; }
}