using System.Globalization;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public enum CtObservabilityStatus
{
    Ideal,
    Nonlinear,
    Saturated
}

public sealed record CtObservabilityWaveformEvidence(
    VirtualInjectionCurrentReference Ideal,
    IReadOnlyList<double> PhaseASecondary,
    IReadOnlyList<double> PhaseBSecondary,
    IReadOnlyList<double> PhaseCSecondary,
    IReadOnlyList<double> NeutralOrResidualSecondary,
    int NominalSamplesPerCycle);

public sealed record CtChannelObservation(
    string Channel,
    string State,
    bool Available,
    bool Enabled,
    bool Saturated,
    double InitialFluxPerUnit,
    double FinalFluxPerUnit,
    double MaximumAbsoluteFluxPerUnit,
    double IdealRmsA,
    double SecondaryRmsA,
    double RmsMagnitudeErrorPercent,
    double FundamentalIdealRmsA,
    double FundamentalSecondaryRmsA,
    double FundamentalMagnitudeErrorPercent,
    double PhaseDisplacementDegrees,
    double WaveformErrorPercent,
    double MaximumExcitationCurrentA,
    double MaximumSecondaryVoltageV,
    double FirstSaturationMilliseconds,
    long FirstSaturationAbsoluteSample,
    long InitialProcessedSampleCount,
    long FinalProcessedSampleCount);

public sealed record CtObservabilitySnapshot(
    CtObservabilityStatus Status,
    string BadgeText,
    string StatusText,
    string SettingsSummary,
    string RuntimeSummary,
    string EventSummary,
    string EngineeringBoundary,
    bool IsEnabled,
    bool IsRunning,
    bool CanResetState,
    bool CanDemagnetize,
    bool CanRestartEvent,
    IReadOnlyList<CtChannelObservation> Channels);

/// <summary>
/// Converts CT configuration, runtime state, waveform evidence and per-frame diagnostics
/// into a stable, framework-neutral presentation contract for public desktop observability.
/// </summary>
public static class CtObservabilityProjector
{
    public const string EngineeringBoundary =
        "Engineering-study equivalent-circuit model · not an IEC 61869 type test or manufacturer-calibrated magnetic digital twin.";

    public static CtObservabilitySnapshot Project(
        VirtualInjectionProfile profile,
        CtSaturationFrameDiagnostics diagnostics,
        CtSaturationRuntimeState runtimeState,
        long sourceSampleIndex,
        bool isRunning)
        => Project(
            profile,
            diagnostics,
            runtimeState,
            sourceSampleIndex,
            evidence: null,
            isRunning);

