namespace Arvrel.Protection;

public sealed record RelayOperationRecord(
    int Sequence,
    DateTimeOffset PickupTimestamp,
    DateTimeOffset? TripTimestamp,
    string PickupElement,
    string TripElement,
    double PickupQuantity,
    double TripQuantity,
    string QuantitySymbol,
    string QuantityUnit)
{
    public TimeSpan? OperateTime => TripTimestamp is { } trip ? trip - PickupTimestamp : null;

    // Compatibility aliases retained for existing evidence/UI consumers.
    public double PickupCurrentA => PickupQuantity;
    public double TripCurrentA => TripQuantity;
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

        var pickup = HasPickup(snapshot);
        var pickupStarted = pickup && !_lastPickup;
        var tripOccurred = snapshot.TripLatched && !_lastTrip;
        var blockedStarted = snapshot.Blocked && !_lastBlocked;
        var resetObserved = !snapshot.TripLatched && _lastTrip;

        if (pickupStarted)
        {
            var element = ResolveElement(snapshot);
            var quantity = ResolveOperatingQuantity(element, snapshot, measurement);
            Current = new RelayOperationRecord(
                ++_sequence,
                snapshot.Timestamp,
                null,
                element,
                string.Empty,
                quantity.Value,
                0,
                quantity.Symbol,
                quantity.Unit);
        }

        if (tripOccurred)
        {
            var element = ResolveElement(snapshot);
            var quantity = ResolveOperatingQuantity(element, snapshot, measurement);
            Current ??= new RelayOperationRecord(
                ++_sequence,
                snapshot.Timestamp,
                null,
                element,
                string.Empty,
                quantity.Value,
                0,
                quantity.Symbol,
                quantity.Unit);
            Current = Current with
            {
                TripTimestamp = snapshot.Timestamp,
                TripElement = element,
                TripQuantity = quantity.Value,
                QuantitySymbol = quantity.Symbol,
                QuantityUnit = quantity.Unit
            };
        }

        if (!pickup && !snapshot.TripLatched && Current?.TripTimestamp is null)
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

    private static bool HasPickup(ProtectionSnapshot snapshot)
        => snapshot.Phase50.Pickup ||
           snapshot.Phase51.Pickup ||
           snapshot.Earth50.Pickup ||
           snapshot.Earth51.Pickup ||
           snapshot.Feeder.DirectionalPhase67.Pickup ||
           snapshot.Feeder.DirectionalEarth67N.Pickup ||
           snapshot.Feeder.Undervoltage27.Pickup ||
           snapshot.Feeder.Overvoltage59.Pickup ||
           snapshot.Feeder.ResidualOvervoltage59N.Pickup;

    private static string ResolveElement(ProtectionSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.ActiveElement) &&
            !string.Equals(snapshot.ActiveElement, "NONE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snapshot.ActiveElement, "READY", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snapshot.ActiveElement, "PHASOR UNAVAILABLE", StringComparison.OrdinalIgnoreCase))
            return NormalizeElement(snapshot.ActiveElement);

        if (snapshot.Phase50.Pickup || snapshot.Phase50.Operated) return "50P-1";
        if (snapshot.Phase51.Pickup || snapshot.Phase51.Operated) return "51P";
        if (snapshot.Earth50.Pickup || snapshot.Earth50.Operated) return "50N";
        if (snapshot.Earth51.Pickup || snapshot.Earth51.Operated) return "51N";
        if (snapshot.Feeder.DirectionalPhase67.Pickup || snapshot.Feeder.DirectionalPhase67.Operated) return "67P";
        if (snapshot.Feeder.DirectionalEarth67N.Pickup || snapshot.Feeder.DirectionalEarth67N.Operated) return "67N";
        if (snapshot.Feeder.Undervoltage27.Pickup || snapshot.Feeder.Undervoltage27.Operated) return "27";
        if (snapshot.Feeder.Overvoltage59.Pickup || snapshot.Feeder.Overvoltage59.Operated) return "59";
        if (snapshot.Feeder.ResidualOvervoltage59N.Pickup || snapshot.Feeder.ResidualOvervoltage59N.Operated) return "59N";
        return "UNKNOWN";
    }

    private static string NormalizeElement(string activeElement)
        => activeElement.Replace(" PICKUP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" START", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static OperatingQuantity ResolveOperatingQuantity(
        string element,
        ProtectionSnapshot snapshot,
        MeasurementFrame measurement)
    {
        return element switch
        {
            "50N" or "51N" or "67N" => new OperatingQuantity(measurement.Residual, "3I0", "A"),
            "27" => new OperatingQuantity(snapshot.Feeder.Undervoltage27.OperatingQuantity, "V OP", "V"),
            "59" => new OperatingQuantity(snapshot.Feeder.Overvoltage59.OperatingQuantity, "V OP", "V"),
            "59N" => new OperatingQuantity(snapshot.Feeder.ResidualOvervoltage59N.OperatingQuantity, "3V0", "V"),
            "67P" => new OperatingQuantity(snapshot.Feeder.DirectionalPhase67.OperatingQuantity, "I OP", "A"),
            _ => new OperatingQuantity(measurement.MaximumPhase, "I OP", "A")
        };
    }

    private readonly record struct OperatingQuantity(double Value, string Symbol, string Unit);
}
