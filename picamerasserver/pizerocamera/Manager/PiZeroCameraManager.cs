using System.Collections.Concurrent;
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
    private readonly ChangeListener _changeListener;
    private readonly ILogger<PiZeroCameraManager> _logger;
    private readonly IOptionsMonitor<MqttOptions> _optionsMonitor;

    public readonly IReadOnlyDictionary<string, PiZeroCamera> PiZeroCameras;
    private readonly IDbContextFactory<PiDbContext> _dbContextFactory;

    public PiZeroCameraManager(IMqttClient mqttClient, ILogger<PiZeroCameraManager> logger,
        IOptionsMonitor<MqttOptions> optionsMonitor, IDbContextFactory<PiDbContext> dbContextFactory,
        ChangeListener changeListener)
    {
        _mqttClient = mqttClient;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _dbContextFactory = dbContextFactory;
        _changeListener = changeListener;

        using var piDbContext = _dbContextFactory.CreateDbContext();

        // Add all cameras
        var piZeroCameras = new ConcurrentDictionary<string, PiZeroCamera>();
        foreach (var letter in Enumerable.Range('A', 16).Select(c => ((char)c).ToString()))
        {
            foreach (var number in Enumerable.Range(1, 6))
            {
                var id = letter + number;
                piZeroCameras.TryAdd(id, new PiZeroCamera { Id = id });

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
    public async Task CancelCameraTasks()
    {
        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("cancel")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        await _mqttClient.PublishAsync(message);
    }

    /// <summary>
    /// Sends shutdown to all Pi Zeros
    /// </summary>
    public async Task<bool> ShutdownCameras()
    {
        var options = _optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic(options.CommandTopic)
            .WithPayload("sudo shutdown -h now")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var publishResult = await _mqttClient.PublishAsync(message);

        return publishResult.IsSuccess;
    }

    /// <summary>
    /// Sends shutdown to all Pi Zeros
    /// </summary>
    public async Task<bool> ShutdownCamerasQuadrant(char start)
    {
        var options = _optionsMonitor.CurrentValue;

        var message = new MqttApplicationMessageBuilder()
            .WithContentType("application/json")
            .WithTopic("temp")
            .WithPayload("sudo shutdown -h now")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .Build();

        var cols = Enumerable.Range(start, 4).Select(c => ((char)c).ToString()).ToList();

        var publishResult = true;
        foreach (var col in cols)
        {
            message.Topic = $"{options.CommandTopic}/{col}";
            var thisPublishResult = await _mqttClient.PublishAsync(message);
            publishResult = publishResult && thisPublishResult.IsSuccess;
        }

        return publishResult;
    }
}