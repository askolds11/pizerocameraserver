using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using picamerasserver.Database;
using picamerasserver.Database.Models;

namespace picamerasserver.Settings;

public class SettingsService(
    IDbContextFactory<PiDbContext> dbContextFactory,
    ILogger<SettingsService> logger
)
{
    public async Task<Result<T, Exception>> GetAsync<T>() where T : Setting
    {
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();
        var typeName = typeof(T).Name;
        var row = await piDbContext.Settings.FindAsync(typeName);
        return row == null ? Result.Failure<T, Exception>(new ArgumentNullException()) : Json.TryDeserialize<T>(row.Json, logger);
    }

    public async Task SetAsync<T>(T value) where T : Setting
    {
        await using var piDbContext = await dbContextFactory.CreateDbContextAsync();
        var typeName = typeof(T).Name;
        var json = Json.Serialize(value);

        var row = await piDbContext.Settings.FindAsync(typeName);
        if (row == null)
            piDbContext.Add(new SettingModel { Type = typeName, Json = json });
        else
            row.Json = json;

        await piDbContext.SaveChangesAsync();
    }
}