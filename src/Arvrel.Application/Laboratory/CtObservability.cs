using System.Globalization;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public enum CtObservabilityStatus
{
    Ideal,
    Nonlinear,
    Saturated
}

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
    string EngineeringBoundary,
    bool IsEnabled,
    bool IsRunning,
    bool CanResetState,
    bool CanDemagnetize,
    IReadOnlyList<CtChannelObservation> Channels);

/// <summary>
/// Converts CT configuration, runtime state and per-frame diagnostics into a stable,
/// framework-neutral presentation contract for public desktop observability.
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
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(runtimeState);

        profile = profile.Normalize();
        var settings = profile.CurrentTransformer;
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
                EngineeringBoundary,
                false,
                isRunning,
                false,
                false,
                BuildChannels(profile, diagnostics));
        }

        var status = diagnostics.Saturated
            ? CtObservabilityStatus.Saturated
            : CtObservabilityStatus.Nonlinear;
        var badge = status == CtObservabilityStatus.Saturated ? "CT SAT" : "CT NONLINEAR";
        var statusText = status == CtObservabilityStatus.Saturated
            ? $"Saturation visible in {diagnostics.SaturatedChannelCount} current channel(s); relay-side RMS and waveform shape are distorted."
            : diagnostics.StateWasCarried
                ? "Nonlinear CT state is active and magnetic history is being carried across runtime frames."
                : "Nonlinear CT model is active; the current preview remains below knee flux in this window.";
        var settingsSummary = FormattableString.Invariant(
            $"{settings.RatedSecondaryCurrentA:0.###} A secondary · Vk {settings.KneePointVoltageRms:0.###} V · Rct {settings.SecondaryWindingResistanceOhm:0.###} Ω · burden {settings.BurdenResistanceOhm:0.###} Ω + {settings.BurdenInductanceMilliHenries:0.###} mH · rem {settings.RemanencePercent:+0.##;-0.##;0}%");
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
            EngineeringBoundary,
            true,
            isRunning,
            isRunning,
            isRunning,
            BuildChannels(profile, diagnostics));
    }

    private static IReadOnlyList<CtChannelObservation> BuildChannels(
        VirtualInjectionProfile profile,
        CtSaturationFrameDiagnostics diagnostics)
        =>
        [
            Build("IA", true, diagnostics.PhaseA),
            Build("IB", true, diagnostics.PhaseB),
            Build("IC", true, diagnostics.PhaseC),
            profile.ExplicitNeutralCurrent
                ? Build("IN", true, diagnostics.Neutral)
                : Build("3I0", false, CtSaturationChannelDiagnostics.Disabled)
        ];

    private static CtChannelObservation Build(
        string channel,
        bool available,
        CtSaturationChannelDiagnostics diagnostics)
    {
        var state = !available
            ? "CALCULATED SUM"
            : !diagnostics.Enabled
                ? "IDEAL"
                : diagnostics.Saturated
                    ? "SATURATED"
                    : diagnostics.StateWasCarried
                        ? "STATEFUL"
                        : "NONLINEAR";

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
            diagnostics.WaveformErrorPercent,
            diagnostics.MaximumExcitationCurrentA,
            diagnostics.MaximumSecondaryVoltageV,
            diagnostics.FirstSaturationMilliseconds,
            diagnostics.FirstSaturationAbsoluteSample,
            diagnostics.InitialProcessedSampleCount,
            diagnostics.FinalProcessedSampleCount);
    }
}
