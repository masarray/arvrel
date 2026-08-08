using System.Numerics;
using Arvrel.Protection;

namespace Arvrel.ProcessBus;

/// <summary>
/// One editable current source used by the internal Transformer Differential test set.
/// RMS and angle are expressed in CT-secondary amperes and electrical degrees.
/// Enabled is meaningful for the independent neutral/NGR channel; phase channels are
/// normally left enabled.
/// </summary>
public sealed record TransformerVirtualInjectionChannel(
    double RmsA,
    double AngleDegrees,
    bool Enabled = true)
{
    public Complex Phasor => Enabled
        ? Complex.FromPolarCoordinates(RmsA, AngleDegrees * Math.PI / 180.0)
        : Complex.Zero;

    public void Validate(string name)
    {
        if (!double.IsFinite(RmsA) || RmsA < 0 || RmsA > 100_000)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(RmsA)}");
        if (!double.IsFinite(AngleDegrees) || AngleDegrees is < -3600 or > 3600)
            throw new ArgumentOutOfRangeException($"{name}.{nameof(AngleDegrees)}");
    }

    public static TransformerVirtualInjectionChannel FromPhasor(Complex value, bool enabled = true)
        => new(value.Magnitude, NormalizeDegrees(value.Phase * 180.0 / Math.PI), enabled);

    private static double NormalizeDegrees(double value)
    {
        while (value > 180) value -= 360;
        while (value <= -180) value += 360;
        return value;
    }
}

/// <summary>
/// Eight-current virtual secondary-injection profile: three phase currents and one
/// independent neutral/NGR current on each transformer side.
/// </summary>
public sealed record TransformerVirtualInjectionProfile(
    string Name,
    double FrequencyHz,
    TransformerVirtualInjectionChannel HighVoltageA,
    TransformerVirtualInjectionChannel HighVoltageB,
    TransformerVirtualInjectionChannel HighVoltageC,
    TransformerVirtualInjectionChannel HighVoltageNeutral,
    TransformerVirtualInjectionChannel LowVoltageA,
    TransformerVirtualInjectionChannel LowVoltageB,
    TransformerVirtualInjectionChannel LowVoltageC,
    TransformerVirtualInjectionChannel LowVoltageNeutral)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (!double.IsFinite(FrequencyHz) || FrequencyHz is < 45 or > 65)
            throw new ArgumentOutOfRangeException(nameof(FrequencyHz));
        HighVoltageA.Validate(nameof(HighVoltageA));
        HighVoltageB.Validate(nameof(HighVoltageB));
        HighVoltageC.Validate(nameof(HighVoltageC));
        HighVoltageNeutral.Validate(nameof(HighVoltageNeutral));
        LowVoltageA.Validate(nameof(LowVoltageA));
        LowVoltageB.Validate(nameof(LowVoltageB));
        LowVoltageC.Validate(nameof(LowVoltageC));
        LowVoltageNeutral.Validate(nameof(LowVoltageNeutral));
    }

    public TransformerVirtualInjectionChannel HighVoltage(TransformerPhase phase) => phase switch
    {
        TransformerPhase.A => HighVoltageA,
        TransformerPhase.B => HighVoltageB,
        TransformerPhase.C => HighVoltageC,
        _ => HighVoltageA
    };

    public TransformerVirtualInjectionChannel LowVoltage(TransformerPhase phase) => phase switch
    {
        TransformerPhase.A => LowVoltageA,
        TransformerPhase.B => LowVoltageB,
        TransformerPhase.C => LowVoltageC,
        _ => LowVoltageA
    };

    /// <summary>
    /// Builds a physically stable through-load starting point from the configured CT
    /// ratios and vector-group compensation. The normalized LV current is the negative
    /// of the normalized HV current, so Idiff tends to zero without hard-coding Dyn11.
    /// </summary>
    public static TransformerVirtualInjectionProfile BalancedThroughLoad(
        TransformerProtectionRuntimeConfiguration configuration,
        double loadingPu = 1.0)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        if (!double.IsFinite(loadingPu) || loadingPu < 0 || loadingPu > 20)
            throw new ArgumentOutOfRangeException(nameof(loadingPu));

        var plan = TransformerEngineeringAdapter.Build(
            configuration.Nameplate,
            configuration.HighVoltageEngineering,
            configuration.LowVoltageEngineering);
        var hvFactor = CompensationFactor(plan.HighVoltageCompensation);
        var lvFactor = CompensationFactor(plan.LowVoltageCompensation);

        var targetA = Complex.FromPolarCoordinates(loadingPu, 0);
        var targetB = Complex.FromPolarCoordinates(loadingPu, -2 * Math.PI / 3);
        var targetC = Complex.FromPolarCoordinates(loadingPu, 2 * Math.PI / 3);

        return new TransformerVirtualInjectionProfile(
            "Balanced through load",
            50,
            TransformerVirtualInjectionChannel.FromPhasor(targetA / hvFactor),
            TransformerVirtualInjectionChannel.FromPhasor(targetB / hvFactor),
            TransformerVirtualInjectionChannel.FromPhasor(targetC / hvFactor),
            new TransformerVirtualInjectionChannel(0, 0, false),
            TransformerVirtualInjectionChannel.FromPhasor(-targetA / lvFactor),
            TransformerVirtualInjectionChannel.FromPhasor(-targetB / lvFactor),
            TransformerVirtualInjectionChannel.FromPhasor(-targetC / lvFactor),
            new TransformerVirtualInjectionChannel(0, 0, false));
    }

    private static Complex CompensationFactor(TransformerWindingCompensation compensation)
    {
        var factor = Complex.FromPolarCoordinates(
            compensation.CurrentScaleToPu,
            compensation.PhaseShiftDegrees * Math.PI / 180.0);
        return compensation.ReversePolarity ? -factor : factor;
    }
}

