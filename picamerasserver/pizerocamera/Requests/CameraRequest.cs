using System.Text.Json.Serialization;
using picamerasserver.pizerocamera.manager;

namespace picamerasserver.pizerocamera.Requests;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TakePicture), nameof(TakePicture))]
[JsonDerivedType(typeof(SendPicture), nameof(SendPicture))]
[JsonDerivedType(typeof(StartPreview), nameof(StartPreview))]
[JsonDerivedType(typeof(StopPreview), nameof(StopPreview))]
[JsonDerivedType(typeof(SetControls), nameof(SetControls))]
[JsonDerivedType(typeof(GetControls), nameof(GetControls))]
[JsonDerivedType(typeof(GetControlLimits), nameof(GetControlLimits))]
public abstract record CameraRequest
{
    public sealed record TakePicture(
        long PictureEpoch,
        Guid Uuid
    ) : CameraRequest;

    public sealed record SendPicture(
        Guid Uuid
    ) : CameraRequest;

    public sealed record SetControls(PiZeroCameraCameraMode CameraMode, Controls CameraControls) : CameraRequest;

    public sealed record GetControlLimits : CameraRequest;

    public sealed record GetControls(PiZeroCameraCameraMode CameraMode) : CameraRequest;

    public sealed record StartPreview : CameraRequest;

    public sealed record StopPreview : CameraRequest;
}