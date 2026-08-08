using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.Protection;

namespace Arvrel.App;

/// <summary>
/// P1.2 operator-experience layer for the OCR virtual relay.
///
/// This layer deliberately separates three authorities:
/// - protection-authoritative outputs come only from ProtectionSnapshot / SMV trust;
/// - simulated binary inputs are HMI-only laboratory contacts;
/// - setting-group activation reuses the existing settings apply path and resets
///   protection timers/latches exactly as the normal settings editor does.
///
/// It is a training/operator UX, not a cyber-security authentication boundary and
/// not a physical binary I/O implementation.
/// </summary>
public partial class MainWindow
{
    private enum RelayOperatorPage
    {
        None,
        BinaryIo,
        Alarms,
        SettingGroups,
        LedAssignment,
        Access,
        System
    }

    private readonly VirtualRelayOperatorIoModel _relayOperatorIo = new();
    private readonly VirtualRelayOperatorAlarmLog _relayOperatorAlarms = new();
    private readonly Dictionary<string, ProtectionSettings> _relaySettingGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly VirtualRelayLedSignal[] _relayProgrammableLedAssignments =
    {
        VirtualRelayLedSignal.PhaseA,
        VirtualRelayLedSignal.PhaseB,
        VirtualRelayLedSignal.PhaseC,
        VirtualRelayLedSignal.Earth
    };

    private DispatcherTimer? _relayOperatorTimer;
    private RelayOperatorPage _relayOperatorPage;
    private int _relayOperatorSelection;
    private VirtualRelayAccessLevel _relayAccessLevel = VirtualRelayAccessLevel.Operator;
    private bool _relayOperatorInitialized;
    private bool _relaySettingGroupSwitching;
    private string _relayActiveSettingGroup = "GROUP A";
    private string? _relayOperatorObservedFingerprint;

    internal void InitializeRelayOperatorExperience(VirtualRelayControl relay)
    {
        if (_relayOperatorInitialized)
            return;

        _relayOperatorInitialized = true;
        _relayActiveSettingGroup = string.Equals(_settings.GroupName.Trim(), "GROUP B", StringComparison.OrdinalIgnoreCase)
            ? "GROUP B"
            : "GROUP A";
        var inactive = _relayActiveSettingGroup == "GROUP A" ? "GROUP B" : "GROUP A";
        _relaySettingGroups[_relayActiveSettingGroup] = _settings with { GroupName = _relayActiveSettingGroup };
        _relaySettingGroups[inactive] = _settings with { GroupName = inactive };
        _relayOperatorObservedFingerprint = _settings.Fingerprint();

        relay.SetProgrammableLampLabels(CurrentProgrammableLedLabels());

        _relayOperatorTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _relayOperatorTimer.Tick += RelayOperatorTimer_Tick;
        _relayOperatorTimer.Start();
        Closed += RelayOperatorWindow_Closed;

        AddRelayFaceplateEvent(DateTimeOffset.Now, "HMI", "P1.2 operator experience initialized");
    }

    private void RelayOperatorWindow_Closed(object? sender, EventArgs e)
    {
        if (_relayOperatorTimer is null)
            return;
        _relayOperatorTimer.Stop();
        _relayOperatorTimer.Tick -= RelayOperatorTimer_Tick;
        _relayOperatorTimer = null;
    }

    private void RelayOperatorTimer_Tick(object? sender, EventArgs e)
    {
        SyncActiveSettingGroupSlot();
        var snapshot = CurrentRelayOperatorSnapshot();
        ObserveRelayOperatorAlarms(snapshot);

        if (_relayOperatorPage == RelayOperatorPage.None)
            return;

        // The normal faceplate timer is paused while an operator page owns the LCD.
        // Run the same authoritative faceplate tick here first so event/trip records
        // continue to advance, then paint the operator page in the same dispatcher turn.
        TickRelayFaceplate();
        snapshot = CurrentRelayOperatorSnapshot();
        ObserveRelayOperatorAlarms(snapshot);

        if (snapshot.TripLatched && _relayLcdPage == RelayLcdPage.TripRecord)
        {
            ExitRelayOperatorPage(restartFaceplateTimer: true);
            RenderRelayLcd(_relayLastFrame);
            return;
        }

        RenderRelayOperatorPage(snapshot);
    }

