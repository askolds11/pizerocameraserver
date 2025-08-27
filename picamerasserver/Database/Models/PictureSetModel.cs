using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace picamerasserver.Database.Models;

public class PictureSetModel
{
    [Key, Required] public required Guid Uuid { get; init; }
    [MaxLength(200)]
    public required string Name { get; set; }
    public bool IsDone { get; set; } = false;
    
    [InverseProperty(nameof(PictureRequestModel.PictureSet))]
    public List<PictureRequestModel> PictureRequests { get; } = new();
}