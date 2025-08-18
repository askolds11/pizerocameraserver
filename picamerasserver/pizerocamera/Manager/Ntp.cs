using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.pizerocamera.Requests;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    public event Action? OnNtpChange;
    
    /// <summary>
    /// Request a NTP time sync.
    /// </summary>
    public async Task RequestNtpSync(NtpRequest ntpRequest)
    {
        var options = _optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.NtpTopic)
            .WithPayload(Json.Serialize(ntpRequest))
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
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure.FailedToRequest(publishResult.ReasonString);
            }
        }

        OnNtpChange?.Invoke();
    }

    public void ResponseNtpSync(MqttApplicationMessage message, string id)
    {
        var piZeroCamera = PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<string>>(text, _logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Success(successWrapper.Value);
                const string pattern = @"(?:^CLOCK:.*?\\n)?(.*?)\s([-+]?\d+\.\d+)\s\+\/-\s(\d+\.\d+)";
                var match = Regex.Match(successWrapper.Value, pattern, RegexOptions.Multiline);

                if (match.Success)
                {
                    var timestamp = match.Groups[1].Value;
                    var offset = match.Groups[2].Value;
                    var error = match.Groups[3].Value;

                    var date = DateTimeOffset.ParseExact(
                        timestamp,
                        "yyyy-MM-dd HH:mm:ss.ffffff (zzz)",
                        CultureInfo.InvariantCulture
                    );
                    var offsetSeconds = float.Parse(offset, CultureInfo.InvariantCulture);
                    var errorSeconds = float.Parse(error, CultureInfo.InvariantCulture);

                    piZeroCamera.LastNtpSync = date;
                    piZeroCamera.LastNtpOffsetMillis = offsetSeconds * 1000;
                    piZeroCamera.LastNtpErrorMillis = errorSeconds * 1000;
                }
                else
                {
                    piZeroCamera.NtpRequest =
                        new PiZeroCameraNtpRequest.Failure.FailedToParseRegex(successWrapper.Value);
                }
            }
            else
            {
                piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure.Failed(successWrapper.Value);
            }
        }
        else
        {
            piZeroCamera.NtpRequest = new PiZeroCameraNtpRequest.Failure.FailedToParseJson(response.Error.ToString());
        }

        OnNtpChange?.Invoke();
    }
}