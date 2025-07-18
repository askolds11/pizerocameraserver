using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Failure.PictureFailedToTake), nameof(Failure.PictureFailedToTake))]
[JsonDerivedType(typeof(PictureSavedOnDevice), nameof(PictureSavedOnDevice))]
[JsonDerivedType(typeof(Failure.PictureFailedToSave), nameof(Failure.PictureFailedToSave))]
[JsonDerivedType(typeof(PictureSent), nameof(PictureSent))]
[JsonDerivedType(typeof(Failure.PictureFailedToSend), nameof(Failure.PictureFailedToSend))]
public abstract record TakePictureResponse
{
    public sealed record PictureSavedOnDevice : TakePictureResponse;

    public sealed record PictureSent : TakePictureResponse;

    public abstract record Failure : TakePictureResponse
    {
        public sealed record PictureFailedToTake(string Message) : TakePictureResponse;

        public sealed record PictureFailedToSave(string Message) : TakePictureResponse;

        public sealed record PictureFailedToSend(string Message) : TakePictureResponse;
    }
}