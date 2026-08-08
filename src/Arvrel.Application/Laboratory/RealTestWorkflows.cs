using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public enum RealTestFeedback
{
    None,
    Pickup,
    Trip
}

public enum RealTestWorkflowOutcome
{
    Completed,
    FeedbackDetected,
    NoFeedback,
    Cancelled
}

public sealed record RealTestObservation(
    DateTimeOffset Timestamp,
    TimeSpan Elapsed,
    string Stage,
    double CommandedRms,
    double RelayMeasuredRms,
    bool PickupInput,
    bool TripInput);

public sealed record StepRampDefinition(
    string Name,
    VirtualInjectionSignal Signal,
    double StartRms,
    double EndRms,
    double StepRms,
    TimeSpan Dwell,
    RealTestFeedback StopOn = RealTestFeedback.Pickup)
{
    public void Validate()
    {
        ValidateName(Name);
        ValidateRms(StartRms, nameof(StartRms));
        ValidateRms(EndRms, nameof(EndRms));
        ValidatePositive(StepRms, nameof(StepRms));
        ValidateDwell(Dwell, nameof(Dwell));
        var steps = Math.Abs(EndRms - StartRms) / StepRms;
        if (steps > 10_000)
            throw new ArgumentOutOfRangeException(nameof(StepRms), "Ramp is limited to 10,000 commanded levels.");
    }
}

public sealed record PulseRampDefinition(
    string Name,
    VirtualInjectionSignal Signal,
    double BaselineRms,
    double StartRms,
    double EndRms,
    double StepRms,
    TimeSpan PulseDuration,
    TimeSpan ResetDuration,
    RealTestFeedback StopOn = RealTestFeedback.Pickup)
{
    public void Validate()
    {
        ValidateName(Name);
        ValidateRms(BaselineRms, nameof(BaselineRms));
        ValidateRms(StartRms, nameof(StartRms));
        ValidateRms(EndRms, nameof(EndRms));
        ValidatePositive(StepRms, nameof(StepRms));
        ValidateDwell(PulseDuration, nameof(PulseDuration));
        ValidateDwell(ResetDuration, nameof(ResetDuration));
        var steps = Math.Abs(EndRms - StartRms) / StepRms;
        if (steps > 2_000)
            throw new ArgumentOutOfRangeException(nameof(StepRms), "Pulse ramp is limited to 2,000 pulses.");
    }
}

public sealed record PickupDropoutSearchDefinition(
    string Name,
    VirtualInjectionSignal Signal,
    double MinimumRms,
    double MaximumRms,
    double StepRms,
    TimeSpan Dwell)
{
    public void Validate()
    {
        ValidateName(Name);
        ValidateRms(MinimumRms, nameof(MinimumRms));
        ValidateRms(MaximumRms, nameof(MaximumRms));
        if (MaximumRms <= MinimumRms)
            throw new ArgumentOutOfRangeException(nameof(MaximumRms), "Maximum RMS must be greater than minimum RMS.");
        ValidatePositive(StepRms, nameof(StepRms));
        ValidateDwell(Dwell, nameof(Dwell));
        if ((MaximumRms - MinimumRms) / StepRms > 10_000)
            throw new ArgumentOutOfRangeException(nameof(StepRms), "Search is limited to 10,000 commanded levels.");
    }
}

public sealed record RealTestState(
    string Name,
    VirtualInjectionProfile Profile,
    TimeSpan MaximumDuration,
    RealTestFeedback AdvanceOn = RealTestFeedback.None)
{
    public void Validate()
    {
        ValidateName(Name);
        ArgumentNullException.ThrowIfNull(Profile);
        Profile.Validate();
        ValidateDwell(MaximumDuration, nameof(MaximumDuration), maximum: TimeSpan.FromSeconds(30));
    }
}

public sealed record StateSequenceDefinition(
    string Name,
    IReadOnlyList<RealTestState> States)
{
    public void Validate()
    {
        ValidateName(Name);
        ArgumentNullException.ThrowIfNull(States);
        if (States.Count is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(States), "State sequence requires 1 to 32 states.");
        foreach (var state in States)
            state.Validate();
        if (States.Sum(state => state.MaximumDuration.TotalSeconds) > 60)
            throw new ArgumentOutOfRangeException(nameof(States), "State sequence total duration is limited to 60 seconds.");
    }
}

