using System;
using System.Collections.Generic;
using System.Linq;
using Arvrel.Protection;

namespace Arvrel.App.Controls.VirtualRelay;

public enum VirtualRelayIoAuthority
{
    SimulatedHmiOnly,
    ProtectionAuthoritative
}

public readonly record struct VirtualRelayAccessLevel
{
    private VirtualRelayAccessLevel(int rank, string name)
    {
        Rank = rank;
        Name = name;
    }

    public int Rank { get; }
    public string Name { get; }

    public static VirtualRelayAccessLevel View { get; } = new(0, "View");
    public static VirtualRelayAccessLevel Operator { get; } = new(1, "Operator");
    public static VirtualRelayAccessLevel Engineer { get; } = new(2, "Engineer");

    public static explicit operator VirtualRelayAccessLevel(int rank) => rank switch
    {
        0 => View,
        1 => Operator,
        2 => Engineer,
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public static explicit operator int(VirtualRelayAccessLevel level) => level.Rank;

    public static bool operator >=(VirtualRelayAccessLevel left, VirtualRelayAccessLevel right)
        => left.Rank >= right.Rank;

    public static bool operator <=(VirtualRelayAccessLevel left, VirtualRelayAccessLevel right)
        => left.Rank <= right.Rank;

    public static bool operator >(VirtualRelayAccessLevel left, VirtualRelayAccessLevel right)
        => left.Rank > right.Rank;

    public static bool operator <(VirtualRelayAccessLevel left, VirtualRelayAccessLevel right)
        => left.Rank < right.Rank;

    public override string ToString() => Name;
}

public enum VirtualRelayLedSignal
{
    PhaseA,
    PhaseB,
    PhaseC,
    Earth,
    Pickup,
    Trip,
    SmvBlock,
    Healthy,
    ExternalAlarm,
    TestMode,
    LocalMode,
    SettingGroupB
}

public enum VirtualRelayAlarmSeverity
{
    Info,
    Warning,
    Trip
}

public sealed record VirtualRelayBinaryPoint(
    int Number,
    string Name,
    bool State,
    bool Writable,
    VirtualRelayIoAuthority Authority,
    string Detail);

public sealed class VirtualRelayOperatorIoModel
{
    public bool TestMode { get; private set; }
    public bool ExternalAlarm { get; private set; }
    public bool LocalMode { get; private set; } = true;
    public bool Breaker52A { get; private set; } = true;

    public IReadOnlyList<VirtualRelayBinaryPoint> Inputs()
        => new[]
        {
            new VirtualRelayBinaryPoint(1, "TEST MODE", TestMode, true, VirtualRelayIoAuthority.SimulatedHmiOnly,
                "Virtual laboratory contact; not wired into protection equations."),
            new VirtualRelayBinaryPoint(2, "EXT ALARM", ExternalAlarm, true, VirtualRelayIoAuthority.SimulatedHmiOnly,
                "Virtual laboratory alarm contact; drives HMI alarm/LED evidence only."),
            new VirtualRelayBinaryPoint(3, "LOCAL MODE", LocalMode, true, VirtualRelayIoAuthority.SimulatedHmiOnly,
                "Virtual local/remote selector for operator-experience training."),
            new VirtualRelayBinaryPoint(4, "CB 52A", Breaker52A, true, VirtualRelayIoAuthority.SimulatedHmiOnly,
                "Virtual breaker auxiliary contact for front-panel training only.")
        };

    public IReadOnlyList<VirtualRelayBinaryPoint> Outputs(ProtectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var pickup = HasAnyPickup(snapshot);
        var healthy = snapshot.SmvTrust.AllowsMeasurement &&
                      snapshot.SmvTrust.AllowsPickup &&
                      snapshot.SmvTrust.AllowsTrip &&
                      !snapshot.Blocked;

        return new[]
        {
            new VirtualRelayBinaryPoint(1, "VIRTUAL TRIP", snapshot.TripLatched, false,
                VirtualRelayIoAuthority.ProtectionAuthoritative,
                "Authoritative protection trip latch; virtual output only."),
            new VirtualRelayBinaryPoint(2, "PICKUP", pickup, false,
                VirtualRelayIoAuthority.ProtectionAuthoritative,
                "Derived from active OCR/feeder protection element pickup state."),
            new VirtualRelayBinaryPoint(3, "HEALTHY", healthy, false,
                VirtualRelayIoAuthority.ProtectionAuthoritative,
                "Derived from current SMV trust permissions and block state."),
            new VirtualRelayBinaryPoint(4, "SMV BLOCK", !snapshot.SmvTrust.AllowsTrip, false,
                VirtualRelayIoAuthority.ProtectionAuthoritative,
                "Derived from the existing process-bus trust gate; not an HMI override.")
        };
    }

    public bool ToggleInput(int number)
    {
        switch (number)
        {
            case 1:
                TestMode = !TestMode;
                return TestMode;
            case 2:
                ExternalAlarm = !ExternalAlarm;
                return ExternalAlarm;
            case 3:
                LocalMode = !LocalMode;
                return LocalMode;
            case 4:
                Breaker52A = !Breaker52A;
                return Breaker52A;
            default:
                throw new ArgumentOutOfRangeException(nameof(number));
        }
    }

    public static bool HasAnyPickup(ProtectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
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

public sealed class VirtualRelayOperatorAlarm
{
    internal VirtualRelayOperatorAlarm(
        int sequence,
        DateTimeOffset timestamp,
        string code,
        string message,
        VirtualRelayAlarmSeverity severity)
    {
        Sequence = sequence;
        Timestamp = timestamp;
        Code = code;
        Message = message;
        Severity = severity;
        Active = true;
    }

    public int Sequence { get; }
    public DateTimeOffset Timestamp { get; }
    public string Code { get; }
    public string Message { get; }
    public VirtualRelayAlarmSeverity Severity { get; }
    public bool Active { get; internal set; }
    public bool Acknowledged { get; internal set; }
}

public sealed class VirtualRelayOperatorAlarmLog
{
    private readonly List<VirtualRelayOperatorAlarm> _alarms = new();
    private readonly Dictionary<string, bool> _conditionState = new(StringComparer.Ordinal);
    private int _nextSequence = 1;

    public IReadOnlyList<VirtualRelayOperatorAlarm> NewestFirst
        => _alarms.AsEnumerable().Reverse().ToArray();

    public int ActiveCount => _alarms.Count(item => item.Active);
    public int UnacknowledgedCount => _alarms.Count(item => !item.Acknowledged);

    public VirtualRelayOperatorAlarm? Observe(
        DateTimeOffset timestamp,
        string code,
        bool condition,
        VirtualRelayAlarmSeverity severity,
        string message)
    {
        var wasActive = _conditionState.TryGetValue(code, out var prior) && prior;
        _conditionState[code] = condition;

        if (condition && !wasActive)
        {
            var alarm = new VirtualRelayOperatorAlarm(_nextSequence++, timestamp, code, message, severity);
            _alarms.Add(alarm);
            if (_alarms.Count > 80)
                _alarms.RemoveAt(0);
            return alarm;
        }

        if (!condition && wasActive)
        {
            var active = _alarms.LastOrDefault(item => item.Code == code && item.Active);
            if (active is not null)
                active.Active = false;
        }

        return null;
    }

    public bool Acknowledge(int sequence)
    {
        var alarm = _alarms.FirstOrDefault(item => item.Sequence == sequence);
        if (alarm is null)
            return false;
        alarm.Acknowledged = true;
        return true;
    }

    public int AcknowledgeAll()
    {
        var changed = 0;
        foreach (var alarm in _alarms.Where(item => !item.Acknowledged))
        {
            alarm.Acknowledged = true;
            changed++;
        }
        return changed;
    }

    public int ClearInactiveAcknowledged()
    {
        var removed = _alarms.RemoveAll(item => !item.Active && item.Acknowledged);
        return removed;
    }
}

public static class VirtualRelayLedSignalLabels
{
    public static string Label(VirtualRelayLedSignal signal) => signal switch
    {
        VirtualRelayLedSignal.PhaseA => "PHASE A",
        VirtualRelayLedSignal.PhaseB => "PHASE B",
        VirtualRelayLedSignal.PhaseC => "PHASE C",
        VirtualRelayLedSignal.Earth => "EARTH",
        VirtualRelayLedSignal.Pickup => "PICKUP",
        VirtualRelayLedSignal.Trip => "TRIP",
        VirtualRelayLedSignal.SmvBlock => "SMV BLOCK",
        VirtualRelayLedSignal.Healthy => "HEALTHY",
        VirtualRelayLedSignal.ExternalAlarm => "EXT ALARM",
        VirtualRelayLedSignal.TestMode => "TEST MODE",
        VirtualRelayLedSignal.LocalMode => "LOCAL",
        VirtualRelayLedSignal.SettingGroupB => "GROUP B",
        _ => signal.ToString().ToUpperInvariant()
    };
}
