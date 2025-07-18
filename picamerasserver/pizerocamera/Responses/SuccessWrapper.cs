namespace picamerasserver.pizerocamera.manager;

public record SuccessWrapper<T>
{
    public required bool Success { get; init; }
    public required T Value { get; init; }
}