public sealed record StepRampResult(
    string Name,
    RealTestWorkflowOutcome Outcome,
    RealTestFeedback StopOn,
    double? DetectedAtRms,
    TimeSpan? DetectedAtTime,
    IReadOnlyList<RealTestObservation> Observations);

public sealed record PulseRampResult(
    string Name,
    RealTestWorkflowOutcome Outcome,
    RealTestFeedback StopOn,
    int PulsesApplied,
    double? DetectedAtRms,
    TimeSpan? DetectedAtTime,
    IReadOnlyList<RealTestObservation> Observations);

public sealed record PickupDropoutSearchResult(
    string Name,
    RealTestWorkflowOutcome Outcome,
    double? PickupRms,
    double? DropoutRms,
    double? DropoutRatio,
    IReadOnlyList<RealTestObservation> Observations);

public sealed record StateSequenceStateResult(
    string Name,
    TimeSpan StartedAt,
    TimeSpan EndedAt,
    string ExitReason,
    bool PickupObserved,
    bool TripObserved);

public sealed record StateSequenceResult(
    string Name,
    RealTestWorkflowOutcome Outcome,
    IReadOnlyList<StateSequenceStateResult> States,
    IReadOnlyList<RealTestObservation> Observations);

/// <summary>
/// Deterministic test-set workflow engine built strictly on top of the P0 closed-loop
/// backplane. Decisions are made from TESTSET BI states only. ProtectionSnapshot pickup
/// and trip internals are deliberately not used as workflow triggers.
/// </summary>
public sealed class RealTestWorkflowEngine
{
    private static readonly TimeSpan InitialCoherence = TimeSpan.FromSeconds(1 / DeterministicLabScenario.Frequency);
    private readonly DeterministicLabScenario _source;
    private readonly ClosedLoopVirtualTestBench _bench;

