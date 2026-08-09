using Arvrel.Protection;

namespace Arvrel.Application.Laboratory;

/// <summary>
/// Correlates the relay element that actually produced the latched operation with the
/// independent TESTSET clock. TESTSET.BI2 intentionally remains a generic ANY-PICKUP
/// contact and can therefore precede the pickup of the element that ultimately trips.
/// </summary>
public sealed record RelayOperationTimingCorrelation(
    string Element,
    TimeSpan ElementPickupFromStart,
    TimeSpan OperationRecordTripFromStart,
    TimeSpan ElementPickupToTrip,
    TimeSpan? LiveTripRequestFromStart,
    TimeSpan? LiveTripRequestToBi1,
    TimeSpan? OperationRecordToLiveTripRequest)
{
    public bool LiveTripMatchesOperationRecord(TimeSpan tolerance)
        => OperationRecordToLiveTripRequest is not { } difference ||
           difference.Duration() <= tolerance;
}

public static class RelayOperationTimingCorrelator
{
    public static RelayOperationTimingCorrelation? Correlate(
        VirtualTestSetTimingSnapshot testSet,
        ProtectionSnapshot protection)
    {
        ArgumentNullException.ThrowIfNull(testSet);
        ArgumentNullException.ThrowIfNull(protection);

        if (testSet.InjectionStartedAt is not { } start ||
            protection.LatchedOperation is not { } operation ||
            operation.PickupTimestamp < start ||
            operation.TripTimestamp < start)
        {
            return null;
        }

        var pickupFromStart = operation.PickupTimestamp - start;
        var recordTripFromStart = operation.TripTimestamp - start;
        var pickupToTrip = operation.TripTimestamp - operation.PickupTimestamp;
        var liveTripFromStart = testSet.RelayTripTime;
        var liveToBi1 = testSet.RelayTripToBi1;
        TimeSpan? recordToLive = liveTripFromStart is { } live
            ? live - recordTripFromStart
            : null;

        return new RelayOperationTimingCorrelation(
            operation.Element,
            pickupFromStart,
            recordTripFromStart,
            pickupToTrip,
            liveTripFromStart,
            liveToBi1,
            recordToLive);
    }
}
