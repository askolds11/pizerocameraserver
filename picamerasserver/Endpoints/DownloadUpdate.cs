using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using picamerasserver.Database;
using picamerasserver.Options;

namespace picamerasserver.Endpoints;

public static class DownloadUpdateEndpoint
{
    public static void AddDownloadUpdateEndpoint(this WebApplication app)
    {
        app.MapGet("/downloadupdate", async ([FromServices] IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor,
            [FromServices] IDbContextFactory<PiDbContext> dbContextFactory) =>
        {
            await using var piDbContext = await dbContextFactory.CreateDbContextAsync();
            var activeVersion = piDbContext.Updates.FirstOrDefault(x => x.IsCurrent);

            if (activeVersion == null)
            {
                return Results.InternalServerError();
            }
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UpdateDirectory, $"pizerocamera-{activeVersion.Version}");

            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var contentType = "application/octet-stream";
            return Results.File(fileStream, contentType, fileDownloadName: "pizerocamera");
        });
    }
}