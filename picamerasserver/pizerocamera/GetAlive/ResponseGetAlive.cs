using System.Text;
using MQTTnet;

namespace picamerasserver.pizerocamera.GetAlive;

public partial class GetAlive
{
    /// <inheritdoc />
    public async Task ResponseGetStatus(MqttApplicationMessage message, string id)
    {
        var piZeroCamera = piZeroCameraManager.PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<StatusResponse>(text, logger);
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
}