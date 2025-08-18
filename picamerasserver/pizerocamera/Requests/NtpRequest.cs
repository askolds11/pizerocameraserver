using System.Text.Json.Serialization;

namespace picamerasserver.pizerocamera.Requests;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Step), nameof(Step))]
[JsonDerivedType(typeof(Slew), nameof(Slew))]
public abstract record NtpRequest
{
    public sealed record Step : NtpRequest;

    public sealed record Slew : NtpRequest;
}