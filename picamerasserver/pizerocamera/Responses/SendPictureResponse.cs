using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PictureSent), nameof(PictureSent))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
[JsonDerivedType(typeof(Failure.PictureFailedToRead), nameof(Failure.PictureFailedToRead))]
[JsonDerivedType(typeof(Failure.PictureFailedToSend), nameof(Failure.PictureFailedToSend))]
public abstract record SendPictureResponse(Guid Uuid)
{
    public sealed record PictureSent(Guid Uuid) : SendPictureResponse(Uuid);

    public abstract record Failure(Guid Uuid) : SendPictureResponse(Uuid)
    {
        public sealed record Failed(Guid Uuid, string Message) : Failure(Uuid);

        public sealed record PictureFailedToRead(Guid Uuid, string Message) : Failure(Uuid);
        public sealed record PictureFailedToSend(Guid Uuid, string Message) : Failure(Uuid);
    }
}