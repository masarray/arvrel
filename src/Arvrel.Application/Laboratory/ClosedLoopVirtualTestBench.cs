using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

public enum VirtualBinarySignal
{
    Pickup,
    Trip
}

public enum VirtualTestSetTimerState
{
    Idle,
    Armed,
    Completed,
    Blocked
}

public sealed record VirtualAnalogWire(
    string Id,
    VirtualInjectionSignal Signal,
    string SourceTerminal,
    string DestinationTerminal,
    bool Connected = true);

public sealed record VirtualBinaryWire(
    string Id,
    VirtualBinarySignal Signal,
    string SourceTerminal,
    string DestinationTerminal,
    bool Connected = true);

public sealed record VirtualTestBenchTopology(
    IReadOnlyList<VirtualAnalogWire> AnalogWires,
    IReadOnlyList<VirtualBinaryWire> BinaryWires)
{
    public const string TripFeedbackWireId = "relay.bo1-trip->testset.bi1-trip";
    public const string PickupFeedbackWireId = "relay.bo2-pickup->testset.bi2-pickup";

    public static VirtualTestBenchTopology Default { get; } = new(
        new[]
        {
            Analog("VA", VirtualInjectionSignal.PhaseAVoltage),
            Analog("VB", VirtualInjectionSignal.PhaseBVoltage),
            Analog("VC", VirtualInjectionSignal.PhaseCVoltage),
            Analog("VN", VirtualInjectionSignal.NeutralVoltage),
            Analog("IA", VirtualInjectionSignal.PhaseACurrent),
            Analog("IB", VirtualInjectionSignal.PhaseBCurrent),
            Analog("IC", VirtualInjectionSignal.PhaseCCurrent),
            Analog("IN", VirtualInjectionSignal.NeutralCurrent)
        },
        new[]
        {
            new VirtualBinaryWire(
                TripFeedbackWireId,
                VirtualBinarySignal.Trip,
                "RELAY.BO1.TRIP",
                "TESTSET.BI1.TRIP"),
            new VirtualBinaryWire(
                PickupFeedbackWireId,
                VirtualBinarySignal.Pickup,
                "RELAY.BO2.PICKUP",
                "TESTSET.BI2.PICKUP")
        });

    public bool IsAnalogConnected(VirtualInjectionSignal signal)
        => AnalogWires.Any(wire => wire.Signal == signal && wire.Connected);

    public bool IsBinaryConnected(VirtualBinarySignal signal)
        => BinaryWires.Any(wire => wire.Signal == signal && wire.Connected);

    public VirtualTestBenchTopology WithWireConnection(string wireId, bool connected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wireId);
        var found = false;
        var analog = AnalogWires.Select(wire =>
        {
            if (!string.Equals(wire.Id, wireId, StringComparison.OrdinalIgnoreCase))
                return wire;
            found = true;
            return wire with { Connected = connected };
        }).ToArray();
        var binary = BinaryWires.Select(wire =>
        {
            if (!string.Equals(wire.Id, wireId, StringComparison.OrdinalIgnoreCase))
                return wire;
            found = true;
            return wire with { Connected = connected };
        }).ToArray();
        if (!found)
            throw new ArgumentException($"Unknown virtual wire '{wireId}'.", nameof(wireId));
        return new VirtualTestBenchTopology(analog, binary);
    }

    public string Fingerprint()
    {
        var canonical = string.Join(
            '|',
            AnalogWires
                .OrderBy(wire => wire.Id, StringComparer.Ordinal)
                .Select(wire => $"A:{wire.Id}:{wire.Signal}:{wire.SourceTerminal}:{wire.DestinationTerminal}:{wire.Connected}")
                .Concat(BinaryWires
                    .OrderBy(wire => wire.Id, StringComparer.Ordinal)
                    .Select(wire => $"B:{wire.Id}:{wire.Signal}:{wire.SourceTerminal}:{wire.DestinationTerminal}:{wire.Connected}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static VirtualAnalogWire Analog(string terminal, VirtualInjectionSignal signal)
        => new(
            $"testset.{terminal.ToLowerInvariant()}->relay.{terminal.ToLowerInvariant()}",
            signal,
            $"TESTSET.{terminal}",
            $"RELAY.{terminal}");
}

public sealed record VirtualRelayContactProfile(
    TimeSpan PickupOperateDelay,
    TimeSpan TripOperateDelay,
    TimeSpan ReleaseDelay,
    TimeSpan ContactBounceDuration = default,
    TimeSpan ContactBouncePeriod = default,
    TimeSpan TestSetInputDebounce = default)
{
    public static VirtualRelayContactProfile NumericalRelayDefault { get; } = new(
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(3),
        TimeSpan.FromMilliseconds(1));

    public static VirtualRelayContactProfile RealisticNumericalRelay { get; } = new(
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(3),
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(0.25),
        TimeSpan.FromMilliseconds(0.5));

    public void Validate()
    {
        ValidateDelay(PickupOperateDelay, nameof(PickupOperateDelay));
        ValidateDelay(TripOperateDelay, nameof(TripOperateDelay));
        ValidateDelay(ReleaseDelay, nameof(ReleaseDelay));
        ValidateDelay(ContactBounceDuration, nameof(ContactBounceDuration));
        ValidateDelay(TestSetInputDebounce, nameof(TestSetInputDebounce));
        if (ContactBounceDuration > TimeSpan.Zero)
        {
            if (ContactBouncePeriod <= TimeSpan.Zero || ContactBouncePeriod > ContactBounceDuration)
                throw new ArgumentOutOfRangeException(nameof(ContactBouncePeriod), "Contact bounce period must be positive and no longer than the bounce duration.");
            if (ContactBouncePeriod.Ticks % ClosedLoopVirtualTestBench.SimulationQuantum.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ContactBouncePeriod),
                    $"Contact bounce period must align to the {ClosedLoopVirtualTestBench.SimulationQuantum.TotalMilliseconds:0.###} ms deterministic grid.");
            }
        }
        else if (ContactBouncePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ContactBouncePeriod));
        }
    }

    private static void ValidateDelay(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero || value > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(name, "Virtual contact delay must be between 0 and 1 second.");
    }
}

