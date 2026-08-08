using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private bool _iec61850RemoteBlock;
    private AvrOperatingMode _iec61850RequestedMode = AvrOperatingMode.Automatic;

    /// <summary>
    /// Runs on the WPF dispatcher timer after the AVR simulation tick. Accepted
    /// MMS controls are never applied from the TCP worker thread; they are drained
    /// here and then reflected back into the authoritative AVR snapshot/HMI.
    /// </summary>
    private void ProcessIec61850ControlRuntime()
    {
        var modeCommandReceived = false;
        var changed = false;
        var processed = 0;
        while (processed++ < 32 && _iec61850Server.TryDequeueCommand(out var command))
        {
            changed = true;
            switch (command.Kind)
            {
                case AvrIec61850CommandKind.SetAutomaticMode:
                    _iec61850RequestedMode = command.BooleanValue
                        ? AvrOperatingMode.Automatic
                        : AvrOperatingMode.Manual;
                    modeCommandReceived = true;
                    _engine.SetMode(_iec61850RemoteBlock ? AvrOperatingMode.Manual : _iec61850RequestedMode);
                    AddEvent("IEC CTRL", $"{RemoteLabel(command)} · {(_iec61850RequestedMode == AvrOperatingMode.Automatic ? "AUTO" : "MANUAL")} accepted");
                    break;

                case AvrIec61850CommandKind.SetRemoteBlock:
                    _iec61850RemoteBlock = command.BooleanValue;
                    if (_iec61850RemoteBlock)
                    {
                        // An external blocking command inhibits both automatic and
                        // manual tap operations and cancels a virtual motor travel
                        // already in progress without changing completed tap feedback.
                        _engine.ApplySettings(_settings, keepTapPosition: true);
                        _engine.SetMode(AvrOperatingMode.Manual);
                    }
                    else
                    {
                        _engine.SetMode(_iec61850RequestedMode);
                    }
                    AddEvent("IEC CTRL", $"{RemoteLabel(command)} · LTC BLOCK {(_iec61850RemoteBlock ? "ON" : "OFF")}");
                    break;

                case AvrIec61850CommandKind.TapChange:
                    ExecuteIecTapChange(command);
                    break;

                case AvrIec61850CommandKind.SetSetpoint:
                    ApplyIecSetting(command, _settings with { SetpointVoltageV = command.NumericValue },
                        $"setpoint {command.NumericValue:0.###} V");
                    break;

                case AvrIec61850CommandKind.SetBandwidth:
                    var tolerance = command.NumericValue / Math.Max(0.001, _settings.SetpointVoltageV) * 50.0;
                    ApplyIecSetting(command, _settings with { TolerancePercent = tolerance },
                        $"bandwidth {command.NumericValue:0.###} V (±{tolerance:0.###}%)");
                    break;

                case AvrIec61850CommandKind.SetT1Delay:
                    ApplyIecSetting(command, _settings with { T1Seconds = command.NumericValue / 1000.0 },
                        $"T1 {command.NumericValue:0} ms");
                    break;
            }
        }

        if (!_iec61850RemoteBlock && !modeCommandReceived)
            _iec61850RequestedMode = _engine.Mode;

        if (_iec61850RemoteBlock && _engine.Mode != AvrOperatingMode.Manual)
            _engine.SetMode(AvrOperatingMode.Manual);
        else if (!_iec61850RemoteBlock && _engine.Mode != _iec61850RequestedMode)
            _engine.SetMode(_iec61850RequestedMode);

        if (changed || _iec61850RemoteBlock)
        {
            _snapshot = _engine.Advance(
                TimeSpan.Zero,
                _injectedVoltageV,
                _injectedCurrentA,
                _currentAngleDegrees,
                _sourceEnergized);

            if (_iec61850RemoteBlock)
            {
                _snapshot = _snapshot with
                {
                    Mode = _iec61850RequestedMode,
                    State = AvrControlState.Blocked,
                    Blocked = true,
                    PendingTapPosition = null,
                    RaiseOutput = false,
                    LowerOutput = false,
                    TapMoving = false,
                    Reason = "Remote LTC blocking active · tap operation inhibited",
                    DelayElapsedSeconds = 0,
                    DelayTargetSeconds = 0,
                    MotorTravelRemainingSeconds = 0,
                    CommandPulseRemainingSeconds = 0
                };
            }

            ObserveTransition(_snapshot);
            RenderSnapshot(_snapshot);
        }
    }

    private void ExecuteIecTapChange(AvrIec61850Command command)
    {
        var ctlVal = (int)Math.Round(command.NumericValue);
        if (ctlVal == 0)
        {
            if (_engine.TapMoving)
            {
                _engine.ApplySettings(_settings, keepTapPosition: true);
                AddEvent("IEC CTRL", $"{RemoteLabel(command)} · TapChg STOP · motor travel cancelled");
            }
            else
            {
                AddEvent("IEC CTRL", $"{RemoteLabel(command)} · TapChg STOP · already idle");
            }
            return;
        }

        if (_iec61850RemoteBlock)
        {
            AddEvent("IEC INH", $"{RemoteLabel(command)} · TapChg inhibited by LTC BLOCK");
            return;
        }

        // The engine deliberately exposes local-manual actuator methods only.
        // Execute a remote manual command through a very short authority adapter,
        // then restore REMOTE before producing the next authoritative snapshot.
        // This preserves one actuator implementation and all tap-limit/motor logic.
        var originalAuthority = _engine.Authority;
        try
        {
            _engine.SetAuthority(AvrControlAuthority.Local);
            var moved = ctlVal == 2 ? _engine.RaiseTap() : _engine.LowerTap();
            AddEvent(moved ? "IEC CTRL" : "IEC INH",
                $"{RemoteLabel(command)} · TapChg {(ctlVal == 2 ? "HIGHER / RAISE" : "LOWER")} {(moved ? "issued" : "not accepted")}");
        }
        finally
        {
            _engine.SetAuthority(originalAuthority);
        }
    }

    private void ApplyIecSetting(AvrIec61850Command command, AvrSettings proposed, string description)
    {
        try
        {
            proposed.Validate();
            _settings = proposed;
            _engine.ApplySettings(_settings, keepTapPosition: true);
            if (_iec61850RemoteBlock)
                _engine.SetMode(AvrOperatingMode.Manual);
            PopulateSettingsUi();
            AddEvent("IEC SET", $"{RemoteLabel(command)} · {description}");
        }
        catch (ArgumentException ex)
        {
            // Network-side range validation should normally prevent this path.
            // Keep it explicit so a future settings expansion cannot corrupt the
            // running virtual IED if application validation becomes stricter.
            AddEvent("IEC ERR", $"{RemoteLabel(command)} · {description} rejected: {ex.Message}");
        }
    }

    private static string RemoteLabel(AvrIec61850Command command)
        => string.IsNullOrWhiteSpace(command.RemoteEndPoint)
            ? "SAS"
            : $"SAS {command.RemoteEndPoint}";
}
