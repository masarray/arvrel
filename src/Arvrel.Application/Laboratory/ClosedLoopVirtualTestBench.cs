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
    TimeSpan ReleaseDelay)
{
    public static VirtualRelayContactProfile NumericalRelayDefault { get; } = new(
        TimeSpan.FromMilliseconds(1),
        TimeSpan.FromMilliseconds(3),
        TimeSpan.FromMilliseconds(1));

    public void Validate()
    {
        ValidateDelay(PickupOperateDelay, nameof(PickupOperateDelay));
        ValidateDelay(TripOperateDelay, nameof(TripOperateDelay));
        ValidateDelay(ReleaseDelay, nameof(ReleaseDelay));
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
    string TopologyFingerprint)
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
/// and the virtual protection relay. Analog measurements cross typed virtual wires.
/// Relay pickup/trip outputs cross delayed virtual contacts and separate binary wires
/// before the test set can observe them. The test set never reads TripLatched directly
/// to stop output or measure time.
/// </summary>
public sealed class ClosedLoopVirtualTestBench
{
    public static readonly TimeSpan SimulationQuantum = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 4_000);

    private readonly DeterministicLabScenario _source;
    private readonly ResearchProtectionEngine _relay;
    private readonly DelayedBinaryContact _pickupContact;
    private readonly DelayedBinaryContact _tripContact;
    private VirtualTestBenchTopology _topology;
    private DateTimeOffset? _injectionStartedAt;
    private DateTimeOffset? _pickupDetectedAt;
    private DateTimeOffset? _tripDetectedAt;
    private DateTimeOffset? _outputStoppedAt;
    private bool _lastPickupInput;
    private bool _lastTripInput;
    private ProtectionSnapshot _lastProtection;

    public ClosedLoopVirtualTestBench(
        DeterministicLabScenario source,
        ResearchProtectionEngine relay,
        VirtualTestBenchTopology? topology = null,
        VirtualRelayContactProfile? contactProfile = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        _topology = topology ?? VirtualTestBenchTopology.Default;
        ContactProfile = contactProfile ?? VirtualRelayContactProfile.NumericalRelayDefault;
        ContactProfile.Validate();
        _pickupContact = new DelayedBinaryContact(ContactProfile.PickupOperateDelay, ContactProfile.ReleaseDelay);
        _tripContact = new DelayedBinaryContact(ContactProfile.TripOperateDelay, ContactProfile.ReleaseDelay);
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    public VirtualTestBenchTopology Topology => _topology;
    public VirtualRelayContactProfile ContactProfile { get; }
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
        ResetObserverState();
        _lastProtection = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears only the external test-set/contact observer. The injector profile,
    /// relay settings and relay algorithm state remain owned by their existing
    /// reset/apply workflows.
    /// </summary>
    public void ResetObserverState()
    {
        _pickupContact.Reset();
        _tripContact.Reset();
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
        var relayMeasurement = ApplyAnalogWiring(sourceStep.Measurement);
        var protection = _relay.Evaluate(relayMeasurement);
        _lastProtection = protection;

        var pickupRequested = HasAnyPickup(protection);
        var timestamp = relayMeasurement.Timestamp;
        var pickupContact = _pickupContact.Update(pickupRequested, timestamp);
        var tripContact = _tripContact.Update(protection.TripLatched, timestamp);
        var pickupInput = _topology.IsBinaryConnected(VirtualBinarySignal.Pickup) && pickupContact;
        var tripInput = _topology.IsBinaryConnected(VirtualBinarySignal.Trip) && tripContact;

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
        _pickupContact.Reset();
        _tripContact.Reset();
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
            _topology.Fingerprint());

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
        private bool _state;
        private bool _target;
        private DateTimeOffset? _transitionDue;

        public DelayedBinaryContact(TimeSpan operateDelay, TimeSpan releaseDelay)
        {
            _operateDelay = operateDelay;
            _releaseDelay = releaseDelay;
        }

        public bool Update(bool requested, DateTimeOffset timestamp)
        {
            if (requested != _target)
            {
                _target = requested;
                _transitionDue = timestamp + (requested ? _operateDelay : _releaseDelay);
            }

            if (_transitionDue is { } due && timestamp >= due)
            {
                _state = _target;
                _transitionDue = null;
            }
            return _state;
        }

        public void Reset()
        {
            _state = false;
            _target = false;
            _transitionDue = null;
        }
    }
}
