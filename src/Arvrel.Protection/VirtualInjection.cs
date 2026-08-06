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
    /// <summary>
    /// Decaying unidirectional component expressed as a percentage of sinusoidal
    /// peak. This represents fault-current asymmetry at the selected inception angle.
    /// </summary>
    public double DcOffsetPercent { get; init; }

    public double DcTimeConstantMilliseconds { get; init; } = 60;

    public VirtualInjectionChannel Normalize()
        => this with { AngleDegrees = NormalizeAngle(AngleDegrees) };

    public void Validate(string name)
    {
        if (!double.IsFinite(Rms) || Rms < 0 || Rms > 1_000_000_000)
            throw new ArgumentOutOfRangeException(name, "RMS value must be finite and between 0 and 1e9.");
        if (!double.IsFinite(AngleDegrees))
            throw new ArgumentOutOfRangeException(name, "Angle must be finite.");
        if (!double.IsFinite(DcOffsetPercent) || DcOffsetPercent is < -100 or > 100)
            throw new ArgumentOutOfRangeException(name, "DC offset must be finite and between -100% and 100% of sinusoidal peak.");
        if (!double.IsFinite(DcTimeConstantMilliseconds) || DcTimeConstantMilliseconds is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(name, "DC time constant must be finite and between 1 ms and 10,000 ms.");
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

    public CtSaturationSettings CurrentTransformer { get; init; } = CtSaturationSettings.Disabled;

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
        CurrentTransformer.Validate();
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
            NeutralCurrent = NeutralCurrent.Normalize(),
            CurrentTransformer = CurrentTransformer.Normalize()
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
        var ct = normalized.CurrentTransformer;
        var canonical = ProtectionSettings.CanonicalJoin(
            normalized.Name,
            normalized.FrequencyHz,
            normalized.PhaseAVoltage.Enabled, normalized.PhaseAVoltage.Rms, normalized.PhaseAVoltage.AngleDegrees, normalized.PhaseAVoltage.DcOffsetPercent, normalized.PhaseAVoltage.DcTimeConstantMilliseconds,
            normalized.PhaseBVoltage.Enabled, normalized.PhaseBVoltage.Rms, normalized.PhaseBVoltage.AngleDegrees, normalized.PhaseBVoltage.DcOffsetPercent, normalized.PhaseBVoltage.DcTimeConstantMilliseconds,
            normalized.PhaseCVoltage.Enabled, normalized.PhaseCVoltage.Rms, normalized.PhaseCVoltage.AngleDegrees, normalized.PhaseCVoltage.DcOffsetPercent, normalized.PhaseCVoltage.DcTimeConstantMilliseconds,
            normalized.NeutralVoltage.Enabled, normalized.NeutralVoltage.Rms, normalized.NeutralVoltage.AngleDegrees, normalized.NeutralVoltage.DcOffsetPercent, normalized.NeutralVoltage.DcTimeConstantMilliseconds,
            normalized.PhaseACurrent.Enabled, normalized.PhaseACurrent.Rms, normalized.PhaseACurrent.AngleDegrees, normalized.PhaseACurrent.DcOffsetPercent, normalized.PhaseACurrent.DcTimeConstantMilliseconds,
            normalized.PhaseBCurrent.Enabled, normalized.PhaseBCurrent.Rms, normalized.PhaseBCurrent.AngleDegrees, normalized.PhaseBCurrent.DcOffsetPercent, normalized.PhaseBCurrent.DcTimeConstantMilliseconds,
            normalized.PhaseCCurrent.Enabled, normalized.PhaseCCurrent.Rms, normalized.PhaseCCurrent.AngleDegrees, normalized.PhaseCCurrent.DcOffsetPercent, normalized.PhaseCCurrent.DcTimeConstantMilliseconds,
            normalized.NeutralCurrent.Enabled, normalized.NeutralCurrent.Rms, normalized.NeutralCurrent.AngleDegrees, normalized.NeutralCurrent.DcOffsetPercent, normalized.NeutralCurrent.DcTimeConstantMilliseconds,
            ct.Enabled,
            ct.RatedSecondaryCurrentA,
            ct.KneePointVoltageRms,
            ct.SecondaryWindingResistanceOhm,
            ct.BurdenResistanceOhm,
            ct.BurdenInductanceMilliHenries,
            ct.ExcitationCurrentAtKneeA,
            ct.ExcitationCurrentAtTwiceKneeA,
            ct.SaturationExponent,
            ct.RemanencePercent,
            ct.MaximumFluxPerUnit,
            ct.MaximumExcitationCurrentA);
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
    int Cycles,
    double NominalFrequencyHz,
    double SampleRateHz)
{
    public CtSaturationFrameDiagnostics CtSaturation { get; init; } = CtSaturationFrameDiagnostics.Disabled;

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
        int cycles = 2,
        double nominalFrequencyHz = 50)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(trust);
        if (samplesPerCycle < 4)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle));
        if (cycles < 1 || cycles > 20)
            throw new ArgumentOutOfRangeException(nameof(cycles));
        if (!double.IsFinite(nominalFrequencyHz) || nominalFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(nominalFrequencyHz));

        profile = profile.Normalize();
        var count = checked(samplesPerCycle * cycles);
        var sampleRateHz = nominalFrequencyHz * samplesPerCycle;

        var va = GenerateChannel(profile.PhaseAVoltage, count, sampleRateHz, profile.FrequencyHz);
        var vb = GenerateChannel(profile.PhaseBVoltage, count, sampleRateHz, profile.FrequencyHz);
        var vc = GenerateChannel(profile.PhaseCVoltage, count, sampleRateHz, profile.FrequencyHz);
        var vnExplicit = GenerateChannel(profile.NeutralVoltage, count, sampleRateHz, profile.FrequencyHz);
        var iaIdeal = GenerateChannel(profile.PhaseACurrent, count, sampleRateHz, profile.FrequencyHz);
        var ibIdeal = GenerateChannel(profile.PhaseBCurrent, count, sampleRateHz, profile.FrequencyHz);
        var icIdeal = GenerateChannel(profile.PhaseCCurrent, count, sampleRateHz, profile.FrequencyHz);
        var inIdeal = GenerateChannel(profile.NeutralCurrent, count, sampleRateHz, profile.FrequencyHz);

        var iaCt = CtSaturationModel.Apply(iaIdeal, sampleRateHz, profile.FrequencyHz, profile.CurrentTransformer);
        var ibCt = CtSaturationModel.Apply(ibIdeal, sampleRateHz, profile.FrequencyHz, profile.CurrentTransformer);
        var icCt = CtSaturationModel.Apply(icIdeal, sampleRateHz, profile.FrequencyHz, profile.CurrentTransformer);
        var inCt = profile.ExplicitNeutralCurrent
            ? CtSaturationModel.Apply(inIdeal, sampleRateHz, profile.FrequencyHz, profile.CurrentTransformer)
            : CtSaturationChannelResult.PassThrough(inIdeal);

        var ia = iaCt.SecondaryCurrentA;
        var ib = ibCt.SecondaryCurrentA;
        var ic = icCt.SecondaryCurrentA;
        var inExplicit = inCt.SecondaryCurrentA;
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
            cycles,
            nominalFrequencyHz,
            sampleRateHz)
        {
            CtSaturation = new CtSaturationFrameDiagnostics(
                iaCt.Diagnostics,
                ibCt.Diagnostics,
                icCt.Diagnostics,
                inCt.Diagnostics)
        };
    }

    private static double[] GenerateChannel(
        VirtualInjectionChannel channel,
        int count,
        double sampleRateHz,
        double injectedFrequencyHz)
    {
        if (!channel.Enabled)
            return new double[count];

        var result = new double[count];
        var peak = channel.Rms * Math.Sqrt(2);
        var phase = VirtualInjectionChannel.NormalizeAngle(channel.AngleDegrees) * Math.PI / 180;
        var dcFraction = channel.DcOffsetPercent / 100d;
        var dcTimeConstantSeconds = channel.DcTimeConstantMilliseconds / 1_000d;
        for (var index = 0; index < count; index++)
        {
            var timeSeconds = index / sampleRateHz;
            var angle = 2 * Math.PI * injectedFrequencyHz * timeSeconds;
            // Cosine convention keeps the entered engineering angle aligned with
            // the estimator at nominal frequency. Off-nominal signals deliberately
            // expose the response of the fixed nominal-frequency DFT window.
            var sinusoidal = peak * Math.Cos(angle + phase);
            var decayingDc = peak * dcFraction * Math.Exp(-timeSeconds / dcTimeConstantSeconds);
            result[index] = sinusoidal + decayingDc;
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
        "CT saturation - A-G asymmetrical",
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
            "CT saturation - A-G asymmetrical" => CtSaturationGroundFault(name, frequencyHz),
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

    private static VirtualInjectionProfile CtSaturationGroundFault(string name, double frequency)
    {
        var profile = Balanced(name, frequency, 63.5, 1) with
        {
            PhaseAVoltage = On(12, 0),
            PhaseACurrent = OnAsymmetrical(20, 0, 100, 60),
            CurrentTransformer = new CtSaturationSettings
            {
                Enabled = true,
                RatedSecondaryCurrentA = 5,
                KneePointVoltageRms = 70,
                SecondaryWindingResistanceOhm = 1,
                BurdenResistanceOhm = 3.5,
                BurdenInductanceMilliHenries = 0.2,
                ExcitationCurrentAtKneeA = 0.03,
                ExcitationCurrentAtTwiceKneeA = 50,
                SaturationExponent = 2,
                RemanencePercent = 60,
                MaximumFluxPerUnit = 2.5,
                MaximumExcitationCurrentA = 200
            }
        };
        return profile.Normalize();
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

    private static VirtualInjectionChannel OnAsymmetrical(
        double rms,
        double angle,
        double dcOffsetPercent,
        double dcTimeConstantMilliseconds)
        => new VirtualInjectionChannel(true, rms, angle)
        {
            DcOffsetPercent = dcOffsetPercent,
            DcTimeConstantMilliseconds = dcTimeConstantMilliseconds
        };

    private static VirtualInjectionChannel Off()
        => new(false, 0, 0);
}