using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TakePicture), nameof(TakePicture))]
[JsonDerivedType(typeof(SendPicture), nameof(SendPicture))]
public abstract record CameraResponse
{
    public sealed record TakePicture(SuccessWrapper<TakePictureResponse> Response) : CameraResponse;

    public sealed record SendPicture(SuccessWrapper<SendPictureResponse> Response) : CameraResponse;
}
