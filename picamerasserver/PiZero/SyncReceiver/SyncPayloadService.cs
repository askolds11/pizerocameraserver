namespace picamerasserver.PiZero.SyncReceiver;

/// <summary>
/// Service to store and get the latest sync packet.
/// </summary>
public class SyncPayloadService
{
    private SyncPayload? _latest;
    private readonly Lock _lock = new(); // simple lock for atomic update

    public void Update(SyncPayload payload)
    {
        lock (_lock)
        {
            _latest = payload;
        }
    }

    public SyncPayload? GetLatest()
    {
        lock (_lock)
        {
            return _latest;
        }
    }
}