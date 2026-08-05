using Arvrel.Protection;

namespace Arvrel.ProcessBus;

public enum ProcessBusSourceMode
{
    InternalDemo,
    LiveCapture,
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
    public double VtPrimaryV { get; init; } = 1;
    public double VtSecondaryV { get; init; } = 1;
    public double NominalFrequencyHz { get; init; } = 50;

    public double PrimaryRatio => CtSecondaryA <= 0 ? 1 : CtPrimaryA / CtSecondaryA;
    public double VoltagePrimaryRatio => VtSecondaryV <= 0 ? 1 : VtPrimaryV / VtSecondaryV;

    public void Validate()
    {
        if (!double.IsFinite(CtPrimaryA) || CtPrimaryA <= 0)
            throw new ArgumentOutOfRangeException(nameof(CtPrimaryA));
        if (!double.IsFinite(CtSecondaryA) || CtSecondaryA <= 0)
            throw new ArgumentOutOfRangeException(nameof(CtSecondaryA));
        if (!double.IsFinite(VtPrimaryV) || VtPrimaryV <= 0)
            throw new ArgumentOutOfRangeException(nameof(VtPrimaryV));
        if (!double.IsFinite(VtSecondaryV) || VtSecondaryV <= 0)
            throw new ArgumentOutOfRangeException(nameof(VtSecondaryV));
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
    int SamplesPerCycle,
    IReadOnlyList<double>? PhaseAVoltage = null,
    IReadOnlyList<double>? PhaseBVoltage = null,
    IReadOnlyList<double>? PhaseCVoltage = null,
    IReadOnlyList<double>? NeutralVoltage = null)
{
    public IReadOnlyList<double> VoltageA => PhaseAVoltage ?? Array.Empty<double>();
    public IReadOnlyList<double> VoltageB => PhaseBVoltage ?? Array.Empty<double>();
    public IReadOnlyList<double> VoltageC => PhaseCVoltage ?? Array.Empty<double>();
    public IReadOnlyList<double> VoltageN => NeutralVoltage ?? Array.Empty<double>();

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
