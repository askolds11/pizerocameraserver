using System.Text.Json.Serialization;

namespace picamerasserver.PiZero.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TakePicture), nameof(TakePicture))]
[JsonDerivedType(typeof(SendPicture), nameof(SendPicture))]
[JsonDerivedType(typeof(SyncStatus), nameof(SyncStatus))]
public abstract record CameraResponse
{
    public sealed record TakePicture(SuccessWrapper<TakePictureResponse> Response) : CameraResponse;

    public sealed record SendPicture(SuccessWrapper<SendPictureResponse> Response) : CameraResponse;
    
    public sealed record SyncStatus(SuccessWrapper<SyncStatusResponse> Response) : CameraResponse;
}
