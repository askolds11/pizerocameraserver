using System.Text;
using MQTTnet;
using MQTTnet.Protocol;

namespace picamerasserver.pizerocamera;

public class PiZeroCameraManager
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<PiZeroCameraManager> _logger;

    public readonly IReadOnlyList<string> PiZeroCameraIds;
    public readonly IReadOnlyDictionary<string, PiZeroCamera> PiZeroCameras;
    public event Action? OnChange;

    public PiZeroCameraManager(IMqttClient mqttClient, ILogger<PiZeroCameraManager> logger)
    {
        _mqttClient = mqttClient;
        _logger = logger;
        // Add all cameras
        var piZeroCamerasIds = new List<string>();
        var piZeroCameras = new Dictionary<string, PiZeroCamera>();
        foreach (var letter in Enumerable.Range('A', 16).Select(c => ((char)c).ToString()))
        {
            foreach (var number in Enumerable.Range(1, 6))
            {
                piZeroCamerasIds.Add(letter + number);
                piZeroCameras.Add(letter + number, new PiZeroCamera());
            }
        }

        PiZeroCameraIds = piZeroCamerasIds;
        PiZeroCameras = piZeroCameras;
    }

    /// <summary>
    /// Request a NTP time sync.
    /// </summary>
    /// <param name="ids">Provide a List for specific devices, null for global</param>
    public async Task RequestNtpSync(IEnumerable<string>? ids)
    {
        if (ids == null)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithContentType("application/json")
                .WithTopic("ntp")
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
                .WithTopic($"ntp/{id}")
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