    private ProtectionSnapshot CurrentRelayOperatorSnapshot()
        => _relayLastFrame?.Snapshot ?? _snapshot;

    private void ObserveRelayOperatorAlarms(ProtectionSnapshot snapshot)
    {
        var timestamp = snapshot.Timestamp == default ? DateTimeOffset.Now : snapshot.Timestamp;
        AddOperatorAlarmIfNew(_relayOperatorAlarms.Observe(
            timestamp,
            "TRIP",
            snapshot.TripLatched,
            VirtualRelayAlarmSeverity.Trip,
            $"Trip latched · {snapshot.ActiveElement}"));
        AddOperatorAlarmIfNew(_relayOperatorAlarms.Observe(
            timestamp,
            "SMV_BLOCK",
            !snapshot.SmvTrust.AllowsTrip,
            VirtualRelayAlarmSeverity.Warning,
            $"Trip permission blocked · {snapshot.SmvTrust.Code}"));
        AddOperatorAlarmIfNew(_relayOperatorAlarms.Observe(
            timestamp,
            "PICKUP",
            VirtualRelayOperatorIoModel.HasAnyPickup(snapshot) && !snapshot.TripLatched,
            VirtualRelayAlarmSeverity.Info,
            $"Protection pickup · {snapshot.ActiveElement}"));
        AddOperatorAlarmIfNew(_relayOperatorAlarms.Observe(
            timestamp,
            "EXT_ALARM",
            _relayOperatorIo.ExternalAlarm,
            VirtualRelayAlarmSeverity.Warning,
            "Simulated external alarm input asserted"));
        AddOperatorAlarmIfNew(_relayOperatorAlarms.Observe(
            timestamp,
            "TEST_MODE",
            _relayOperatorIo.TestMode,
            VirtualRelayAlarmSeverity.Info,
            "Simulated test-mode input asserted"));
    }

    private void AddOperatorAlarmIfNew(VirtualRelayOperatorAlarm? alarm)
    {
        if (alarm is null)
            return;
        AddRelayFaceplateEvent(alarm.Timestamp, alarm.Code, alarm.Message);
        AddEvent($"ALARM {alarm.Code}", alarm.Message);
    }

    internal bool HandleRelayOperatorHardwareKey(VirtualRelayHardwareKey key)
    {
        if (_relayOperatorPage == RelayOperatorPage.None)
            return false;

        switch (key)
        {
            case VirtualRelayHardwareKey.Home:
                ExitRelayOperatorPage(restartFaceplateTimer: true);
                NavigateRelayPage(RelayLcdPage.Measurements);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Menu:
                ExitRelayOperatorPage(restartFaceplateTimer: true);
                OpenRelayMenu(RelayMenuLevel.Root);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Favorite:
                ExitRelayOperatorPage(restartFaceplateTimer: true);
                NavigateRelayPage(_relayFavoritePage);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Back:
            case VirtualRelayHardwareKey.Left:
                RelayOperatorBack();
                return true;

            case VirtualRelayHardwareKey.Up:
                MoveRelayOperatorSelection(-1);
                break;

            case VirtualRelayHardwareKey.Down:
                MoveRelayOperatorSelection(+1);
                break;

            case VirtualRelayHardwareKey.Right:
            case VirtualRelayHardwareKey.Ok:
                ActivateRelayOperatorSelection();
                break;

            case VirtualRelayHardwareKey.F1:
                HandleRelayOperatorFunctionKey(0);
                break;
            case VirtualRelayHardwareKey.F2:
                HandleRelayOperatorFunctionKey(1);
                break;
            case VirtualRelayHardwareKey.F3:
                HandleRelayOperatorFunctionKey(2);
                break;
            case VirtualRelayHardwareKey.F4:
                HandleRelayOperatorFunctionKey(3);
                break;
            case VirtualRelayHardwareKey.F5:
                HandleRelayOperatorFunctionKey(4);
                break;
        }

        RenderRelayOperatorPage(CurrentRelayOperatorSnapshot());
        return true;
    }

