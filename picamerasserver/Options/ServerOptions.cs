namespace picamerasserver.Options;

public class ServerOptions
{
    public const string Server = "Server";

    public required string Username { get; init; }
    public required string Password { get; init; }
}