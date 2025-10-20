using System.ComponentModel.DataAnnotations;

namespace picamerasserver.Database.Models;

public class SettingModel
{
    [MaxLength(100)]
    [Key] public required string Type { get; init; }
    [MaxLength(1000)]
    public required string Json { get; set; }
}