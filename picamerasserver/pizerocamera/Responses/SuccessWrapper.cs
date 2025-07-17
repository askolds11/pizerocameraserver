namespace picamerasserver.pizerocamera;

public record SuccessWrapper<T>
{
    public required bool Success { get; init; }
    public required T Value { get; init; }
}
