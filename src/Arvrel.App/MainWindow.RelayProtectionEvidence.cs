using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.Protection;

namespace Arvrel.App;

/// <summary>
/// P1.3 relay-local event and trip evidence browser.
///
/// The front panel presents protection evidence in the compact multi-page style of
/// a modern numerical relay. It never reconstructs a fault quantity that the engine
/// did not retain. Element, pickup/trip timestamps, operating quantity and phase
/// attribution come from ProtectionSnapshot.LatchedOperation / RelayOperationRecorder.
/// Per-phase currents are shown only when the UI captured the exact trip sample;
/// otherwise the display explicitly says that IABC was not latched and keeps the
/// authoritative I OP / 3I0 / voltage operating quantity instead.
/// </summary>
public partial class MainWindow
{
    private enum RelayEvidencePage
    {
        None,
        Events,
        TripSummary,
        TripFault,
        TripSystem
    }

    private readonly List<RelayTripEvidenceLogEntry> _relayTripEvidenceLog = new();
    private readonly Dictionary<int, RelayEventEvidenceMeta> _relayEventEvidenceMeta = new();
    private DispatcherTimer? _relayEvidenceTimer;
    private RelayEvidencePage _relayEvidencePage;
    private int _relayTripEvidenceIndex;
    private int _relayEvidenceLastScannedEventSequence;
    private bool _relayEvidenceInitialized;

    internal void InitializeRelayProtectionEvidence()
    {
        if (_relayEvidenceInitialized)
            return;

        _relayEvidenceInitialized = true;
        _relayEvidenceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _relayEvidenceTimer.Tick += RelayEvidenceTimer_Tick;
        _relayEvidenceTimer.Start();
        Closed += RelayEvidenceWindow_Closed;

        CaptureRelayProtectionEvidence();
    }

    private void RelayEvidenceWindow_Closed(object? sender, EventArgs e)
    {
        if (_relayEvidenceTimer is null)
            return;

        _relayEvidenceTimer.Stop();
        _relayEvidenceTimer.Tick -= RelayEvidenceTimer_Tick;
        _relayEvidenceTimer = null;
    }

    private void RelayEvidenceTimer_Tick(object? sender, EventArgs e)
    {
        CaptureRelayProtectionEvidence();
        if (_relayEvidencePage == RelayEvidencePage.None)
            return;

        // While an evidence page owns the LCD, keep the same authoritative faceplate
        // observer alive so pickup/trip/event transitions are not lost.
        TickRelayFaceplate();
        CaptureRelayProtectionEvidence();

        if (_relayLcdPage == RelayLcdPage.TripRecord && _relayEvidencePage == RelayEvidencePage.Events)
        {
            _relayEvidencePage = RelayEvidencePage.TripSummary;
            _relayTripEvidenceIndex = 0;
        }

        RenderRelayEvidencePage();
    }

    internal void RouteRelayEvidenceHostPage()
    {
        if (_relayEvidencePage != RelayEvidencePage.None ||
            _relayMenuOpen ||
            _relayOperatorPage != RelayOperatorPage.None)
            return;

        if (_relayLcdPage == RelayLcdPage.Events)
            EnterRelayEvidencePage(RelayEvidencePage.Events);
        else if (_relayLcdPage == RelayLcdPage.TripRecord)
            EnterRelayEvidencePage(RelayEvidencePage.TripSummary);
    }

