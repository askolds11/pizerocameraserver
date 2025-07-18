using System.Text;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.manager.Requests;

namespace picamerasserver.mqtt;

public class MqttStuff
{
    private readonly IMqttClient _mqttClient;
    private readonly PiZeroCameraManager _piZeroCameraManager;
    private readonly IOptionsMonitor<MqttOptions> _optionsMonitor;
    private MqttOptions _currentOptions;
    private readonly ILogger<MqttStuff> _logger;

    public MqttStuff(IMqttClient mqttClient, PiZeroCameraManager piZeroCameraManager,
        IOptionsMonitor<MqttOptions> optionsMonitor, ILogger<MqttStuff> logger)
    {
        _mqttClient = mqttClient;
        _piZeroCameraManager = piZeroCameraManager;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _currentOptions = _optionsMonitor.CurrentValue;
        Task.Run(() => InitClient());
    }

    private MqttClientOptions GetMqttClientOptions()
    {
        return new MqttClientOptionsBuilder()
            .WithClientId("MOTHERSHIP")
            .WithTcpServer(_currentOptions.Host, _currentOptions.Port)
            .Build();
    }

    /// <summary>
    /// Gets answers topic from topic
    /// </summary>
    /// <param name="topic">Topic</param>
    /// <returns>Answers topic</returns>
    private static string GetAnswersTopic(string topic) => $"{topic}/answer/#";

    /// <summary>
    /// Resubscribes to topic if changed
    /// </summary>
    /// <param name="oldTopic">Old topic</param>
    /// <param name="newTopic">New topic</param>
    /// <returns></returns>
    private async Task ResubscribeIfChanged(string oldTopic, string newTopic)
    {
        if (oldTopic != newTopic)
        {
            await _mqttClient.UnsubscribeAsync(GetAnswersTopic(oldTopic));
            await _mqttClient.SubscribeAsync(GetAnswersTopic(newTopic));
        }
    }

    /// <summary>
    /// Handler for OptionsMonitor options change
    /// </summary>
    /// <param name="options"></param>
    private async Task HandleOptionsChange(MqttOptions options)
    {
        if (_currentOptions.Host != options.Host || _currentOptions.Port != options.Port)
        {
            await _mqttClient.DisconnectAsync();
            _currentOptions = options;
            await SetupClient();
        }

        await ResubscribeIfChanged(_currentOptions.NtpTopic, options.NtpTopic);
        await ResubscribeIfChanged(_currentOptions.CameraTopic, options.CameraTopic);
        await ResubscribeIfChanged(_currentOptions.CommandTopic, options.CommandTopic);
        await ResubscribeIfChanged(_currentOptions.StatusTopic, options.StatusTopic);
        await ResubscribeIfChanged(_currentOptions.UpdateTopic, options.UpdateTopic);
        await ResubscribeIfChanged(_currentOptions.ErrorTopic, options.ErrorTopic);

        _currentOptions = options;
    }

    /// <summary>
    /// Handles Mqtt received event
    /// </summary>
    /// <param name="e">Event</param>
    private async Task MessageHandler(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic.Split("/").First();
        if (topic == _currentOptions.NtpTopic)
        {
            _piZeroCameraManager.ResponseNtpSync(e.ApplicationMessage);
        }
        else if (topic == _currentOptions.CameraTopic)
        {
            _piZeroCameraManager.ResponseTakePicture(e.ApplicationMessage);
        }
        else if (topic == _currentOptions.CommandTopic)
        {
        }
        else if (topic == _currentOptions.StatusTopic)
        {
        }
        else if (topic == _currentOptions.UpdateTopic)
        {
        }
        else if (topic == _currentOptions.ErrorTopic)
        {
        }

        var text = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        _logger.LogInformation("Received application message: {ApplicationMessageTopic} - {Text}",
            e.ApplicationMessage.Topic, text);
    }

    /// <summary>
    /// Sets up handlers for Mqtt client
    /// </summary>
    private async Task InitClient()
    {
        _logger.LogInformation("Initializing MQTT client.");
        _optionsMonitor.OnChange((options, _) => { Task.Run(async () => { await HandleOptionsChange(options); }); });
        _mqttClient.ApplicationMessageReceivedAsync += MessageHandler;
        _mqttClient.DisconnectedAsync += async e =>
        {
            // TODO: Does this work?
            if (e.ClientWasConnected && e.Reason != MqttClientDisconnectReason.NormalDisconnection)
            {
                var newMqttClientOptions = GetMqttClientOptions();
                await _mqttClient.ConnectAsync(newMqttClientOptions, CancellationToken.None);
            }
        };
        await SetupClient();
    }

    /// <summary>
    /// Connects and sets up Mqtt client subscriptions
    /// </summary>
    private async Task SetupClient()
    {
        var mqttClientOptions = GetMqttClientOptions();
        await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.NtpTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.CameraTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.CommandTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.StatusTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.UpdateTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.ErrorTopic));
    }

    public async Task SetCameraMode(CameraRequest cameraRequest)
    {
        // Send
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("camera")
            .WithPayload(Json.Serialize(cameraRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await _mqttClient.PublishAsync(message);
    }

    public async Task SetConfig(CameraRequest cameraControls)
    {
        Console.WriteLine(Json.Serialize(cameraControls));
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("camera")
            .WithPayload(Json.Serialize(cameraControls))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await _mqttClient.PublishAsync(message);
    }
}