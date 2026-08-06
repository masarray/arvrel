using Arvrel.Application.Laboratory;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

/// <summary>
/// One immutable presentation projection for both the deterministic internal
/// laboratory and external process-bus sources. UI controls consume this model;
/// they never inspect private ViewModel fields or instantiate protection logic.
/// </summary>
public sealed record DesktopPresentationSnapshot(
    string SourceMode,
    string SourceIdentity,
    MeasurementFrame Measurement,
    ProtectionSnapshot Protection,
    ScenarioWaveform Waveform,
    long SampleCounter,
    string SourceSummary,
    string TrustSummary,
    string MappingSummary,
    IReadOnlyList<string> Diagnostics)
{
    public static DesktopPresentationSnapshot FromInternal(InternalLabTick tick, long sampleCounter)
        => new(
            "INTERNAL VIRTUAL TEST SET",
            tick.Scenario.Profile.Name,
            tick.Scenario.Measurement,
            tick.Protection,
            tick.Scenario.Waveform,
            sampleCounter,
            tick.Scenario.OutputState,
            $"{tick.Protection.SmvTrust.Code} · {tick.Protection.SmvTrust.Detail}",
            "Portable deterministic 4I + 4V profile",
            Array.Empty<string>());

    public static DesktopPresentationSnapshot FromProcessBus(SmvRuntimeSnapshot snapshot)
    {
        var waveform = snapshot.Waveform;
        var projectedWaveform = new ScenarioWaveform(
            waveform.PhaseA.ToArray(),
            waveform.PhaseB.ToArray(),
            waveform.PhaseC.ToArray(),
            waveform.Residual.ToArray(),
            waveform.FrequencyHz,
            waveform.SamplesPerCycle,
            waveform.FrequencyHz * waveform.SamplesPerCycle);

        return new DesktopPresentationSnapshot(
            snapshot.IsReplaySource() ? "PCAP / PCAPNG REPLAY" : "LIVE PROCESS BUS",
            snapshot.Stream.DisplayName,
            snapshot.Measurement,
            snapshot.Protection,
            projectedWaveform,
            snapshot.SampleCounter,
            snapshot.SourceSummary,
            snapshot.TrustSummary,
            snapshot.MappingSummary,
            snapshot.Diagnostics);
    }
}

internal static class SmvRuntimeSnapshotPresentationExtensions
{
    internal static bool IsReplaySource(this SmvRuntimeSnapshot snapshot)
        => snapshot.SourceSummary.Contains("replay", StringComparison.OrdinalIgnoreCase);
}