    internal bool HandleRelayEvidenceHardwareKey(VirtualRelayHardwareKey key)
    {
        if (_relayEvidencePage == RelayEvidencePage.None)
            return false;

        switch (key)
        {
            case VirtualRelayHardwareKey.Home:
                ExitRelayEvidencePage(restartFaceplateTimer: true);
                NavigateRelayPage(RelayLcdPage.Measurements);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Menu:
                ExitRelayEvidencePage(restartFaceplateTimer: true);
                OpenRelayMenu(RelayMenuLevel.Root);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Favorite:
                ExitRelayEvidencePage(restartFaceplateTimer: true);
                NavigateRelayPage(_relayFavoritePage);
                RenderRelayLcd(_relayLastFrame);
                return true;

            case VirtualRelayHardwareKey.Back:
            case VirtualRelayHardwareKey.Left:
                RelayEvidenceBack();
                return true;

            case VirtualRelayHardwareKey.Up:
                MoveRelayEvidenceSelection(+1);
                break;

            case VirtualRelayHardwareKey.Down:
                MoveRelayEvidenceSelection(-1);
                break;

            case VirtualRelayHardwareKey.Right:
            case VirtualRelayHardwareKey.Ok:
                OpenNextRelayEvidenceDetail();
                break;

            case VirtualRelayHardwareKey.F1:
                HandleRelayEvidenceFunctionKey(0);
                break;
            case VirtualRelayHardwareKey.F2:
                HandleRelayEvidenceFunctionKey(1);
                break;
            case VirtualRelayHardwareKey.F3:
                HandleRelayEvidenceFunctionKey(2);
                break;
            case VirtualRelayHardwareKey.F4:
                HandleRelayEvidenceFunctionKey(3);
                break;
            case VirtualRelayHardwareKey.F5:
                HandleRelayEvidenceFunctionKey(4);
                break;
        }

        RenderRelayEvidencePage();
        return true;
    }

    internal void NotifyRelayEvidenceReset()
    {
        // Protection RESET clears the latch, not the stored fault/event history.
        // Return the operator to measurement home while retaining trip records.
        if (_relayEvidencePage == RelayEvidencePage.None)
            return;

        ExitRelayEvidencePage(restartFaceplateTimer: true);
        NavigateRelayPage(RelayLcdPage.Measurements, remember: false);
        RenderRelayLcd(_relayLastFrame);
    }

    private void EnterRelayEvidencePage(RelayEvidencePage page)
    {
        CaptureRelayProtectionEvidence();
        _relayEvidencePage = page;
        _relayMenuOpen = false;
        _relayMenuLevel = RelayMenuLevel.None;
        _relayMenuIndex = 0;
        _relayFaceplateTimer?.Stop();

        if (page == RelayEvidencePage.Events)
        {
            _relayLcdPage = RelayLcdPage.Events;
            _relayEventIndex = Math.Clamp(_relayEventIndex, 0, Math.Max(0, _relayFaceplateEvents.Count - 1));
        }
        else
        {
            _relayLcdPage = RelayLcdPage.TripRecord;
            _relayTripEvidenceIndex = Math.Clamp(_relayTripEvidenceIndex, 0, Math.Max(0, _relayTripEvidenceLog.Count - 1));
        }

        RenderRelayEvidencePage();
    }

    private void ExitRelayEvidencePage(bool restartFaceplateTimer)
    {
        _relayEvidencePage = RelayEvidencePage.None;
        if (restartFaceplateTimer)
            _relayFaceplateTimer?.Start();
    }

    private void RelayEvidenceBack()
    {
        if (_relayEvidencePage is RelayEvidencePage.TripFault or RelayEvidencePage.TripSystem)
        {
            _relayEvidencePage = RelayEvidencePage.TripSummary;
            RenderRelayEvidencePage();
            return;
        }

        ExitRelayEvidencePage(restartFaceplateTimer: true);
        OpenRelayMenu(RelayMenuLevel.Records);
        RenderRelayLcd(_relayLastFrame);
    }

    private void MoveRelayEvidenceSelection(int olderDirection)
    {
        if (_relayEvidencePage == RelayEvidencePage.Events)
        {
            BrowseRelayEvents(olderDirection);
            return;
        }

        if (_relayTripEvidenceLog.Count == 0)
            return;

        _relayTripEvidenceIndex = Math.Clamp(
            _relayTripEvidenceIndex + olderDirection,
            0,
            _relayTripEvidenceLog.Count - 1);
    }

    private void OpenNextRelayEvidenceDetail()
    {
        if (_relayEvidencePage == RelayEvidencePage.Events)
        {
            if (SelectedRelayEventTrip() is not null)
                EnterRelayEvidencePage(RelayEvidencePage.TripSummary);
            return;
        }

        _relayEvidencePage = _relayEvidencePage switch
        {
            RelayEvidencePage.TripSummary => RelayEvidencePage.TripFault,
            RelayEvidencePage.TripFault => RelayEvidencePage.TripSystem,
            RelayEvidencePage.TripSystem => RelayEvidencePage.TripSummary,
            _ => _relayEvidencePage
        };
    }

