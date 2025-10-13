namespace picamerasserver.Options;

public class MqttOptions
{
    public const string Mqtt = "Mqtt";

    public required string Host { get; init; }
    public int? Port { get; init; }
    public required string NtpTopic { get; init; }
    public required string CameraTopic { get; init; }
    public required string CommandTopic { get; init; }
    public required string StatusTopic { get; init; }
    public required string UpdateTopic { get; init; }
    public required string ErrorTopic { get; init; }
    public required string IndicatorTopic { get; init; }
}