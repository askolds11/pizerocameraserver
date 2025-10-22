using System.Text.Json.Serialization;

namespace picamerasserver.PiZero.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DownloadingUpdate), nameof(DownloadingUpdate))]
[JsonDerivedType(typeof(UpdateDownloaded), nameof(UpdateDownloaded))]
[JsonDerivedType(typeof(AlreadyUpdated), nameof(AlreadyUpdated))]
[JsonDerivedType(typeof(Failure.Failed), nameof(Failure.Failed))]
public abstract record UpdateResponse
{
    public sealed record DownloadingUpdate(
        string NewVersion,
        string Version
    ) : UpdateResponse;

    public sealed record UpdateDownloaded(
        string NewVersion,
        string Version
    ) : UpdateResponse;

    public sealed record AlreadyUpdated(
        string NewVersion,
        string Version
    ) : UpdateResponse;

    public abstract record Failure : UpdateResponse
    {
        public sealed record Failed(
            string? NewVersion,
            string? Version,
            string Message
        ) : Failure;
    }
}