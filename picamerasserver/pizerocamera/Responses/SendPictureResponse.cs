using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PictureSent), nameof(PictureSent))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
[JsonDerivedType(typeof(Failure.PictureFailedToSend), nameof(Failure.PictureFailedToSend))]
public abstract record SendPictureResponse
{
    public sealed record PictureSent : SendPictureResponse;

    public abstract record Failure : SendPictureResponse
    {
        public sealed record Failed : Failure;

        public sealed record PictureFailedToSend(string Message) : Failure;
    }
}