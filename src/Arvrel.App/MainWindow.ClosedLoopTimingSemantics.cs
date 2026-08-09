using System.Windows.Controls;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private long _firstAnyPickupSourceRunId = -1;
    private string? _firstAnyPickupSource;
    private long _closedLoopTransitionRunId = -1;
    private bool _closedLoopLastAnyPickup;
    private bool _closedLoopLastTrip;
    private bool _closedLoopLastBlocked;

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
        // therefore the same protection frame that requested generic BO2 pickup.
        // Do not infer this later from the trip snapshot because other elements can
        // pick up during BO2 operate/bounce/BI-deglitch time.
        if (observedUs != relayPickupUs)
            return;

        _firstAnyPickupSourceRunId = testSet.TestRunId;
        _firstAnyPickupSource = DescribeActivePickupSources(protection);
    }

    private void ObserveClosedLoopProtectionTransitions(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        if (testSet.TestRunId != _closedLoopTransitionRunId)
        {
            _closedLoopTransitionRunId = testSet.TestRunId;
            _closedLoopLastAnyPickup = false;
            _closedLoopLastTrip = false;
            _closedLoopLastBlocked = false;
        }

        var anyPickup = HasAnyProtectionPickup(protection);
        if (anyPickup && !_closedLoopLastAnyPickup)
        {
            _pickupPosition = 0.58;
            var source = DescribeActivePickupSources(protection);
            var timing = testSet.RelayPickupTime is { } relayPickup
                ? $"{relayPickup.TotalMilliseconds:0.000} ms · "
                : string.Empty;
            AddEvent("RELAY PU", $"{timing}{source} · ANY BO2 REQUEST");
        }

        if (protection.TripLatched && !_closedLoopLastTrip)
        {
            _tripPosition = 0.76;
            var timing = testSet.RelayTripTime is { } relayTrip
                ? $"{relayTrip.TotalMilliseconds:0.000} ms · "
                : string.Empty;
            AddEvent("RELAY TRIP", $"{timing}{protection.ActiveElement} · BO1 REQUEST");
        }

        if (protection.Blocked && !_closedLoopLastBlocked)
            AddEvent("BLOCK", protection.SmvTrust.Code);

        _closedLoopLastAnyPickup = anyPickup;
        _closedLoopLastTrip = protection.TripLatched;
        _closedLoopLastBlocked = protection.Blocked;

        // Keep legacy presentation state aligned so switching source/workspace does
        // not replay a stale transition after closed-loop ownership ends.
        _lastPickup = anyPickup;
        _lastTrip = protection.TripLatched;
        _lastBlocked = protection.Blocked;
    }

    private string FirstAnyPickupSourceFor(VirtualTestSetTimingSnapshot testSet)
        => _firstAnyPickupSourceRunId == testSet.TestRunId &&
           !string.IsNullOrWhiteSpace(_firstAnyPickupSource)
            ? _firstAnyPickupSource!
            : "unresolved";

    private static bool HasAnyProtectionPickup(ProtectionSnapshot snapshot)
        => ActivePickupSources(snapshot).Count > 0;

    private static string DescribeActivePickupSources(ProtectionSnapshot snapshot)
    {
        var sources = ActivePickupSources(snapshot);
        return sources.Count == 0 ? "unknown pickup" : string.Join(" + ", sources);
    }

    private static List<string> ActivePickupSources(ProtectionSnapshot snapshot)
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
        return sources;
    }

    private string BuildClosedLoopTimingRail(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        var events = new List<(TimeSpan Time, int Order, string Label)>();
        var firstSource = FirstAnyPickupSourceFor(testSet);

        if (testSet.RelayPickupTime is { } relayAnyPickup)
        {
            events.Add((
                relayAnyPickup,
                10,
                $"RELAY ANY PU [{firstSource}] {relayAnyPickup.TotalMilliseconds:0.000} ms"));
        }

        var operation = RelayOperationTimingCorrelator.Correlate(testSet, protection);
        if (operation is { } timing)
        {
            var duplicateOfAnyPickup =
                testSet.RelayPickupTime is { } relayAny &&
                string.Equals(firstSource, timing.Element, StringComparison.Ordinal) &&
                Math.Abs((timing.ElementPickupFromStart - relayAny).TotalMilliseconds) <= 0.250001;

            if (!duplicateOfAnyPickup)
            {
                events.Add((
                    timing.ElementPickupFromStart,
                    20,
                    $"{timing.Element} PU {timing.ElementPickupFromStart.TotalMilliseconds:0.000} ms"));
            }

            var trip = timing.LiveTripRequestFromStart ?? timing.OperationRecordTripFromStart;
            events.Add((
                trip,
                40,
                $"{timing.Element} TRIP {trip.TotalMilliseconds:0.000} ms"));
        }

        if (testSet.PickupTime is { } bi2)
        {
            events.Add((
                bi2,
                30,
                $"TESTSET BI2 ACCEPT {bi2.TotalMilliseconds:0.000} ms"));
        }

        if (testSet.TripTime is { } bi1)
        {
            events.Add((
                bi1,
                50,
                $"TESTSET BI1 ACCEPT {bi1.TotalMilliseconds:0.000} ms"));
        }

        return "T0  →  " + string.Join(
            "  →  ",
            events
                .OrderBy(item => item.Time)
                .ThenBy(item => item.Order)
                .Select(item => item.Label));
    }

    private string BuildClosedLoopTimingDetail(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        var items = new List<string>();

        if (testSet.RelayPickupTime is { } relayPickup &&
            testSet.PickupTime is { } bi2 &&
            bi2 >= relayPickup)
        {
            items.Add($"relay ANY→BI2 {(bi2 - relayPickup).TotalMilliseconds:0.000} ms");
        }

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

    private void RefreshClosedLoopOperatorState(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        // Generic PICKUP on the physical relay faceplate must mean the same ANY
        // protection pickup that drives BO2. This includes feeder functions such as
        // 67/27/59, not only the four legacy OCR elements.
        PickupLed.Fill = HasAnyProtectionPickup(protection) ? WarningBrush : LedOffBrush;

        if (testSet.TimerState != VirtualTestSetTimerState.Completed || testSet.OutputRunning)
            return;

        MarkFrozenElementState(Phase50StateText);
        MarkFrozenElementState(Phase51StateText);
        MarkFrozenElementState(Earth50StateText);
        MarkFrozenElementState(Earth51StateText);

        if (testSet.TripTime is { } trip)
        {
            const string marker = "FROZEN @ TESTSET BI1";
            if (!ProtectionReasonText.Text.Contains(marker, StringComparison.Ordinal))
            {
                ProtectionReasonText.Text =
                    $"{ProtectionReasonText.Text} · {marker} {trip.TotalMilliseconds:0.000} ms · OUTPUT OFF";
            }
        }
    }

    private static void MarkFrozenElementState(TextBlock stateText)
    {
        if (stateText.Text is "TIMING" or "PICKUP")
            stateText.Text += " · FROZEN";
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