    internal void RouteRelayOperatorHostPage()
    {
        if (_relayMenuOpen || _relayOperatorPage != RelayOperatorPage.None)
            return;

        if (_relayLcdPage == RelayLcdPage.Diagnostics)
            EnterRelayOperatorPage(RelayOperatorPage.BinaryIo);
        else if (_relayLcdPage == RelayLcdPage.DeviceInfo)
            EnterRelayOperatorPage(RelayOperatorPage.System);
    }

    internal void NotifyRelayOperatorReset()
    {
        // RESET and ACK are intentionally different operations. The existing Reset_Click
        // remains the only protection reset authority. Alarm history is retained until
        // the operator explicitly acknowledges and clears inactive records.
        AddRelayFaceplateEvent(DateTimeOffset.Now, "RESET KEY", "Protection reset; alarm history retained until ACK/CLEAR");
        if (_relayOperatorPage != RelayOperatorPage.None)
            RenderRelayOperatorPage(CurrentRelayOperatorSnapshot());
    }

    private void EnterRelayOperatorPage(RelayOperatorPage page)
    {
        _relayOperatorPage = page;
        _relayOperatorSelection = 0;
        _relayMenuOpen = false;
        _relayMenuLevel = RelayMenuLevel.None;
        _relayMenuIndex = 0;
        _relayFaceplateTimer?.Stop();
        RenderRelayOperatorPage(CurrentRelayOperatorSnapshot());
    }

    private void ExitRelayOperatorPage(bool restartFaceplateTimer)
    {
        _relayOperatorPage = RelayOperatorPage.None;
        _relayOperatorSelection = 0;
        if (restartFaceplateTimer)
            _relayFaceplateTimer?.Start();
    }

    private void RelayOperatorBack()
    {
        if (_relayOperatorPage != RelayOperatorPage.System)
        {
            EnterRelayOperatorPage(RelayOperatorPage.System);
            return;
        }

        ExitRelayOperatorPage(restartFaceplateTimer: true);
        OpenRelayMenu(RelayMenuLevel.Diagnostics);
        RenderRelayLcd(_relayLastFrame);
    }

    private void MoveRelayOperatorSelection(int direction)
    {
        var count = RelayOperatorSelectionCount();
        if (count <= 0)
            return;
        _relayOperatorSelection = Wrap(_relayOperatorSelection + direction, count);
    }

    private int RelayOperatorSelectionCount() => _relayOperatorPage switch
    {
        RelayOperatorPage.BinaryIo => 8,
        RelayOperatorPage.Alarms => Math.Max(1, _relayOperatorAlarms.NewestFirst.Count),
        RelayOperatorPage.SettingGroups => 2,
        RelayOperatorPage.LedAssignment => _relayProgrammableLedAssignments.Length,
        RelayOperatorPage.Access => 3,
        _ => 1
    };

    private void ActivateRelayOperatorSelection()
    {
        switch (_relayOperatorPage)
        {
            case RelayOperatorPage.BinaryIo:
                ToggleSelectedRelayBinaryInput();
                break;
            case RelayOperatorPage.Alarms:
                AcknowledgeSelectedRelayAlarm();
                break;
            case RelayOperatorPage.SettingGroups:
                _ = SwitchRelaySettingGroupAsync(_relayOperatorSelection == 0 ? "GROUP A" : "GROUP B");
                break;
            case RelayOperatorPage.LedAssignment:
                CycleSelectedProgrammableLed(+1);
                break;
            case RelayOperatorPage.Access:
                SetRelayAccessLevel((VirtualRelayAccessLevel)Math.Clamp(_relayOperatorSelection, 0, 2));
                break;
            case RelayOperatorPage.System:
                EnterRelayOperatorPage(RelayOperatorPage.BinaryIo);
                break;
        }
    }

