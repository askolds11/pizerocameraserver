using System.Text;
using MQTTnet;
using picamerasserver.PiZero.Responses;

namespace picamerasserver.PiZero.Update;

public partial class Update
{
    /// <inheritdoc />
    public async Task ResponseUpdate(
        MqttApplicationMessage message,
        string id
    )
    {
        var piZeroCamera = piZeroManager.PiZeroCameras[id];

        var text = Encoding.UTF8.GetString(message.Payload);
        var statusResponse = Json.TryDeserialize<SuccessWrapper<UpdateResponse>>(text, logger);

        if (statusResponse.IsFailure)
        {
            // piZeroCamera.Status = null;
            logger.LogError(statusResponse.Error, "Failed to parse update response");

            return;
        }

        var successWrapper = statusResponse.Value;

        // Send received signal so that next cameras can be updated
        if (successWrapper.Value is UpdateResponse.UpdateDownloaded or UpdateResponse.AlreadyUpdated)
        {
            _updateChannel?.Writer.TryWrite(id);
        }

        if (successWrapper.Success)
        {
            piZeroCamera.UpdateRequest = successWrapper.Value switch
            {
                UpdateResponse.DownloadingUpdate => new PiZeroCameraUpdateRequest.Downloading(),
                UpdateResponse.UpdateDownloaded => new PiZeroCameraUpdateRequest.Downloaded(),
                UpdateResponse.AlreadyUpdated => new PiZeroCameraUpdateRequest.Success(),
                _ => new PiZeroCameraUpdateRequest.UnknownSuccess()
            };
        }
        else
        {
            piZeroCamera.UpdateRequest = successWrapper.Value switch
            {
                UpdateResponse.Failure failure => failure switch
                {
                    UpdateResponse.Failure.Failed failed =>
                        new PiZeroCameraUpdateRequest.Failure.Failed(failed.Message),
                    _ => throw new ArgumentOutOfRangeException(nameof(failure))
                },
                _ => new PiZeroCameraUpdateRequest.Failure.UnknownFailure()
            };
        }

        changeListener.UpdateUpdate();
        await Task.Yield();
    }
}