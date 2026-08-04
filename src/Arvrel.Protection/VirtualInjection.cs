using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Arvrel.Protection;

public enum VirtualInjectionSignal
{
    PhaseAVoltage,
    PhaseBVoltage,
    PhaseCVoltage,
    NeutralVoltage,
    PhaseACurrent,
    PhaseBCurrent,
    PhaseCCurrent,
    NeutralCurrent
}

public sealed record VirtualInjectionChannel(
    bool Enabled,
    double Rms,
    double AngleDegrees)
{
    public VirtualInjectionChannel Normalize()
        => this with { AngleDegrees = NormalizeAngle(AngleDegrees) };

    public void Validate(string name)
    {
        if (!double.IsFinite(Rms) || Rms < 0 || Rms > 1_000_000_000)
            throw new ArgumentOutOfRangeException(name, "RMS value must be finite and between 0 and 1e9.");
        if (!double.IsFinite(AngleDegrees))
            throw new ArgumentOutOfRangeException(name, "Angle must be finite.");
    }

    public static double NormalizeAngle(double angle)
    {
        if (!double.IsFinite(angle))
            return angle;
        angle %= 360;
        if (angle > 180)
            angle -= 360;
        if (angle <= -180)
            angle += 360;
        return angle;
    }
}

public sealed record VirtualInjectionProfile(
    string Name,
    double FrequencyHz,
    VirtualInjectionChannel PhaseAVoltage,
    VirtualInjectionChannel PhaseBVoltage,
    VirtualInjectionChannel PhaseCVoltage,
    VirtualInjectionChannel NeutralVoltage,
    VirtualInjectionChannel PhaseACurrent,
    VirtualInjectionChannel PhaseBCurrent,
    VirtualInjectionChannel PhaseCCurrent,
    VirtualInjectionChannel NeutralCurrent)
{
    public const double MinimumFrequencyHz = 40;
    public const double MaximumFrequencyHz = 70;

    public bool ExplicitNeutralVoltage => NeutralVoltage.Enabled;
    public bool ExplicitNeutralCurrent => NeutralCurrent.Enabled;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Injection profile name is required.", nameof(Name));
        if (!double.IsFinite(FrequencyHz) || FrequencyHz < MinimumFrequencyHz || FrequencyHz > MaximumFrequencyHz)
            throw new ArgumentOutOfRangeException(nameof(FrequencyHz), $"Frequency must be between {MinimumFrequencyHz:0} and {MaximumFrequencyHz:0} Hz.");

        PhaseAVoltage.Validate(nameof(PhaseAVoltage));
        PhaseBVoltage.Validate(nameof(PhaseBVoltage));
        PhaseCVoltage.Validate(nameof(PhaseCVoltage));
        NeutralVoltage.Validate(nameof(NeutralVoltage));
        PhaseACurrent.Validate(nameof(PhaseACurrent));
        PhaseBCurrent.Validate(nameof(PhaseBCurrent));
        PhaseCCurrent.Validate(nameof(PhaseCCurrent));
        NeutralCurrent.Validate(nameof(NeutralCurrent));
    }

    public VirtualInjectionProfile Normalize()
    {
        Validate();
        return this with
        {
            Name = Name.Trim(),
            PhaseAVoltage = PhaseAVoltage.Normalize(),
            PhaseBVoltage = PhaseBVoltage.Normalize(),
            PhaseCVoltage = PhaseCVoltage.Normalize(),
            NeutralVoltage = NeutralVoltage.Normalize(),
            PhaseACurrent = PhaseACurrent.Normalize(),
            PhaseBCurrent = PhaseBCurrent.Normalize(),
            PhaseCCurrent = PhaseCCurrent.Normalize(),
            NeutralCurrent = NeutralCurrent.Normalize()
        };
    }

    public VirtualInjectionChannel Channel(VirtualInjectionSignal signal) => signal switch
    {
        VirtualInjectionSignal.PhaseAVoltage => PhaseAVoltage,
        VirtualInjectionSignal.PhaseBVoltage => PhaseBVoltage,
        VirtualInjectionSignal.PhaseCVoltage => PhaseCVoltage,
        VirtualInjectionSignal.NeutralVoltage => NeutralVoltage,
        VirtualInjectionSignal.PhaseACurrent => PhaseACurrent,
        VirtualInjectionSignal.PhaseBCurrent => PhaseBCurrent,
        VirtualInjectionSignal.PhaseCCurrent => PhaseCCurrent,
        VirtualInjectionSignal.NeutralCurrent => NeutralCurrent,
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, "Unknown injection signal.")
    };

    public string Fingerprint()
    {
        var normalized = Normalize();
        var canonical = ProtectionSettings.CanonicalJoin(
            normalized.Name,
            normalized.FrequencyHz,
            normalized.PhaseAVoltage.Enabled, normalized.PhaseAVoltage.Rms, normalized.PhaseAVoltage.AngleDegrees,
            normalized.PhaseBVoltage.Enabled, normalized.PhaseBVoltage.Rms, normalized.PhaseBVoltage.AngleDegrees,
            normalized.PhaseCVoltage.Enabled, normalized.PhaseCVoltage.Rms, normalized.PhaseCVoltage.AngleDegrees,
            normalized.NeutralVoltage.Enabled, normalized.NeutralVoltage.Rms, normalized.NeutralVoltage.AngleDegrees,
            normalized.PhaseACurrent.Enabled, normalized.PhaseACurrent.Rms, normalized.PhaseACurrent.AngleDegrees,
            normalized.PhaseBCurrent.Enabled, normalized.PhaseBCurrent.Rms, normalized.PhaseBCurrent.AngleDegrees,
            normalized.PhaseCCurrent.Enabled, normalized.PhaseCCurrent.Rms, normalized.PhaseCCurrent.AngleDegrees,
            normalized.NeutralCurrent.Enabled, normalized.NeutralCurrent.Rms, normalized.NeutralCurrent.AngleDegrees);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed record VirtualInjectionFrame(
    VirtualInjectionProfile Profile,
    MeasurementFrame Measurement,
    double[] PhaseACurrentSamples,
    double[] PhaseBCurrentSamples,
    double[] PhaseCCurrentSamples,
    double[] ResidualCurrentSamples,
    double[] PhaseAVoltageSamples,
    double[] PhaseBVoltageSamples,
    double[] PhaseCVoltageSamples,
    double[] ResidualVoltageSamples,
    int SamplesPerCycle,
    int Cycles)
{
    public string NeutralCurrentProvenance => Profile.ExplicitNeutralCurrent
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";

    public string NeutralVoltageProvenance => Profile.ExplicitNeutralVoltage
        ? "explicit-virtual-channel"
        : "calculated-phase-sum";
}

public static class VirtualInjectionGenerator
{
    public static VirtualInjectionFrame Generate(
        VirtualInjectionProfile profile,
        DateTimeOffset timestamp,
        SmvTrustState trust,
        int samplesPerCycle = 80,
        int cycles = 2)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(trust);
        if (samplesPerCycle < 4)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle));
        if (cycles < 1 || cycles > 20)
            throw new ArgumentOutOfRangeException(nameof(cycles));

        profile = profile.Normalize();
        var count = checked(samplesPerCycle * cycles);

        var va = GenerateChannel(profile.PhaseAVoltage, count, samplesPerCycle, phaseChannel: true);
        var vb = GenerateChannel(profile.PhaseBVoltage, count, samplesPerCycle, phaseChannel: true);
        var vc = GenerateChannel(profile.PhaseCVoltage, count, samplesPerCycle, phaseChannel: true);
        var vnExplicit = GenerateChannel(profile.NeutralVoltage, count, samplesPerCycle, phaseChannel: false);
        var ia = GenerateChannel(profile.PhaseACurrent, count, samplesPerCycle, phaseChannel: true);
        var ib = GenerateChannel(profile.PhaseBCurrent, count, samplesPerCycle, phaseChannel: true);
        var ic = GenerateChannel(profile.PhaseCCurrent, count, samplesPerCycle, phaseChannel: true);
        var inExplicit = GenerateChannel(profile.NeutralCurrent, count, samplesPerCycle, phaseChannel: false);

        var residualCurrent = profile.ExplicitNeutralCurrent
            ? inExplicit
            : Sum(ia, ib, ic);
        var residualVoltage = profile.ExplicitNeutralVoltage
            ? vnExplicit
            : Sum(va, vb, vc);

        var phaseAVoltage = FundamentalPhasorEstimator.EstimateRms(va, samplesPerCycle);
        var phaseBVoltage = FundamentalPhasorEstimator.EstimateRms(vb, samplesPerCycle);
        var phaseCVoltage = FundamentalPhasorEstimator.EstimateRms(vc, samplesPerCycle);
        var phaseACurrent = FundamentalPhasorEstimator.EstimateRms(ia, samplesPerCycle);
        var phaseBCurrent = FundamentalPhasorEstimator.EstimateRms(ib, samplesPerCycle);
        var phaseCCurrent = FundamentalPhasorEstimator.EstimateRms(ic, samplesPerCycle);
        var neutralVoltage = profile.ExplicitNeutralVoltage
            ? FundamentalPhasorEstimator.EstimateRms(vnExplicit, samplesPerCycle)
            : Complex.Zero;
        var neutralCurrent = profile.ExplicitNeutralCurrent
            ? FundamentalPhasorEstimator.EstimateRms(inExplicit, samplesPerCycle)
            : Complex.Zero;

        var phasors = new PhasorMeasurementSet(
            phaseACurrent,
            phaseBCurrent,
            phaseCCurrent,
            neutralCurrent,
            phaseAVoltage,
            phaseBVoltage,
            phaseCVoltage,
            neutralVoltage,
            profile.FrequencyHz)
        {
            NeutralCurrentAvailable = profile.ExplicitNeutralCurrent,
            NeutralVoltageAvailable = profile.ExplicitNeutralVoltage
        };

        var measurement = new MeasurementFrame(
            timestamp,
            phasors.PhaseACurrent.Magnitude,
            phasors.PhaseBCurrent.Magnitude,
            phasors.PhaseCCurrent.Magnitude,
            phasors.ResidualCurrent.Magnitude,
            trust,
            phasors);

        return new VirtualInjectionFrame(
            profile,
            measurement,
            ia,
            ib,
            ic,
            residualCurrent,
            va,
            vb,
            vc,
            residualVoltage,
            samplesPerCycle,
            cycles);
    }

    private static double[] GenerateChannel(
        VirtualInjectionChannel channel,
        int count,
        int samplesPerCycle,
        bool phaseChannel)
    {
        if (!channel.Enabled && phaseChannel)
            return new double[count];
        if (!channel.Enabled)
            return new double[count];

        var result = new double[count];
        var peak = channel.Rms * Math.Sqrt(2);
        var phase = VirtualInjectionChannel.NormalizeAngle(channel.AngleDegrees) * Math.PI / 180;
        for (var index = 0; index < count; index++)
        {
            var fundamental = 2 * Math.PI * index / samplesPerCycle;
            // Cosine convention keeps the entered engineering phasor angle equal to
            // the complex angle returned by the single-bin estimator.
            result[index] = peak * Math.Cos(fundamental + phase);
        }
        return result;
    }

    private static double[] Sum(double[] first, double[] second, double[] third)
    {
        var result = new double[first.Length];
        for (var index = 0; index < result.Length; index++)
            result[index] = first[index] + second[index] + third[index];
        return result;
    }
}

