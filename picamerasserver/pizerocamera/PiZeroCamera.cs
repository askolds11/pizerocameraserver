using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera;

public enum PiZeroCameraCameraMode
{
    Still,
    Video
}

public abstract record PiZeroCameraNtpRequest
{
    public sealed record Requested : PiZeroCameraNtpRequest;
    public sealed record FailedToRequest(string Message) : PiZeroCameraNtpRequest;
    public sealed record Failure(string Message) : PiZeroCameraNtpRequest;
    public sealed record Success(string Message) : PiZeroCameraNtpRequest;
    public sealed record Unknown(string Message) : PiZeroCameraNtpRequest;
}

public abstract record PiZeroCameraTakePictureRequest
{
    public sealed record Requested : PiZeroCameraTakePictureRequest;
    public sealed record FailedToRequest(string Message) : PiZeroCameraTakePictureRequest;

    public sealed record RequestedSend : PiZeroCameraTakePictureRequest;
    public sealed record FailedToRequestSend(string Message) : PiZeroCameraTakePictureRequest;
    public sealed record Taken : PiZeroCameraTakePictureRequest;
    public sealed record SavedOnDevice : PiZeroCameraTakePictureRequest;
    public sealed record Failure(TakePictureResponse.Failure Reason) : PiZeroCameraTakePictureRequest;
    public sealed record FailureSend(SendPictureResponse.Failure Reason) : PiZeroCameraTakePictureRequest;
    public sealed record Success : PiZeroCameraTakePictureRequest;
    public sealed record Unknown(string Message) : PiZeroCameraTakePictureRequest;
}

public class PiZeroCameraStatus
{
    public required string Version { get; set; }
    public string? IpAddress { get; set; }
    public PiZeroCameraCameraMode CameraMode { get; set; }
}

public class PiZeroCamera
{
    public required string Id { get; set; }
    public PiZeroCameraStatus? Status { get; set; }
    
    // Pending requests
    public PiZeroCameraCameraMode? PendingCameraMode { get; set; }
    public PiZeroCameraNtpRequest? NtpRequest { get; set; }
    public PiZeroCameraTakePictureRequest? TakePictureRequest { get; set; }
}