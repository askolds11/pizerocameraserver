using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PictureTaken), nameof(PictureTaken))]
[JsonDerivedType(typeof(PictureSavedOnDevice), nameof(PictureSavedOnDevice))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
[JsonDerivedType(typeof(Failure.PictureFailedToSchedule), nameof(Failure.PictureFailedToSchedule))]
[JsonDerivedType(typeof(Failure.PictureFailedToTake), nameof(Failure.PictureFailedToTake))]
[JsonDerivedType(typeof(Failure.PictureFailedToSave), nameof(Failure.PictureFailedToSave))]
public abstract record TakePictureResponse(Guid Uuid)
{
    public sealed record PictureTaken(Guid Uuid, long MonotonicTime) : TakePictureResponse(Uuid);

    public sealed record PictureSavedOnDevice(Guid Uuid) : TakePictureResponse(Uuid);


    public abstract record Failure(Guid Uuid) : TakePictureResponse(Uuid)
    {
        public sealed record Failed(Guid Uuid, string Message) : Failure(Uuid);

        public sealed record PictureFailedToSchedule(Guid Uuid, string Message) : Failure(Uuid);

        public sealed record PictureFailedToTake(Guid Uuid, string Message) : Failure(Uuid);

        public sealed record PictureFailedToSave(Guid Uuid, string Message) : Failure(Uuid);
    }
}