using System.Text.Json.Serialization;

namespace picamerasserver.Settings;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MaxConcurrentNtp), nameof(MaxConcurrentNtp))]
[JsonDerivedType(typeof(MaxConcurrentSend), nameof(MaxConcurrentSend))]
[JsonDerivedType(typeof(RequestPictureDelay), nameof(RequestPictureDelay))]

public abstract record Setting
{
    public sealed record MaxConcurrentNtp(int Value) : Setting;

    public sealed record MaxConcurrentSend(int Value) : Setting;
    public sealed record RequestPictureDelay(int Value) : Setting;
}