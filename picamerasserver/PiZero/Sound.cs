using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Options;

namespace picamerasserver.PiZero;

public class Sound(IMqttClient mqttClient, IOptionsMonitor<MqttOptions> mqttOptionsMonitor)
{
    public async Task SendSignal()
    {
        var mqttOptions = mqttOptionsMonitor.CurrentValue;
        
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(mqttOptions.IndicatorTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await mqttClient.PublishAsync(message);
    }
}