    private void HandleRelayEvidenceFunctionKey(int index)
    {
        if (_relayEvidencePage == RelayEvidencePage.Events)
        {
            if (index == 0) BrowseRelayEvents(+1);
            else if (index == 1) BrowseRelayEvents(-1);
            else if (index == 2) EnterRelayEvidencePage(RelayEvidencePage.TripSummary);
            else if (index == 3) OpenNextRelayEvidenceDetail();
            else RelayEvidenceBack();
            return;
        }

        if (index == 0) _relayEvidencePage = RelayEvidencePage.TripSummary;
        else if (index == 1) _relayEvidencePage = RelayEvidencePage.TripFault;
        else if (index == 2) _relayEvidencePage = RelayEvidencePage.TripSystem;
        else if (index == 3) EnterRelayEvidencePage(RelayEvidencePage.Events);
        else RelayEvidenceBack();
    }

    private void RenderRelayEvidencePage()
    {
        switch (_relayEvidencePage)
        {
            case RelayEvidencePage.Events:
                RenderModernRelayEventPage();
                break;
            case RelayEvidencePage.TripSummary:
                RenderRelayTripSummaryPage();
                break;
            case RelayEvidencePage.TripFault:
                RenderRelayTripFaultPage();
                break;
            case RelayEvidencePage.TripSystem:
                RenderRelayTripSystemPage();
                break;
        }

        UpdateRelayEvidenceSoftKeys();
    }

    private void RenderModernRelayEventPage()
    {
        if (_relayFaceplateEvents.Count == 0)
        {
            SetRelayLcd("EVENT LOG", "NO EVENTS RECORDED", "BACK · RECORDS MENU");
            return;
        }

        var events = _relayFaceplateEvents.AsEnumerable().Reverse().ToArray();
        _relayEventIndex = Math.Clamp(_relayEventIndex, 0, events.Length - 1);
        var item = events[_relayEventIndex];
        _relayEventEvidenceMeta.TryGetValue(item.Sequence, out var meta);
        var trip = FindRelayTripEvidence(item.Timestamp);

        var eventClass = meta?.EventClass ?? RelayEventClass(item.Code);
        var line2 = trip is not null
            ? $"{trip.Element} · {trip.PhaseLabel}"
            : !string.IsNullOrWhiteSpace(meta?.PhaseLabel)
                ? $"{eventClass} · {meta!.PhaseLabel}"
                : eventClass;
        var line3 = trip is not null
            ? $"{trip.QuantitySymbol} {trip.TripQuantity:0.00} {trip.QuantityUnit} · {trip.OperateTimeMs:0.0} ms"
            : TrimLine(item.Detail, 31);
        var line4 = trip is not null
            ? $"TRUST {trip.TrustCode} · {TrimLine(trip.SourceSummary, 16)}"
            : meta?.StateDetail ?? string.Empty;

        SetRelayLcd(
            $"EVENT {item.Sequence:000} · {item.Code}",
            $"{item.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\n" +
            $"{line2}\n" +
            $"{line3}\n" +
            line4,
            $"▲ OLDER · ▼ NEWER · {_relayEventIndex + 1}/{events.Length}");
    }

    private void RenderRelayTripSummaryPage()
    {
        var trip = SelectedRelayTripEvidence();
        if (trip is null)
        {
            SetRelayLcd("TRIP LOG", "NO STORED TRIP RECORDS", "F4 EVENTS · BACK");
            return;
        }

        SetRelayLcd(
            $"TRIP LOG {trip.LogNumber:000} · {trip.Element}",
            $"PHASE  {trip.PhaseLabel}\n" +
            $"PICKUP {trip.PickupTimestamp:HH:mm:ss.fff}\n" +
            $"TRIP   {trip.TripTimestamp:HH:mm:ss.fff}\n" +
            $"P→T    {trip.OperateTimeMs,8:0.0} ms\n" +
            $"{trip.QuantitySymbol,-6} {trip.TripQuantity,8:0.00} {trip.QuantityUnit}",
            $"▲ OLD · ▼ NEW · {_relayTripEvidenceIndex + 1}/{_relayTripEvidenceLog.Count}");
    }

