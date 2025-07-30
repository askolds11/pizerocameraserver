using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using picamerasserver.Options;

namespace picamerasserver.Endpoints;

public static class DownloadPicturesEndpoint
{
    public static void AddDownloadPicturesEndpoint(this WebApplication app)
    {
        app.MapGet("/downloadpictures/{uuid:guid}", async (
            Guid uuid,
            [FromServices] IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor) =>
        {
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, uuid.ToString());

            if (!Directory.Exists(path))
            {
                return Results.NotFound();
            }

            var memoryStream = new MemoryStream();
            // !! Must be using brackets, as zipArchive has to be disposed
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var filePath in Directory.GetFiles(path))
                {
                    var fileName = Path.GetFileName(filePath);
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Fastest);

                    await using var entryStream = entry.Open();
                    await using var fileStream = File.OpenRead(filePath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            // Reset stream position
            memoryStream.Seek(0, SeekOrigin.Begin);
            // Return stream and dispose of it after (provided by Results.Stream)
            return Results.Stream(memoryStream, "application/zip", fileDownloadName: $"{uuid}.zip");
        });
    }
}