    public RealTestWorkflowEngine(DeterministicLabScenario source, ClosedLoopVirtualTestBench bench)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _bench = bench ?? throw new ArgumentNullException(nameof(bench));
    }

    public StepRampResult RunRamp(StepRampDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        var observations = new List<RealTestObservation>();
        var levels = EnumerateLevels(definition.StartRms, definition.EndRms, definition.StepRms).ToArray();
        var originalAutoStop = _bench.AutoStopOnTrip;
        try
        {
            var initial = WithSignalRms(_source.ActiveProfile, definition.Signal, levels[0], definition.Name);
            var prime = Prepare(initial);
            var startedAt = prime.RelayMeasurement.Timestamp;

            foreach (var level in levels)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new StepRampResult(definition.Name, RealTestWorkflowOutcome.Cancelled, definition.StopOn, null, null, observations);

                _source.ApplyContinuousProfile(WithSignalRms(_source.ActiveProfile, definition.Signal, level, definition.Name));
                var result = AdvanceFor(definition.Dwell, cancellationToken);
                observations.Add(Observe(result, startedAt, $"RAMP {level:0.###}", level, definition.Signal));

                if (FeedbackAsserted(result.TestSet, definition.StopOn))
                {
                    return new StepRampResult(
                        definition.Name,
                        RealTestWorkflowOutcome.FeedbackDetected,
                        definition.StopOn,
                        level,
                        result.RelayMeasurement.Timestamp - startedAt,
                        observations);
                }
            }

            return new StepRampResult(
                definition.Name,
                definition.StopOn == RealTestFeedback.None ? RealTestWorkflowOutcome.Completed : RealTestWorkflowOutcome.NoFeedback,
                definition.StopOn,
                null,
                null,
                observations);
        }
        finally
        {
            _bench.StopInjection();
            _bench.AutoStopOnTrip = originalAutoStop;
        }
    }

    public PulseRampResult RunPulseRamp(PulseRampDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        var observations = new List<RealTestObservation>();
        var originalAutoStop = _bench.AutoStopOnTrip;
        var pulses = 0;
        try
        {
            var baseline = WithSignalRms(_source.ActiveProfile, definition.Signal, definition.BaselineRms, definition.Name);
            var prime = Prepare(baseline);
            var startedAt = prime.RelayMeasurement.Timestamp;

            foreach (var level in EnumerateLevels(definition.StartRms, definition.EndRms, definition.StepRms))
            {
                if (cancellationToken.IsCancellationRequested)
                    return new PulseRampResult(definition.Name, RealTestWorkflowOutcome.Cancelled, definition.StopOn, pulses, null, null, observations);

                pulses++;
                _source.ApplyContinuousProfile(WithSignalRms(_source.ActiveProfile, definition.Signal, level, definition.Name));
                var pulse = AdvanceFor(definition.PulseDuration, cancellationToken);
                observations.Add(Observe(pulse, startedAt, $"PULSE {pulses}", level, definition.Signal));
                if (FeedbackAsserted(pulse.TestSet, definition.StopOn))
                {
                    return new PulseRampResult(
                        definition.Name,
                        RealTestWorkflowOutcome.FeedbackDetected,
                        definition.StopOn,
                        pulses,
                        level,
                        pulse.RelayMeasurement.Timestamp - startedAt,
                        observations);
                }

                _source.ApplyContinuousProfile(WithSignalRms(_source.ActiveProfile, definition.Signal, definition.BaselineRms, definition.Name));
                var reset = AdvanceFor(definition.ResetDuration, cancellationToken);
                observations.Add(Observe(reset, startedAt, $"RESET {pulses}", definition.BaselineRms, definition.Signal));
            }

            return new PulseRampResult(
                definition.Name,
                definition.StopOn == RealTestFeedback.None ? RealTestWorkflowOutcome.Completed : RealTestWorkflowOutcome.NoFeedback,
                definition.StopOn,
                pulses,
                null,
                null,
                observations);
        }
        finally
        {
            _bench.StopInjection();
            _bench.AutoStopOnTrip = originalAutoStop;
        }
    }

    public PickupDropoutSearchResult RunPickupDropoutSearch(
        PickupDropoutSearchDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        var observations = new List<RealTestObservation>();
        var originalAutoStop = _bench.AutoStopOnTrip;
        double? pickup = null;
        double? dropout = null;
        try
        {
            var initial = WithSignalRms(_source.ActiveProfile, definition.Signal, definition.MinimumRms, definition.Name);
            var prime = Prepare(initial);
            var startedAt = prime.RelayMeasurement.Timestamp;

            foreach (var level in EnumerateLevels(definition.MinimumRms, definition.MaximumRms, definition.StepRms))
            {
                if (cancellationToken.IsCancellationRequested)
                    return SearchResult(RealTestWorkflowOutcome.Cancelled);

                _source.ApplyContinuousProfile(WithSignalRms(_source.ActiveProfile, definition.Signal, level, definition.Name));
                var step = AdvanceFor(definition.Dwell, cancellationToken);
                observations.Add(Observe(step, startedAt, "PICKUP SEARCH", level, definition.Signal));
                if (step.TestSet.PickupInput)
                {
                    pickup = level;
                    break;
                }
            }

            if (pickup is null)
                return SearchResult(RealTestWorkflowOutcome.NoFeedback);

            foreach (var level in EnumerateLevels(pickup.Value, definition.MinimumRms, definition.StepRms).Skip(1))
            {
                if (cancellationToken.IsCancellationRequested)
                    return SearchResult(RealTestWorkflowOutcome.Cancelled);

                _source.ApplyContinuousProfile(WithSignalRms(_source.ActiveProfile, definition.Signal, level, definition.Name));
                var step = AdvanceFor(definition.Dwell, cancellationToken);
                observations.Add(Observe(step, startedAt, "DROPOUT SEARCH", level, definition.Signal));
                if (!step.TestSet.PickupInput)
                {
                    dropout = level;
                    break;
                }
            }

            return SearchResult(dropout is null ? RealTestWorkflowOutcome.NoFeedback : RealTestWorkflowOutcome.Completed);
        }
        finally
        {
            _bench.StopInjection();
            _bench.AutoStopOnTrip = originalAutoStop;
        }

        PickupDropoutSearchResult SearchResult(RealTestWorkflowOutcome outcome)
            => new(
                definition.Name,
                outcome,
                pickup,
                dropout,
                pickup is > 0 && dropout is not null ? dropout.Value / pickup.Value : null,
                observations);
    }

    public StateSequenceResult RunStateSequence(
        StateSequenceDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        var observations = new List<RealTestObservation>();
        var stateResults = new List<StateSequenceStateResult>();
        var originalAutoStop = _bench.AutoStopOnTrip;
        try
        {
            var prime = Prepare(definition.States[0].Profile);
            var startedAt = prime.RelayMeasurement.Timestamp;

            foreach (var state in definition.States)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new StateSequenceResult(definition.Name, RealTestWorkflowOutcome.Cancelled, stateResults, observations);

                _source.ApplyContinuousProfile(state.Profile);
                var stateStarted = LastTimestamp() - startedAt;
                var previousFeedback = FeedbackAsserted(_bench.TestSetSnapshot, state.AdvanceOn);
                var pickupObserved = _bench.TestSetSnapshot.PickupInput;
                var tripObserved = _bench.TestSetSnapshot.TripInput;
                var elapsed = TimeSpan.Zero;
                var exit = "duration";

                while (elapsed < state.MaximumDuration)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        exit = "cancelled";
                        break;
                    }

                    var quantum = Min(ClosedLoopVirtualTestBench.SimulationQuantum, state.MaximumDuration - elapsed);
                    var step = _bench.Advance(quantum);
                    elapsed += quantum;
                    pickupObserved |= step.TestSet.PickupInput;
                    tripObserved |= step.TestSet.TripInput;

                    var currentFeedback = FeedbackAsserted(step.TestSet, state.AdvanceOn);
                    if (state.AdvanceOn != RealTestFeedback.None && currentFeedback && !previousFeedback)
                    {
                        exit = state.AdvanceOn == RealTestFeedback.Pickup ? "pickup-edge" : "trip-edge";
                        observations.Add(Observe(step, startedAt, state.Name, CommandedMagnitude(state.Profile), DominantSignal(state.Profile)));
                        break;
                    }
                    previousFeedback = currentFeedback;

                    if (elapsed == state.MaximumDuration)
                        observations.Add(Observe(step, startedAt, state.Name, CommandedMagnitude(state.Profile), DominantSignal(state.Profile)));
                }

                var stateEnded = LastTimestamp() - startedAt;
                stateResults.Add(new StateSequenceStateResult(
                    state.Name,
                    stateStarted,
                    stateEnded,
                    exit,
                    pickupObserved,
                    tripObserved));

                if (exit == "cancelled")
                    return new StateSequenceResult(definition.Name, RealTestWorkflowOutcome.Cancelled, stateResults, observations);
            }

            return new StateSequenceResult(definition.Name, RealTestWorkflowOutcome.Completed, stateResults, observations);
        }
        finally
        {
            _bench.StopInjection();
            _bench.AutoStopOnTrip = originalAutoStop;
        }
    }

    private ClosedLoopVirtualTestBenchStep Prepare(VirtualInjectionProfile initialProfile)
    {
        _bench.Reset(keepProfile: true);
        _source.ApplyProfile(initialProfile);
        _bench.AutoStopOnTrip = false;
        _bench.StartInjection();
        return _bench.Advance(InitialCoherence);
    }

    private ClosedLoopVirtualTestBenchStep AdvanceFor(TimeSpan duration, CancellationToken cancellationToken)
    {
        var elapsed = TimeSpan.Zero;
        ClosedLoopVirtualTestBenchStep? last = null;
        while (elapsed < duration)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            var quantum = Min(ClosedLoopVirtualTestBench.SimulationQuantum, duration - elapsed);
            last = _bench.Advance(quantum);
            elapsed += quantum;
        }
        return last ?? _bench.Advance(TimeSpan.Zero);
    }

    private DateTimeOffset LastTimestamp()
        => _bench.Advance(TimeSpan.Zero).RelayMeasurement.Timestamp;

    private static RealTestObservation Observe(
        ClosedLoopVirtualTestBenchStep result,
        DateTimeOffset startedAt,
        string stage,
        double commandedRms,
        VirtualInjectionSignal signal)
        => new(
            result.RelayMeasurement.Timestamp,
            result.RelayMeasurement.Timestamp - startedAt,
            stage,
            commandedRms,
            MeasuredRms(result.RelayMeasurement, signal),
            result.TestSet.PickupInput,
            result.TestSet.TripInput);

    private static bool FeedbackAsserted(VirtualTestSetTimingSnapshot snapshot, RealTestFeedback feedback)
        => feedback switch
        {
            RealTestFeedback.None => false,
            RealTestFeedback.Pickup => snapshot.PickupInput,
            RealTestFeedback.Trip => snapshot.TripInput,
            _ => false
        };

    public static VirtualInjectionProfile WithSignalRms(
        VirtualInjectionProfile profile,
        VirtualInjectionSignal signal,
        double rms,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateRms(rms, nameof(rms));
        var channel = profile.Channel(signal) with { Enabled = true, Rms = rms };
        var updated = signal switch
        {
            VirtualInjectionSignal.PhaseAVoltage => profile with { PhaseAVoltage = channel },
            VirtualInjectionSignal.PhaseBVoltage => profile with { PhaseBVoltage = channel },
            VirtualInjectionSignal.PhaseCVoltage => profile with { PhaseCVoltage = channel },
            VirtualInjectionSignal.NeutralVoltage => profile with { NeutralVoltage = channel },
            VirtualInjectionSignal.PhaseACurrent => profile with { PhaseACurrent = channel },
            VirtualInjectionSignal.PhaseBCurrent => profile with { PhaseBCurrent = channel },
            VirtualInjectionSignal.PhaseCCurrent => profile with { PhaseCCurrent = channel },
            VirtualInjectionSignal.NeutralCurrent => profile with { NeutralCurrent = channel },
            _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, "Unknown virtual injection signal.")
        };
        return (updated with { Name = string.IsNullOrWhiteSpace(name) ? updated.Name : name.Trim() }).Normalize();
    }

    private static double MeasuredRms(MeasurementFrame measurement, VirtualInjectionSignal signal)
        => signal switch
        {
            VirtualInjectionSignal.PhaseACurrent => measurement.PhaseA,
            VirtualInjectionSignal.PhaseBCurrent => measurement.PhaseB,
            VirtualInjectionSignal.PhaseCCurrent => measurement.PhaseC,
            VirtualInjectionSignal.NeutralCurrent => measurement.Residual,
            VirtualInjectionSignal.PhaseAVoltage => measurement.Phasors?.PhaseAVoltage.Magnitude ?? 0,
            VirtualInjectionSignal.PhaseBVoltage => measurement.Phasors?.PhaseBVoltage.Magnitude ?? 0,
            VirtualInjectionSignal.PhaseCVoltage => measurement.Phasors?.PhaseCVoltage.Magnitude ?? 0,
            VirtualInjectionSignal.NeutralVoltage => measurement.Phasors?.ResidualVoltage.Magnitude ?? 0,
            _ => 0
        };

    private static VirtualInjectionSignal DominantSignal(VirtualInjectionProfile profile)
    {
        var channels = Enum.GetValues<VirtualInjectionSignal>()
            .Select(signal => (Signal: signal, Rms: profile.Channel(signal).Rms))
            .OrderByDescending(item => item.Rms)
            .ToArray();
        return channels[0].Signal;
    }

    private static double CommandedMagnitude(VirtualInjectionProfile profile)
        => Enum.GetValues<VirtualInjectionSignal>()
            .Select(signal => profile.Channel(signal).Rms)
            .DefaultIfEmpty(0)
            .Max();

    private static IEnumerable<double> EnumerateLevels(double start, double end, double step)
    {
        var direction = end >= start ? 1d : -1d;
        var signedStep = step * direction;
        var value = start;
        var count = 0;
        while ((direction > 0 && value <= end + 1e-12) || (direction < 0 && value >= end - 1e-12))
        {
            yield return value;
            count++;
            if (count > 10_001)
                yield break;
            value += signedStep;
        }

        if (Math.Abs(value - signedStep - end) > 1e-9)
            yield return end;
    }

    private static TimeSpan Min(TimeSpan first, TimeSpan second)
        => first <= second ? first : second;

    private static void ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Workflow name is required.");
    }

    private static void ValidateRms(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1_000_000_000)
            throw new ArgumentOutOfRangeException(name, "RMS value must be finite and between 0 and 1e9.");
    }

    private static void ValidatePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateDwell(TimeSpan value, string name, TimeSpan? maximum = null)
    {
        var max = maximum ?? TimeSpan.FromSeconds(5);
        if (value < ClosedLoopVirtualTestBench.SimulationQuantum || value > max)
            throw new ArgumentOutOfRangeException(name, $"Duration must be between {ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds:0.###} ms and {max.TotalSeconds:0.###} s.");
    }
}
