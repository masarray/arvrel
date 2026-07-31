using Arvrel.Protection;

namespace Arvrel.ProcessBus;

public enum ProcessBusSourceMode
{
    InternalDemo,
    LiveNpcap,
    PcapReplay
}

public sealed record ProcessBusAdapter(string Selector, string DisplayName, string MacAddress)
{
    public override string ToString() => DisplayName;
}

public sealed record SmvStreamInfo(
    string Key,
    ushort AppId,
    string SvId,
    string Destination,
    ushort? VlanId,
    string Health,
    bool IsSclBound)
{
    public string DisplayName => $"{SvId}  ·  APPID 0x{AppId:X4}  ·  {Health}";
    public override string ToString() => DisplayName;
}

public sealed record SmvMeasurementContext
{
    public double CtPrimaryA { get; init; } = 1;
    public double CtSecondaryA { get; init; } = 1;
    public double NominalFrequencyHz { get; init; } = 50;

    public double PrimaryRatio => CtSecondaryA <= 0 ? 1 : CtPrimaryA / CtSecondaryA;

    public void Validate()
    {
        if (!double.IsFinite(CtPrimaryA) || CtPrimaryA <= 0)
            throw new ArgumentOutOfRangeException(nameof(CtPrimaryA));
        if (!double.IsFinite(CtSecondaryA) || CtSecondaryA <= 0)
            throw new ArgumentOutOfRangeException(nameof(CtSecondaryA));
        if (!double.IsFinite(NominalFrequencyHz) || NominalFrequencyHz is < 45 or > 65)
            throw new ArgumentOutOfRangeException(nameof(NominalFrequencyHz));
    }
}

public sealed record SmvWaveformWindow(
    IReadOnlyList<double> PhaseA,
    IReadOnlyList<double> PhaseB,
    IReadOnlyList<double> PhaseC,
    IReadOnlyList<double> Residual,
    double FrequencyHz,
    int SamplesPerCycle)
{
    public static SmvWaveformWindow Empty { get; } = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        50,
        80);
}

public sealed record SmvRuntimeSnapshot(
    SmvStreamInfo Stream,
    DateTimeOffset Timestamp,
    MeasurementFrame Measurement,
    ProtectionSnapshot Protection,
    SmvWaveformWindow Waveform,
    ushort SampleCounter,
    byte SampleSynchronization,
    int SamplesPerCycle,
    double FramesPerSecond,
    long FrameCount,
    long AsduCount,
    int SequenceGapCount,
    int DuplicateCount,
    int OutOfOrderCount,
    int InvalidQualityCount,
    bool ScalingResolved,
    string ScalingSummary,
    string MappingSummary,
    string TrustSummary,
    string SclSummary,
    string SourceSummary,
    IReadOnlyList<string> Diagnostics)
{
    public static SmvRuntimeSnapshot Empty { get; } = new(
        new SmvStreamInfo("", 0, "No stream", "", null, "IDLE", false),
        DateTimeOffset.UtcNow,
        new MeasurementFrame(DateTimeOffset.UtcNow, 0, 0, 0, 0,
            new SmvTrustState(false, false, false, "NO_STREAM", "No Sampled Values stream is selected.")),
        ProtectionSnapshot.Ready(DateTimeOffset.UtcNow),
        SmvWaveformWindow.Empty,
        0,
        0,
        80,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        "Unresolved",
        "No mapping",
        "No stream",
        "No SCL",
        "Idle",
        Array.Empty<string>());
}

public sealed record ProcessBusEvidence(
    DateTimeOffset ExportedAt,
    string Application,
    string SourceMode,
    SmvRuntimeSnapshot Snapshot,
    ProtectionSettings ProtectionSettings,
    SmvMeasurementContext MeasurementContext,
    IReadOnlyList<string> Events);
