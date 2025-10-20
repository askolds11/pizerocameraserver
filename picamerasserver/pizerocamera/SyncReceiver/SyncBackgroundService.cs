using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace picamerasserver.pizerocamera.syncreceiver;

/// <summary>
/// Background service to receive sync packets from Pi Camera and store the latest one
/// </summary>
/// <param name="logger"></param>
/// <param name="syncService"></param>
public class UdpListenerService(
    ILogger<UdpListenerService> logger,
    SyncPayloadService syncService
) : BackgroundService
{
    private const string MulticastGroup = "239.255.255.250";
    private const int Port = 10000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Bind to port to receive multicast
        var localEp = new IPEndPoint(IPAddress.Any, Port);
        udpClient.Client.Bind(localEp);

        // Join the multicast group
        udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastGroup));
        logger.LogInformation("Listening for multicast on {Group}:{Port}", MulticastGroup, Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(stoppingToken);
                if (result.Buffer.Length < Marshal.SizeOf<SyncPayload>())
                {
                    logger.LogWarning("Received packet too small ({Length} bytes)", result.Buffer.Length);
                    continue;
                }

                var payload = SyncPayload.FromBytes(result.Buffer);

                logger.LogDebug(
                    "FrameDuration={FrameDuration}, SystemFrameTimestamp={SystemFrameTimestamp}, WallClockFrameTimestamp={WallClockFrameTimestamp}, SystemReadyTime={SystemReadyTime}, WallClockReadyTime={WallClockReadyTime}",
                    payload.FrameDuration, payload.SystemFrameTimestamp, payload.WallClockFrameTimestamp,
                    payload.SystemReadyTime, payload.WallClockReadyTime
                );

                syncService.Update(payload);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error receiving UDP multicast packet");
                await Task.Delay(1000, stoppingToken); // brief pause before retry
            }
        }

        logger.LogInformation("UDP listener stopped");
    }
}