    private void HandleRelayOperatorFunctionKey(int index)
    {
        switch (_relayOperatorPage)
        {
            case RelayOperatorPage.BinaryIo:
                if (index == 0) EnterRelayOperatorPage(RelayOperatorPage.Alarms);
                else if (index == 1) EnterRelayOperatorPage(RelayOperatorPage.SettingGroups);
                else if (index == 2) EnterRelayOperatorPage(RelayOperatorPage.LedAssignment);
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.Access);
                else EnterRelayOperatorPage(RelayOperatorPage.System);
                break;

            case RelayOperatorPage.Alarms:
                if (index == 0) AcknowledgeSelectedRelayAlarm();
                else if (index == 1) AcknowledgeAllRelayAlarms();
                else if (index == 2) ClearRelayAlarmHistory();
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.BinaryIo);
                else EnterRelayOperatorPage(RelayOperatorPage.System);
                break;

            case RelayOperatorPage.SettingGroups:
                if (index == 0) OpenActiveRelaySettingsEditor();
                else if (index == 1) _ = SwitchRelaySettingGroupAsync("GROUP A");
                else if (index == 2) _ = SwitchRelaySettingGroupAsync("GROUP B");
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.Access);
                else EnterRelayOperatorPage(RelayOperatorPage.System);
                break;

            case RelayOperatorPage.LedAssignment:
                if (index == 0) CycleSelectedProgrammableLed(-1);
                else if (index == 1) CycleSelectedProgrammableLed(+1);
                else if (index == 2) RestoreDefaultProgrammableLeds();
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.Access);
                else EnterRelayOperatorPage(RelayOperatorPage.System);
                break;

            case RelayOperatorPage.Access:
                if (index <= 2) SetRelayAccessLevel((VirtualRelayAccessLevel)index);
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.BinaryIo);
                else EnterRelayOperatorPage(RelayOperatorPage.System);
                break;

            case RelayOperatorPage.System:
                if (index == 0) EnterRelayOperatorPage(RelayOperatorPage.BinaryIo);
                else if (index == 1) EnterRelayOperatorPage(RelayOperatorPage.Alarms);
                else if (index == 2) EnterRelayOperatorPage(RelayOperatorPage.SettingGroups);
                else if (index == 3) EnterRelayOperatorPage(RelayOperatorPage.LedAssignment);
                else EnterRelayOperatorPage(RelayOperatorPage.Access);
                break;
        }
    }

    private void ToggleSelectedRelayBinaryInput()
    {
        if (_relayOperatorSelection >= 4)
        {
            StatusText.Text = "Selected BO is protection-authoritative and read-only from the relay HMI.";
            return;
        }
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Operator, "toggle simulated binary input"))
            return;

        var point = _relayOperatorIo.Inputs()[_relayOperatorSelection];
        var state = _relayOperatorIo.ToggleInput(point.Number);
        var detail = $"BI{point.Number} {point.Name} = {(state ? 1 : 0)} · SIMULATED HMI-ONLY";
        AddRelayFaceplateEvent(DateTimeOffset.Now, "BINARY", detail);
        AddEvent("BINARY", detail);
        ObserveRelayOperatorAlarms(CurrentRelayOperatorSnapshot());
        RefreshRelayAnnunciation();
    }

    private void AcknowledgeSelectedRelayAlarm()
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Operator, "acknowledge alarm"))
            return;

        var alarms = _relayOperatorAlarms.NewestFirst;
        if (alarms.Count == 0)
            return;
        _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, alarms.Count - 1);
        var alarm = alarms[_relayOperatorSelection];
        _relayOperatorAlarms.Acknowledge(alarm.Sequence);
        AddRelayFaceplateEvent(DateTimeOffset.Now, "ACK", $"Alarm {alarm.Sequence:000} {alarm.Code}");
    }

    private void AcknowledgeAllRelayAlarms()
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Operator, "acknowledge all alarms"))
            return;
        var count = _relayOperatorAlarms.AcknowledgeAll();
        AddRelayFaceplateEvent(DateTimeOffset.Now, "ACK ALL", $"{count} alarm(s) acknowledged");
    }

    private void ClearRelayAlarmHistory()
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Engineer, "clear inactive alarm history"))
            return;
        var count = _relayOperatorAlarms.ClearInactiveAcknowledged();
        AddRelayFaceplateEvent(DateTimeOffset.Now, "ALARM CLEAR", $"{count} inactive acknowledged record(s) cleared");
        _relayOperatorSelection = 0;
    }

    private void OpenActiveRelaySettingsEditor()
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Engineer, "edit protection settings"))
            return;
        OpenRelaySettingsEditor();
    }

    private async Task SwitchRelaySettingGroupAsync(string group)
    {
        if (_relaySettingGroupSwitching || string.Equals(group, _relayActiveSettingGroup, StringComparison.OrdinalIgnoreCase))
            return;
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Engineer, "switch protection setting group"))
            return;
        if (!_relaySettingGroups.TryGetValue(group, out var stored))
            return;

        _relaySettingGroupSwitching = true;
        try
        {
            SyncActiveSettingGroupSlot();
            var settings = stored with { GroupName = group };
            settings.Validate();

            // Reuse the exact authority sequence used by ProtectionSettings_Click.
            // No second protection engine and no HMI-only settings shadow is evaluated.
            _settings = settings;
            _internalEngine.UpdateSettings(settings, keepTripLatch: false);
            _scenario.Reset();
            _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
            ResetTransitionMarkers();
            await RecreateProcessBusAsync().ConfigureAwait(true);
            UpdateSettingSummaries();
            RenderInitialFrame();

            _relayActiveSettingGroup = group;
            _relaySettingGroups[group] = settings;
            _relayOperatorObservedFingerprint = settings.Fingerprint();
            var detail = $"{group} activated · rev {settings.Revision} · {settings.Fingerprint()[..8]}";
            AddRelayFaceplateEvent(DateTimeOffset.Now, "SETTING GROUP", detail);
            AddEvent("SETTING GROUP", detail);
            StatusText.Text = $"{group} active. Protection timers and virtual trip latch reset by settings authority.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or InvalidOperationException)
        {
            AddRelayFaceplateEvent(DateTimeOffset.Now, "GROUP ERROR", ex.Message);
            StatusText.Text = $"Setting-group switch failed: {ex.Message}";
        }
        finally
        {
            _relaySettingGroupSwitching = false;
            if (_relayOperatorPage != RelayOperatorPage.None)
                RenderRelayOperatorPage(CurrentRelayOperatorSnapshot());
        }
    }

    private void SyncActiveSettingGroupSlot()
    {
        if (!_relayOperatorInitialized)
            return;
        var fingerprint = _settings.Fingerprint();
        if (string.Equals(fingerprint, _relayOperatorObservedFingerprint, StringComparison.Ordinal))
            return;

        _relaySettingGroups[_relayActiveSettingGroup] = _settings with { GroupName = _relayActiveSettingGroup };
        _relayOperatorObservedFingerprint = fingerprint;
    }

    private void CycleSelectedProgrammableLed(int direction)
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Engineer, "change programmable LED assignment"))
            return;

        _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, _relayProgrammableLedAssignments.Length - 1);
        var options = Enum.GetValues<VirtualRelayLedSignal>();
        var current = Array.IndexOf(options, _relayProgrammableLedAssignments[_relayOperatorSelection]);
        current = Wrap(current + direction, options.Length);
        _relayProgrammableLedAssignments[_relayOperatorSelection] = options[current];
        ApplyProgrammableLedPresentation();
    }

    private void RestoreDefaultProgrammableLeds()
    {
        if (!RequireRelayAccess(VirtualRelayAccessLevel.Engineer, "restore default LED assignment"))
            return;
        _relayProgrammableLedAssignments[0] = VirtualRelayLedSignal.PhaseA;
        _relayProgrammableLedAssignments[1] = VirtualRelayLedSignal.PhaseB;
        _relayProgrammableLedAssignments[2] = VirtualRelayLedSignal.PhaseC;
        _relayProgrammableLedAssignments[3] = VirtualRelayLedSignal.Earth;
        ApplyProgrammableLedPresentation();
    }

    private void ApplyProgrammableLedPresentation()
    {
        _p6VirtualRelay?.SetProgrammableLampLabels(CurrentProgrammableLedLabels());
        RefreshRelayAnnunciation();
        AddRelayFaceplateEvent(
            DateTimeOffset.Now,
            "LED CFG",
            string.Join(" / ", CurrentProgrammableLedLabels()));
    }

    private string[] CurrentProgrammableLedLabels()
        => _relayProgrammableLedAssignments.Select(VirtualRelayLedSignalLabels.Label).ToArray();

    private void SetRelayAccessLevel(VirtualRelayAccessLevel level)
    {
        _relayAccessLevel = level;
        _relayOperatorSelection = (int)level;
        AddRelayFaceplateEvent(DateTimeOffset.Now, "ACCESS", $"Lab access profile = {level.ToString().ToUpperInvariant()}");
        StatusText.Text = $"Relay lab access profile: {level}. This is a training profile, not authentication/security.";
    }

    private bool RequireRelayAccess(VirtualRelayAccessLevel required, string action)
    {
        if (_relayAccessLevel >= required)
            return true;
        StatusText.Text = $"{action} requires {required} access. Current lab profile: {_relayAccessLevel}.";
        AddRelayFaceplateEvent(DateTimeOffset.Now, "ACCESS DENIED", $"{action} · requires {required}");
        return false;
    }

    private void RenderRelayOperatorPage(ProtectionSnapshot snapshot)
    {
        if (_relayLcdHeader is null || _relayLcdBody is null || _relayLcdFooter is null || _p6VirtualRelay is null)
            return;

        switch (_relayOperatorPage)
        {
            case RelayOperatorPage.BinaryIo:
                RenderRelayBinaryIoPage(snapshot);
                break;
            case RelayOperatorPage.Alarms:
                RenderRelayAlarmPage();
                break;
            case RelayOperatorPage.SettingGroups:
                RenderRelaySettingGroupPage();
                break;
            case RelayOperatorPage.LedAssignment:
                RenderRelayLedAssignmentPage();
                break;
            case RelayOperatorPage.Access:
                RenderRelayAccessPage();
                break;
            case RelayOperatorPage.System:
                RenderRelayOperatorSystemPage(snapshot);
                break;
        }
    }

    private void RenderRelayBinaryIoPage(ProtectionSnapshot snapshot)
    {
        var inputs = _relayOperatorIo.Inputs();
        var outputs = _relayOperatorIo.Outputs(snapshot);
        var rows = inputs.Select(point => (Prefix: "BI", Point: point))
            .Concat(outputs.Select(point => (Prefix: "BO", Point: point)))
            .ToArray();
        _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, rows.Length - 1);
        var start = Math.Clamp(_relayOperatorSelection - 2, 0, Math.Max(0, rows.Length - 5));
        var lines = rows.Skip(start).Take(5).Select((row, offset) =>
        {
            var selected = start + offset == _relayOperatorSelection ? ">" : " ";
            var source = row.Point.Authority == VirtualRelayIoAuthority.ProtectionAuthoritative ? "AUTH" : "SIM";
            return $"{selected}{row.Prefix}{row.Point.Number} {TrimLine(row.Point.Name, 12),-12} {(row.Point.State ? 1 : 0)} {source}";
        });

        SetRelayLcd(
            "MONITOR I/O · BI / BO",
            string.Join(Environment.NewLine, lines),
            "OK TOGGLE SIM BI · BO AUTH READ-ONLY");
        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("ALARMS"),
            new VirtualRelaySoftKey("GROUPS"),
            new VirtualRelaySoftKey("LED CFG"),
            new VirtualRelaySoftKey("ACCESS"),
            new VirtualRelaySoftKey("SYSTEM")
        });
    }

    private void RenderRelayAlarmPage()
    {
        var alarms = _relayOperatorAlarms.NewestFirst;
        if (alarms.Count == 0)
        {
            SetRelayLcd("ALARMS", "NO ALARMS RECORDED", "ACK IS SEPARATE FROM RESET");
        }
        else
        {
            _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, alarms.Count - 1);
            var alarm = alarms[_relayOperatorSelection];
            var state = alarm.Active ? "ACTIVE" : "CLEARED";
            var ack = alarm.Acknowledged ? "ACK" : "UNACK";
            SetRelayLcd(
                $"ALARM {alarm.Sequence:000} · {alarm.Severity.ToString().ToUpperInvariant()}",
                $"{alarm.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                $"{alarm.Code} · {state} · {ack}\n" +
                TrimLine(alarm.Message, 28),
                $"{_relayOperatorSelection + 1}/{alarms.Count} · RESET DOES NOT ACK");
        }

        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("ACK"),
            new VirtualRelaySoftKey("ACK ALL"),
            new VirtualRelaySoftKey("CLEAR"),
            new VirtualRelaySoftKey("I/O"),
            new VirtualRelaySoftKey("SYSTEM")
        });
    }

    private void RenderRelaySettingGroupPage()
    {
        var a = _relaySettingGroups["GROUP A"];
        var b = _relaySettingGroups["GROUP B"];
        var aSelected = _relayOperatorSelection == 0 ? ">" : " ";
        var bSelected = _relayOperatorSelection == 1 ? ">" : " ";
        var aActive = _relayActiveSettingGroup == "GROUP A" ? "ACTIVE" : "STBY";
        var bActive = _relayActiveSettingGroup == "GROUP B" ? "ACTIVE" : "STBY";

        SetRelayLcd(
            "SETTING GROUPS A / B",
            $"{aSelected}A {aActive,-6} REV {a.Revision,2} {a.Fingerprint()[..6]}\n" +
            $"{bSelected}B {bActive,-6} REV {b.Revision,2} {b.Fingerprint()[..6]}\n" +
            $"ACCESS {_relayAccessLevel.ToString().ToUpperInvariant()}\n" +
            "GROUP SWITCH RESETS TIMERS/LATCH",
            _relaySettingGroupSwitching ? "APPLYING GROUP..." : "OK ACTIVATE · ENGINEER REQUIRED");

        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("EDIT"),
            new VirtualRelaySoftKey("ACT A", _relayActiveSettingGroup == "GROUP A"),
            new VirtualRelaySoftKey("ACT B", _relayActiveSettingGroup == "GROUP B"),
            new VirtualRelaySoftKey("ACCESS"),
            new VirtualRelaySoftKey("SYSTEM")
        });
    }

    private void RenderRelayLedAssignmentPage()
    {
        _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, _relayProgrammableLedAssignments.Length - 1);
        var lines = _relayProgrammableLedAssignments.Select((signal, index) =>
            $"{(index == _relayOperatorSelection ? ">" : " ")}LED{index + 1} {VirtualRelayLedSignalLabels.Label(signal)}");
        SetRelayLcd(
            "PROGRAMMABLE LED",
            string.Join(Environment.NewLine, lines),
            "ENGINEER · ◀/▶ OR F1/F2 CHANGE");
        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("PREV"),
            new VirtualRelaySoftKey("NEXT"),
            new VirtualRelaySoftKey("DEFAULT"),
            new VirtualRelaySoftKey("ACCESS"),
            new VirtualRelaySoftKey("SYSTEM")
        });
    }

    private void RenderRelayAccessPage()
    {
        _relayOperatorSelection = Math.Clamp(_relayOperatorSelection, 0, 2);
        var labels = new[]
        {
            "VIEW      READ ONLY",
            "OPERATOR  ACK / SIM BI",
            "ENGINEER  SETTINGS / LED"
        };
        var lines = labels.Select((label, index) =>
            $"{(index == _relayOperatorSelection ? ">" : " ")}{label}");
        SetRelayLcd(
            "ACCESS PROFILE · LAB",
            string.Join(Environment.NewLine, lines) + "\n\nNO PASSWORD / SECURITY CLAIM",
            $"ACTIVE {_relayAccessLevel.ToString().ToUpperInvariant()} · OK SELECT");
        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("VIEW", _relayAccessLevel == VirtualRelayAccessLevel.View),
            new VirtualRelaySoftKey("OPR", _relayAccessLevel == VirtualRelayAccessLevel.Operator),
            new VirtualRelaySoftKey("ENG", _relayAccessLevel == VirtualRelayAccessLevel.Engineer),
            new VirtualRelaySoftKey("I/O"),
            new VirtualRelaySoftKey("SYSTEM")
        });
    }

    private void RenderRelayOperatorSystemPage(ProtectionSnapshot snapshot)
    {
        var source = _relayLastFrame?.SourceSummary ?? "NO SOURCE";
        SetRelayLcd(
            "DEVICE / SYSTEM",
            $"ARVREL 650 · OCR / FEEDER\n" +
            $"GROUP  {_relayActiveSettingGroup}\n" +
            $"ACCESS {_relayAccessLevel.ToString().ToUpperInvariant()}\n" +
            $"ALARM  A{_relayOperatorAlarms.ActiveCount} U{_relayOperatorAlarms.UnacknowledgedCount}\n" +
            $"SOURCE {TrimLine(source, 20)}",
            snapshot.SmvTrust.AllowsTrip ? "VIRTUAL OUTPUT · TRIP PERMITTED" : $"TRIP BLOCKED · {snapshot.SmvTrust.Code}");
        _p6VirtualRelay!.SetSoftKeys(new[]
        {
            new VirtualRelaySoftKey("I/O"),
            new VirtualRelaySoftKey("ALARMS"),
            new VirtualRelaySoftKey("GROUPS"),
            new VirtualRelaySoftKey("LED CFG"),
            new VirtualRelaySoftKey("ACCESS")
        });
    }

    private void RefreshRelayProgrammableAnnunciation(
        RelayAnnunciationSnapshot indication,
        ProtectionSnapshot snapshot)
    {
        var lamps = new[] { PhaseALed, PhaseBLed, PhaseCLed, EarthLed };
        for (var index = 0; index < lamps.Length; index++)
        {
            SetAnnunciationState(
                lamps[index],
                ResolveProgrammableLampState(_relayProgrammableLedAssignments[index], indication, snapshot));
        }
        _p6VirtualRelay?.SetProgrammableLampLabels(CurrentProgrammableLedLabels());
    }

    private P6AnnunciationLampState ResolveProgrammableLampState(
        VirtualRelayLedSignal signal,
        RelayAnnunciationSnapshot indication,
        ProtectionSnapshot snapshot)
        => signal switch
        {
            VirtualRelayLedSignal.PhaseA => ResolveAnnunciationState(indication.PhaseA),
            VirtualRelayLedSignal.PhaseB => ResolveAnnunciationState(indication.PhaseB),
            VirtualRelayLedSignal.PhaseC => ResolveAnnunciationState(indication.PhaseC),
            VirtualRelayLedSignal.Earth => ResolveAnnunciationState(indication.Earth),
            VirtualRelayLedSignal.Pickup => indication.PickupActive ? P6AnnunciationLampState.Amber : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.Trip => indication.TripLatched ? P6AnnunciationLampState.Red : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.SmvBlock => !snapshot.SmvTrust.AllowsTrip ? P6AnnunciationLampState.Amber : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.Healthy => snapshot.SmvTrust.AllowsTrip && !snapshot.Blocked && !snapshot.TripLatched
                ? P6AnnunciationLampState.Green
                : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.ExternalAlarm => _relayOperatorIo.ExternalAlarm ? P6AnnunciationLampState.Amber : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.TestMode => _relayOperatorIo.TestMode ? P6AnnunciationLampState.Amber : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.LocalMode => _relayOperatorIo.LocalMode ? P6AnnunciationLampState.Green : P6AnnunciationLampState.Off,
            VirtualRelayLedSignal.SettingGroupB => _relayActiveSettingGroup == "GROUP B" ? P6AnnunciationLampState.Green : P6AnnunciationLampState.Off,
            _ => P6AnnunciationLampState.Off
        };
}
