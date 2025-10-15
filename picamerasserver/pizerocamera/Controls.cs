namespace picamerasserver.pizerocamera;

/* Raw Pi Camera 2 output
{
   'AeConstraintMode': (0, 3, 0),
   'AeEnable': (False, True, True),
   'AeExposureMode': (0, 3, 0),
   'AeFlickerMode': (0, 1, 0),
   'AeFlickerPeriod': (100, 1000000, None),
   'AeMeteringMode': (0, 3, 0),
   'AnalogueGain': (1.0, 16.0, 1.0), // !!!!!!!!!!! 'AnalogueGain': (1.0, 10.666666984558105, 1.0), 
   'AnalogueGainMode': (0, 1, 0)
   'AwbEnable': (False, True, None),
   'AwbMode': (0, 7, 0),
   'Brightness': (-1.0, 1.0, 0.0),
   'ColourGains': (0.0, 32.0, None),
   'ColourTemperature': (100, 100000, None),
   'Contrast': (0.0, 32.0, 1.0),
   'CnnEnableInputTensor': (False, True, False),
   'ExposureTime': (1, 66666, 20000), // !!!!!!!!! 'ExposureTime': (75, 1238765, 20000), 
   'ExposureTimeMode': (0, 1, 0),
   'ExposureValue': (-8.0, 8.0, 0.0),
   'FrameDurationLimits': (33333, 120000, 33333), // !!!!!!!!!!!!! 'FrameDurationLimits': (47183, 1238841, 33333), 
   'HdrMode': (0, 4, 0),
   'NoiseReductionMode': (0, 4, 0),
   'Saturation': (0.0, 32.0, 1.0),
   'ScalerCrop': ((0, 0, 0, 0), (65535, 65535, 65535, 65535), (0, 0, 0, 0)), // !!!!!!!!!!! 'ScalerCrop': ((0, 0, 64, 64), (0, 0, 3280, 2464), (0, 0, 3280, 2464)), 
   'Sharpness': (0.0, 16.0, 1.0),
   'SyncMode': (0, 2, 0),
   'SyncFrames': (1, 1000000, 100),
   'StatsOutputEnable': (False, True, False),
}
 */



public class Controls
{
    public AeConstraintModeEnum? AeConstraintMode { get; set; } = AeConstraintModeEnum.Normal;
    public bool? AeEnable { get; set; } = true;
    public AeExposureModeEnum? AeExposureMode { get; set; } = AeExposureModeEnum.Normal;
    public AeFlickerModeEnum? AeFlickerMode { get; set; } = AeFlickerModeEnum.Off;
    public long? AeFlickerPeriod { get; set; } = null;
    public AeMeteringModeEnum? AeMeteringMode { get; set; } = AeMeteringModeEnum.CentreWeighted;
    public float? AnalogueGain { get; set; } = 1.0f;
    public AnalogueGainModeEnum? AnalogueGainMode { get; set; } = AnalogueGainModeEnum.Auto;
    public bool? AwbEnable { get; set; } = null;
    public AwbModeEnum? AwbMode { get; set; } = AwbModeEnum.Auto;
    public float? Brightness { get; set; } = 0.0f;
    public float? ColourGains { get; set; } = null;
    public long? ColourTemperature { get; set; } = null;
    public float? Contrast { get; set; } = 1.0f;
    public bool? CnnEnableInputTensor { get; set; } = false;
    public long? ExposureTime { get; set; } = 20000;
    public ExposureTimeModeEnum? ExposureTimeMode { get; set; } = ExposureTimeModeEnum.Auto;
    public float? ExposureValue { get; set; } = 0.0f;
    public FrameDurationLimits? FrameDurationLimits { get; set; } = new(33333, 120000);
    public HdrModeEnum? HdrMode { get; set; } = HdrModeEnum.Off;
    public NoiseReductionModeEnum? NoiseReductionMode { get; set; } = NoiseReductionModeEnum.Off;
    public float? Saturation { get; set; } = 1.0f;
    public ScalerCrop? ScalerCrop { get; set; } = new(0, 0, 0, 0);
    public float? Sharpness { get; set; } = 1.0f;
    public SyncModeEnum? SyncMode { get; set; } = SyncModeEnum.Off;
    public long? SyncFrames { get; set; } = 100;
    public bool? StatsOutputEnable { get; set; } = false;
}

public record FrameDurationLimits(long Min, long Max);
public record ScalerCrop(uint X, uint Y, uint Width, uint Height);

public enum AeConstraintModeEnum
{
    Normal = 0,
    Highlight = 1,
    Shadows = 2,
    Custom = 3
}

public enum AeExposureModeEnum
{
    Normal = 0,
    Short = 1,
    Long = 2,
    Custom = 3
}

public enum AeFlickerModeEnum
{
    Off = 0,
    Manual = 1,
    // Auto = 2
}

public enum AeMeteringModeEnum
{
    CentreWeighted = 0,
    Spot = 1,
    Matrix = 2,
    Custom = 3
}

public enum AnalogueGainModeEnum
{
    Auto = 0,
    Manual = 1
}

public enum AwbModeEnum
{
    Auto = 0,
    Incandescent = 1,
    Tungsten = 2,
    Fluorescent = 3,
    Indoor = 4,
    Daylight = 5,
    Cloudy = 6,
    Custom = 7
}

public enum ExposureTimeModeEnum
{
    Auto = 0,
    Manual = 1
}

public enum HdrModeEnum
{
    Off = 0,
    MultiExposureUnmerged = 1,
    MultiExposure = 2,
    SingleExposure = 3,
    Night = 4
}

public enum NoiseReductionModeEnum
{
    Off = 0,
    Fast = 1,
    HighQuality = 2,
    Minimal = 3,
    // ReSharper disable once InconsistentNaming
    ZSL = 4
}

public enum SyncModeEnum
{
    Off = 0,
    Server = 1,
    Client = 2
}