/// <summary>
/// Vendor-neutral laboratory defaults used only when the operator selects Internal demo
/// for the Transformer Differential IED. Live capture and PCAP replay continue to use
/// the practitioner configuration and real paired SV streams.
/// </summary>
public static class TransformerVirtualInjectionDefaults
{
    public const string HighVoltageStreamKey = "VIRTUAL-87T-HV";
    public const string LowVoltageStreamKey = "VIRTUAL-87T-LV";

    public static TransformerProtectionRuntimeConfiguration CreateConfiguration()
    {
        var settings = new TransformerProtectionSettings
        {
            Differential87T = new TransformerDifferentialSettings
            {
                Enabled = true,
                HighSetEnabled = true,
                HarmonicSecurityMode = TransformerHarmonicSecurityMode.Blocking
            },
            RefHighVoltage = new RestrictedEarthFaultSettings { Enabled = true },
            RefLowVoltage = new RestrictedEarthFaultSettings { Enabled = true }
        };

        return new TransformerProtectionRuntimeConfiguration(
            HighVoltageStreamKey,
            LowVoltageStreamKey,
            new TransformerNameplate(100, 150, 20, "Dyn11"),
            new TransformerWindingEngineering(
                new TransformerCtRatio(600, 1),
                new TransformerCtRatio(600, 1)),
            new TransformerWindingEngineering(
                new TransformerCtRatio(3000, 1),
                new TransformerCtRatio(3000, 1)),
            settings)
        {
            PairingSettings = new TransformerSvPairingSettings
            {
                RequireGlobalSynchronization = true,
                MaximumSampleCounterSkew = 1,
                MaximumCaptureTimestampSkew = TimeSpan.FromMilliseconds(20)
            }
        };
    }
}

/// <summary>
/// Synthetic paired-SV source for internal secondary-injection tests. It does not
/// publish Ethernet frames and it does not contain a protection algorithm. Every
/// generated HV/LV pair is passed to the same TransformerProtectionRuntime used by
/// process-bus operation.
/// </summary>
public sealed class TransformerVirtualInjectionRuntime
{
    public const int SamplesPerCycle = 80;
    public const int WindowCycles = 2;

