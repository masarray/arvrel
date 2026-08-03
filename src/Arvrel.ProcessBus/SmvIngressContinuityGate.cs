namespace Arvrel.ProcessBus;

internal enum SmvIngressDecision
{
    Accept,
    RestartWindow,
    DropDuplicate,
    DropOutOfOrder
}

internal sealed class SmvIngressContinuityGate
{
    private ushort? _lastAccepted;

    public SmvIngressDecision Observe(ushort current, ushort wrap)
    {
        if (wrap < 2)
            throw new ArgumentOutOfRangeException(nameof(wrap));

        if (!_lastAccepted.HasValue)
        {
            _lastAccepted = current;
            return SmvIngressDecision.Accept;
        }

        var previous = _lastAccepted.Value;
        var expected = (ushort)((previous + 1) % wrap);
        if (current == expected)
        {
            _lastAccepted = current;
            return SmvIngressDecision.Accept;
        }

        if (current == previous)
            return SmvIngressDecision.DropDuplicate;

        var forward = (current - expected + wrap) % wrap;
        if (forward < wrap / 2)
        {
            _lastAccepted = current;
            return SmvIngressDecision.RestartWindow;
        }

        return SmvIngressDecision.DropOutOfOrder;
    }

    public void Reset() => _lastAccepted = null;
}