public sealed record VirtualTestSetTimingSnapshot(
    DateTimeOffset? InjectionStartedAt,
    DateTimeOffset? PickupDetectedAt,
    DateTimeOffset? TripDetectedAt,
    DateTimeOffset? OutputStoppedAt,
    bool PickupInput,
    bool TripInput,
    bool AutoStopOnTrip,
    bool OutputRunning,
    string TopologyFingerprint,
    bool PickupContactRaw = false,
    bool TripContactRaw = false,
    VirtualTestSetTimerState TimerState = VirtualTestSetTimerState.Idle,
    string? ArmBlockReason = null,
    long TestRunId = 0,
    DateTimeOffset? RelayPickupDetectedAt = null,
    DateTimeOffset? RelayTripDetectedAt = null,
    DateTimeOffset? ObservedAt = null,
    long? InjectionStartedMicroseconds = null,
    long? PickupDetectedMicroseconds = null,
    long? TripDetectedMicroseconds = null,
    long? RelayPickupDetectedMicroseconds = null,
    long? RelayTripDetectedMicroseconds = null,
    int TimingResolutionMicroseconds = 250,
    IReadOnlyList<MetrologyEvent>? MetrologyTimeline = null,
    long? ObservedMicroseconds = null)
{
    public bool TimerArmed => TimerState == VirtualTestSetTimerState.Armed;
    public bool TimerCompleted => TimerState == VirtualTestSetTimerState.Completed;

    public TimeSpan? PickupTime => PickupDetectedMicroseconds is { } pickupUs
        ? TimeSpan.FromTicks(pickupUs * 10)
        : InjectionStartedAt is { } start && PickupDetectedAt is { } pickup
            ? pickup - start
            : null;

    public TimeSpan? TripTime => TripDetectedMicroseconds is { } tripUs
        ? TimeSpan.FromTicks(tripUs * 10)
        : InjectionStartedAt is { } start && TripDetectedAt is { } trip
            ? trip - start
            : null;

    public TimeSpan? RelayPickupTime => RelayPickupDetectedMicroseconds is { } pickupUs
        ? TimeSpan.FromTicks(pickupUs * 10)
        : InjectionStartedAt is { } start && RelayPickupDetectedAt is { } pickup
            ? pickup - start
            : null;

    public TimeSpan? RelayTripTime => RelayTripDetectedMicroseconds is { } tripUs
        ? TimeSpan.FromTicks(tripUs * 10)
        : InjectionStartedAt is { } start && RelayTripDetectedAt is { } trip
            ? trip - start
            : null;

    public TimeSpan? RelayPickupToTrip => RelayPickupDetectedMicroseconds is { } pickupUs && RelayTripDetectedMicroseconds is { } tripUs
        ? TimeSpan.FromTicks((tripUs - pickupUs) * 10)
        : RelayPickupDetectedAt is { } pickup && RelayTripDetectedAt is { } trip
            ? trip - pickup
            : null;

    public TimeSpan? RelayPickupToBi2 => RelayPickupDetectedMicroseconds is { } relayPickupUs && PickupDetectedMicroseconds is { } bi2Us
        ? TimeSpan.FromTicks((bi2Us - relayPickupUs) * 10)
        : RelayPickupDetectedAt is { } relayPickup && PickupDetectedAt is { } bi2
            ? bi2 - relayPickup
            : null;

    public TimeSpan? RelayTripToBi1 => RelayTripDetectedMicroseconds is { } relayTripUs && TripDetectedMicroseconds is { } bi1Us
        ? TimeSpan.FromTicks((bi1Us - relayTripUs) * 10)
        : RelayTripDetectedAt is { } relayTrip && TripDetectedAt is { } bi1
            ? bi1 - relayTrip
            : null;

    public TimeSpan? ElapsedTime
    {
        get
        {
            if (TripDetectedMicroseconds is { } tripUs && TimerCompleted)
                return TimeSpan.FromTicks(tripUs * 10);
            if (ObservedMicroseconds is { } observedUs && InjectionStartedMicroseconds is not null)
                return TimeSpan.FromTicks(observedUs * 10);
            if (InjectionStartedAt is not { } start)
                return null;
            var end = TimerCompleted && TripDetectedAt is { } trip
                ? trip
                : ObservedAt;
            return end is { } observed ? observed - start : null;
        }
    }
}

