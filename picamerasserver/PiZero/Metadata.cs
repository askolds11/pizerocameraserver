using System.Text.Json;
using System.Text.Json.Serialization;

namespace picamerasserver.PiZero;

public record Metadata(
    long? SensorTimestamp,
    long? FrameWallClock,
    int? FocusFoM,
    float? AnalogueGain,
    float? DigitalGain,
    int? ExposureTime,
    int? ColourTemperature,
    float? Lux,
    long? FrameDuration,
    byte? AeState
);

public class MetadataConverter : JsonConverter<Metadata>
{
    public override Metadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token for Metadata.");

        long? sensorTimestamp = null;
        long? frameWallClock = null;
        int? frameFoM = null;
        float? analogueGain = null;
        float? digitalGain = null;
        int? exposureTime = null;
        int? colourTemperature = null;
        float? lux = null;
        long? frameDuration = null;
        byte? aeState = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token.");

            var propertyName = reader.GetString();

            reader.Read();

            switch (propertyName)
            {
                case nameof(Metadata.SensorTimestamp):
                    sensorTimestamp = ReadNullableLong(ref reader);
                    break;
                case nameof(Metadata.FrameWallClock):
                    frameWallClock = ReadNullableLong(ref reader);
                    break;
                case nameof(Metadata.FocusFoM):
                    frameFoM = ReadNullableInt(ref reader);
                    break;
                case nameof(Metadata.AnalogueGain):
                    analogueGain = ReadNullableFloat(ref reader);
                    break;
                case nameof(Metadata.DigitalGain):
                    digitalGain = ReadNullableFloat(ref reader);
                    break;
                case nameof(Metadata.ExposureTime):
                    exposureTime = ReadNullableInt(ref reader);
                    break;
                case nameof(Metadata.ColourTemperature):
                    colourTemperature = ReadNullableInt(ref reader);
                    break;
                case nameof(Metadata.Lux):
                    lux = ReadNullableFloat(ref reader);
                    break;
                case nameof(Metadata.FrameDuration):
                    frameDuration = ReadNullableLong(ref reader);
                    break;
                case nameof(Metadata.AeState):
                    aeState = ReadNullableByte(ref reader);
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        return new Metadata(sensorTimestamp, frameWallClock, frameFoM, analogueGain, digitalGain, exposureTime, colourTemperature, lux, frameDuration, aeState);
    }

    public override void Write(Utf8JsonWriter writer, Metadata value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteNullable(writer, nameof(Metadata.SensorTimestamp), value.SensorTimestamp);
        WriteNullable(writer, nameof(Metadata.FrameWallClock), value.FrameWallClock);
        WriteNullable(writer, nameof(Metadata.FocusFoM), value.FocusFoM);
        WriteNullable(writer, nameof(Metadata.AnalogueGain), value.AnalogueGain);
        WriteNullable(writer, nameof(Metadata.DigitalGain), value.DigitalGain);
        WriteNullable(writer, nameof(Metadata.ExposureTime), value.ExposureTime);
        WriteNullable(writer, nameof(Metadata.ColourTemperature), value.ColourTemperature);
        WriteNullable(writer, nameof(Metadata.Lux), value.Lux);

        writer.WriteEndObject();
    }

    // Helper method to read nullable long
    private static long? ReadNullableLong(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : long.Parse(reader.GetString()!);

    // Helper method to read nullable int
    private static int? ReadNullableInt(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : int.Parse(reader.GetString()!);

    // Helper method to read nullable float
    private static float? ReadNullableFloat(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : float.Parse(reader.GetString()!);
    
    // Helper method to read nullable byte
    private static byte? ReadNullableByte(ref Utf8JsonReader reader) =>
        reader.TokenType == JsonTokenType.Null ? null : byte.Parse(reader.GetString()!);

    // Write nullable values as strings
    private static void WriteNullable<T>(Utf8JsonWriter writer, string propertyName, T? value) where T : struct
    {
        writer.WritePropertyName(propertyName);
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}