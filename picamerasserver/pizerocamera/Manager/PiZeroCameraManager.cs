using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MQTTnet;
using picamerasserver.Database;
using picamerasserver.Database.Models;
using picamerasserver.Options;

namespace picamerasserver.pizerocamera.manager;

public partial class PiZeroCameraManager
{
    private readonly IMqttClient _mqttClient;
    private readonly ILogger<PiZeroCameraManager> _logger;
    private readonly IOptionsMonitor<MqttOptions> _optionsMonitor;

    public readonly IReadOnlyList<string> PiZeroCameraIds;
    public readonly IReadOnlyDictionary<string, PiZeroCamera> PiZeroCameras;
    private readonly IDbContextFactory<PiDbContext> _dbContextFactory;
    public event Action? OnChange;

    public PiZeroCameraManager(IMqttClient mqttClient, ILogger<PiZeroCameraManager> logger,
        IOptionsMonitor<MqttOptions> optionsMonitor, IDbContextFactory<PiDbContext> dbContextFactory)
    {
        _mqttClient = mqttClient;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _dbContextFactory = dbContextFactory;

        using var piDbContext = _dbContextFactory.CreateDbContext();
        
        // Add all cameras
        var piZeroCamerasIds = new List<string>();
        var piZeroCameras = new Dictionary<string, PiZeroCamera>();
        foreach (var letter in Enumerable.Range('A', 16).Select(c => ((char)c).ToString()))
        {
            foreach (var number in Enumerable.Range(1, 6))
            {
                var id = letter + number;
                piZeroCamerasIds.Add(id);
                piZeroCameras.Add(id, new PiZeroCamera { Id = id });

                if (!piDbContext.Cameras.Any(x => x.Id == id))
                {
                    piDbContext.Add(new CameraModel { Id = id });
                }
            }
        }
        piDbContext.SaveChanges();

        PiZeroCameraIds = piZeroCamerasIds;
        PiZeroCameras = piZeroCameras;
    }
}