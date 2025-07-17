using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using picamerasserver.Options;

namespace picamerasserver.Endpoints;

public static class DownloadUpdateEndpoint
{
    public static void AddDownloadUpdateEndpoint(this WebApplication app)
    {
        app.MapGet("/downloadupdate", ([FromServices] IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor) =>
        {
            var path = Path.Combine(dirOptionsMonitor.CurrentValue.UpdateDirectory, "pizerocamera");

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