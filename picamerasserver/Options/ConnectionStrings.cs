namespace picamerasserver.Options;

public class ConnectionStringsOptions
{
    public const string ConnectionStrings = "ConnectionStrings";

    public required string Postgres { get; init; }
}