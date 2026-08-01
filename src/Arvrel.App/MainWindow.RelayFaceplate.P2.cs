using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private static readonly (RelayLcdPage Page, string Label)[] RelayMenuItems =
    {
        (RelayLcdPage.Measurements, "MEASUREMENTS"),
        (RelayLcdPage.Protection, "OVERCURRENT"),
        (RelayLcdPage.Feeder, "FEEDER PROTECTION"),
        (RelayLcdPage.Settings, "SETTINGS"),
        (RelayLcdPage.Events, "EVENTS"),
        (RelayLcdPage.Diagnostics, "PROCESS BUS"),
        (RelayLcdPage.TripRecord, "LAST TRIP")
    };

    private readonly RelayOperationRecorder _relayOperationRecorder = new();
    private readonly List<RelayFaceplateEvent> _relayFaceplateEvents = new();
    private DispatcherTimer? _relayFaceplateTimer;
    private TextBlock? _relayLcdHeader;
    private TextBlock? _relayLcdBody;
    private TextBlock? _relayLcdFooter;
    private RelayLcdPage _relayLcdPage = RelayLcdPage.Measurements;
    private bool _relayMenuOpen;
    private int _relayMenuIndex;
    private int _relayEventIndex;
    private bool _relayFaceplateInitialized;
    private string? _relaySourceIdentity;
    private string? _relaySettingsFingerprint;
    private RelayFaceplateFrame? _relayLastFrame;

    internal void InitializeRelayFaceplate()
    {
        if (_relayFaceplateInitialized || LcdHeaderText.Parent is not Panel lcdPanel)
            return;

        _relayFaceplateInitialized = true;
        lcdPanel.Children.Clear();
        _relayLcdHeader = LcdText(10.8, FontWeights.SemiBold);
        _relayLcdHeader.Margin = new Thickness(0, 0, 0, 8);
        _relayLcdBody = LcdText(9.9, FontWeights.Normal);
        _relayLcdBody.LineHeight = 16;
        _relayLcdBody.Height = 106;
        _relayLcdBody.TextWrapping = TextWrapping.NoWrap;
        _relayLcdFooter = LcdText(8.6, FontWeights.Normal);
        _relayLcdFooter.TextTrimming = TextTrimming.CharacterEllipsis;

        lcdPanel.Children.Add(_relayLcdHeader);
        lcdPanel.Children.Add(_relayLcdBody);
        lcdPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(166, 178, 173)),
            Margin = new Thickness(0, 7, 0, 7)
        });
        lcdPanel.Children.Add(_relayLcdFooter);

        foreach (var button in Descendants<Button>(this))
        {
            switch (button.ToolTip?.ToString())
            {
                case "Up": button.Click += RelayUp_Click; break;
                case "Down": button.Click += RelayDown_Click; break;
                case "Enter": button.Click += RelayEnter_Click; break;
                case "Next": button.Click += RelayNext_Click; break;
                case "Cancel": button.Click += RelayBack_Click; break;
            }
        }

        _relaySettingsFingerprint = _settings.Fingerprint();
        _relayFaceplateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _relayFaceplateTimer.Tick += RelayFaceplateTimer_Tick;
        _relayFaceplateTimer.Start();
        AddRelayFaceplateEvent(DateTimeOffset.Now, "READY", "Multifunction faceplate initialized");
        TickRelayFaceplate();
    }

    private TextBlock LcdText(double fontSize, FontWeight weight)
        => new()
        {
            Foreground = FindResource("LcdTextBrush") as Brush,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = fontSize,
            FontWeight = weight
        };

    internal void StopRelayFaceplate()
    {
        _relayFaceplateTimer?.Stop();
        if (_relayFaceplateTimer is not null)
            _relayFaceplateTimer.Tick -= RelayFaceplateTimer_Tick;
        _relayFaceplateTimer = null;
    }

    private void RelayFaceplateTimer_Tick(object? sender, EventArgs e) => TickRelayFaceplate();

    private void TickRelayFaceplate()
    {
        if (!_relayFaceplateInitialized)
            return;
        var frame = ReadRelayFaceplateFrame();
        if (frame is null)
            return;

        if (!string.Equals(_relaySourceIdentity, frame.SourceIdentity, StringComparison.Ordinal))
        {
            _relaySourceIdentity = frame.SourceIdentity;
            _relayOperationRecorder.ResetCurrent();
            _relayLcdPage = RelayLcdPage.Measurements;
            _relayMenuOpen = false;
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "SOURCE", frame.SourceSummary);
        }

        var fingerprint = _settings.Fingerprint();
        if (!string.Equals(_relaySettingsFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _relaySettingsFingerprint = fingerprint;
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "SETTING", $"{_settings.GroupName} rev {_settings.Revision}");
        }

        var transition = _relayOperationRecorder.Observe(frame.Snapshot, frame.Measurement);
        if (transition.PickupStarted && transition.Operation is { } pickup)
        {
            AddRelayFaceplateEvent(
                pickup.PickupTimestamp,
                "PICKUP",
                $"{pickup.PickupElement} · {pickup.QuantitySymbol} {pickup.PickupQuantity:0.00} {pickup.QuantityUnit}");
        }
        if (transition.TripOccurred && transition.Operation is { } trip)
        {
            var operate = trip.OperateTime?.TotalMilliseconds ?? 0;
            AddRelayFaceplateEvent(
                trip.TripTimestamp ?? frame.Snapshot.Timestamp,
                "TRIP",
                $"{trip.TripElement} · {operate:0.0} ms · {trip.QuantitySymbol} {trip.TripQuantity:0.00} {trip.QuantityUnit}");
            _relayMenuOpen = false;
            _relayLcdPage = RelayLcdPage.TripRecord;
        }
        if (transition.BlockedStarted)
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "BLOCK", frame.Snapshot.SmvTrust.Code);
        if (transition.ResetObserved)
        {
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "RESET", "Trip indication reset");
            if (_relayLcdPage == RelayLcdPage.TripRecord)
                _relayLcdPage = RelayLcdPage.Measurements;
        }

        _relayLastFrame = frame;
        RenderRelayLcd(frame);
    }

    private RelayFaceplateFrame? ReadRelayFaceplateFrame()
    {
        try
        {
            if (SourceCombo.SelectedIndex == 0)
            {
                var scenarioFrame = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition).Measurement;
                var measurement = scenarioFrame with
                {
                    Timestamp = _snapshot.Timestamp,
                    SmvTrust = _snapshot.SmvTrust
                };
                return new(
                    measurement,
                    _snapshot,
                    "internal",
                    "INTERNAL DEMO",
                    "Deterministic 4I+4V source · trust guard active");
            }

            var runtime = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
            if (string.IsNullOrWhiteSpace(runtime.Stream.Key))
                return null;
            return new(
                runtime.Measurement,
                runtime.Protection,
                runtime.Stream.Key,
                $"{runtime.Stream.Health} · APPID 0x{runtime.Stream.AppId:X4}",
                runtime.TrustSummary);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void RelayUp_Click(object sender, RoutedEventArgs e)
    {
        if (_relayMenuOpen) _relayMenuIndex = Wrap(_relayMenuIndex - 1, RelayMenuItems.Length);
        else if (_relayLcdPage == RelayLcdPage.Events) BrowseRelayEvents(+1);
        else CycleRelayPage(-1);
        RenderRelayLcd(_relayLastFrame);
    }

    private void RelayDown_Click(object sender, RoutedEventArgs e)
    {
        if (_relayMenuOpen) _relayMenuIndex = Wrap(_relayMenuIndex + 1, RelayMenuItems.Length);
        else if (_relayLcdPage == RelayLcdPage.Events) BrowseRelayEvents(-1);
        else CycleRelayPage(+1);
        RenderRelayLcd(_relayLastFrame);
    }

    private void RelayEnter_Click(object sender, RoutedEventArgs e)
    {
        if (_relayMenuOpen)
        {
            _relayLcdPage = RelayMenuItems[_relayMenuIndex].Page;
            _relayMenuOpen = false;
            _relayEventIndex = 0;
        }
        else if (_relayLcdPage == RelayLcdPage.Settings)
        {
            ProtectionSettings_Click(sender, e);
        }
        else
        {
            _relayMenuOpen = true;
            var index = Array.FindIndex(RelayMenuItems, item => item.Page == _relayLcdPage);
            _relayMenuIndex = index < 0 ? 0 : index;
        }
        RenderRelayLcd(_relayLastFrame);
    }

    private void RelayNext_Click(object sender, RoutedEventArgs e)
    {
        if (_relayMenuOpen)
        {
            _relayLcdPage = RelayMenuItems[_relayMenuIndex].Page;
            _relayMenuOpen = false;
        }
        else CycleRelayPage(+1);
        RenderRelayLcd(_relayLastFrame);
    }

    private void RelayBack_Click(object sender, RoutedEventArgs e)
    {
        _relayMenuOpen = false;
        _relayLcdPage = RelayLcdPage.Measurements;
        RenderRelayLcd(_relayLastFrame);
    }

    private void CycleRelayPage(int direction)
    {
        var current = Array.FindIndex(RelayMenuItems, item => item.Page == _relayLcdPage);
        current = Wrap((current < 0 ? 0 : current) + direction, RelayMenuItems.Length);
        _relayLcdPage = RelayMenuItems[current].Page;
        _relayEventIndex = 0;
    }

    private void BrowseRelayEvents(int olderDirection)
    {
        if (_relayFaceplateEvents.Count > 0)
            _relayEventIndex = Math.Clamp(_relayEventIndex + olderDirection, 0, _relayFaceplateEvents.Count - 1);
    }

    private void RenderRelayLcd(RelayFaceplateFrame? frame)
    {
        if (_relayLcdHeader is null || _relayLcdBody is null || _relayLcdFooter is null)
            return;
        if (_relayMenuOpen) { RenderRelayMenu(); return; }

        switch (_relayLcdPage)
        {
            case RelayLcdPage.Measurements: RenderMeasurementPage(frame); break;
            case RelayLcdPage.Protection: RenderProtectionPage(frame); break;
            case RelayLcdPage.Feeder: RenderFeederProtectionPage(frame); break;
            case RelayLcdPage.Settings: RenderSettingsPage(); break;
            case RelayLcdPage.Events: RenderEventsPage(); break;
            case RelayLcdPage.Diagnostics: RenderDiagnosticsPage(frame); break;
            case RelayLcdPage.TripRecord: RenderTripRecordPage(frame); break;
        }
    }

    private void RenderRelayMenu()
    {
        var start = Math.Clamp(_relayMenuIndex - 2, 0, Math.Max(0, RelayMenuItems.Length - 5));
        var lines = RelayMenuItems.Skip(start).Take(5)
            .Select((item, offset) => start + offset == _relayMenuIndex ? $"> {item.Label}" : $"  {item.Label}");
        SetRelayLcd("MAIN MENU", string.Join(Environment.NewLine, lines), "▲/▼ SELECT · ENTER OPEN");
    }

    private void RenderMeasurementPage(RelayFaceplateFrame? frame)
    {
        if (frame is null) { SetRelayLcd("MEASUREMENTS", "WAITING FOR DATA", "ENTER MENU"); return; }
        var m = frame.Measurement;
        var domain = ViewCombo.SelectedIndex == 1 ? "PRIMARY" : "SECONDARY";
        if (m.Phasors is not { } phasors)
        {
            SetRelayLcd($"{domain} CURRENT",
                $"IA       {m.PhaseA,7:0.00} A\nIB       {m.PhaseB,7:0.00} A\nIC       {m.PhaseC,7:0.00} A\n3I0      {m.Residual,7:0.00} A\nV PHASOR UNAVAILABLE",
                frame.Snapshot.TripLatched ? "TRIP LATCHED · ENTER MENU" : "READY · ENTER MENU");
            return;
        }

        SetRelayLcd($"{domain} PHASORS",
            $"I1       {phasors.PositiveSequenceCurrent.Magnitude,7:0.00} A\n" +
            $"3I0      {phasors.ResidualCurrent.Magnitude,7:0.00} A\n" +
            $"V1       {phasors.PositiveSequenceVoltage.Magnitude,7:0.00} V\n" +
            $"3V0      {phasors.ResidualVoltage.Magnitude,7:0.00} V\n" +
            $"FREQ     {phasors.FrequencyHz,7:0.000} Hz",
            frame.Snapshot.TripLatched ? "TRIP LATCHED · ENTER MENU" : "4I+4V READY · ENTER MENU");
    }

    private void RenderProtectionPage(RelayFaceplateFrame? frame)
    {
        if (frame is null) { SetRelayLcd("OVERCURRENT", "NO ACTIVE DATA", "ENTER MENU"); return; }
        var s = frame.Snapshot;
        SetRelayLcd("OVERCURRENT STATUS",
            $"50P-1 {Stage(s.Phase50),-7} {s.Phase50.Progress * 100,3:0}%\n" +
            $"51P   {Stage(s.Phase51),-7} {s.Phase51.Progress * 100,3:0}%\n" +
            $"50N   {Stage(s.Earth50),-7} {s.Earth50.Progress * 100,3:0}%\n" +
            $"51N   {Stage(s.Earth51),-7} {s.Earth51.Progress * 100,3:0}%",
            s.SmvTrust.AllowsTrip ? "TRIP PERMITTED · NEXT FEEDER" : $"BLOCKED {s.SmvTrust.Code}");
    }

    private void RenderFeederProtectionPage(RelayFaceplateFrame? frame)
    {
        if (frame is null) { SetRelayLcd("FEEDER PROTECTION", "NO ACTIVE DATA", "ENTER MENU"); return; }
        var f = frame.Snapshot.Feeder;
        var footer = frame.Snapshot.SmvTrust.AllowsTrip
            ? DirectionFooter(f)
            : $"BLOCKED {frame.Snapshot.SmvTrust.Code}";
        SetRelayLcd("FEEDER PROTECTION",
            $"67P  {Stage(f.DirectionalPhase67),-7} {f.DirectionalPhase67.Progress * 100,3:0}%\n" +
            $"67N  {Stage(f.DirectionalEarth67N),-7} {f.DirectionalEarth67N.Progress * 100,3:0}%\n" +
            $"27   {Stage(f.Undervoltage27),-7} {f.Undervoltage27.Progress * 100,3:0}%\n" +
            $"59   {Stage(f.Overvoltage59),-7} {f.Overvoltage59.Progress * 100,3:0}%\n" +
            $"59N  {Stage(f.ResidualOvervoltage59N),-7} {f.ResidualOvervoltage59N.Progress * 100,3:0}%",
            footer);
    }

    private static string DirectionFooter(FeederProtectionSnapshot feeder)
    {
        if (feeder.DirectionalPhase67.Pickup || feeder.DirectionalPhase67.Operated)
            return $"67P {DirectionLabel(feeder.PhaseDirectionalDecision)} {feeder.PhaseDirectionalDecision.OperatingAngleDegrees:0.0}°";
        if (feeder.DirectionalEarth67N.Pickup || feeder.DirectionalEarth67N.Operated)
            return $"67N {DirectionLabel(feeder.EarthDirectionalDecision)} {feeder.EarthDirectionalDecision.OperatingAngleDegrees:0.0}°";
        return "TRIP PERMITTED · ENTER MENU";
    }

    private static string DirectionLabel(DirectionalDecision decision)
        => !decision.PolarizingAvailable ? "NO VPOL" : decision.InSelectedDirection ? "SELECTED" : "RESTRAIN";

    private void RenderSettingsPage()
    {
        var phaseCurve = IecCurveCalculator.GetParameters(_settings.PhaseTimeCurve,
            _settings.PhaseTimeUserK, _settings.PhaseTimeUserAlpha, _settings.PhaseTimeUserC).ShortName;
        var earthCurve = IecCurveCalculator.GetParameters(_settings.EarthTimeCurve,
            _settings.EarthTimeUserK, _settings.EarthTimeUserAlpha, _settings.EarthTimeUserC).ShortName;
        var feederEnabled = EnabledFeederFunctions(_settings.Feeder);
        SetRelayLcd($"SETTINGS · {_settings.GroupName}",
            $"REV {_settings.Revision}  {_settings.Fingerprint()[..8]}\n" +
            $"51P {_settings.PhaseTimePickupA:0.###}A {phaseCurve}\n" +
            $"51N {_settings.EarthTimePickupA:0.###}A {earthCurve}\n" +
            $"FEEDER {TrimLine(feederEnabled, 20)}\n" +
            $"VT {_processBus.MeasurementContext.VtPrimaryV:0.#}/{_processBus.MeasurementContext.VtSecondaryV:0.#}V",
            "ENTER EDIT SETTINGS · X BACK");
    }

    private static string EnabledFeederFunctions(FeederProtectionSettings settings)
    {
        var enabled = new List<string>();
        if (settings.DirectionalPhase67Enabled) enabled.Add("67P");
        if (settings.DirectionalEarth67NEnabled) enabled.Add("67N");
        if (settings.Undervoltage27Enabled) enabled.Add("27");
        if (settings.Overvoltage59Enabled) enabled.Add("59");
        if (settings.ResidualOvervoltage59NEnabled) enabled.Add("59N");
        return enabled.Count == 0 ? "OFF" : string.Join(',', enabled);
    }

    private void RenderEventsPage()
    {
        if (_relayFaceplateEvents.Count == 0) { SetRelayLcd("EVENTS", "NO EVENTS RECORDED", "ENTER MENU"); return; }
        var events = _relayFaceplateEvents.AsEnumerable().Reverse().ToArray();
        _relayEventIndex = Math.Clamp(_relayEventIndex, 0, events.Length - 1);
        var item = events[_relayEventIndex];
        SetRelayLcd($"EVENT {item.Sequence:000} · {item.Code}",
            $"DATE  {item.Timestamp:yyyy-MM-dd}\nTIME  {item.Timestamp:HH:mm:ss.fff}\n{TrimLine(item.Detail, 27)}",
            $"▲ OLDER · ▼ NEWER · {_relayEventIndex + 1}/{events.Length}");
    }

    private void RenderDiagnosticsPage(RelayFaceplateFrame? frame)
    {
        if (frame is null) { SetRelayLcd("PROCESS BUS", "WAITING FOR SV", "ENTER MENU"); return; }
        var trust = frame.Snapshot.SmvTrust;
        SetRelayLcd("PROCESS BUS",
            $"SOURCE  {TrimLine(frame.SourceSummary, 19)}\n" +
            $"STATE   {trust.Code}\n" +
            $"MEASURE {(trust.AllowsMeasurement ? "YES" : "NO")}\n" +
            $"PICKUP  {(trust.AllowsPickup ? "YES" : "NO")}\n" +
            $"TRIP    {(trust.AllowsTrip ? "YES" : "NO")}",
            TrimLine(frame.Diagnostics, 33));
    }

    private void RenderTripRecordPage(RelayFaceplateFrame? frame)
    {
        var operation = _relayOperationRecorder.Current;
        if (operation?.TripTimestamp is not { } tripAt)
        {
            SetRelayLcd("LAST TRIP", "NO TRIP RECORD", "ENTER MENU");
            return;
        }

        var element = string.IsNullOrWhiteSpace(operation.TripElement) ? operation.PickupElement : operation.TripElement;
        var operateMs = operation.OperateTime?.TotalMilliseconds ?? 0;
        var fifthLine = DirectionalTripLine(element, frame?.Snapshot.Feeder);
        SetRelayLcd($"TRIP RECORD · {element}",
            $"PICKUP {operation.PickupTimestamp:HH:mm:ss.fff}\n" +
            $"TRIP   {tripAt:HH:mm:ss.fff}\n" +
            $"P→T       {operateMs,7:0.0} ms\n" +
            $"{operation.QuantitySymbol,-5} {operation.TripQuantity,8:0.00} {operation.QuantityUnit}\n" +
            fifthLine,
            "TRIP LATCHED · ENTER MENU");
    }

    private static string DirectionalTripLine(string element, FeederProtectionSnapshot? feeder)
    {
        if (feeder is null)
            return string.Empty;
        var decision = element switch
        {
            "67P" => feeder.PhaseDirectionalDecision,
            "67N" => feeder.EarthDirectionalDecision,
            _ => null
        };
        return decision is null
            ? string.Empty
            : $"ANGLE {decision.OperatingAngleDegrees,8:0.0}°  VP {decision.PolarizingMagnitude:0.0}V";
    }

    private void SetRelayLcd(string header, string body, string footer)
    {
        if (_relayLcdHeader is null || _relayLcdBody is null || _relayLcdFooter is null)
            return;
        _relayLcdHeader.Text = header.ToUpperInvariant();
        _relayLcdBody.Text = body;
        _relayLcdFooter.Text = footer.ToUpperInvariant();
    }

    private void AddRelayFaceplateEvent(DateTimeOffset timestamp, string code, string detail)
    {
        _relayFaceplateEvents.Add(new(
            _relayFaceplateEvents.Count == 0 ? 1 : _relayFaceplateEvents[^1].Sequence + 1,
            timestamp,
            code,
            detail));
        if (_relayFaceplateEvents.Count > 40)
            _relayFaceplateEvents.RemoveAt(0);
        _relayEventIndex = 0;
    }

    private static string Stage(ElementSnapshot element) => element.State switch
    {
        ProtectionStageState.Operated => "TRIP",
        ProtectionStageState.Blocked => "BLOCK",
        ProtectionStageState.Timing => "TIMING",
        ProtectionStageState.Pickup => "PICKUP",
        ProtectionStageState.Disabled => "OFF",
        _ => "READY"
    };

    private static int Wrap(int value, int count) => (value % count + count) % count;

    private static string TrimLine(string value, int length)
    {
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= length ? normalized : normalized[..Math.Max(1, length - 1)] + "…";
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typed)
                yield return typed;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }

    private enum RelayLcdPage
    {
        Measurements,
        Protection,
        Feeder,
        Settings,
        Events,
        Diagnostics,
        TripRecord
    }

    private sealed record RelayFaceplateFrame(
        MeasurementFrame Measurement,
        ProtectionSnapshot Snapshot,
        string SourceIdentity,
        string SourceSummary,
        string Diagnostics);

    private sealed record RelayFaceplateEvent(
        int Sequence,
        DateTimeOffset Timestamp,
        string Code,
        string Detail);
}

internal static class RelayFaceplateBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.UnloadedEvent, new RoutedEventHandler(OnUnloaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeRelayFaceplate();
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopRelayFaceplate();
    }
}
