namespace picamerasserver.Options;

public class SmbOptions
{
    public const string Smb = "Smb";

    public required string Host { get; init; }
    public required string ShareName { get; init; }
    public required string FileDirectory { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}