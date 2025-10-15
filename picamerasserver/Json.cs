using System.Text.Json;
using CSharpFunctionalExtensions;

namespace picamerasserver;

public static class Json
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public static Result<T, Exception> TryDeserialize<T>(string json, ILogger logger)
    {
        try
        {
            return Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is ArgumentNullException or JsonException or NotSupportedException)
        {
            logger.LogError(ex, "Failed to deserialize JSON string {S}", json);
            return Result.Failure<T, Exception>(ex);
        }
    }
    
    public static Result<T, Exception> TryDeserializeWithOptions<T>(string json, ILogger logger, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options)!;
        }
        catch (Exception ex) when (ex is ArgumentNullException or JsonException or NotSupportedException)
        {
            logger.LogError(ex, "Failed to deserialize JSON string {S}", json);
            return Result.Failure<T, Exception>(ex);
        }
    }

    public static string Serialize<T>(T obj) =>
        JsonSerializer.Serialize(obj, DefaultOptions);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultOptions)!;

    public static JsonSerializerOptions GetDefaultOptions() => new(DefaultOptions);
}