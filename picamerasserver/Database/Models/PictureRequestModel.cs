using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace picamerasserver.Database.Models;

public class PictureRequestModel
{
    [Key, Required] public required Guid Uuid { get; init; }

    /// <summary>
    /// Time, when the request was initiated
    /// </summary>
    [Required]
    public required DateTimeOffset RequestTime { get; init; }

    /// <summary>
    /// Time, when the picture should be taken
    /// </summary>
    [Required]
    public required DateTimeOffset PictureTime { get; init; }

    [InverseProperty(nameof(CameraPictureModel.PictureRequest))]
    public List<CameraPictureModel> CameraPictures { get; } = new();
}

public class PictureConfiguration : IEntityTypeConfiguration<PictureRequestModel>
{
    public void Configure(EntityTypeBuilder<PictureRequestModel> builder)
    {
    }
}