using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Executes one relay RESET command as a deterministic equipment transaction.
///
/// The virtual source and TESTSET are separate equipment. When the source is already
/// OFF, a human operator would normally press RESET only after relay acquisition has
/// continued to observe the de-energized secondary circuit. The desktop simulation
/// can otherwise be frozen at the exact BI1 auto-stop edge, leaving a causal DFT
/// window full of pre-stop fault samples. This transaction advances that source-off
/// acquisition state first, clears the relay latch/timers once, then advances the
/// modeled BO/contact/wire/BI release path until the feedback is physically idle.
///
/// Trip capture and completed TESTSET timing evidence are intentionally preserved.
/// </summary>
public static class ClosedLoopRelayResetTransaction
{
    public static readonly TimeSpan DefaultSettleTimeout = TimeSpan.FromMilliseconds(100);

    public static ClosedLoopRelayResetResult Execute(
        ClosedLoopVirtualTestBench bench,
        ResearchProtectionEngine relay,
        TimeSpan? settleTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(bench);
        ArgumentNullException.ThrowIfNull(relay);

        var timeout = settleTimeout ?? DefaultSettleTimeout;
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(2))
            throw new ArgumentOutOfRangeException(nameof(settleTimeout));

        var elapsed = TimeSpan.Zero;
        var step = bench.Advance(TimeSpan.Zero);
        var sourceWasRunning = step.TestSet.OutputRunning;

        if (!sourceWasRunning)
        {
            // Let the relay acquisition/filter path observe the already de-energized
            // secondary circuit before clearing the latch. This prevents a reset at a
            // frozen trip frame from immediately seeing stale fault-window pickup.
            while (HasAnyPickup(step.Protection) && elapsed < timeout)
                step = AdvanceOneQuantum(bench, ref elapsed);
        }

        relay.Reset();

        // A running source is deliberately not stopped by a relay RESET command.
        // Advance once so the reset is observable; if the applied quantities still
        // demand pickup/trip, the result correctly remains not ready to re-arm.
        if (sourceWasRunning)
        {
            if (elapsed < timeout)
                step = AdvanceOneQuantum(bench, ref elapsed);
            var runningReady = IsReadyToRearm(step);
            return new ClosedLoopRelayResetResult(
                ResetApplied: true,
                ReadyToRearm: runningReady,
                SourceWasRunning: true,
                SimulatedSettleTime: elapsed,
                FinalStep: step,
                Detail: runningReady
                    ? "Relay reset applied while the virtual source remained energized; feedback is currently idle."
                    : "Relay reset applied, but the virtual source remains energized and protection/feedback may still be asserted.");
        }

        while (!IsReadyToRearm(step) && elapsed < timeout)
            step = AdvanceOneQuantum(bench, ref elapsed);

        var ready = IsReadyToRearm(step);
        return new ClosedLoopRelayResetResult(
            ResetApplied: true,
            ReadyToRearm: ready,
            SourceWasRunning: false,
            SimulatedSettleTime: elapsed,
            FinalStep: step,
            Detail: ready
                ? "Relay latch/timers cleared and BO1/BO2 plus TESTSET BI1/BI2 released through the modeled feedback path."
                : BuildTimeoutDetail(step, timeout));
    }

    private static ClosedLoopVirtualTestBenchStep AdvanceOneQuantum(
        ClosedLoopVirtualTestBench bench,
        ref TimeSpan elapsed)
    {
        var step = bench.Advance(ClosedLoopVirtualTestBench.SimulationQuantum);
        elapsed += ClosedLoopVirtualTestBench.SimulationQuantum;
        return step;
    }

    private static bool IsReadyToRearm(ClosedLoopVirtualTestBenchStep step)
    {
        var testSet = step.TestSet;
        return !testSet.OutputRunning &&
               !step.Protection.TripLatched &&
               !HasAnyPickup(step.Protection) &&
               !testSet.PickupContactRaw &&
               !testSet.TripContactRaw &&
               !testSet.PickupInput &&
               !testSet.TripInput;
    }

    private static bool HasAnyPickup(ProtectionSnapshot snapshot)
    {
        var feeder = snapshot.Feeder;
        return snapshot.Phase50.Pickup ||
               snapshot.Phase51.Pickup ||
               snapshot.Earth50.Pickup ||
               snapshot.Earth51.Pickup ||
               feeder.DirectionalPhase67.Pickup ||
               feeder.DirectionalEarth67N.Pickup ||
               feeder.Undervoltage27.Pickup ||
               feeder.Overvoltage59.Pickup ||
               feeder.ResidualOvervoltage59N.Pickup;
    }

    private static string BuildTimeoutDetail(
        ClosedLoopVirtualTestBenchStep step,
        TimeSpan timeout)
    {
        var testSet = step.TestSet;
        return
            $"Relay reset did not reach the re-arm postcondition within {timeout.TotalMilliseconds:0.###} ms simulated time. " +
            $"tripLatch={(step.Protection.TripLatched ? 1 : 0)}, " +
            $"pickup={(HasAnyPickup(step.Protection) ? 1 : 0)}, " +
            $"BO2={(testSet.PickupContactRaw ? 1 : 0)}, " +
            $"BO1={(testSet.TripContactRaw ? 1 : 0)}, " +
            $"BI2={(testSet.PickupInput ? 1 : 0)}, " +
            $"BI1={(testSet.TripInput ? 1 : 0)}.";
    }
}

public sealed record ClosedLoopRelayResetResult(
    bool ResetApplied,
    bool ReadyToRearm,
    bool SourceWasRunning,
    TimeSpan SimulatedSettleTime,
    ClosedLoopVirtualTestBenchStep FinalStep,
    string Detail);
