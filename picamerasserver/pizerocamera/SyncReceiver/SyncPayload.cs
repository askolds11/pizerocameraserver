using System.Runtime.InteropServices;

namespace picamerasserver.pizerocamera.syncreceiver;

/// <summary>
/// Pi Camera's sync packet
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncPayload
{
    /* Frame duration in microseconds. */
    public uint FrameDuration;

    /* Frame duration in microseconds. */
    public uint SomePadding;

    /* Server system (kernel) frame timestamp. */
    public ulong SystemFrameTimestamp;

    /* Server wall clock version of the frame timestamp. */
    public ulong WallClockFrameTimestamp;

    /* Server system (kernel) sync time (the time at which frames are marked ready). */
    public ulong SystemReadyTime;

    /* Server wall clock version of the sync time. */
    public ulong WallClockReadyTime;
    
    /// <summary>
    /// Parses a byte array into a SyncPayload struct.
    /// </summary>
    /// <param name="data">Byte buffer</param>
    /// <returns>Sync payload</returns>
    /// <exception cref="ArgumentException"></exception>
    public static SyncPayload FromBytes(byte[] data)
    {
        if (data.Length < Marshal.SizeOf<SyncPayload>())
            throw new ArgumentException("Data too short for SyncPayload");

        // Copy bytes into a struct
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<SyncPayload>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }
};