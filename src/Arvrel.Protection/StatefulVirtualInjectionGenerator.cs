using System.Numerics;

namespace Arvrel.Protection;

/// <summary>
/// Continuous-time virtual source used by <see cref="VirtualInjectionRuntime"/>.
/// It advances CT magnetic state only for elapsed runtime samples, then creates a
/// non-committing evidence preview from the resulting state.
/// </summary>
internal static class StatefulVirtualInjectionGenerator
{
    public static CtSaturationRuntimeState AdvanceCurrentTransformer(
        VirtualInjectionProfile profile,
        long startSampleIndex,
        int sampleCount,
        double sampleRateHz,
        CtSaturationRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        if (startSampleIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startSampleIndex));
        if (sampleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));

        profile = profile.Normalize();
        if (sampleCount == 0 || !profile.CurrentTransformer.Enabled)
            return state;

        var iaIdeal = GenerateChannel(profile.PhaseACurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var ibIdeal = GenerateChannel(profile.PhaseBCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var icIdeal = GenerateChannel(profile.PhaseCCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);
        var inIdeal = GenerateChannel(profile.NeutralCurrent, startSampleIndex, sampleCount, sampleRateHz, profile.FrequencyHz);

        var ia = CtSaturationModel.Apply(
            iaIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseA);
        var ib = CtSaturationModel.Apply(
            ibIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseB);
        var ic = CtSaturationModel.Apply(
            icIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseC);
        var neutral = profile.ExplicitNeutralCurrent
            ? CtSaturationModel.Apply(
                inIdeal,
                sampleRateHz,
                profile.FrequencyHz,
                profile.CurrentTransformer,
                state.Neutral)
            : CtSaturationChannelResult.PassThrough(inIdeal, state.Neutral);

        return new CtSaturationRuntimeState(
            ia.FinalState,
            ib.FinalState,
            ic.FinalState,
            neutral.FinalState);
    }

    public static VirtualInjectionFrame GeneratePreview(
        VirtualInjectionProfile profile,
        DateTimeOffset timestamp,
        SmvTrustState trust,
        int samplesPerCycle,
        int cycles,
        double nominalFrequencyHz,
        long startSampleIndex,
        CtSaturationRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(state);
        if (samplesPerCycle < 4)
            throw new ArgumentOutOfRangeException(nameof(samplesPerCycle));
        if (cycles < 1 || cycles > 20)
            throw new ArgumentOutOfRangeException(nameof(cycles));
        if (!double.IsFinite(nominalFrequencyHz) || nominalFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(nominalFrequencyHz));
        if (startSampleIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startSampleIndex));

        profile = profile.Normalize();
        var count = checked(samplesPerCycle * cycles);
        var sampleRateHz = nominalFrequencyHz * samplesPerCycle;

        var va = GenerateChannel(profile.PhaseAVoltage, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var vb = GenerateChannel(profile.PhaseBVoltage, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var vc = GenerateChannel(profile.PhaseCVoltage, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var vnExplicit = GenerateChannel(profile.NeutralVoltage, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var iaIdeal = GenerateChannel(profile.PhaseACurrent, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var ibIdeal = GenerateChannel(profile.PhaseBCurrent, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var icIdeal = GenerateChannel(profile.PhaseCCurrent, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);
        var inIdeal = GenerateChannel(profile.NeutralCurrent, startSampleIndex, count, sampleRateHz, profile.FrequencyHz);

        var iaCt = CtSaturationModel.Apply(
            iaIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseA);
        var ibCt = CtSaturationModel.Apply(
            ibIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseB);
        var icCt = CtSaturationModel.Apply(
            icIdeal,
            sampleRateHz,
            profile.FrequencyHz,
            profile.CurrentTransformer,
            state.PhaseC);
        var inCt = profile.ExplicitNeutralCurrent
            ? CtSaturationModel.Apply(
                inIdeal,
                sampleRateHz,
                profile.FrequencyHz,
                profile.CurrentTransformer,
                state.Neutral)
            : CtSaturationChannelResult.PassThrough(inIdeal, state.Neutral);

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
        long startSampleIndex,
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
            var absoluteSampleIndex = checked(startSampleIndex + index);
            var timeSeconds = absoluteSampleIndex / sampleRateHz;
            var angle = 2 * Math.PI * injectedFrequencyHz * timeSeconds;
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
