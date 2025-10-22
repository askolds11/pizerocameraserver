using System.Text;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using MudBlazor;
using picamerasserver.Options;
using picamerasserver.pizerocamera.GetAlive;
using picamerasserver.pizerocamera.Ntp;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;
using picamerasserver.pizerocamera.SendPicture;
using picamerasserver.pizerocamera.Sync;
using picamerasserver.pizerocamera.TakePicture;
using picamerasserver.pizerocamera.Update;

namespace picamerasserver.Services;

public class MqttService
{
    private readonly IMqttClient _mqttClient;
    private readonly ITakePictureManager _takePictureManager;
    private readonly ISendPictureManager _sendPictureManager;
    private readonly IGetAliveManager _getAliveManager;
    private readonly INtpManager _ntpManager;
    private readonly IUpdateManager _updateManager;
    private readonly ISyncManager _syncManager;
    private readonly IOptionsMonitor<MqttOptions> _optionsMonitor;
    private MqttOptions _currentOptions;
    private readonly ILogger<MqttService> _logger;
    private readonly NotificationService _notificationService;

    public MqttService(IMqttClient mqttClient,
        IOptionsMonitor<MqttOptions> optionsMonitor,
        ILogger<MqttService> logger,
        ITakePictureManager takePictureManager,
        ISendPictureManager sendPictureManager,
        IGetAliveManager getAliveManager,
        INtpManager ntpManager,
        IUpdateManager updateManager,
        ISyncManager syncManager, NotificationService notificationService)
    {
        _mqttClient = mqttClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _takePictureManager = takePictureManager;
        _sendPictureManager = sendPictureManager;
        _getAliveManager = getAliveManager;
        _ntpManager = ntpManager;
        _updateManager = updateManager;
        _syncManager = syncManager;
        _notificationService = notificationService;
        _currentOptions = _optionsMonitor.CurrentValue;
        Task.Run(InitClient);
    }

    private MqttClientOptions GetMqttClientOptions()
    {
        return new MqttClientOptionsBuilder()
            .WithClientId("MOTHERSHIP")
            .WithTcpServer(_currentOptions.Host, _currentOptions.Port)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build();
    }

    /// <summary>
    /// Gets answers topic from topic
    /// </summary>
    /// <param name="topic">Topic</param>
    /// <returns>Answers topic</returns>
    private static string GetAnswersTopic(string topic) => $"{topic}/answer/#";

    /// <summary>
    /// Resubscribes to the topic if changed
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
        var messageReceived = DateTimeOffset.UtcNow;
        var topic = e.ApplicationMessage.Topic.Split("/").First();
        var id = e.ApplicationMessage.Topic.Split("/").Last();
        if (topic == _currentOptions.NtpTopic)
        {
            await _ntpManager.ResponseNtpSync(e.ApplicationMessage, id);
        }
        else if (topic == _currentOptions.CameraTopic)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var cameraResponse = Json.TryDeserialize<CameraResponse>(payload, _logger);
            if (cameraResponse.IsSuccess)
            {
                var cameraResponseValue = cameraResponse.Value;
                if (cameraResponseValue is CameraResponse.TakePicture takePictureResponse)
                {
                    await _takePictureManager.ResponseTakePicture(e.ApplicationMessage, messageReceived,
                        takePictureResponse, id);
                }
                else if (cameraResponseValue is CameraResponse.SendPicture sendPictureResponse)
                {
                    await _sendPictureManager.ResponseSendPicture(e.ApplicationMessage, messageReceived,
                        sendPictureResponse, id);
                }
                else if (cameraResponseValue is CameraResponse.SyncStatus syncStatusResponse)
                {
                    await _syncManager.ResponseSyncStatus(e.ApplicationMessage, syncStatusResponse, id);
                }
            }
            else if (cameraResponse.IsFailure)
            {
                await _notificationService.AddAsync("Failed to parse camera response: " + cameraResponse.Error, Severity.Error);
            }
        }
        else if (topic == _currentOptions.CommandTopic)
        {
        }
        else if (topic == _currentOptions.StatusTopic)
        {
            await _getAliveManager.ResponseGetStatus(e.ApplicationMessage, id);
        }
        else if (topic == _currentOptions.UpdateTopic)
        {
            await _updateManager.ResponseUpdate(e.ApplicationMessage, id);
        }
        else if (topic == _currentOptions.ErrorTopic)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            await _notificationService.AddAsync($"Error for {id}: {payload}", Severity.Error);
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
        _mqttClient.DisconnectedAsync += async _ =>
        {
            _logger.LogInformation("Disconnected from MQTT server");
            await SetupClient();
        };
        await SetupClient();
    }

    /// <summary>
    /// Connects and sets up Mqtt client subscriptions
    /// </summary>
    private async Task SetupClient()
    {
        // Connect until is connected
        while (!_mqttClient.IsConnected)
        {
            _logger.LogInformation("Connecting to mqtt server");
            var newMqttClientOptions = GetMqttClientOptions();
            await _mqttClient.ConnectAsync(newMqttClientOptions, CancellationToken.None);
        }

        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.NtpTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.CameraTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.CommandTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.StatusTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.UpdateTopic));
        await _mqttClient.SubscribeAsync(GetAnswersTopic(_currentOptions.ErrorTopic));
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