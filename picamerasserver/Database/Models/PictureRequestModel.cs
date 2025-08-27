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
    
    /// <summary>
    /// Picture set which this request belongs to
    /// </summary>
    public Guid? PictureSetId { get; init; }

    /// <summary>
    /// Type for the request
    /// </summary>
    [Column(TypeName = "varchar(50)")] public required PictureRequestType PictureRequestType { get; init; } = PictureRequestType.Other;
    
    /// <summary>
    /// Note for the request
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; init; }
    
    /// <summary>
    /// Whether this request is active or not
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    [ForeignKey(nameof(PictureSetId))] public PictureSetModel? PictureSet { get; init; }

    [InverseProperty(nameof(CameraPictureModel.PictureRequest))]
    public List<CameraPictureModel> CameraPictures { get; } = new();
}

public class PictureConfiguration : IEntityTypeConfiguration<PictureRequestModel>
{
    public void Configure(EntityTypeBuilder<PictureRequestModel> builder)
    {
    }
}

public enum PictureRequestType
{
    StandingSpread,
    StandingTogether,
    Sitting,
    Mask,
    Other
}