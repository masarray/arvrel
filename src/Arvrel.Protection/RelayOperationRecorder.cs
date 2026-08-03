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

/// <summary>
/// Projects engine-owned protection evidence into relay-faceplate transitions.
/// The protection engine captures pickup/trip timestamps, quantities and causes at
/// evaluation time; this recorder therefore remains correct even when the UI polls
/// after a short fault has already dropped out.
/// </summary>
public sealed class RelayOperationRecorder
{
    private string? _lastPickupElement;
    private bool _lastTrip;
    private bool _lastBlocked;
    private DateTimeOffset? _lastConsumedTripTimestamp;
    private int _sequence;
    private RelayOperationRecord? _pending;
    private RelayOperationRecord? _lastCompleted;

    /// <summary>
    /// The last completed trip remains visible until a source reset. Before the first
    /// completed operation, the currently timing pickup is returned.
    /// </summary>
    public RelayOperationRecord? Current => _lastCompleted ?? _pending;

    public RelayOperationTransition Observe(ProtectionSnapshot snapshot, MeasurementFrame measurement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(measurement.SmvTrust);

        var blockedStarted = snapshot.Blocked && !_lastBlocked;
        var resetObserved = !snapshot.TripLatched && _lastTrip;
        var pickupStarted = false;
        var tripOccurred = false;
        RelayOperationRecord? transitionOperation = null;

        if (snapshot.LatchedOperation is { } evidence &&
            (!_lastConsumedTripTimestamp.HasValue ||
             _lastConsumedTripTimestamp.Value != evidence.TripTimestamp))
        {
            var sequence = _pending is not null &&
                           string.Equals(_pending.PickupElement, evidence.Element, StringComparison.Ordinal) &&
                           _pending.PickupTimestamp == evidence.PickupTimestamp
                ? _pending.Sequence
                : ++_sequence;

            pickupStarted = _pending is null ||
                            !string.Equals(_pending.PickupElement, evidence.Element, StringComparison.Ordinal) ||
                            _pending.PickupTimestamp != evidence.PickupTimestamp;

            var completed = new RelayOperationRecord(
                sequence,
                evidence.PickupTimestamp,
                evidence.TripTimestamp,
                evidence.Element,
                evidence.Element,
                evidence.PickupQuantity,
                evidence.TripQuantity,
                evidence.QuantitySymbol,
                evidence.QuantityUnit);

            _lastCompleted = completed;
            _pending = null;
            _lastConsumedTripTimestamp = evidence.TripTimestamp;
            _lastPickupElement = null;
            tripOccurred = true;
            transitionOperation = completed;
        }
        else if (!snapshot.TripLatched)
        {
            var pickupElement = ResolvePickupElement(snapshot);
            if (pickupElement is not null &&
                !string.Equals(pickupElement, _lastPickupElement, StringComparison.Ordinal))
            {
                var quantity = ResolveOperatingQuantity(pickupElement, snapshot, measurement);
                _pending = new RelayOperationRecord(
                    ++_sequence,
                    snapshot.Timestamp,
                    null,
                    pickupElement,
                    string.Empty,
                    quantity.Value,
                    0,
                    quantity.Symbol,
                    quantity.Unit);
                pickupStarted = true;
                transitionOperation = _pending;
            }

            if (pickupElement is null)
                _pending = null;
            _lastPickupElement = pickupElement;
        }
        else if (snapshot.TripLatched && !_lastTrip)
        {
            // Compatibility fallback for snapshots produced by older evidence files.
            var element = ResolveElement(snapshot);
            var quantity = ResolveOperatingQuantity(element, snapshot, measurement);
            var pendingMatches = _pending is not null &&
                                 string.Equals(_pending.PickupElement, element, StringComparison.Ordinal);
            pickupStarted = !pendingMatches;
            var completed = new RelayOperationRecord(
                pendingMatches ? _pending!.Sequence : ++_sequence,
                pendingMatches ? _pending!.PickupTimestamp : snapshot.Timestamp,
                snapshot.Timestamp,
                element,
                element,
                pendingMatches ? _pending!.PickupQuantity : quantity.Value,
                quantity.Value,
                quantity.Symbol,
                quantity.Unit);
            _lastCompleted = completed;
            _pending = null;
            tripOccurred = true;
            transitionOperation = completed;
        }

        _lastTrip = snapshot.TripLatched;
        _lastBlocked = snapshot.Blocked;

        return new RelayOperationTransition(
            pickupStarted,
            tripOccurred,
            blockedStarted,
            resetObserved,
            transitionOperation ?? Current);
    }

    public void ResetCurrent()
    {
        _pending = null;
        _lastCompleted = null;
        _lastPickupElement = null;
        _lastTrip = false;
        _lastBlocked = false;
        _lastConsumedTripTimestamp = null;
    }

    private static string? ResolvePickupElement(ProtectionSnapshot snapshot)
    {
        var elements = EnumerateElements(snapshot);
        var operated = elements.FirstOrDefault(element => element.Snapshot.Operated);
        if (operated.Code is not null)
            return operated.Code;
        var pickup = elements.FirstOrDefault(element => element.Snapshot.Pickup);
        return pickup.Code;
    }

    private static string ResolveElement(ProtectionSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.ActiveElement) &&
            !string.Equals(snapshot.ActiveElement, "NONE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snapshot.ActiveElement, "READY", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snapshot.ActiveElement, "PHASOR UNAVAILABLE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(snapshot.ActiveElement, "SMV BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeElement(snapshot.ActiveElement);
        }

        return ResolvePickupElement(snapshot) ?? "UNKNOWN";
    }

    private static IEnumerable<(string? Code, ElementSnapshot Snapshot)> EnumerateElements(ProtectionSnapshot snapshot)
    {
        yield return ("50P-1", snapshot.Phase50);
        yield return ("51P", snapshot.Phase51);
        yield return ("50N", snapshot.Earth50);
        yield return ("51N", snapshot.Earth51);
        yield return ("67P", snapshot.Feeder.DirectionalPhase67);
        yield return ("67N", snapshot.Feeder.DirectionalEarth67N);
        yield return ("27", snapshot.Feeder.Undervoltage27);
        yield return ("59", snapshot.Feeder.Overvoltage59);
        yield return ("59N", snapshot.Feeder.ResidualOvervoltage59N);
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
            "50N" or "51N" or "67N" => new OperatingQuantity(
                snapshot.Feeder.DirectionalEarth67N.OperatingQuantity > 0 && element == "67N"
                    ? snapshot.Feeder.DirectionalEarth67N.OperatingQuantity
                    : measurement.Residual,
                "3I0",
                "A"),
            "27" => new OperatingQuantity(snapshot.Feeder.Undervoltage27.OperatingQuantity, "V OP", "V"),
            "59" => new OperatingQuantity(snapshot.Feeder.Overvoltage59.OperatingQuantity, "V OP", "V"),
            "59N" => new OperatingQuantity(snapshot.Feeder.ResidualOvervoltage59N.OperatingQuantity, "3V0", "V"),
            "67P" => new OperatingQuantity(snapshot.Feeder.DirectionalPhase67.OperatingQuantity, "I OP", "A"),
            _ => new OperatingQuantity(measurement.MaximumPhase, "I OP", "A")
        };
    }

    private readonly record struct OperatingQuantity(double Value, string Symbol, string Unit);
}
