using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Success), nameof(Success))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
public abstract record SyncStatusResponse
{
    public sealed record Success(bool SyncReady, long SyncTiming) : SyncStatusResponse;

    public abstract record Failure : SyncStatusResponse
    {
        public sealed record Failed(string Message) : Failure;
    }
}