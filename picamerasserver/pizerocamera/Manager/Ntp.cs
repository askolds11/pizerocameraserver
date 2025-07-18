using System.Text;
using MQTTnet;
using MQTTnet.Protocol;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    /// <summary>
    /// Request a NTP time sync.
    /// </summary>
    /// <param name="ids">Provide a List for specific devices, null for global</param>
    public async Task RequestNtpSync(IEnumerable<string>? ids)
    {
        var options = _optionsMonitor.CurrentValue;
        
        if (ids == null)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic(options.NtpTopic)
                // .WithPayload(Json.Serialize(cameraControls))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            var publishResult = await _mqttClient.PublishAsync(message);

            if (publishResult.IsSuccess)
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Requested();
                }
            }
            else
            {
                foreach (var piZeroCamera in PiZeroCameras.Values)
                {
                    piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.FailedToRequest(publishResult.ReasonString);
                }
            }

            OnChange?.Invoke();
            return;
        }

        MqttApplicationMessage GetMessage(string id) =>
            new MqttApplicationMessageBuilder().WithContentType("application/json")
                .WithTopic($"{options.NtpTopic}/{id}")
                // .WithPayload(Json.Serialize(cameraControls))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

        var idList = ids.ToList();

        // PiZeroCameras that are not requested set to null
        foreach (var id in PiZeroCameras.Keys.Except(idList))
        {
            PiZeroCameras[id].NtpRequest = null;
        }

        // PiZeroCameras that are requested
        var tasks = PiZeroCameras.Keys
            .Intersect(idList)
            .Select(async id => (id, await _mqttClient.PublishAsync(GetMessage(id))));


        var tasksResult = await Task.WhenAll(tasks);

        // Process message sending results
        foreach (var (id, publishResult) in tasksResult)
        {
            var piZeroCamera = PiZeroCameras[id];
            if (publishResult.IsSuccess)
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Requested();
            }
            else
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.FailedToRequest(publishResult.ReasonString);
            }
        }

        OnChange?.Invoke();
    }

    public void ResponseNtpSync(MqttApplicationMessage message)
    {
        var id = message.Topic.Split('/').Last();

        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<string>>(text, _logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Success(successWrapper.Value);
            }
            else
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure(successWrapper.Value);
            }
        }
        else
        {
            piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Unknown(response.Error.ToString());
        }

        OnChange?.Invoke();
    }
}