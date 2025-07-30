using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace picamerasserver.Database.Models;

public class CameraModel
{
    [Key, StringLength(2), Required]
    public required string Id { get; init; }
}

public class CameraConfiguration : IEntityTypeConfiguration<CameraModel>
{
    public void Configure(EntityTypeBuilder<CameraModel> builder)
    {
        builder.Property(p => p.Id)
            .IsFixedLength();
    }
}