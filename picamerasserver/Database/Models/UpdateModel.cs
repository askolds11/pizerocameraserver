using System.ComponentModel.DataAnnotations;

namespace picamerasserver.Database.Models;

public class UpdateModel
{
    /// <summary>SemVer version</summary>
    /// <example>0.1.0</example>
    [Key, MaxLength(10)]
    public required string Version { get; init; }
    /// <summary>
    /// Time when the update was uploaded to the server
    /// </summary>
    public required DateTimeOffset UploadedTime { get; init; }
    /// <summary>
    /// Time when the update started to be applied
    /// </summary>
    public DateTimeOffset? UpdatedTime { get; set; }
    /// <summary>
    /// Use this update for updates
    /// </summary>
    public bool IsCurrent { get; set; } = false;
}