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
    /// <summary>
    /// Compatibility profile used by the P0/P1 regression suite. It retains the
    /// original operate/release latency without bounce or input debounce.
    /// </summary>
    public static VirtualRelayContactProfile NumericalRelayDefault { get; } = new(
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(3),
        TimeSpan.FromMilliseconds(1));

    /// <summary>
    /// Desktop realism profile. The relay contact is allowed to chatter briefly and
    /// the virtual test-set binary input requires a stable state before accepting it.
    /// </summary>
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
    bool TripContactRaw = false)
{
    public TimeSpan? PickupTime => InjectionStartedAt is { } start && PickupDetectedAt is { } pickup
        ? pickup - start
        : null;

    public TimeSpan? TripTime => InjectionStartedAt is { } start && TripDetectedAt is { } trip
        ? trip - start
        : null;
}

public sealed record ClosedLoopVirtualTestBenchStep(
    ScenarioStep Source,
    MeasurementFrame RelayMeasurement,
    ProtectionSnapshot Protection,
    VirtualTestSetTimingSnapshot TestSet);

/// <summary>
/// Deterministic black-box laboratory backplane between the virtual secondary injector
/// and the virtual protection relay. Analog measurements cross typed virtual wires and
/// then pass through a configurable numerical-relay front end before protection sees
/// them. Relay pickup/trip outputs cross delayed/bouncing virtual contacts, separate
/// binary wires and test-set input debounce before they become external test feedback.
/// The test set never reads TripLatched directly to stop output or measure time.
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
    private VirtualTestBenchTopology _topology;
    private DateTimeOffset? _injectionStartedAt;
    private DateTimeOffset? _pickupDetectedAt;
    private DateTimeOffset? _tripDetectedAt;
    private DateTimeOffset? _outputStoppedAt;
    private bool _lastPickupInput;
    private bool _lastTripInput;
    private bool _lastPickupContactRaw;
    private bool _lastTripContactRaw;
    private ProtectionSnapshot _lastProtection;

    public ClosedLoopVirtualTestBench(
        DeterministicLabScenario source,
        ResearchProtectionEngine relay,
        VirtualTestBenchTopology? topology = null,
        VirtualRelayContactProfile? contactProfile = null,
        VirtualRelayFrontEndProfile? frontEndProfile = null)
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
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public VirtualTestBenchTopology Topology => _topology;
    public VirtualRelayContactProfile ContactProfile { get; }
    public VirtualRelayFrontEndProfile FrontEndProfile { get; }
    public VirtualRelayFrontEndSnapshot FrontEndSnapshot => _frontEnd.Snapshot;
    public bool AutoStopOnTrip { get; set; } = true;
    public ProtectionSnapshot LastProtection => _lastProtection;
    public VirtualTestSetTimingSnapshot TestSetSnapshot => Snapshot();

    public void SetWireConnected(string wireId, bool connected)
        => _topology = _topology.WithWireConnection(wireId, connected);

    public bool StartInjection()
    {
        var changed = _source.StartInjection();
        if (!changed)
            return false;
        BeginTest(_source.OutputStateChangedAt);
        return true;
    }

    public bool StopInjection()
    {
        var changed = _source.StopInjection();
        if (changed)
            _outputStoppedAt ??= _source.OutputStateChangedAt;
        return changed;
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
        ResetObserverState();
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears only the external test-set/contact observer. The injector profile,
    /// relay settings, analog front-end state and relay algorithm state remain owned
    /// by their existing reset/apply workflows.
    /// </summary>
    public void ResetObserverState()
    {
        _pickupContact.Reset();
        _tripContact.Reset();
        _pickupInput.Reset();
        _tripInput.Reset();
        _lastPickupContactRaw = false;
        _lastTripContactRaw = false;
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

        var remaining = delta;
        ClosedLoopVirtualTestBenchStep? last = null;
        while (remaining > TimeSpan.Zero)
        {
            var step = remaining > SimulationQuantum ? SimulationQuantum : remaining;
            last = AdvanceQuantum(step);
            remaining -= step;
        }
        return last!;
    }

    private ClosedLoopVirtualTestBenchStep AdvanceQuantum(TimeSpan delta)
    {
        var sourceStep = _source.Advance(delta);
        var terminalMeasurement = ApplyAnalogWiring(sourceStep.Measurement);
        var relayMeasurement = _frontEnd.Process(terminalMeasurement);
        var protection = _relay.Evaluate(relayMeasurement);
        _lastProtection = protection;

        var pickupRequested = HasAnyPickup(protection);
        var timestamp = relayMeasurement.Timestamp;
        _lastPickupContactRaw = _pickupContact.Update(pickupRequested, timestamp);
        _lastTripContactRaw = _tripContact.Update(protection.TripLatched, timestamp);

        var wiredPickup = _topology.IsBinaryConnected(VirtualBinarySignal.Pickup) && _lastPickupContactRaw;
        var wiredTrip = _topology.IsBinaryConnected(VirtualBinarySignal.Trip) && _lastTripContactRaw;
        var pickupInput = _pickupInput.Update(wiredPickup, timestamp);
        var tripInput = _tripInput.Update(wiredTrip, timestamp);

        if (pickupInput && !_lastPickupInput && _injectionStartedAt is not null)
            _pickupDetectedAt ??= timestamp;

        if (tripInput && !_lastTripInput && _injectionStartedAt is not null)
        {
            _tripDetectedAt ??= timestamp;
            if (AutoStopOnTrip && _source.IsRunning)
            {
                _source.StopInjection();
                _outputStoppedAt ??= _source.OutputStateChangedAt;
            }
        }

        _lastPickupInput = pickupInput;
        _lastTripInput = tripInput;
        return new ClosedLoopVirtualTestBenchStep(sourceStep, relayMeasurement, protection, Snapshot());
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
        _injectionStartedAt = timestamp;
        _pickupDetectedAt = null;
        _tripDetectedAt = null;
        _outputStoppedAt = null;
        _lastPickupInput = false;
        _lastTripInput = false;
        _lastPickupContactRaw = false;
        _lastTripContactRaw = false;
        _frontEnd.Reset();
        _pickupContact.Reset();
        _tripContact.Reset();
        _pickupInput.Reset();
        _tripInput.Reset();
    }

    private void ClearTestTiming()
    {
        _injectionStartedAt = null;
        _pickupDetectedAt = null;
        _tripDetectedAt = null;
        _outputStoppedAt = null;
        _lastPickupInput = false;
        _lastTripInput = false;
    }

    private VirtualTestSetTimingSnapshot Snapshot()
        => new(
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
            _lastTripContactRaw);

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
