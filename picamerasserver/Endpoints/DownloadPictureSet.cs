using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;

namespace picamerasserver.Endpoints;

public static class DownloadPictureSetEndpoint
{
    public static void AddDownloadPictureSetEndpoint(this WebApplication app)
    {
        app.MapGet("/downloadpictureset/{uuid:guid}", async (
            Guid uuid,
            [FromServices] IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor,
            [FromServices] IDbContextFactory<PiDbContext> dbContextFactory) =>
        {
            var piDbContext = await dbContextFactory.CreateDbContextAsync();
            var pictureSet = await piDbContext.PictureSets
                .Include(x => x.PictureRequests.Where(y => y.IsActive))
                .FirstOrDefaultAsync(x => x.Uuid == uuid);

            if (pictureSet == null)
            {
                return Results.NotFound();
            }
            
            // check if all sent
            foreach (var pictureRequest in pictureSet.PictureRequests)
            {
                var path = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, pictureRequest.Uuid.ToString());

                if (!Directory.Exists(path))
                {
                    return Results.NotFound();
                }
            }

            var memoryStream = new MemoryStream();
            // !! Must be using brackets, as zipArchive has to be disposed
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var pictureRequest in pictureSet.PictureRequests)
                {
                    var path = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, pictureRequest.Uuid.ToString());

                    // directory name for request
                    var dir = pictureRequest.PictureRequestType switch
                    {
                        PictureRequestType.StandingSpread => "stav-starpa",
                        PictureRequestType.StandingTogether => "stav-kopa",
                        PictureRequestType.Sitting => "sez",
                        PictureRequestType.Mask => "maska",
                        PictureRequestType.Other => "other",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    dir += $"_{pictureRequest.Uuid}";

                    foreach (var filePath in Directory.GetFiles(path))
                    {
                        var fileName = Path.GetFileName(filePath).Replace(pictureRequest.Uuid + "_", "");
                        var entry = zipArchive.CreateEntry(Path.Combine(dir, fileName), CompressionLevel.NoCompression);

                        await using var entryStream = entry.Open();
                        await using var fileStream = File.OpenRead(filePath);
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }

            // Reset stream position
            memoryStream.Seek(0, SeekOrigin.Begin);
            // Return stream and dispose of it after (provided by Results.Stream)
            return Results.Stream(memoryStream, "application/zip", fileDownloadName: $"{pictureSet.Name}_{uuid}.zip");
        });
    }
}