    private TransformerProtectionRuntime _runtime;
    private TransformerProtectionRuntimeConfiguration _configuration;
    private TransformerVirtualInjectionProfile _profile;
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private ushort _sampleCounter;
    private SmvRuntimeSnapshot _highVoltageSnapshot = SmvRuntimeSnapshot.Empty;
    private SmvRuntimeSnapshot _lowVoltageSnapshot = SmvRuntimeSnapshot.Empty;

    public TransformerVirtualInjectionRuntime(TransformerProtectionRuntimeConfiguration? configuration = null)
    {
        _configuration = configuration ?? TransformerVirtualInjectionDefaults.CreateConfiguration();
        _configuration.Validate();
        _runtime = new TransformerProtectionRuntime(_configuration);
        _profile = TransformerVirtualInjectionProfile.BalancedThroughLoad(_configuration);
        BuildPair(energized: false);
    }

    public event EventHandler<TransformerProtectionRuntimeSnapshotChangedEventArgs>? SnapshotChanged
    {
        add => _runtime.SnapshotChanged += value;
        remove => _runtime.SnapshotChanged -= value;
    }

    public bool IsRunning { get; private set; }
    public TransformerVirtualInjectionProfile ActiveProfile => _profile;
    public TransformerProtectionRuntimeConfiguration Configuration => _configuration;
    public TransformerProtectionRuntimeSnapshot CurrentSnapshot => _runtime.CurrentSnapshot;
    public SmvRuntimeSnapshot HighVoltageSnapshot => _highVoltageSnapshot;
    public SmvRuntimeSnapshot LowVoltageSnapshot => _lowVoltageSnapshot;

    public void ApplyProfile(TransformerVirtualInjectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        _profile = profile;
        // Editing an injection is an atomic source change. Reset timing so a pickup
        // cannot bridge two unrelated test vectors.
        _runtime.Reset();
        BuildPair(IsRunning);
    }

    public void UpdateConfiguration(TransformerProtectionRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        _configuration = configuration with
        {
            HighVoltageStreamKey = TransformerVirtualInjectionDefaults.HighVoltageStreamKey,
            LowVoltageStreamKey = TransformerVirtualInjectionDefaults.LowVoltageStreamKey
        };
        _configuration.Validate();
        _runtime = new TransformerProtectionRuntime(_configuration);
        _profile = TransformerVirtualInjectionProfile.BalancedThroughLoad(_configuration);
        BuildPair(IsRunning);
    }

    public TransformerProtectionRuntimeSnapshot Start()
    {
        IsRunning = true;
        _runtime.Reset();
        return Advance(TimeSpan.FromMilliseconds(20));
    }

    public TransformerProtectionRuntimeSnapshot Stop()
    {
        IsRunning = false;
        _runtime.Reset();
        BuildPair(energized: false);
        return _runtime.CurrentSnapshot;
    }

    public TransformerProtectionRuntimeSnapshot Reset()
    {
        _runtime.Reset();
        return _runtime.CurrentSnapshot;
    }

    public TransformerProtectionRuntimeSnapshot Advance(TimeSpan elapsed)
    {
        if (!IsRunning)
            return _runtime.CurrentSnapshot;

        var step = elapsed <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(20) : elapsed;
        _timestamp = _timestamp.Add(step);
        _sampleCounter = unchecked((ushort)(_sampleCounter + 1));
        BuildPair(energized: true);
        return _runtime.EvaluateSnapshots(
            _highVoltageSnapshot,
            _lowVoltageSnapshot,
            ProcessBusSourceMode.InternalDemo);
    }

    public SmvRuntimeSnapshot SnapshotForSide(bool highVoltage)
        => highVoltage ? _highVoltageSnapshot : _lowVoltageSnapshot;

