namespace Arvrel.Protection;

public enum RelayLampState
{
    Off,
    Pickup,
    Trip
}

public sealed record RelayAnnunciationSnapshot(
    bool PickupActive,
    bool TripLatched,
    RelayLampState PhaseA,
    RelayLampState PhaseB,
    RelayLampState PhaseC,
    RelayLampState Earth);

/// <summary>
/// Models numerical-relay annunciation behaviour independently from the WPF renderer.
/// Pickup indications are momentary amber indications. The phase/earth causes captured
/// at the trip transition remain latched red until the protection trip latch is reset.
/// </summary>
public sealed class RelayAnnunciationLatch
{
    private bool _tripWasLatched;
    private bool _phaseATripLatched;
    private bool _phaseBTripLatched;
    private bool _phaseCTripLatched;
    private bool _earthTripLatched;

    public RelayAnnunciationSnapshot Observe(ProtectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (_tripWasLatched && !snapshot.TripLatched)
            Reset();

        var anyPickup = HasAnyPickup(snapshot);
        if (snapshot.TripLatched && !_tripWasLatched)
        {
            _phaseATripLatched = snapshot.PhaseAPickup;
            _phaseBTripLatched = snapshot.PhaseBPickup;
            _phaseCTripLatched = snapshot.PhaseCPickup;
            _earthTripLatched = snapshot.EarthPickup;
        }

        _tripWasLatched = snapshot.TripLatched;
        return new RelayAnnunciationSnapshot(
            PickupActive: anyPickup && !snapshot.TripLatched,
            TripLatched: snapshot.TripLatched,
            PhaseA: ResolveElementState(snapshot.PhaseAPickup, _phaseATripLatched, snapshot.TripLatched),
            PhaseB: ResolveElementState(snapshot.PhaseBPickup, _phaseBTripLatched, snapshot.TripLatched),
            PhaseC: ResolveElementState(snapshot.PhaseCPickup, _phaseCTripLatched, snapshot.TripLatched),
            Earth: ResolveElementState(snapshot.EarthPickup, _earthTripLatched, snapshot.TripLatched));
    }

    public void Reset()
    {
        _tripWasLatched = false;
        _phaseATripLatched = false;
        _phaseBTripLatched = false;
        _phaseCTripLatched = false;
        _earthTripLatched = false;
    }

    private static RelayLampState ResolveElementState(bool livePickup, bool tripCauseLatched, bool tripLatched)
    {
        if (tripCauseLatched)
            return RelayLampState.Trip;
        if (!tripLatched && livePickup)
            return RelayLampState.Pickup;
        return RelayLampState.Off;
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
}