    public static CtObservabilitySnapshot Project(
        VirtualInjectionProfile profile,
        CtSaturationFrameDiagnostics diagnostics,
        CtSaturationRuntimeState runtimeState,
        long sourceSampleIndex,
        CtObservabilityWaveformEvidence? evidence,
        bool isRunning)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(runtimeState);
        if (sourceSampleIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceSampleIndex));

        profile = profile.Normalize();
        var settings = profile.CurrentTransformer;
        var sampleRateHz = evidence?.Ideal.SampleRateHz ?? DeterministicLabScenario.SampleRateHz;
        var eventSummary = BuildEventSummary(profile, sourceSampleIndex, sampleRateHz, isRunning);
        if (!settings.Enabled)
        {
            return new CtObservabilitySnapshot(
                CtObservabilityStatus.Ideal,
                "CT IDEAL",
                "Nonlinear CT stage disabled; relay current equals the configured ideal source.",
                "CT nonlinear model disabled",
                isRunning
                    ? $"RUNNING · source sample {sourceSampleIndex.ToString("N0", CultureInfo.InvariantCulture)} · no magnetic state"
                    : "STOPPED · no magnetic state",
                eventSummary,
                EngineeringBoundary,
                false,
                isRunning,
                false,
                false,
                false,
                BuildChannels(profile, diagnostics, evidence));
        }

        var status = diagnostics.Saturated
            ? CtObservabilityStatus.Saturated
            : CtObservabilityStatus.Nonlinear;
        var badge = status == CtObservabilityStatus.Saturated ? "CT SAT" : "CT NONLINEAR";
        var statusText = status == CtObservabilityStatus.Saturated
            ? $"Saturation visible in {diagnostics.SaturatedChannelCount} current channel(s); relay-side RMS and waveform shape are distorted."
            : diagnostics.StateWasCarried
                ? "CT remains below knee in this evidence window while magnetic history is carried from earlier samples."
                : "Nonlinear CT model is active; the current evidence window remains below knee flux.";
        var settingsSummary = FormattableString.Invariant(
            $"{settings.RatedSecondaryCurrentA:0.###} A secondary · Vk {settings.KneePointVoltageRms:0.###} V RMS · Rct {settings.SecondaryWindingResistanceOhm:0.###} Ω · burden {settings.BurdenResistanceOhm:0.###} Ω + {settings.BurdenInductanceMilliHenries:0.###} mH · rem {settings.RemanencePercent:+0.##;-0.##;0}%");
        var runtimeSummary = isRunning
            ? FormattableString.Invariant(
                $"RUNNING · source sample {sourceSampleIndex:N0} · committed CT history {runtimeState.MaximumProcessedSampleCount:N0} samples")
            : "STOPPED · transient CT history cleared; configured remanence remains armed";

        return new CtObservabilitySnapshot(
            status,
            badge,
            statusText,
            settingsSummary,
            runtimeSummary,
            eventSummary,
            EngineeringBoundary,
            true,
            isRunning,
            isRunning,
            isRunning,
            isRunning,
            BuildChannels(profile, diagnostics, evidence));
    }

    private static IReadOnlyList<CtChannelObservation> BuildChannels(
        VirtualInjectionProfile profile,
        CtSaturationFrameDiagnostics diagnostics,
        CtObservabilityWaveformEvidence? evidence)
    {
        var sourceWindowStartSample = evidence?.Ideal.StartSampleIndex ?? -1;
        return
        [
            Build(
                "IA",
                true,
                diagnostics.PhaseA,
                evidence?.Ideal.PhaseA,
                evidence?.PhaseASecondary,
                evidence?.NominalSamplesPerCycle ?? 0,
                sourceWindowStartSample),
            Build(
                "IB",
                true,
                diagnostics.PhaseB,
                evidence?.Ideal.PhaseB,
                evidence?.PhaseBSecondary,
                evidence?.NominalSamplesPerCycle ?? 0,
                sourceWindowStartSample),
            Build(
                "IC",
                true,
                diagnostics.PhaseC,
                evidence?.Ideal.PhaseC,
                evidence?.PhaseCSecondary,
                evidence?.NominalSamplesPerCycle ?? 0,
                sourceWindowStartSample),
            profile.ExplicitNeutralCurrent
                ? Build(
                    "IN",
                    true,
                    diagnostics.Neutral,
                    evidence?.Ideal.NeutralOrResidual,
                    evidence?.NeutralOrResidualSecondary,
                    evidence?.NominalSamplesPerCycle ?? 0,
                    sourceWindowStartSample)
                : Build("3I0", false, CtSaturationChannelDiagnostics.Disabled, null, null, 0, -1)
        ];
    }

    private static CtChannelObservation Build(
        string channel,
        bool available,
        CtSaturationChannelDiagnostics diagnostics,
        IReadOnlyList<double>? ideal,
        IReadOnlyList<double>? secondary,
        int samplesPerCycle,
        long sourceWindowStartSample)
    {
        var state = !available
            ? "CALCULATED SUM"
            : !diagnostics.Enabled
                ? "IDEAL"
                : diagnostics.Saturated
                    ? "SATURATED"
                    : diagnostics.StateWasCarried
                        ? "BELOW KNEE + HISTORY"
                        : "BELOW KNEE";

        var fundamentalIdeal = double.NaN;
        var fundamentalSecondary = double.NaN;
        var fundamentalMagnitudeError = double.NaN;
        var phaseDisplacement = double.NaN;
        if (available &&
            ideal is not null &&
            secondary is not null &&
            samplesPerCycle >= 4 &&
            ideal.Count >= samplesPerCycle &&
            secondary.Count >= samplesPerCycle)
        {
            var idealPhasor = FundamentalPhasorEstimator.EstimateRms(ideal, samplesPerCycle);
            var secondaryPhasor = FundamentalPhasorEstimator.EstimateRms(secondary, samplesPerCycle);
            fundamentalIdeal = idealPhasor.Magnitude;
            fundamentalSecondary = secondaryPhasor.Magnitude;
            fundamentalMagnitudeError = fundamentalIdeal > 1e-12
                ? 100 * (fundamentalSecondary - fundamentalIdeal) / fundamentalIdeal
                : 0;
            phaseDisplacement = fundamentalIdeal > 1e-12 && fundamentalSecondary > 1e-12
                ? NormalizeDegrees((secondaryPhasor.Phase - idealPhasor.Phase) * 180 / Math.PI)
                : double.NaN;
        }

        var firstSaturationSourceSample = diagnostics.FirstSaturationAbsoluteSample;
        if (diagnostics.FirstSaturatedSample >= 0 && sourceWindowStartSample >= 0)
        {
            firstSaturationSourceSample = checked(
                sourceWindowStartSample + diagnostics.FirstSaturatedSample);
        }

        return new CtChannelObservation(
            channel,
            state,
            available,
            diagnostics.Enabled,
            diagnostics.Saturated,
            diagnostics.InitialFluxPerUnit,
            diagnostics.FinalFluxPerUnit,
            diagnostics.MaximumAbsoluteFluxPerUnit,
            diagnostics.IdealRmsA,
            diagnostics.SecondaryRmsA,
            diagnostics.RmsMagnitudeErrorPercent,
            fundamentalIdeal,
            fundamentalSecondary,
            fundamentalMagnitudeError,
            phaseDisplacement,
            diagnostics.WaveformErrorPercent,
            diagnostics.MaximumExcitationCurrentA,
            diagnostics.MaximumSecondaryVoltageV,
            diagnostics.FirstSaturationMilliseconds,
            firstSaturationSourceSample,
            diagnostics.InitialProcessedSampleCount,
            diagnostics.FinalProcessedSampleCount);
    }

    private static string BuildEventSummary(
        VirtualInjectionProfile profile,
        long sourceSampleIndex,
        double sampleRateHz,
        bool isRunning)
    {
        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
            sampleRateHz = DeterministicLabScenario.SampleRateHz;
        var elapsedSeconds = sourceSampleIndex / sampleRateHz;
        var dcChannels = new[]
        {
            profile.PhaseACurrent,
            profile.PhaseBCurrent,
            profile.PhaseCCurrent,
            profile.NeutralCurrent
        }
        .Where(channel => channel.Enabled && Math.Abs(channel.DcOffsetPercent) > 1e-9)
        .ToArray();

        string dcText;
        if (dcChannels.Length == 0)
        {
            dcText = "DC transient: none configured";
        }
        else if (!isRunning)
        {
            var armed = dcChannels.Max(channel => Math.Abs(channel.DcOffsetPercent));
            dcText = FormattableString.Invariant($"DC transient: armed · {armed:0.##}% of sinusoidal peak at event start");
        }
        else
        {
            var remainingPercentOfPeak = dcChannels.Max(channel =>
                Math.Abs(channel.DcOffsetPercent) *
                Math.Exp(-elapsedSeconds / (channel.DcTimeConstantMilliseconds / 1_000d)));
            dcText = remainingPercentOfPeak < 0.1
                ? "DC transient: decayed · <0.1% of sinusoidal peak"
                : FormattableString.Invariant($"DC transient: active · {remainingPercentOfPeak:0.##}% of sinusoidal peak");
        }

        return isRunning
            ? FormattableString.Invariant($"EVENT +{elapsedSeconds:0.000} s · {dcText}")
            : $"EVENT ARMED · {dcText}";
    }

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle <= -180) angle += 360;
        return angle;
    }
}