    private void RenderRelayTripFaultPage()
    {
        var trip = SelectedRelayTripEvidence();
        if (trip is null)
        {
            SetRelayLcd("FAULT QUANTITIES", "NO STORED TRIP RECORD", "F1 SUMMARY · F4 EVENTS");
            return;
        }

        string body;
        if (trip.ExactTripCurrentSample &&
            trip.TripPhaseA is { } ia &&
            trip.TripPhaseB is { } ib &&
            trip.TripPhaseC is { } ic &&
            trip.TripResidual is { } i0)
        {
            body =
                $"PHASE  {trip.PhaseLabel}\n" +
                $"IA {ia,7:0.00} A  IB {ib,7:0.00} A\n" +
                $"IC {ic,7:0.00} A  3I0{i0,7:0.00} A\n" +
                $"PICK {trip.PickupQuantity,7:0.00} {trip.QuantityUnit}\n" +
                $"TRIP {trip.TripQuantity,7:0.00} {trip.QuantityUnit}";
        }
        else
        {
            body =
                $"PHASE  {trip.PhaseLabel}\n" +
                $"{trip.QuantitySymbol} {trip.TripQuantity:0.00} {trip.QuantityUnit} · LATCHED\n" +
                $"PICKUP {trip.PickupQuantity:0.00} {trip.QuantityUnit}\n" +
                "IABC  NOT LATCHED\n" +
                "SHORT-FAULT EVIDENCE SAFE";
        }

        if (!string.IsNullOrWhiteSpace(trip.DirectionLabel))
            body = TrimEvidenceLines(body, 4) + $"\nDIR {trip.DirectionLabel} · {trip.DirectionAngleDegrees:0.0}°";

        SetRelayLcd(
            $"FAULT · {trip.Element}",
            body,
            trip.ExactTripCurrentSample
                ? "EXACT TRIP SAMPLE · F1 SUMMARY"
                : "I OP / PHASE ARE ENGINE-LATCHED");
    }

    private void RenderRelayTripSystemPage()
    {
        var trip = SelectedRelayTripEvidence();
        if (trip is null)
        {
            SetRelayLcd("TRIP CONTEXT", "NO STORED TRIP RECORD", "F1 SUMMARY · F4 EVENTS");
            return;
        }

        var fingerprint = trip.SettingsFingerprint.Length >= 8
            ? trip.SettingsFingerprint[..8]
            : trip.SettingsFingerprint;
        SetRelayLcd(
            $"TRIP CONTEXT · {trip.Element}",
            $"GROUP {trip.SettingGroup} · REV {trip.SettingRevision}\n" +
            $"SET FP {fingerprint}\n" +
            $"SOURCE {TrimLine(trip.SourceSummary, 20)}\n" +
            $"TRUST  {trip.TrustCode}\n" +
            $"TRIP PERMIT {(trip.TripPermitted ? "YES" : "NO")}",
            $"REC {trip.TripTimestamp:yyyy-MM-dd HH:mm:ss.fff}");
    }

    private void UpdateRelayEvidenceSoftKeys()
    {
        if (_p6VirtualRelay is not { } relay)
            return;

        relay.SetSoftKeys(_relayEvidencePage == RelayEvidencePage.Events
            ? new[]
            {
                new VirtualRelaySoftKey("OLDER"),
                new VirtualRelaySoftKey("NEWER"),
                new VirtualRelaySoftKey("TRIPS"),
                new VirtualRelaySoftKey("DETAIL"),
                new VirtualRelaySoftKey("BACK")
            }
            : new[]
            {
                new VirtualRelaySoftKey("SUMMARY", _relayEvidencePage == RelayEvidencePage.TripSummary),
                new VirtualRelaySoftKey("FAULT", _relayEvidencePage == RelayEvidencePage.TripFault),
                new VirtualRelaySoftKey("SYSTEM", _relayEvidencePage == RelayEvidencePage.TripSystem),
                new VirtualRelaySoftKey("EVENTS"),
                new VirtualRelaySoftKey("BACK")
            });
    }

