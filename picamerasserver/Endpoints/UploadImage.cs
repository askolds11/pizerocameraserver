using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using picamerasserver.Options;

namespace picamerasserver.Endpoints;

public static class UploadImageEndpoint
{
    public static void AddUploadImageEndpoint(this WebApplication app)
    {
        app.MapPost("/uploadimage",
            async (
                IFormFile image,
                [FromForm] string metadata,
                [FromForm] Guid uuid,
                [FromServices] IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor
            ) =>
            {
                var directoryName = $"{uuid}";
                var directory = Path.Combine(dirOptionsMonitor.CurrentValue.UploadDirectory, directoryName);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save image
                var filePath = Path.Combine(directory, image.FileName);
                await using (var stream = File.Create(filePath))
                {
                    await image.CopyToAsync(stream);
                }
                app.Logger.LogInformation("Saved image {F}", image.FileName);

                // Save metadata
                var metadataFileName = image.FileName.Split('.').First() + "_metadata.json";
                var metadataFilePath = Path.Combine(directory, metadataFileName);
                await File.WriteAllTextAsync(metadataFilePath, metadata);
                app.Logger.LogInformation("Saved metadata {F}", metadataFileName);
                

                return Results.Ok(new { status = "Success" });
            }).DisableAntiforgery();
    }
}