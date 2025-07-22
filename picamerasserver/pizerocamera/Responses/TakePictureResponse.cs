using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PictureTaken), nameof(PictureTaken))]
[JsonDerivedType(typeof(PictureSavedOnDevice), nameof(PictureSavedOnDevice))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
[JsonDerivedType(typeof(Failure.PictureFailedToSchedule), nameof(Failure.PictureFailedToSchedule))]
[JsonDerivedType(typeof(Failure.PictureFailedToTake), nameof(Failure.PictureFailedToTake))]
[JsonDerivedType(typeof(Failure.PictureFailedToSave), nameof(Failure.PictureFailedToSave))]
public abstract record TakePictureResponse
{
    public sealed record PictureTaken : TakePictureResponse;

    public sealed record PictureSavedOnDevice : TakePictureResponse;


    public abstract record Failure : TakePictureResponse
    {
        public sealed record Failed : Failure;

        public sealed record PictureFailedToSchedule : Failure;

        public sealed record PictureFailedToTake(string Message) : Failure;

        public sealed record PictureFailedToSave(string Message) : Failure;
    }
}