    private void CaptureRelayProtectionEvidence()
    {
        CaptureRelayTripEvidenceIfNew();
        CaptureRelayEventMetadata();
    }

    private void CaptureRelayTripEvidenceIfNew()
    {
        var operation = _relayOperationRecorder.Current;
        if (operation?.TripTimestamp is not { } tripAt ||
            _relayTripEvidenceLog.Any(item => item.TripTimestamp == tripAt))
            return;

        var frame = _relayLastFrame;
        var snapshot = frame?.Snapshot;
        var latched = snapshot?.LatchedOperation;
        if (latched?.TripTimestamp != tripAt)
            latched = null;

        var phaseLabel = latched is not null
            ? RelayFaultPhaseLabel(latched.PhaseA, latched.PhaseB, latched.PhaseC, latched.Earth)
            : snapshot is not null
                ? RelayFaultPhaseLabel(snapshot.PhaseAPickup, snapshot.PhaseBPickup, snapshot.PhaseCPickup, snapshot.EarthPickup)
                : "UNKNOWN";

        var exactTripSample = frame?.Measurement.Timestamp == tripAt;
        double? ia = exactTripSample ? frame!.Measurement.PhaseA : null;
        double? ib = exactTripSample ? frame!.Measurement.PhaseB : null;
        double? ic = exactTripSample ? frame!.Measurement.PhaseC : null;
        double? i0 = exactTripSample ? frame!.Measurement.Residual : null;

        var directionLabel = string.Empty;
        double? directionAngle = null;
        if (snapshot is not null && operation.TripElement == "67P" &&
            (snapshot.Feeder.DirectionalPhase67.Pickup || snapshot.Feeder.DirectionalPhase67.Operated) &&
            snapshot.Feeder.PhaseDirectionalDecision.PolarizingAvailable)
        {
            directionLabel = DirectionLabel(snapshot.Feeder.PhaseDirectionalDecision);
            directionAngle = snapshot.Feeder.PhaseDirectionalDecision.OperatingAngleDegrees;
        }
        else if (snapshot is not null && operation.TripElement == "67N" &&
                 (snapshot.Feeder.DirectionalEarth67N.Pickup || snapshot.Feeder.DirectionalEarth67N.Operated) &&
                 snapshot.Feeder.EarthDirectionalDecision.PolarizingAvailable)
        {
            directionLabel = DirectionLabel(snapshot.Feeder.EarthDirectionalDecision);
            directionAngle = snapshot.Feeder.EarthDirectionalDecision.OperatingAngleDegrees;
        }

        _relayTripEvidenceLog.Add(new RelayTripEvidenceLogEntry(
            _relayTripEvidenceLog.Count == 0 ? 1 : _relayTripEvidenceLog[^1].LogNumber + 1,
            operation.PickupTimestamp,
            tripAt,
            operation.TripElement,
            phaseLabel,
            operation.PickupQuantity,
            operation.TripQuantity,
            operation.QuantitySymbol,
            operation.QuantityUnit,
            operation.OperateTime?.TotalMilliseconds ?? 0,
            exactTripSample,
            ia,
            ib,
            ic,
            i0,
            _settings.GroupName,
            _settings.Revision,
            _settings.Fingerprint(),
            frame?.SourceIdentity ?? "NO SOURCE",
            frame?.SourceSummary ?? "NO SOURCE",
            snapshot?.SmvTrust.Code ?? "N/A",
            snapshot?.SmvTrust.AllowsTrip ?? false,
            directionLabel,
            directionAngle));

        if (_relayTripEvidenceLog.Count > 20)
            _relayTripEvidenceLog.RemoveAt(0);
        _relayTripEvidenceIndex = 0;
    }

