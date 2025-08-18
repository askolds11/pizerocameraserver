using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<PiZeroCameraManager> _logger;
    private readonly IOptionsMonitor<MqttOptions> _optionsMonitor;
    private readonly IOptionsMonitor<DirectoriesOptions> _dirOptionsMonitor;

    public readonly IReadOnlyDictionary<string, PiZeroCamera> PiZeroCameras;
    private readonly IDbContextFactory<PiDbContext> _dbContextFactory;

    public PiZeroCameraManager(IMqttClient mqttClient, ILogger<PiZeroCameraManager> logger,
        IOptionsMonitor<MqttOptions> optionsMonitor, IDbContextFactory<PiDbContext> dbContextFactory,
        IOptionsMonitor<DirectoriesOptions> dirOptionsMonitor)
    {
        _mqttClient = mqttClient;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _dbContextFactory = dbContextFactory;
        _dirOptionsMonitor = dirOptionsMonitor;

        using var piDbContext = _dbContextFactory.CreateDbContext();

        // Add all cameras
        var piZeroCameras = new Dictionary<string, PiZeroCamera>();
        foreach (var letter in Enumerable.Range('A', 16).Select(c => ((char)c).ToString()))
        {
            foreach (var number in Enumerable.Range(1, 6))
            {
                var id = letter + number;
                piZeroCameras.Add(id, new PiZeroCamera { Id = id });

                if (!piDbContext.Cameras.Any(x => x.Id == id))
                {
                    piDbContext.Add(new CameraModel { Id = id });
                }
            }
        }

        piDbContext.SaveChanges();

        PiZeroCameras = piZeroCameras;
    }

    /// <summary>
    /// Sends a cancel message to cameras to stop existing tasks
    /// </summary>
    private async Task CancelCameraTasks()
    {
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("cancel")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        await _mqttClient.PublishAsync(message);
    }
}