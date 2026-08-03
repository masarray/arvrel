namespace Arvrel.ProcessBus;

internal enum SmvIngressDecision
{
    Accept,
    RestartWindow,
    DropDuplicate,
    DropOutOfOrder
}

internal readonly record struct SmvIngressResult(
    SmvIngressDecision Decision,
    ushort SampleCount);

internal sealed class SmvIngressContinuityGate
{
    private ushort? _lastAccepted;

    public SmvIngressDecision Observe(ushort current, ushort wrap)
        => ObserveFrame(new[] { current }, wrap).Decision;

    public SmvIngressResult ObserveFrame(IReadOnlyList<ushort> counters, ushort wrap)
    {
        ArgumentNullException.ThrowIfNull(counters);
        if (counters.Count == 0)
            throw new ArgumentException("At least one smpCnt value is required.", nameof(counters));
        if (wrap < 2)
            throw new ArgumentOutOfRangeException(nameof(wrap));

        var localLast = _lastAccepted;
        var aggregate = SmvIngressDecision.Accept;

        foreach (var current in counters)
        {
            if (!localLast.HasValue)
            {
                localLast = current;
                continue;
            }

            var previous = localLast.Value;
            var expected = (ushort)((previous + 1) % wrap);
            if (current == expected)
            {
                localLast = current;
                continue;
            }

            if (current == previous)
                return new SmvIngressResult(SmvIngressDecision.DropDuplicate, current);

            var forward = (current - expected + wrap) % wrap;
            if (forward < wrap / 2)
            {
                aggregate = SmvIngressDecision.RestartWindow;
                localLast = current;
                continue;
            }

            return new SmvIngressResult(SmvIngressDecision.DropOutOfOrder, current);
        }

        _lastAccepted = localLast;
        return new SmvIngressResult(aggregate, counters[^1]);
    }

    public void Reset() => _lastAccepted = null;
}
