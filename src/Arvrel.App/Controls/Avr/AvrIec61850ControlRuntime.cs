using Arvrel.Application.Ied;

#if ARIEC61850_SIBLING
using AR.Iec61850.Mms;
using AR.Iec61850.Simulation;
#endif

namespace Arvrel.App.Controls.Avr;

internal enum AvrIec61850CommandKind
{
    SetAutomaticMode,
    SetRemoteBlock,
    TapChange,
    SetSetpoint,
    SetBandwidth,
    SetT1Delay
}

internal sealed record AvrIec61850Command(
    AvrIec61850CommandKind Kind,
    double NumericValue,
    bool BooleanValue,
    string RemoteEndPoint,
    string Target,
    byte? ControlNumber = null,
    bool Test = false);

internal sealed record AvrIec61850ControlContext(
    AvrControlAuthority Authority,
    AvrOperatingMode Mode,
    int TapPosition,
    int MinimumTap,
    int MaximumTap,
    bool TapMoving,
    bool RemoteBlock,
    double NominalVoltageV,
    double SetpointVoltageV,
    double BandwidthVoltageV);

#if ARIEC61850_SIBLING
/// <summary>
/// Per-MMS-association process-control runtime for the virtual AVR.
/// It claims only explicitly modelled AVR controls/settings; all other writes
/// continue to ARIEC61850's read-only guard. The dispatcher removes the FC
/// segment when converting MMS names to IEC references, so this runtime matches
/// decoded paths such as YLTC1.TapChg.Oper.
/// </summary>
internal sealed class AvrIec61850ControlRuntime : IMmsAssociationRuntime, IDisposable
{
    private const int DataAccessErrorTemporarilyUnavailable = 2;
    private const int DataAccessErrorObjectAccessDenied = 3;
    private const int DataAccessErrorTypeInconsistent = 7;
    private const int DataAccessErrorObjectNonExistent = 10;
    private static readonly TimeSpan SboTimeout = TimeSpan.FromSeconds(5);

    private sealed record Selection(string ControlRoot, long? ControlValue, byte? ControlNumber, DateTimeOffset ExpiresAtUtc);

    private readonly string _remoteEndPoint;
    private readonly Func<AvrIec61850ControlContext> _context;
    private readonly Action<AvrIec61850Command> _enqueue;
    private readonly Action<bool, string, string> _audit;
    private readonly Dictionary<string, Selection> _selections = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public AvrIec61850ControlRuntime(
        string remoteEndPoint,
        Func<AvrIec61850ControlContext> context,
        Action<AvrIec61850Command> enqueue,
        Action<bool, string, string> audit)
    {
        _remoteEndPoint = remoteEndPoint ?? string.Empty;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public bool TryReadRcbAttribute(string iecTarget, out MmsDataValue value)
    {
        value = MmsDataValue.Boolean(false);
        if (_disposed)
            return false;

        var target = Normalize(iecTarget);
        if (!TryGetControl(target, out var controlRoot, out var action) || action != "SBO")
            return false;

        PurgeExpiredSelections();
        if (!CanSelect(controlRoot, _context(), out var reason))
        {
            value = MmsDataValue.VisibleString(string.Empty);
            _audit(false, target, reason);
            return true;
        }

        _selections[controlRoot] = new Selection(controlRoot, null, null, DateTimeOffset.UtcNow + SboTimeout);
        value = MmsDataValue.VisibleString(controlRoot);
        _audit(true, target, $"SBO selected for {SboTimeout.TotalSeconds:0}s");
        return true;
    }

    public bool TryWriteRcbAttribute(string iecTarget, MmsDataValue value, out int dataAccessError)
    {
        dataAccessError = 0;
        if (_disposed)
            return false;

        var target = Normalize(iecTarget);
        PurgeExpiredSelections();

        if (TryHandleSettingWrite(target, value, out dataAccessError))
            return true;

        if (!TryGetControl(target, out var controlRoot, out var action))
            return false;

        switch (action)
        {
            case "SBOW":
                return SelectWithValue(controlRoot, target, value, out dataAccessError);
            case "OPER":
                return Operate(controlRoot, target, value, out dataAccessError);
            case "CANCEL":
                _selections.Remove(controlRoot);
                _audit(true, target, "Selection cancelled");
                return true;
            default:
                dataAccessError = DataAccessErrorObjectNonExistent;
                _audit(false, target, "Unsupported control service attribute");
                return true;
        }
    }

    private bool SelectWithValue(string controlRoot, string target, MmsDataValue value, out int error)
    {
        error = 0;
        if (!CanSelect(controlRoot, _context(), out var reason))
        {
            error = reason.Contains("moving", StringComparison.OrdinalIgnoreCase)
                ? DataAccessErrorTemporarilyUnavailable
                : DataAccessErrorObjectAccessDenied;
            _audit(false, target, reason);
            return true;
        }

        if (!TryControlValue(controlRoot, value, out var ctlVal, out var ctlNum, out _, out reason))
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, reason);
            return true;
        }