    private void BuildPair(bool energized)
    {
        var hvA = energized ? _profile.HighVoltageA : _profile.HighVoltageA with { RmsA = 0 };
        var hvB = energized ? _profile.HighVoltageB : _profile.HighVoltageB with { RmsA = 0 };
        var hvC = energized ? _profile.HighVoltageC : _profile.HighVoltageC with { RmsA = 0 };
        var hvN = energized ? _profile.HighVoltageNeutral : _profile.HighVoltageNeutral with { RmsA = 0 };
        var lvA = energized ? _profile.LowVoltageA : _profile.LowVoltageA with { RmsA = 0 };
        var lvB = energized ? _profile.LowVoltageB : _profile.LowVoltageB with { RmsA = 0 };
        var lvC = energized ? _profile.LowVoltageC : _profile.LowVoltageC with { RmsA = 0 };
        var lvN = energized ? _profile.LowVoltageNeutral : _profile.LowVoltageNeutral with { RmsA = 0 };

        _highVoltageSnapshot = BuildSnapshot(
            TransformerVirtualInjectionDefaults.HighVoltageStreamKey,
            "VIRTUAL_87T_HV",
            0x4A01,
            hvA,
            hvB,
            hvC,
            hvN,
            _profile.FrequencyHz,
            _timestamp,
            _sampleCounter);
        _lowVoltageSnapshot = BuildSnapshot(
            TransformerVirtualInjectionDefaults.LowVoltageStreamKey,
            "VIRTUAL_87T_LV",
            0x4A02,
            lvA,
            lvB,
            lvC,
            lvN,
            _profile.FrequencyHz,
            _timestamp,
            _sampleCounter);
    }

    private static SmvRuntimeSnapshot BuildSnapshot(
        string key,
        string svId,
        ushort appId,
        TransformerVirtualInjectionChannel phaseA,
        TransformerVirtualInjectionChannel phaseB,
        TransformerVirtualInjectionChannel phaseC,
        TransformerVirtualInjectionChannel neutral,
        double frequency,
        DateTimeOffset timestamp,
        ushort sampleCounter)
    {
        var a = phaseA.Phasor;
        var b = phaseB.Phasor;
        var c = phaseC.Phasor;
        var n = neutral.Phasor;
        var zeros = Complex.Zero;
        var phasors = new PhasorMeasurementSet(a, b, c, n, zeros, zeros, zeros, zeros, frequency)
        {
            NeutralCurrentAvailable = neutral.Enabled,
            NeutralVoltageAvailable = false
        };
        var waveform = new SmvWaveformWindow(
            Samples(a),
            Samples(b),
            Samples(c),
            Samples(n),
            frequency,
            SamplesPerCycle);
        var measurement = new MeasurementFrame(
            timestamp,
            a.Magnitude,
            b.Magnitude,
            c.Magnitude,
            neutral.Enabled ? n.Magnitude : (a + b + c).Magnitude,
            SmvTrustState.Healthy,
            phasors);
        var stream = new SmvStreamInfo(key, appId, svId, "VIRTUAL", null, "GOOD", false);

        return new SmvRuntimeSnapshot(
            stream,
            timestamp,
            measurement,
            ProtectionSnapshot.Ready(timestamp),
            waveform,
            sampleCounter,
            2,
            SamplesPerCycle,
            frequency * SamplesPerCycle,
            sampleCounter,
            sampleCounter,
            0,
            0,
            0,
            0,
            true,
            "Virtual secondary amperes",
            "IA/IB/IC + independent IN/NGR",
            "Internal synchronized pair",
            "No SCL · internal test source",
            "Virtual paired secondary injection",
            Array.Empty<string>());
    }

    private static IReadOnlyList<double> Samples(Complex phasor)
    {
        var result = new double[SamplesPerCycle * WindowCycles];
        var peak = Math.Sqrt(2) * phasor.Magnitude;
        var phase = phasor.Phase;
        for (var index = 0; index < result.Length; index++)
        {
            var angle = 2 * Math.PI * index / SamplesPerCycle + phase;
            result[index] = peak * Math.Cos(angle);
        }
        return result;
    }
}