    private void CaptureRelayEventMetadata()
    {
        if (_relayFaceplateEvents.Count == 0)
            return;

        var snapshot = _relayLastFrame?.Snapshot;
        foreach (var item in _relayFaceplateEvents.Where(item => item.Sequence > _relayEvidenceLastScannedEventSequence))
        {
            var trip = FindRelayTripEvidence(item.Timestamp);
            string phase;
            string state;

            if (trip is not null)
            {
                phase = trip.PhaseLabel;
                state = $"{trip.Element} · {trip.QuantitySymbol} {trip.TripQuantity:0.00} {trip.QuantityUnit}";
            }
            else if (item.Code == "PICKUP" && snapshot is not null && snapshot.Timestamp == item.Timestamp)
            {
                phase = RelayFaultPhaseLabel(
                    snapshot.PhaseAPickup,
                    snapshot.PhaseBPickup,
                    snapshot.PhaseCPickup,
                    snapshot.EarthPickup);
                state = TrimLine(snapshot.ActiveElement, 30);
            }
            else
            {
                phase = string.Empty;
                state = string.Empty;
            }

            _relayEventEvidenceMeta[item.Sequence] = new RelayEventEvidenceMeta(
                RelayEventClass(item.Code),
                phase,
                state);
            _relayEvidenceLastScannedEventSequence = Math.Max(_relayEvidenceLastScannedEventSequence, item.Sequence);
        }
    }

    private RelayTripEvidenceLogEntry? SelectedRelayTripEvidence()
    {
        if (_relayTripEvidenceLog.Count == 0)
            return null;

        var newest = _relayTripEvidenceLog.AsEnumerable().Reverse().ToArray();
        _relayTripEvidenceIndex = Math.Clamp(_relayTripEvidenceIndex, 0, newest.Length - 1);
        return newest[_relayTripEvidenceIndex];
    }

    private RelayTripEvidenceLogEntry? SelectedRelayEventTrip()
    {
        if (_relayFaceplateEvents.Count == 0)
            return null;

        var events = _relayFaceplateEvents.AsEnumerable().Reverse().ToArray();
        _relayEventIndex = Math.Clamp(_relayEventIndex, 0, events.Length - 1);
        return FindRelayTripEvidence(events[_relayEventIndex].Timestamp);
    }

    private RelayTripEvidenceLogEntry? FindRelayTripEvidence(DateTimeOffset timestamp)
        => _relayTripEvidenceLog.LastOrDefault(item =>
            item.TripTimestamp == timestamp || item.PickupTimestamp == timestamp);

    private static string RelayFaultPhaseLabel(bool phaseA, bool phaseB, bool phaseC, bool earth)
    {
        var phases = new List<string>(3);
        if (phaseA) phases.Add("A");
        if (phaseB) phases.Add("B");
        if (phaseC) phases.Add("C");

        if (phases.Count == 0)
            return earth ? "EARTH" : "UNKNOWN";

        var label = string.Join("+", phases);
        return earth ? $"{label}+G" : label;
    }

    private static string RelayEventClass(string code)
        => code switch
        {
            "PICKUP" or "TRIP" => "PROTECTION",
            "BLOCK" or "SMV_BLOCK" => "PROTECTION BLOCK",
            "RESET" or "RESET KEY" => "RESET",
            "SETTING" => "CONFIGURATION",
            "SOURCE" => "SOURCE / PROCESS BUS",
            "BINARY" => "BINARY I/O",
            "ACK" or "ACK ALL" => "OPERATOR ACTION",
            "HMI" or "READY" => "SYSTEM",
            _ => "EVENT / ALARM"
        };

    private static string TrimEvidenceLines(string value, int maxLines)
        => string.Join(
            Environment.NewLine,
            value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Take(maxLines));

    private sealed record RelayEventEvidenceMeta(
        string EventClass,
        string PhaseLabel,
        string StateDetail);

    private sealed record RelayTripEvidenceLogEntry(
        int LogNumber,
        DateTimeOffset PickupTimestamp,
        DateTimeOffset TripTimestamp,
        string Element,
        string PhaseLabel,
        double PickupQuantity,
        double TripQuantity,
        string QuantitySymbol,
        string QuantityUnit,
        double OperateTimeMs,
        bool ExactTripCurrentSample,
        double? TripPhaseA,
        double? TripPhaseB,
        double? TripPhaseC,
        double? TripResidual,
        string SettingGroup,
        int SettingRevision,
        string SettingsFingerprint,
        string SourceIdentity,
        string SourceSummary,
        string TrustCode,
        bool TripPermitted,
        string DirectionLabel,
        double? DirectionAngleDegrees);
}
