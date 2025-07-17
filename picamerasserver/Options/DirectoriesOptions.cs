namespace picamerasserver.Options;

public class DirectoriesOptions
{
    public const string Directories = "Directories";
    
    public required string UploadDirectory { get; init; }
    public required string UpdateDirectory { get; init; }
}