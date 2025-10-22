using System.Text;
using MQTTnet;

namespace picamerasserver.PiZero.GetAlive;

public partial class GetAlive
{
    /// <inheritdoc />
    public async Task ResponseGetStatus(MqttApplicationMessage message, string id)
    {
        if (id == PiZeroIndicator.Id)
        {
            await ResponseGetStatusIndicator(message);
        }
        else
        {
            await ResponseGetStatusCamera(message, id);
        }
    }
    
    private async Task ResponseGetStatusCamera(MqttApplicationMessage message, string id)
    {
        var piZeroCamera = piZeroManager.PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<CameraStatusResponse>(text, logger);
        if (statusResponse.IsFailure)
        {
            piZeroCamera.Status = null;
            logger.LogError(statusResponse.Error, "Failed to parse status response");
        }
        else
        {
            var successWrapper = statusResponse.Value;
            piZeroCamera.Status = successWrapper.Success ? successWrapper.Value : null;

            if (successWrapper.Success)
            {
                // TODO: Move this code to Update class
                var activeVersion = (await updateManager.GetActiveVersion())?.Version;
                if (activeVersion == successWrapper.Value.Version)
                {
                    piZeroCamera.UpdateRequest = new PiZeroCameraUpdateRequest.Success();
                }
                else
                {
                    piZeroCamera.UpdateRequest = new PiZeroCameraUpdateRequest.Failure.VersionMismatch();
                }

                changeListener.UpdateUpdate();
            }
        }

        changeListener.UpdatePing();
    }
    
    private Task ResponseGetStatusIndicator(MqttApplicationMessage message)
    {
        var piZeroIndicator = piZeroManager.PiZeroIndicator;

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<IndicatorStatusResponse>(text, logger);
        if (statusResponse.IsFailure)
        {
            piZeroIndicator.Status = null;
            logger.LogError(statusResponse.Error, "Failed to parse status response");
        }
        else
        {
            var successWrapper = statusResponse.Value;
            piZeroIndicator.Status = successWrapper.Success ? successWrapper.Value : null;
        }

        changeListener.UpdatePing();
        return Task.CompletedTask;
    }
}