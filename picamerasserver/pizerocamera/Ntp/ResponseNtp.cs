using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MQTTnet;
using picamerasserver.pizerocamera.Responses;

namespace picamerasserver.pizerocamera.Ntp;

public partial class Ntp
{
    /// <inheritdoc />
    public Task ResponseNtpSync(MqttApplicationMessage message, string id)
    {
        _ntpChannel?.Writer.TryWrite(id);

        return id == PiZeroIndicator.Id
            ? ResponseNtpSyncIndicator(message)
            : ResponseNtpSyncCamera(message, id);
    }

    /// <summary>
    /// Handle an indicator's Ntp response
    /// </summary>
    /// <param name="message">MQTT message</param>
    private Task ResponseNtpSyncIndicator(MqttApplicationMessage message)
    {
        var piZeroIndicator = piZeroCameraManager.PiZeroIndicator;

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<string>>(text, logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroIndicator.NtpRequest = new PiZeroNtpRequest.Success(successWrapper.Value);
                const string pattern = @"(?:^CLOCK:.*?\\n)?(.*?)\s([-+]?\d+\.\d+)\s\+\/-\s(\d+\.\d+)";
                var match = Regex.Match(successWrapper.Value, pattern, RegexOptions.Multiline);

                if (match.Success)
                {
                    var timestamp = match.Groups[1].Value;
                    var offset = match.Groups[2].Value;
                    var error = match.Groups[3].Value;

                    var date = DateTimeOffset.ParseExact(
                        timestamp,
                        "yyyy-MM-dd HH:mm:ss.FFFFFF (zzz)",
                        CultureInfo.InvariantCulture
                    );
                    var offsetSeconds = float.Parse(offset, CultureInfo.InvariantCulture);
                    var errorSeconds = float.Parse(error, CultureInfo.InvariantCulture);

                    piZeroIndicator.LastNtpSync = date;
                    piZeroIndicator.LastNtpOffsetMillis = offsetSeconds * 1000;
                    piZeroIndicator.LastNtpErrorMillis = errorSeconds * 1000;
                }
                else
                {
                    piZeroIndicator.NtpRequest =
                        new PiZeroNtpRequest.Failure.FailedToParseRegex(successWrapper.Value);
                }
            }
            else
            {
                piZeroIndicator.NtpRequest = new PiZeroNtpRequest.Failure.Failed(successWrapper.Value);
            }
        }
        else
        {
            piZeroIndicator.NtpRequest = new PiZeroNtpRequest.Failure.FailedToParseJson(response.Error.ToString());
        }

        changeListener.UpdateNtp();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle a camera's Ntp response
    /// </summary>
    /// <param name="message">MQTT message</param>
    /// <param name="id">Camera's id</param>
    private Task ResponseNtpSyncCamera(MqttApplicationMessage message, string id)
    {
        var piZeroCamera = piZeroCameraManager.PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);

        var response = Json.TryDeserialize<SuccessWrapper<string>>(text, logger);

        if (response.IsSuccess)
        {
            var successWrapper = response.Value;
            if (successWrapper.Success)
            {
                piZeroCamera.NtpRequest = new PiZeroNtpRequest.Success(successWrapper.Value);
                const string pattern = @"(?:^CLOCK:.*?\\n)?(.*?)\s([-+]?\d+\.\d+)\s\+\/-\s(\d+\.\d+)";
                var match = Regex.Match(successWrapper.Value, pattern, RegexOptions.Multiline);

                if (match.Success)
                {
                    var timestamp = match.Groups[1].Value;
                    var offset = match.Groups[2].Value;
                    var error = match.Groups[3].Value;

                    var date = DateTimeOffset.ParseExact(
                        timestamp,
                        "yyyy-MM-dd HH:mm:ss.FFFFFF (zzz)",
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
                        new PiZeroNtpRequest.Failure.FailedToParseRegex(successWrapper.Value);
                }
            }
            else
            {
                piZeroCamera.NtpRequest = new PiZeroNtpRequest.Failure.Failed(successWrapper.Value);
            }
        }
        else
        {
            piZeroCamera.NtpRequest = new PiZeroNtpRequest.Failure.FailedToParseJson(response.Error.ToString());
        }

        changeListener.UpdateNtp();

        return Task.CompletedTask;
    }
}