public static class VirtualInjectionPresets
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Normal balanced",
        "A-G fault",
        "B-G fault",
        "C-G fault",
        "A-B fault",
        "A-B-G fault",
        "Three-phase fault",
        "27 undervoltage",
        "59 overvoltage",
        "59N residual voltage",
        "67P forward",
        "67P reverse",
        "67N forward",
        "67N reverse"
    ];

    public static VirtualInjectionProfile Create(string name, double frequencyHz = 50)
        => name switch
        {
            "Normal balanced" => Balanced(name, frequencyHz, 63.5, 1),
            "A-G fault" => GroundFault(name, frequencyHz, 0, 15, 8.4, 45),
            "B-G fault" => GroundFault(name, frequencyHz, 1, 15, 8.4, -75),
            "C-G fault" => GroundFault(name, frequencyHz, 2, 15, 8.4, 165),
            "A-B fault" => PhaseFault(name, frequencyHz, ground: false),
            "A-B-G fault" => PhaseFault(name, frequencyHz, ground: true),
            "Three-phase fault" => Balanced(name, frequencyHz, 25, 8.4),
            "27 undervoltage" => Balanced(name, frequencyHz, 40, 1),
            "59 overvoltage" => Balanced(name, frequencyHz, 125, 1),
            "59N residual voltage" => WithExplicitResidualVoltage(Balanced(name, frequencyHz, 63.5, 1), 12, 0),
            "67P forward" => DirectionalPhase(name, frequencyHz, 45),
            "67P reverse" => DirectionalPhase(name, frequencyHz, -135),
            "67N forward" => DirectionalEarth(name, frequencyHz, -45),
            "67N reverse" => DirectionalEarth(name, frequencyHz, 135),
            _ => throw new ArgumentException($"Unknown virtual injection preset '{name}'.", nameof(name))
        };

    private static VirtualInjectionProfile Balanced(
        string name,
        double frequency,
        double voltage,
        double current)
        => new VirtualInjectionProfile(
            name,
            frequency,
            On(voltage, 0),
            On(voltage, -120),
            On(voltage, 120),
            Off(),
            On(current, 0),
            On(current, -120),
            On(current, 120),
            Off()).Normalize();

    private static VirtualInjectionProfile GroundFault(
        string name,
        double frequency,
        int phase,
        double faultVoltage,
        double faultCurrent,
        double faultCurrentAngle)
    {
        var profile = Balanced(name, frequency, 63.5, 1);
        return phase switch
        {
            0 => profile with
            {
                PhaseAVoltage = On(faultVoltage, 0),
                PhaseACurrent = On(faultCurrent, faultCurrentAngle)
            },
            1 => profile with
            {
                PhaseBVoltage = On(faultVoltage, -120),
                PhaseBCurrent = On(faultCurrent, faultCurrentAngle)
            },
            _ => profile with
            {
                PhaseCVoltage = On(faultVoltage, 120),
                PhaseCCurrent = On(faultCurrent, faultCurrentAngle)
            }
        };
    }

    private static VirtualInjectionProfile PhaseFault(string name, double frequency, bool ground)
    {
        var profile = Balanced(name, frequency, 63.5, 1) with
        {
            PhaseAVoltage = On(25, 0),
            PhaseBVoltage = On(25, 180),
            PhaseACurrent = On(8.4, 30),
            PhaseBCurrent = On(8.4, -150)
        };
        return ground
            ? profile with { NeutralCurrent = On(4.0, -45), NeutralVoltage = On(12, 0) }
            : profile;
    }

    private static VirtualInjectionProfile DirectionalPhase(string name, double frequency, double currentAngle)
        => Balanced(name, frequency, 63.5, 2) with
        {
            PhaseACurrent = On(2, currentAngle),
            PhaseBCurrent = On(2, currentAngle - 120),
            PhaseCCurrent = On(2, currentAngle + 120)
        };

    private static VirtualInjectionProfile DirectionalEarth(string name, double frequency, double residualCurrentAngle)
        => Balanced(name, frequency, 63.5, 1) with
        {
            NeutralCurrent = On(0.6, residualCurrentAngle),
            NeutralVoltage = On(15, 0)
        };

    private static VirtualInjectionProfile WithExplicitResidualVoltage(
        VirtualInjectionProfile profile,
        double voltage,
        double angle)
        => profile with { NeutralVoltage = On(voltage, angle) };

    private static VirtualInjectionChannel On(double rms, double angle)
        => new(true, rms, angle);

    private static VirtualInjectionChannel Off()
        => new(false, 0, 0);
}
