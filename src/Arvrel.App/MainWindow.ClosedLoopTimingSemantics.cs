using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private long _firstAnyPickupSourceRunId = -1;
    private string? _firstAnyPickupSource;

    private void ObserveFirstAnyPickupSource(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        if (testSet.TestRunId <= 0 ||
            _firstAnyPickupSourceRunId == testSet.TestRunId ||
            testSet.RelayPickupDetectedMicroseconds is not { } relayPickupUs ||
            testSet.ObservedMicroseconds is not { } observedUs)
        {
            return;
        }

        // ClosedLoopTimer_Tick advances one relay quantum at a time. The first
        // snapshot in which RelayPickupDetectedMicroseconds becomes available is
        // therefore the same protection frame that requested the generic BO2 pickup.
        // Do not infer this later from the trip snapshot because other elements can
        // pick up during BO2 operate/bounce/BI-deglitch time.
        if (observedUs != relayPickupUs)
            return;

        _firstAnyPickupSourceRunId = testSet.TestRunId;
        _firstAnyPickupSource = DescribeActivePickupSources(protection);
    }

    private string FirstAnyPickupSourceFor(VirtualTestSetTimingSnapshot testSet)
        => _firstAnyPickupSourceRunId == testSet.TestRunId &&
           !string.IsNullOrWhiteSpace(_firstAnyPickupSource)
            ? _firstAnyPickupSource!
            : "unresolved";

    private static string DescribeActivePickupSources(ProtectionSnapshot snapshot)
    {
        var sources = new List<string>();
        if (snapshot.Phase50.Pickup) sources.Add("50P-1");
        if (snapshot.Phase51.Pickup) sources.Add("51P");
        if (snapshot.Earth50.Pickup) sources.Add("50N");
        if (snapshot.Earth51.Pickup) sources.Add("51N");

        var feeder = snapshot.Feeder;
        if (feeder.DirectionalPhase67.Pickup) sources.Add("67P");
        if (feeder.DirectionalEarth67N.Pickup) sources.Add("67N");
        if (feeder.Undervoltage27.Pickup) sources.Add("27");
        if (feeder.Overvoltage59.Pickup) sources.Add("59");
        if (feeder.ResidualOvervoltage59N.Pickup) sources.Add("59N");

        return sources.Count == 0 ? "unknown pickup" : string.Join(" + ", sources);
    }

    private string BuildClosedLoopTimingRail(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        var parts = new List<string> { "T0" };
        if (testSet.PickupTime is { } bi2)
            parts.Add($"BI2 ANY [{FirstAnyPickupSourceFor(testSet)}] {bi2.TotalMilliseconds:0.000}");

        var operation = RelayOperationTimingCorrelator.Correlate(testSet, protection);
        if (operation is { } timing)
        {
            parts.Add($"{timing.Element} PU {timing.ElementPickupFromStart.TotalMilliseconds:0.000}");
            var trip = timing.LiveTripRequestFromStart ?? timing.OperationRecordTripFromStart;
            parts.Add($"{timing.Element} TRIP {trip.TotalMilliseconds:0.000}");
        }

        if (testSet.TripTime is { } bi1)
            parts.Add($"BI1 {bi1.TotalMilliseconds:0.000} ms");

        return string.Join("  →  ", parts);
    }

    private string BuildClosedLoopTimingDetail(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        var items = new List<string>();
        var operation = RelayOperationTimingCorrelator.Correlate(testSet, protection);
        if (operation is { } timing)
            items.Add($"{timing.Element} P→T {timing.ElementPickupToTrip.TotalMilliseconds:0.000} ms");
        if (testSet.RelayTripToBi1 is { } external)
            items.Add($"relay TRIP→BI1 {external.TotalMilliseconds:0.000} ms");

        if (TryGetTripCaptureFrameOffsetMicroseconds(_closedLoopBench?.TripCapture, out var offsetUs))
            items.Add($"capture frame +{offsetUs} µs after BI1");

        items.Add(testSet.OutputRunning ? "OUTPUT ON" : "OUTPUT OFF");
        return string.Join(" · ", items);
    }

    private static bool TryGetTripCaptureFrameOffsetMicroseconds(
        ClosedLoopTripCapture? capture,
        out long offsetMicroseconds)
    {
        offsetMicroseconds = 0;
        if (capture?.TestSet.TripDetectedMicroseconds is not { } bi1Us ||
            capture.TestSet.ObservedMicroseconds is not { } captureFrameUs)
        {
            return false;
        }

        offsetMicroseconds = Math.Max(0, captureFrameUs - bi1Us);
        return true;
    }
}
