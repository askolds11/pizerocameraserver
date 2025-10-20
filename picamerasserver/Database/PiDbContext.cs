using System.Reflection;
using Microsoft.EntityFrameworkCore;
using picamerasserver.Database.Models;

namespace picamerasserver.Database;

public class PiDbContext(DbContextOptions<PiDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public DbSet<PictureSetModel> PictureSets { get; init; }
    public DbSet<PictureRequestModel> PictureRequests { get; init; }
    public DbSet<CameraModel> Cameras { get; init; }
    public DbSet<CameraPictureModel> CameraPictures { get; init; }
    public DbSet<UpdateModel> Updates { get; init; }
    public DbSet<SettingModel> Settings { get; init; }
}