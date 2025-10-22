using MQTTnet;
using picamerasserver.PiZero.Responses;

namespace picamerasserver.PiZero.Sync;

public partial class Sync
{
    /// <inheritdoc />
    public Task ResponseSyncStatus(MqttApplicationMessage message, CameraResponse.SyncStatus syncStatus, string id)
    {
        // inform channel about response
        _syncChannel?.Writer.TryWrite(id);

        var piZeroCamera = piZeroManager.PiZeroCameras[id];
        
        var successWrapper = syncStatus.Response;
        if (successWrapper.Success)
        {
            piZeroCamera.SyncStatus = successWrapper.Value switch
            {
                SyncStatusResponse.Success success => new SyncStatus.Success(success.SyncReady, success.SyncTiming),
                _ => throw new ArgumentOutOfRangeException(nameof(successWrapper.Value))
            };
        }
        else
        {
            piZeroCamera.SyncStatus = successWrapper.Value switch
            {
                SyncStatusResponse.Failure.Failed failed => new SyncStatus.Failure.Failed(failed.Message),
                _ => throw new ArgumentOutOfRangeException(nameof(successWrapper.Value))
            };
        }

        changeListener.UpdateSync();

        return Task.CompletedTask;
    }
}