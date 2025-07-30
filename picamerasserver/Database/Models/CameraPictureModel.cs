using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace picamerasserver.Database.Models;

[PrimaryKey(nameof(CameraId), nameof(PictureRequestId))]
public class CameraPictureModel
{
    [StringLength(2), Required] public required string CameraId { get; init; }
    [Required] public required Guid PictureRequestId { get; init; }
    [Column(TypeName = "varchar(20)")] public CameraPictureStatus? CameraPictureStatus { get; set; }
    public string? StatusMessage { get; set; }
    /// <summary>
    /// Time when the "taken" message was received
    /// </summary>
    public DateTimeOffset? ReceivedTaken { get; set; }
    /// <summary>
    /// Time when the "saved" message was received
    /// </summary>
    public DateTimeOffset? ReceivedSaved { get; set; }
    /// <summary>
    /// Time when the "sent" message was received
    /// </summary>
    public DateTimeOffset? ReceivedSent { get; set; }
    
    /// <summary>
    /// Time when the picture was taken according to the Pi
    /// </summary>
    public DateTimeOffset? PictureTaken { get; init; }
    

    [ForeignKey(nameof(CameraId))] public CameraModel Camera { get; init; } = null!;
    [ForeignKey(nameof(PictureRequestId))] public PictureRequestModel PictureRequest { get; init; } = null!;
}

public class CameraPictureConfiguration : IEntityTypeConfiguration<CameraPictureModel>
{
    public void Configure(EntityTypeBuilder<CameraPictureModel> builder)
    {
        builder.Property(p => p.CameraId)
            .IsFixedLength();
    }
}

public enum CameraPictureStatus
{
    Requested,
    FailedToRequest, //(string Message)
    Taken,
    SavedOnDevice,
    Failed,
    PictureFailedToSave,
    PictureFailedToSchedule,
    PictureFailedToTake,
    RequestedSend,
    FailedToRequestSend, //(string Message)
    FailureSend, //(SendPictureResponse.Failure Reason)
    PictureFailedToRead,
    PictureFailedToSend,
    Success,
    Unknown //(string Message)
}