public sealed record ClosedLoopVirtualTestBenchStep(
    ScenarioStep Source,
    MeasurementFrame RelayMeasurement,
    ProtectionSnapshot Protection,
    VirtualTestSetTimingSnapshot TestSet);

public sealed record ClosedLoopTripCapture(
    long TestRunId,
    DateTimeOffset CapturedAt,
    ScenarioStep Source,
    MeasurementFrame RelayMeasurement,
    ProtectionSnapshot Protection,
    VirtualTestSetTimingSnapshot TestSet);

/// <summary>
/// Deterministic black-box laboratory backplane between the virtual secondary injector
/// and the virtual protection relay. The optional metrology path uses a monotonic
/// microsecond clock, causal instantaneous-sample relay acquisition and an independent
/// binary-input sampler. TESTSET timing authority remains wired BI edges only.
/// </summary>
public sealed class ClosedLoopVirtualTestBench
{
    public const int SimulationSampleRateHz = 4_000;
    public static readonly TimeSpan SimulationQuantum = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / SimulationSampleRateHz);

    private readonly DeterministicLabScenario _source;
    private readonly ResearchProtectionEngine _relay;
    private readonly VirtualRelayFrontEndRuntime _frontEnd;
    private readonly DelayedBinaryContact _pickupContact;
    private readonly DelayedBinaryContact _tripContact;
    private readonly DebouncedBinaryInput _pickupInput;
    private readonly DebouncedBinaryInput _tripInput;
    private readonly MetrologyTimingProfile? _metrologyProfile;
    private readonly MetrologyClock? _metrologyClock;
    private readonly CausalRelayFrontEndRuntime? _causalFrontEnd;
    private readonly MetrologyBinaryContact? _metrologyPickupContact;
    private readonly MetrologyBinaryContact? _metrologyTripContact;
    private readonly MetrologyBinaryInput? _metrologyPickupInput;
    private readonly MetrologyBinaryInput? _metrologyTripInput;
    private readonly List<MetrologyEvent> _metrologyTimeline = new();
    private VirtualTestBenchTopology _topology;
    private DateTimeOffset? _injectionStartedAt;
    private DateTimeOffset? _pickupDetectedAt;
    private DateTimeOffset? _tripDetectedAt;
    private DateTimeOffset? _outputStoppedAt;
    private DateTimeOffset? _relayPickupDetectedAt;
    private DateTimeOffset? _relayTripDetectedAt;
    private DateTimeOffset? _lastObservedAt;
    private long? _injectionStartUs;
    private long? _pickupDetectedUs;
    private long? _tripDetectedUs;
    private long? _relayPickupDetectedUs;
    private long? _relayTripDetectedUs;
    private bool _lastPickupInput;
    private bool _lastTripInput;
    private bool _lastPickupContactRaw;
    private bool _lastTripContactRaw;
    private bool _lastRelayPickupRequested;
    private bool _lastRelayTripLatched;
    private long _testRunId;
    private VirtualTestSetTimerState _timerState;
    private string? _armBlockReason;
    private ClosedLoopTripCapture? _tripCapture;
    private ProtectionSnapshot _lastProtection;

    public ClosedLoopVirtualTestBench(
        DeterministicLabScenario source,
        ResearchProtectionEngine relay,
        VirtualTestBenchTopology? topology = null,
        VirtualRelayContactProfile? contactProfile = null,
        VirtualRelayFrontEndProfile? frontEndProfile = null,
        MetrologyTimingProfile? metrologyProfile = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        _topology = topology ?? VirtualTestBenchTopology.Default;
        ContactProfile = contactProfile ?? VirtualRelayContactProfile.NumericalRelayDefault;
        ContactProfile.Validate();
        FrontEndProfile = frontEndProfile ?? VirtualRelayFrontEndProfile.Ideal;
        FrontEndProfile.Validate();
        _frontEnd = new VirtualRelayFrontEndRuntime(FrontEndProfile);
        _pickupContact = new DelayedBinaryContact(
            ContactProfile.PickupOperateDelay,
            ContactProfile.ReleaseDelay,
            ContactProfile.ContactBounceDuration,
            ContactProfile.ContactBouncePeriod);
        _tripContact = new DelayedBinaryContact(
            ContactProfile.TripOperateDelay,
            ContactProfile.ReleaseDelay,
            ContactProfile.ContactBounceDuration,
            ContactProfile.ContactBouncePeriod);
        _pickupInput = new DebouncedBinaryInput(ContactProfile.TestSetInputDebounce);
        _tripInput = new DebouncedBinaryInput(ContactProfile.TestSetInputDebounce);

        _metrologyProfile = metrologyProfile;
        if (_metrologyProfile is not null)
        {
            _metrologyProfile.Validate();
            _metrologyClock = new MetrologyClock();
            _causalFrontEnd = new CausalRelayFrontEndRuntime(FrontEndProfile);
            _metrologyPickupContact = new MetrologyBinaryContact(
                ContactProfile.PickupOperateDelay,
                ContactProfile.ReleaseDelay,
                ContactProfile.ContactBounceDuration,
                ContactProfile.ContactBouncePeriod);
            _metrologyTripContact = new MetrologyBinaryContact(
                ContactProfile.TripOperateDelay,
                ContactProfile.ReleaseDelay,
                ContactProfile.ContactBounceDuration,
                ContactProfile.ContactBouncePeriod);
            _metrologyPickupInput = new MetrologyBinaryInput(_metrologyProfile);
            _metrologyTripInput = new MetrologyBinaryInput(_metrologyProfile);
        }

        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        _timerState = VirtualTestSetTimerState.Idle;
    }

    public VirtualTestBenchTopology Topology => _topology;
    public VirtualRelayContactProfile ContactProfile { get; }
    public VirtualRelayFrontEndProfile FrontEndProfile { get; }
    public MetrologyTimingProfile? MetrologyProfile => _metrologyProfile;
    public VirtualRelayFrontEndSnapshot FrontEndSnapshot => _frontEnd.Snapshot;
    public CausalRelayFrontEndSnapshot? CausalFrontEndSnapshot => _causalFrontEnd?.Snapshot;
    public bool AutoStopOnTrip { get; set; } = true;
    public ProtectionSnapshot LastProtection => _lastProtection;
    public VirtualTestSetTimingSnapshot TestSetSnapshot => Snapshot();
    public ClosedLoopTripCapture? TripCapture => _tripCapture;

    public TimeSpan FeedbackSettleTime
    {
        get
        {
            var maxContact = ContactProfile.PickupOperateDelay;
            if (ContactProfile.TripOperateDelay > maxContact)
                maxContact = ContactProfile.TripOperateDelay;
            if (ContactProfile.ReleaseDelay > maxContact)
                maxContact = ContactProfile.ReleaseDelay;
            return FrontEndProfile.MeasurementGroupDelay +
                   maxContact +
                   ContactProfile.ContactBounceDuration +
                   ContactProfile.TestSetInputDebounce +
                   SimulationQuantum;
        }
    }

    public void SetWireConnected(string wireId, bool connected)
        => _topology = _topology.WithWireConnection(wireId, connected);

    public bool StartInjection()
        => TryStartInjection(out _);

    public bool TryStartInjection(out string? blockReason)
    {
        blockReason = null;
        if (_source.IsRunning)
            return false;

        Advance(FeedbackSettleTime);

        if (_lastPickupInput || _lastTripInput)
        {
            var active = string.Join(
                " + ",
                new[]
                {
                    _lastTripInput ? "TESTSET.BI1 TRIP" : null,
                    _lastPickupInput ? "TESTSET.BI2 PICKUP" : null
                }.Where(value => value is not null));
            _timerState = VirtualTestSetTimerState.Blocked;
            _armBlockReason = $"{active} already active. Reset the relay or clear the feedback contact before starting a new timed test.";
            blockReason = _armBlockReason;
            return false;
        }

        var changed = _source.StartInjection();
        if (!changed)
            return false;

        BeginTest(_source.OutputStateChangedAt);
        return true;
    }

    public bool StopInjection()
    {
        var changed = _source.StopInjection();
        if (!changed)
            return false;

        _outputStoppedAt ??= _source.OutputStateChangedAt;
        if (_timerState == VirtualTestSetTimerState.Armed)
        {
            _timerState = VirtualTestSetTimerState.Idle;
            _armBlockReason = null;
            _injectionStartedAt = null;
            _pickupDetectedAt = null;
            _tripDetectedAt = null;
            _relayPickupDetectedAt = null;
            _relayTripDetectedAt = null;
            _injectionStartUs = null;
            _pickupDetectedUs = null;
            _tripDetectedUs = null;
            _relayPickupDetectedUs = null;
            _relayTripDetectedUs = null;
        }
        return true;
    }

    public void UpdateSettings(ProtectionSettings settings, bool keepTripLatch = false)
    {
        _relay.UpdateSettings(settings, keepTripLatch);
        ResetObserverState();
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public void Reset(bool keepProfile = false)
    {
        _relay.Reset();
        _source.Restart(keepProfile);
        _frontEnd.Reset();
        _causalFrontEnd?.Reset();
        ResetObserverState();
        _metrologyClock?.Reset();
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public void ResetObserverState()
    {
        _pickupContact.Reset();
        _tripContact.Reset();
        _pickupInput.Reset();
        _tripInput.Reset();
        _metrologyPickupContact?.Reset();
        _metrologyTripContact?.Reset();
        _metrologyPickupInput?.Reset();
        _metrologyTripInput?.Reset();
        _lastPickupContactRaw = false;
        _lastTripContactRaw = false;
        _lastRelayPickupRequested = false;
        _lastRelayTripLatched = false;
        ClearTestTiming();
    }

    public ClosedLoopVirtualTestBenchStep Advance(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delta));

        if (_source.IsRunning && _injectionStartedAt is null)
            BeginTest(_source.OutputStateChangedAt);
        else if (!_source.IsRunning && _injectionStartedAt is not null && _outputStoppedAt is null)
            _outputStoppedAt = _source.OutputStateChangedAt;

        if (delta == TimeSpan.Zero)
            return AdvanceQuantum(TimeSpan.Zero);

        var stopExactlyOnTripEdge = _source.IsRunning && AutoStopOnTrip;
        var remaining = delta;
        ClosedLoopVirtualTestBenchStep? last = null;
        while (remaining > TimeSpan.Zero)
        {
            var step = remaining > SimulationQuantum ? SimulationQuantum : remaining;
            last = AdvanceQuantum(step);
            remaining -= step;
            if (stopExactlyOnTripEdge &&
                last.TestSet.TimerCompleted &&
                !last.TestSet.OutputRunning)
                break;
        }
        return last!;
    }

    private ClosedLoopVirtualTestBenchStep AdvanceQuantum(TimeSpan delta)
        => _metrologyProfile is null
            ? AdvanceLegacyQuantum(delta)
            : AdvanceMetrologyQuantum(delta);

    private ClosedLoopVirtualTestBenchStep AdvanceLegacyQuantum(TimeSpan delta)
    {
        var sourceStep = _source.Advance(delta);
        var terminalMeasurement = ApplyAnalogWiring(sourceStep.Measurement);
        var relayMeasurement = _frontEnd.Process(terminalMeasurement);
        var protection = _relay.Evaluate(relayMeasurement);
        _lastProtection = protection;

        var pickupRequested = HasAnyPickup(protection);
        var timestamp = relayMeasurement.Timestamp;
        _lastObservedAt = timestamp;
        ObserveRelayTiming(protection, timestamp);

        _lastPickupContactRaw = _pickupContact.Update(pickupRequested, timestamp);
        _lastTripContactRaw = _tripContact.Update(protection.TripLatched, timestamp);

        var wiredPickup = _topology.IsBinaryConnected(VirtualBinarySignal.Pickup) && _lastPickupContactRaw;
        var wiredTrip = _topology.IsBinaryConnected(VirtualBinarySignal.Trip) && _lastTripContactRaw;
        var pickupInput = _pickupInput.Update(wiredPickup, timestamp);
        var tripInput = _tripInput.Update(wiredTrip, timestamp);

        if (_timerState == VirtualTestSetTimerState.Armed &&
            pickupInput &&
            !_lastPickupInput &&
            _injectionStartedAt is not null)
        {
            _pickupDetectedAt ??= timestamp;
        }

        if (_timerState == VirtualTestSetTimerState.Armed &&
            tripInput &&
            !_lastTripInput &&
            _injectionStartedAt is not null)
        {
            _tripDetectedAt ??= timestamp;
            _timerState = VirtualTestSetTimerState.Completed;
            if (AutoStopOnTrip && _source.IsRunning)
            {
                _source.StopInjection();
                _outputStoppedAt ??= _source.OutputStateChangedAt;
            }

            _lastPickupInput = pickupInput;
            _lastTripInput = tripInput;
            var completed = Snapshot();
            _tripCapture = new ClosedLoopTripCapture(
                _testRunId,
                timestamp,
                sourceStep,
                relayMeasurement,
                protection,
                completed);
            return new ClosedLoopVirtualTestBenchStep(sourceStep, relayMeasurement, protection, completed);
        }

        _lastPickupInput = pickupInput;
        _lastTripInput = tripInput;
        return new ClosedLoopVirtualTestBenchStep(sourceStep, relayMeasurement, protection, Snapshot());
    }

    private ClosedLoopVirtualTestBenchStep AdvanceMetrologyQuantum(TimeSpan delta)
    {
        var clock = _metrologyClock!;
        clock.Advance(delta);
        var nowUs = clock.Microseconds;
        var sourceStep = _source.Advance(delta);
        var terminal = BuildTerminalSample(sourceStep, nowUs);
        var relayMeasurement = _causalFrontEnd!.Process(terminal);
        var protection = _relay.Evaluate(relayMeasurement);
        _lastProtection = protection;
        _lastObservedAt = relayMeasurement.Timestamp;

        var pickupRequested = HasAnyPickup(protection);
        if (_timerState == VirtualTestSetTimerState.Armed && pickupRequested && !_lastRelayPickupRequested)
        {
            _relayPickupDetectedUs ??= nowUs;
            _relayPickupDetectedAt ??= relayMeasurement.Timestamp;
            AddMetrologyEvent(MetrologyEventKind.RelayPickupAssert, nowUs, "Relay pickup output requested from live protection state.");
        }
        if (_timerState == VirtualTestSetTimerState.Armed && protection.TripLatched && !_lastRelayTripLatched)
        {
            _relayTripDetectedUs ??= nowUs;
            _relayTripDetectedAt ??= relayMeasurement.Timestamp;
            AddMetrologyEvent(MetrologyEventKind.RelayTripRequest, nowUs, "Relay TripLatched rising edge requested BO1.");
        }
        _lastRelayPickupRequested = pickupRequested;
        _lastRelayTripLatched = protection.TripLatched;

        _metrologyPickupContact!.Request(pickupRequested, nowUs);
        _metrologyTripContact!.Request(protection.TripLatched, nowUs);

        var pickupUpdate = _metrologyPickupInput!.Observe(
            sampleUs => _topology.IsBinaryConnected(VirtualBinarySignal.Pickup) && _metrologyPickupContact.StateAt(sampleUs),
            nowUs);
        var tripUpdate = _metrologyTripInput!.Observe(
            sampleUs => _topology.IsBinaryConnected(VirtualBinarySignal.Trip) && _metrologyTripContact.StateAt(sampleUs),
            nowUs);

        _metrologyPickupContact.Commit(nowUs);
        _metrologyTripContact.Commit(nowUs);
        _lastPickupContactRaw = _metrologyPickupContact.StateAt(nowUs);
        _lastTripContactRaw = _metrologyTripContact.StateAt(nowUs);
        _lastPickupInput = pickupUpdate.State;
        _lastTripInput = tripUpdate.State;

        if (_timerState == VirtualTestSetTimerState.Armed &&
            pickupUpdate.RisingEdgeMicroseconds is { } pickupEdgeUs &&
            _injectionStartUs is not null)
        {
            _pickupDetectedUs ??= pickupEdgeUs;
            _pickupDetectedAt ??= TimestampForMetrologyEdge(pickupEdgeUs);
            AddMetrologyEvent(MetrologyEventKind.TestSetBi2Pickup, pickupEdgeUs, "TESTSET.BI2 accepted pickup after BI sampling and deglitch.");
        }

        if (_timerState == VirtualTestSetTimerState.Armed &&
            tripUpdate.RisingEdgeMicroseconds is { } tripEdgeUs &&
            _injectionStartUs is not null)
        {
            _tripDetectedUs ??= tripEdgeUs;
            _tripDetectedAt ??= TimestampForMetrologyEdge(tripEdgeUs);
            _timerState = VirtualTestSetTimerState.Completed;
            AddMetrologyEvent(MetrologyEventKind.TestSetBi1Trip, tripEdgeUs, "TESTSET.BI1 accepted trip edge; this is the measured stop time.");

            if (AutoStopOnTrip && _source.IsRunning)
            {
                _source.StopInjection();
                _outputStoppedAt ??= _tripDetectedAt;
                AddMetrologyEvent(MetrologyEventKind.OutputStopCommand, tripEdgeUs, "Virtual test-set stop command issued on accepted BI1 edge.");
            }

            var completed = Snapshot();
            _tripCapture = new ClosedLoopTripCapture(
                _testRunId,
                relayMeasurement.Timestamp,
                sourceStep,
                relayMeasurement,
                protection,
                completed);
            return new ClosedLoopVirtualTestBenchStep(sourceStep, relayMeasurement, protection, completed);
        }

        return new ClosedLoopVirtualTestBenchStep(sourceStep, relayMeasurement, protection, Snapshot());
    }

    private VirtualRelayTerminalSample BuildTerminalSample(ScenarioStep source, long nowUs)
    {
        static double First(double[] values) => values.Length == 0 ? 0 : values[0];
        double Wired(VirtualInjectionSignal signal, double value)
            => _topology.IsAnalogConnected(signal) ? value : 0;

        var phasors = source.Measurement.Phasors;
        return new VirtualRelayTerminalSample(
            source.Measurement.Timestamp,
            nowUs,
            source.SourceSampleIndex,
            source.Waveform.SampleRateHz,
            source.Waveform.FrequencyHz,
            Wired(VirtualInjectionSignal.PhaseACurrent, First(source.Waveform.PhaseA)),
            Wired(VirtualInjectionSignal.PhaseBCurrent, First(source.Waveform.PhaseB)),
            Wired(VirtualInjectionSignal.PhaseCCurrent, First(source.Waveform.PhaseC)),
            Wired(VirtualInjectionSignal.NeutralCurrent, First(source.Waveform.Residual)),
            Wired(VirtualInjectionSignal.PhaseAVoltage, First(source.Waveform.PhaseAVoltage)),
            Wired(VirtualInjectionSignal.PhaseBVoltage, First(source.Waveform.PhaseBVoltage)),
            Wired(VirtualInjectionSignal.PhaseCVoltage, First(source.Waveform.PhaseCVoltage)),
            Wired(VirtualInjectionSignal.NeutralVoltage, First(source.Waveform.ResidualVoltage)),
            phasors?.NeutralCurrentAvailable == true && _topology.IsAnalogConnected(VirtualInjectionSignal.NeutralCurrent),
            phasors?.NeutralVoltageAvailable == true && _topology.IsAnalogConnected(VirtualInjectionSignal.NeutralVoltage),
            source.Measurement.SmvTrust);
    }

    private MeasurementFrame ApplyAnalogWiring(MeasurementFrame source)
    {
        if (source.Phasors is not { } phasors)
        {
            var phaseA = _topology.IsAnalogConnected(VirtualInjectionSignal.PhaseACurrent) ? source.PhaseA : 0;
            var phaseB = _topology.IsAnalogConnected(VirtualInjectionSignal.PhaseBCurrent) ? source.PhaseB : 0;
            var phaseC = _topology.IsAnalogConnected(VirtualInjectionSignal.PhaseCCurrent) ? source.PhaseC : 0;
            var residualAvailable = _topology.IsAnalogConnected(VirtualInjectionSignal.NeutralCurrent) ||
                                    (_topology.IsAnalogConnected(VirtualInjectionSignal.PhaseACurrent) &&
                                     _topology.IsAnalogConnected(VirtualInjectionSignal.PhaseBCurrent) &&
                                     _topology.IsAnalogConnected(VirtualInjectionSignal.PhaseCCurrent));
            return source with
            {
                PhaseA = phaseA,
                PhaseB = phaseB,
                PhaseC = phaseC,
                Residual = residualAvailable ? source.Residual : 0
            };
        }

        Complex Pass(VirtualInjectionSignal signal, Complex value)
            => _topology.IsAnalogConnected(signal) ? value : Complex.Zero;

        var wired = new PhasorMeasurementSet(
            Pass(VirtualInjectionSignal.PhaseACurrent, phasors.PhaseACurrent),
            Pass(VirtualInjectionSignal.PhaseBCurrent, phasors.PhaseBCurrent),
            Pass(VirtualInjectionSignal.PhaseCCurrent, phasors.PhaseCCurrent),
            Pass(VirtualInjectionSignal.NeutralCurrent, phasors.NeutralCurrent),
            Pass(VirtualInjectionSignal.PhaseAVoltage, phasors.PhaseAVoltage),
            Pass(VirtualInjectionSignal.PhaseBVoltage, phasors.PhaseBVoltage),
            Pass(VirtualInjectionSignal.PhaseCVoltage, phasors.PhaseCVoltage),
            Pass(VirtualInjectionSignal.NeutralVoltage, phasors.NeutralVoltage),
            phasors.FrequencyHz)
        {
            NeutralCurrentAvailable = phasors.NeutralCurrentAvailable &&
                                      _topology.IsAnalogConnected(VirtualInjectionSignal.NeutralCurrent),
            NeutralVoltageAvailable = phasors.NeutralVoltageAvailable &&
                                      _topology.IsAnalogConnected(VirtualInjectionSignal.NeutralVoltage)
        };

        return new MeasurementFrame(
            source.Timestamp,
            wired.PhaseACurrent.Magnitude,
            wired.PhaseBCurrent.Magnitude,
            wired.PhaseCCurrent.Magnitude,
            wired.ResidualCurrent.Magnitude,
            source.SmvTrust,
            wired);
    }

    private void BeginTest(DateTimeOffset timestamp)
    {
        _testRunId++;
        _injectionStartedAt = timestamp;
        _pickupDetectedAt = null;
        _tripDetectedAt = null;
        _outputStoppedAt = null;
        _relayPickupDetectedAt = null;
        _relayTripDetectedAt = null;
        _lastObservedAt = timestamp;
        _timerState = VirtualTestSetTimerState.Armed;
        _armBlockReason = null;
        _tripCapture = null;
        _lastRelayPickupRequested = false;
        _lastRelayTripLatched = false;
        _frontEnd.Reset();
        _causalFrontEnd?.Reset();

        if (_metrologyClock is not null)
        {
            _injectionStartUs = _metrologyClock.Microseconds;
            _pickupDetectedUs = null;
            _tripDetectedUs = null;
            _relayPickupDetectedUs = null;
            _relayTripDetectedUs = null;
            _metrologyTimeline.Clear();
            _metrologyPickupInput!.Synchronize(_metrologyClock.Microseconds);
            _metrologyTripInput!.Synchronize(_metrologyClock.Microseconds);
            AddMetrologyEvent(MetrologyEventKind.OutputApplied, _metrologyClock.Microseconds, "Virtual secondary output application defines T0.");
        }
    }

    private void ObserveRelayTiming(ProtectionSnapshot protection, DateTimeOffset timestamp)
    {
        if (_timerState != VirtualTestSetTimerState.Armed ||
            _injectionStartedAt is not { } start)
            return;

        if (_relayPickupDetectedAt is null && HasAnyPickup(protection))
            _relayPickupDetectedAt = timestamp;

        if (protection.LatchedOperation is not { } operation ||
            operation.PickupTimestamp < start ||
            operation.TripTimestamp < start)
            return;

        _relayPickupDetectedAt = operation.PickupTimestamp;
        _relayTripDetectedAt = operation.TripTimestamp;
    }

    private DateTimeOffset TimestampForMetrologyEdge(long absoluteUs)
    {
        if (_injectionStartedAt is not { } start || _injectionStartUs is not { } startUs)
            return _lastObservedAt ?? DateTimeOffset.UtcNow;
        return start + TimeSpan.FromTicks((absoluteUs - startUs) * 10);
    }

    private void AddMetrologyEvent(MetrologyEventKind kind, long absoluteUs, string detail)
    {
        if (_injectionStartUs is not { } startUs)
            return;
        var relativeUs = Math.Max(0, absoluteUs - startUs);
        if (_metrologyTimeline.Count >= 128)
            _metrologyTimeline.RemoveAt(0);
        _metrologyTimeline.Add(new MetrologyEvent(kind, relativeUs, detail));
    }

    private void ClearTestTiming()
    {
        _injectionStartedAt = null;
        _pickupDetectedAt = null;
        _tripDetectedAt = null;
        _outputStoppedAt = null;
        _relayPickupDetectedAt = null;
        _relayTripDetectedAt = null;
        _lastObservedAt = null;
        _injectionStartUs = null;
        _pickupDetectedUs = null;
        _tripDetectedUs = null;
        _relayPickupDetectedUs = null;
        _relayTripDetectedUs = null;
        _lastPickupInput = false;
        _lastTripInput = false;
        _timerState = VirtualTestSetTimerState.Idle;
        _armBlockReason = null;
        _tripCapture = null;
        _metrologyTimeline.Clear();
    }

    private VirtualTestSetTimingSnapshot Snapshot()
    {
        long? Relative(long? absolute)
            => absolute is { } value && _injectionStartUs is { } start ? value - start : null;

        return new VirtualTestSetTimingSnapshot(
            _injectionStartedAt,
            _pickupDetectedAt,
            _tripDetectedAt,
            _outputStoppedAt,
            _lastPickupInput,
            _lastTripInput,
            AutoStopOnTrip,
            _source.IsRunning,
            _topology.Fingerprint(),
            _lastPickupContactRaw,
            _lastTripContactRaw,
            _timerState,
            _armBlockReason,
            _testRunId,
            _relayPickupDetectedAt,
            _relayTripDetectedAt,
            _lastObservedAt,
            _injectionStartUs is null ? null : 0,
            Relative(_pickupDetectedUs),
            Relative(_tripDetectedUs),
            Relative(_relayPickupDetectedUs),
            Relative(_relayTripDetectedUs),
            _metrologyProfile?.BinaryInputSamplePeriodMicroseconds ?? (int)Math.Round(SimulationQuantum.TotalMilliseconds * 1_000),
            _metrologyProfile is null ? null : _metrologyTimeline.ToArray(),
            _metrologyProfile is null ? null : Relative(_metrologyClock?.Microseconds));
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

    private sealed class DelayedBinaryContact
    {
        private readonly TimeSpan _operateDelay;
        private readonly TimeSpan _releaseDelay;
        private readonly TimeSpan _bounceDuration;
        private readonly TimeSpan _bouncePeriod;
        private bool _state;
        private bool _target;
        private DateTimeOffset? _transitionDue;
        private DateTimeOffset? _bounceStartedAt;

        public DelayedBinaryContact(
            TimeSpan operateDelay,
            TimeSpan releaseDelay,
            TimeSpan bounceDuration,
            TimeSpan bouncePeriod)
        {
            _operateDelay = operateDelay;
            _releaseDelay = releaseDelay;
            _bounceDuration = bounceDuration;
            _bouncePeriod = bouncePeriod;
        }

        public bool Update(bool requested, DateTimeOffset timestamp)
        {
            if (requested != _target)
            {
                _target = requested;
                _transitionDue = timestamp + (requested ? _operateDelay : _releaseDelay);
                _bounceStartedAt = null;
            }

            if (_transitionDue is { } due && timestamp >= due)
            {
                _transitionDue = null;
                if (_bounceDuration <= TimeSpan.Zero)
                {
                    _state = _target;
                }
                else
                {
                    _bounceStartedAt = due;
                }
            }

            if (_bounceStartedAt is { } started)
            {
                var elapsed = timestamp - started;
                if (elapsed >= _bounceDuration)
                {
                    _state = _target;
                    _bounceStartedAt = null;
                }
                else
                {
                    var phase = elapsed.Ticks / _bouncePeriod.Ticks;
                    _state = phase % 2 == 0 ? _target : !_target;
                }
            }
            return _state;
        }

        public void Reset()
        {
            _state = false;
            _target = false;
            _transitionDue = null;
            _bounceStartedAt = null;
        }
    }

    private sealed class DebouncedBinaryInput
    {
        private readonly TimeSpan _debounce;
        private bool _state;
        private bool? _candidate;
        private DateTimeOffset? _candidateStableAt;

        public DebouncedBinaryInput(TimeSpan debounce)
        {
            _debounce = debounce;
        }

        public bool Update(bool raw, DateTimeOffset timestamp)
        {
            if (raw == _state)
            {
                _candidate = null;
                _candidateStableAt = null;
                return _state;
            }

            if (_debounce <= TimeSpan.Zero)
            {
                _state = raw;
                _candidate = null;
                _candidateStableAt = null;
                return _state;
            }

            if (_candidate != raw)
            {
                _candidate = raw;
                _candidateStableAt = timestamp + _debounce;
            }

            if (_candidateStableAt is { } stableAt && timestamp >= stableAt)
            {
                _state = raw;
                _candidate = null;
                _candidateStableAt = null;
            }
            return _state;
        }

        public void Reset()
        {
            _state = false;
            _candidate = null;
            _candidateStableAt = null;
        }
    }
}
