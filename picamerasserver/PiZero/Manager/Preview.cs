using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.PiZero.Requests;

namespace picamerasserver.PiZero.Manager;

public partial class PiZeroManager
{
    public async Task StartPreview()
    {
        CameraRequest cameraRequest = new CameraRequest.StartPreview();
        
        // Send
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("camera")
            .WithPayload(Json.Serialize(cameraRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await _mqttClient.PublishAsync(message);
    }
    
    public async Task StopPreview()
    {
        CameraRequest cameraRequest = new CameraRequest.StopPreview();
        
        // Send
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("camera")
            .WithPayload(Json.Serialize(cameraRequest))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();
        await _mqttClient.PublishAsync(message);
    }
}