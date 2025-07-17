using System.Text.Json;
using CSharpFunctionalExtensions;

namespace picamerasserver;

public static class Json
{
    private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
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
        catch (Exception ex) when (ex is ArgumentNullException || ex is JsonException || ex is NotSupportedException)
        {
            logger.LogError(ex, "Failed to deserialize JSON string {S}", json);
            return Result.Failure<T, Exception>(ex);
        }
    }

    public static string Serialize<T>(T obj) =>
        JsonSerializer.Serialize(obj, DefaultOptions);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, DefaultOptions)!;

    public static JsonSerializerOptions GetDefaultOptions() => new JsonSerializerOptions(DefaultOptions);
}