        _selections[controlRoot] = new Selection(controlRoot, ctlVal, ctlNum, DateTimeOffset.UtcNow + SboTimeout);
        _audit(true, target, $"SBOw selected ctlVal={ctlVal} ctlNum={(ctlNum?.ToString() ?? "-")}");
        return true;
    }

    private bool Operate(string controlRoot, string target, MmsDataValue value, out int error)
    {
        error = 0;
        if (!TryControlValue(controlRoot, value, out var ctlVal, out var ctlNum, out var test, out var reason))
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, reason);
            return true;
        }

        if (!_selections.TryGetValue(controlRoot, out var selection) || selection.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _selections.Remove(controlRoot);
            error = DataAccessErrorObjectAccessDenied;
            _audit(false, target, "Operate rejected: object is not selected or SBO timeout expired");
            return true;
        }

        if (selection.ControlValue.HasValue && selection.ControlValue.Value != ctlVal)
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, $"Operate ctlVal={ctlVal} does not match selected ctlVal={selection.ControlValue.Value}");
            return true;
        }

        if (selection.ControlNumber.HasValue && ctlNum.HasValue && selection.ControlNumber.Value != ctlNum.Value)
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, $"Operate ctlNum={ctlNum} does not match selected ctlNum={selection.ControlNumber}");
            return true;
        }

        if (!ValidateOperate(controlRoot, ctlVal, _context(), out var command, out reason))
        {
            error = reason.Contains("moving", StringComparison.OrdinalIgnoreCase) || reason.Contains("limit", StringComparison.OrdinalIgnoreCase)
                ? DataAccessErrorTemporarilyUnavailable
                : DataAccessErrorObjectAccessDenied;
            _audit(false, target, reason);
            return true;
        }

        _selections.Remove(controlRoot);
        _enqueue(command with { RemoteEndPoint = _remoteEndPoint, Target = target, ControlNumber = ctlNum, Test = test });
        _audit(true, target, $"Operate accepted ctlVal={ctlVal} ctlNum={(ctlNum?.ToString() ?? "-")}{(test ? " TEST" : string.Empty)}");
        return true;
    }

    private bool TryHandleSettingWrite(string target, MmsDataValue value, out int error)
    {
        error = 0;
        var kind = target switch
        {
            "ARVAVR1/ATCC1$BNDCTR$SETMAG$F" => AvrIec61850CommandKind.SetSetpoint,
            "ARVAVR1/ATCC1$BNDWID$SETMAG$F" => AvrIec61850CommandKind.SetBandwidth,
            "ARVAVR1/ATCC1$CTLDLTMMS$SETVAL" => AvrIec61850CommandKind.SetT1Delay,
            _ => (AvrIec61850CommandKind?)null
        };
        if (!kind.HasValue)
            return false;

        var context = _context();
        if (context.Authority != AvrControlAuthority.Remote)
        {
            error = DataAccessErrorObjectAccessDenied;
            _audit(false, target, "Setting write requires REMOTE authority");
            return true;
        }
        if (context.TapMoving)
        {
            error = DataAccessErrorTemporarilyUnavailable;
            _audit(false, target, "Setting write inhibited while tap changer is moving");
            return true;
        }
        if (!TryDouble(value, out var number))
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, "Setting value is not numeric");
            return true;
        }

        var valid = kind.Value switch
        {
            AvrIec61850CommandKind.SetSetpoint => number >= context.NominalVoltageV * 0.5 && number <= context.NominalVoltageV * 1.5,
            AvrIec61850CommandKind.SetBandwidth => number > 0 && number <= context.NominalVoltageV * 0.4,
            AvrIec61850CommandKind.SetT1Delay => number >= 0 && number <= 600_000,
            _ => false
        };
        if (!valid)
        {
            error = DataAccessErrorTypeInconsistent;
            _audit(false, target, $"Setting value {number:0.###} is outside AVR lab limits");
            return true;
        }

        _enqueue(new AvrIec61850Command(kind.Value, number, false, _remoteEndPoint, target));
        _audit(true, target, $"Setting write accepted value={number:0.###}");
        return true;
    }

    private static bool CanSelect(string controlRoot, AvrIec61850ControlContext context, out string reason)
    {
        if (context.Authority != AvrControlAuthority.Remote)
        {
            reason = "Control selection requires REMOTE authority";
            return false;
        }
        if (controlRoot.Contains("YLTC1$TAPCHG", StringComparison.OrdinalIgnoreCase) && context.TapMoving)
        {
            reason = "Tap-change selection unavailable while tap changer is moving";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool ValidateOperate(string controlRoot, long ctlVal, AvrIec61850ControlContext context, out AvrIec61850Command command, out string reason)
    {
        command = new AvrIec61850Command(AvrIec61850CommandKind.TapChange, 0, false, string.Empty, string.Empty);
        if (context.Authority != AvrControlAuthority.Remote)
        {
            reason = "Operate requires REMOTE authority";
            return false;
        }

        if (controlRoot.Contains("YLTC1$TAPCHG", StringComparison.OrdinalIgnoreCase))
        {
            if (ctlVal is < 0 or > 2)
            {
                reason = "TapChg ctlVal must be stop=0, lower=1, or higher=2";
                return false;
            }
            if (ctlVal != 0 && context.Mode != AvrOperatingMode.Manual)
            {
                reason = "TapChg RAISE/LOWER requires REMOTE + MANUAL";
                return false;
            }
            if (ctlVal != 0 && context.RemoteBlock)
            {
                reason = "TapChg inhibited by remote LTC block";
                return false;
            }
            if (ctlVal != 0 && context.TapMoving)
            {
                reason = "TapChg rejected: tap changer already moving";
                return false;
            }
            if (ctlVal == 1 && context.TapPosition <= context.MinimumTap)
            {
                reason = "TapChg LOWER rejected: minimum tap limit reached";
                return false;
            }
            if (ctlVal == 2 && context.TapPosition >= context.MaximumTap)
            {
                reason = "TapChg RAISE rejected: maximum tap limit reached";
                return false;
            }
            command = new AvrIec61850Command(AvrIec61850CommandKind.TapChange, ctlVal, false, string.Empty, string.Empty);
            reason = string.Empty;
            return true;
        }

        if (controlRoot.Contains("ATCC1$AUTO", StringComparison.OrdinalIgnoreCase))
        {
            command = new AvrIec61850Command(AvrIec61850CommandKind.SetAutomaticMode, 0, ctlVal != 0, string.Empty, string.Empty);
            reason = string.Empty;
            return true;
        }

        if (controlRoot.Contains("ATCC1$LTCBLK", StringComparison.OrdinalIgnoreCase))
        {
            command = new AvrIec61850Command(AvrIec61850CommandKind.SetRemoteBlock, 0, ctlVal != 0, string.Empty, string.Empty);
            reason = string.Empty;
            return true;
        }

        reason = "Unknown AVR control object";
        return false;
    }

    private static bool TryControlValue(string controlRoot, MmsDataValue value, out long ctlVal, out byte? ctlNum, out bool test, out string reason)
    {
        ctlVal = 0;
        ctlNum = null;
        test = false;
        var primary = value.Kind is MmsDataKind.Structure or MmsDataKind.Array ? value.Children.FirstOrDefault() : value;
        if (primary is null || !TryLong(primary, out ctlVal))
        {
            reason = "Control value has no decodable ctlVal";
            return false;
        }

        if (controlRoot.Contains("ATCC1$AUTO", StringComparison.OrdinalIgnoreCase) || controlRoot.Contains("ATCC1$LTCBLK", StringComparison.OrdinalIgnoreCase))
            ctlVal = ctlVal == 0 ? 0 : 1;

        if (value.Kind is MmsDataKind.Structure or MmsDataKind.Array)
        {
            if (value.Children.Count > 2 && TryLong(value.Children[2], out var number) && number is >= 0 and <= byte.MaxValue)
                ctlNum = (byte)number;
            if (value.Children.Count > 4 && TryBoolean(value.Children[4], out var isTest))
                test = isTest;
        }
        reason = string.Empty;
        return true;
    }

    private static bool TryGetControl(string target, out string controlRoot, out string action)
    {
        controlRoot = string.Empty;
        action = string.Empty;
        var roots = new[] { "ARVAVR1/YLTC1$TAPCHG", "ARVAVR1/ATCC1$AUTO", "ARVAVR1/ATCC1$LTCBLK" };
        foreach (var root in roots)
        {
            if (!target.StartsWith(root + "$", StringComparison.OrdinalIgnoreCase))
                continue;
            controlRoot = root;
            var suffix = target[(root.Length + 1)..];
            action = suffix.Split('$', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();
            return action is "SBO" or "SBOW" or "OPER" or "CANCEL";
        }
        return false;
    }

    private static bool TryLong(MmsDataValue value, out long result)
    {
        switch (value.Kind)
        {
            case MmsDataKind.Boolean when value.Value is bool boolean: result = boolean ? 1 : 0; return true;
            case MmsDataKind.Integer when value.Value is long signed: result = signed; return true;
            case MmsDataKind.Unsigned when value.Value is ulong unsigned && unsigned <= long.MaxValue: result = (long)unsigned; return true;
            case MmsDataKind.FloatingPoint when value.Value is float single: result = (long)single; return true;
            case MmsDataKind.FloatingPoint when value.Value is double floating: result = (long)floating; return true;
            default: result = 0; return false;
        }
    }

    private static bool TryBoolean(MmsDataValue value, out bool result)
    {
        if (value.Kind == MmsDataKind.Boolean && value.Value is bool boolean) { result = boolean; return true; }
        if (TryLong(value, out var numeric)) { result = numeric != 0; return true; }
        result = false;
        return false;
    }

    private static bool TryDouble(MmsDataValue value, out double result)
    {
        var primary = value.Kind is MmsDataKind.Structure or MmsDataKind.Array ? value.Children.FirstOrDefault() : value;
        if (primary is null) { result = 0; return false; }
        switch (primary.Kind)
        {
            case MmsDataKind.FloatingPoint when primary.Value is float single: result = single; return double.IsFinite(result);
            case MmsDataKind.FloatingPoint when primary.Value is double floating: result = floating; return double.IsFinite(result);
            case MmsDataKind.Integer when primary.Value is long signed: result = signed; return true;
            case MmsDataKind.Unsigned when primary.Value is ulong unsigned: result = unsigned; return true;
            default: result = 0; return false;
        }
    }

    private void PurgeExpiredSelections()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _selections.Where(x => x.Value.ExpiresAtUtc <= now).Select(x => x.Key).ToArray())
            _selections.Remove(key);
    }

    private static string Normalize(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return string.Empty;
        var normalized = target.Trim().Replace('\\', '/');
        var slash = normalized.IndexOf('/');
        if (slash >= 0 && slash < normalized.Length - 1)
            normalized = normalized[..(slash + 1)] + normalized[(slash + 1)..].Replace('.', '$');
        return normalized.ToUpperInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _selections.Clear();
    }
}
#endif
