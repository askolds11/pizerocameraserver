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
    /// <summary>
    /// Time when the request was sent
    /// </summary>
    public DateTimeOffset? Requested { get; init; }
    [Column(TypeName = "varchar(50)")] public CameraPictureStatus? CameraPictureStatus { get; set; }
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
    /// Time when the picture request was received according to the Pi
    /// </summary>
    public DateTimeOffset? PictureRequestReceived { get; set; }
    
    /// <summary>
    /// How long the code had to wait 
    /// </summary>
    public long WaitTimeNanos { get; set; }
    
    /// <summary>
    /// Time when the picture was taken according to the Pi
    /// </summary>
    public DateTimeOffset? PictureTaken { get; set; }
    
    public float? NtpErrorMillis { get; init; }
    
    /// <summary>
    /// Monotonic time, when picture was supposed to be taken on the Pi (FrameWallClock)
    /// </summary>
    public long? MonotonicTime { get; set; }
    /// <summary>
    /// Monotonic time, when picture was taken on the Pi according to the metadata
    /// </summary>
    public long? SensorTimestamp { get; set; }
    public int? FocusFoM { get; set; }
    public float? AnalogueGain { get; set; }
    public float? DigitalGain { get; set; }
    public int? ExposureTime { get; set; }
    public int? ColourTemperature { get; set; }
    public float? Lux { get; set; }
    public long? FrameDuration { get; set; }
    public byte? AeState { get; set; }
    

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