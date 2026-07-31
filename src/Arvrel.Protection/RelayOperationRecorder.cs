namespace Arvrel.Protection;

public sealed record RelayOperationRecord(
    int Sequence,
    DateTimeOffset PickupTimestamp,
    DateTimeOffset? TripTimestamp,
    string PickupElement,
    string TripElement,
    double PickupCurrentA,
    double TripCurrentA)
{
    public TimeSpan? OperateTime => TripTimestamp is { } trip ? trip - PickupTimestamp : null;
}

public sealed record RelayOperationTransition(
    bool PickupStarted,
    bool TripOccurred,
    bool BlockedStarted,
    bool ResetObserved,
    RelayOperationRecord? Operation);

public sealed class RelayOperationRecorder
{
    private bool _lastPickup;
    private bool _lastTrip;
    private bool _lastBlocked;
    private int _sequence;

    public RelayOperationRecord? Current { get; private set; }

    public RelayOperationTransition Observe(ProtectionSnapshot snapshot, MeasurementFrame measurement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(measurement.SmvTrust);

        var pickup = snapshot.Phase50.Pickup || snapshot.Phase51.Pickup || snapshot.Earth50.Pickup || snapshot.Earth51.Pickup;
        var pickupStarted = pickup && !_lastPickup;
        var tripOccurred = snapshot.TripLatched && !_lastTrip;
        var blockedStarted = snapshot.Blocked && !_lastBlocked;
        var resetObserved = !snapshot.TripLatched && _lastTrip;

        if (pickupStarted)
        {
            var element = ResolveElement(snapshot);
            Current = new RelayOperationRecord(++_sequence, snapshot.Timestamp, null, element, string.Empty,
                ResolveOperatingQuantity(element, measurement), 0);
        }

        if (tripOccurred)
        {
            var element = ResolveElement(snapshot);
            var quantity = ResolveOperatingQuantity(element, measurement);
            Current ??= new RelayOperationRecord(++_sequence, snapshot.Timestamp, null, element, string.Empty, quantity, 0);
            Current = Current with { TripTimestamp = snapshot.Timestamp, TripElement = element, TripCurrentA = quantity };
        }

        if (!pickup && !snapshot.TripLatched && Current?.TripTimestamp is null)
            Current = null;
        if (resetObserved)
            Current = null;

        _lastPickup = pickup;
        _lastTrip = snapshot.TripLatched;
        _lastBlocked = snapshot.Blocked;

        return new RelayOperationTransition(pickupStarted, tripOccurred, blockedStarted, resetObserved, Current);
    }

    public void ResetCurrent()
    {
        Current = null;
        _lastPickup = false;
        _lastTrip = false;
        _lastBlocked = false;
    }

    private static string ResolveElement(ProtectionSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.ActiveElement) &&
            !string.Equals(snapshot.ActiveElement, "NONE", StringComparison.OrdinalIgnoreCase))
            return snapshot.ActiveElement;
        if (snapshot.Phase50.Pickup || snapshot.Phase50.Operated) return "50P-1";
        if (snapshot.Phase51.Pickup || snapshot.Phase51.Operated) return "51P";
        if (snapshot.Earth50.Pickup || snapshot.Earth50.Operated) return "50N";
        if (snapshot.Earth51.Pickup || snapshot.Earth51.Operated) return "51N";
        return "UNKNOWN";
    }

    private static double ResolveOperatingQuantity(string element, MeasurementFrame measurement)
        => element is "50N" or "51N" ? measurement.Residual : measurement.MaximumPhase;
}
