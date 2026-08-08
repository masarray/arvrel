using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App.Controls.Transformer;

/// <summary>
/// Transformer-specific presenter for the shared P6/OCR virtual-relay hardware.
/// It owns no relay geometry, material, tactile style, lamp optics, protection
/// algorithm, or process-bus runtime. It only projects authoritative transformer
/// snapshots into the existing <see cref="VirtualRelayControl"/> anchors.
/// </summary>
internal sealed class TransformerVirtualRelayPresenter
{
    private static readonly (TransformerRelayPage Page, string Label)[] MenuItems =
    [
        (TransformerRelayPage.Home, "HOME"),
        (TransformerRelayPage.Measurements, "MEASUREMENTS"),
        (TransformerRelayPage.Protection, "PROTECTION"),
        (TransformerRelayPage.Harmonics, "HARMONICS H2/H5"),
        (TransformerRelayPage.Ref, "REF HV/LV"),
        (TransformerRelayPage.PairTrust, "SV PAIR / TRUST"),
        (TransformerRelayPage.Settings, "SETTINGS"),
        (TransformerRelayPage.Events, "EVENTS"),
        (TransformerRelayPage.Records, "RECORDS / SELF-TEST"),
        (TransformerRelayPage.Diagnostics, "DIAGNOSTICS")
    ];

    private readonly VirtualRelayControl _relay;
    private readonly Brush _ledOffBrush;
    private readonly Brush _healthyBrush;
    private readonly Brush _warningBrush;
    private readonly Brush _tripBrush;
    private readonly Action _openEngineering;
    private readonly Action _runSelfTest;
    private readonly Action _resetRuntime;
    private readonly TextBlock _lcdHeader;
    private readonly TextBlock _lcdBody;
    private readonly TextBlock _lcdFooter;
    private readonly List<TransformerRelayEvent> _events = [];

    private TransformerProtectionRuntimeSnapshot? _snapshot;
    private TransformerPublicSelfTestReport? _selfTest;
    private ProcessBusSourceMode _sourceMode = ProcessBusSourceMode.InternalDemo;
    private int _streamCount;
    private bool _engineeringOpen;
    private TransformerRelayPage _page = TransformerRelayPage.Home;
    private bool _menuOpen;
    private int _menuIndex;
    private int _eventIndex;
    private int _sequence;
    private TransformerRuntimeState? _lastRuntimeState;
    private string? _lastActiveElement;
    private bool? _lastBlocked;

    public TransformerVirtualRelayPresenter(
        VirtualRelayControl relay,
        Brush ledOffBrush,
        Brush healthyBrush,
        Brush warningBrush,
        Brush tripBrush,
        Action openEngineering,
        Action runSelfTest,
        Action resetRuntime)
    {
        _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        _ledOffBrush = ledOffBrush ?? throw new ArgumentNullException(nameof(ledOffBrush));
        _healthyBrush = healthyBrush ?? throw new ArgumentNullException(nameof(healthyBrush));
        _warningBrush = warningBrush ?? throw new ArgumentNullException(nameof(warningBrush));
        _tripBrush = tripBrush ?? throw new ArgumentNullException(nameof(tripBrush));
        _openEngineering = openEngineering ?? throw new ArgumentNullException(nameof(openEngineering));
        _runSelfTest = runSelfTest ?? throw new ArgumentNullException(nameof(runSelfTest));
        _resetRuntime = resetRuntime ?? throw new ArgumentNullException(nameof(resetRuntime));

        // Presentation-only substitutions. The XAML, dimensions, shell, modules,
        // brushes, lamp controls, tactile behavior and scaling remain the OCR P6
        // implementation without a Transformer-specific clone.
        _relay.ApplyTextOverrides(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" 650"] = " 87T",
            ["PROCESS BUS PROTECTION RELAY"] = "TRANSFORMER DIFFERENTIAL RELAY",
            ["ARV-650"] = "ARV-87T",
            ["BAY 12 · FEEDER"] = "TRANSFORMER · DIFFERENTIAL",
            ["SV READY"] = "SV PAIR",
            ["PHASE A"] = "87T",
            ["PHASE B"] = "87T-HS",
            ["PHASE C"] = "REF HV",
            ["EARTH"] = "REF LV",
            ["SMV BLOCK"] = "BLOCK"
        });

        (_lcdHeader, _lcdBody, _lcdFooter) = InstallLcdPresenter();
        BindHardwareKeys();
        _relay.ResetRequested += (_, _) => _resetRuntime();

        SetAllLampsOff();
        AddEvent("READY", "Transformer presenter mounted on shared OCR relay hardware");
        Render();
    }

    public void UpdateEnvironment(ProcessBusSourceMode sourceMode, int streamCount, bool engineeringOpen)
    {
        var normalizedStreamCount = Math.Max(0, streamCount);
        var changed = _sourceMode != sourceMode ||
                      _streamCount != normalizedStreamCount ||
                      _engineeringOpen != engineeringOpen;

        _sourceMode = sourceMode;
        _streamCount = normalizedStreamCount;
        _engineeringOpen = engineeringOpen;
        UpdateRelayFooter();
        if (changed)
            Render();
    }

    public void UpdateSnapshot(TransformerProtectionRuntimeSnapshot? snapshot)
    {
        _snapshot = snapshot;
        if (snapshot is not null)
            ObserveSnapshot(snapshot);
        UpdateLamps();
        UpdateRelayFooter();
        Render();
    }

    public void UpdateSelfTest(TransformerPublicSelfTestReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _selfTest = report;
        AddEvent(report.AllPassed ? "TEST PASS" : "TEST FAIL",
            $"{report.PassedCount}/{report.Cases.Count} · {report.SuiteId}");
        _page = TransformerRelayPage.Records;
        _menuOpen = false;
        Render();
    }

    public void NoteRuntimeClosed()
    {
        _engineeringOpen = false;
        AddEvent("ENGINEER", "Engineering workspace closed; last evidence retained");
        UpdateRelayFooter();
        Render();
    }

    private (TextBlock Header, TextBlock Body, TextBlock Footer) InstallLcdPresenter()
    {
        if (_relay.LcdHeaderText.Parent is not Panel lcdPanel)
            throw new InvalidOperationException("Shared OCR relay LCD host could not be resolved.");

        lcdPanel.Children.Clear();
        var lcdText = ResolveBrush("VrLcdTextBrush", Brushes.Black);
        var lcdMuted = ResolveBrush("VrLcdMutedBrush", Brushes.DimGray);

        var header = new TextBlock
        {
            Foreground = lcdText,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.8,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var body = new TextBlock
        {
            Foreground = lcdText,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.1,
            FontWeight = FontWeights.Normal,
            LineHeight = 16.5,
            Height = 106,
            TextWrapping = TextWrapping.NoWrap
        };
        var footer = new TextBlock
        {
            Foreground = lcdMuted,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.6,
            FontWeight = FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        lcdPanel.Children.Add(header);
        lcdPanel.Children.Add(body);
        lcdPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(166, 178, 173)),
            Margin = new Thickness(0, 7, 0, 7)
        });
        lcdPanel.Children.Add(footer);
        return (header, body, footer);
    }

    private void BindHardwareKeys()
    {
        foreach (var button in Descendants<Button>(_relay))
        {
            if (button.Content is string functionKey)
            {
                switch (functionKey)
                {
                    case "F1": button.ToolTip = "Transformer measurements"; button.Click += (_, _) => OpenPage(TransformerRelayPage.Measurements); break;
                    case "F2": button.ToolTip = "Transformer events"; button.Click += (_, _) => OpenPage(TransformerRelayPage.Events); break;
                    case "F3": button.ToolTip = "Transformer records and self-test"; button.Click += (_, _) => OpenPage(TransformerRelayPage.Records); break;
                    case "F4": button.ToolTip = "Transformer engineering"; button.Click += (_, _) => _openEngineering(); break;
                    case "F5": button.ToolTip = "Reset transformer runtime"; button.Click += (_, _) => _resetRuntime(); break;
                }
            }

            switch (button.ToolTip?.ToString())
            {
                case "Up": button.Click += (_, _) => Move(-1); break;
                case "Down": button.Click += (_, _) => Move(1); break;
                case "Enter": button.Click += (_, _) => Enter(); break;
                case "Next": button.Click += (_, _) => Next(); break;
                case "Cancel": button.Click += (_, _) => Back(); break;
                case "Home page": button.Click += (_, _) => OpenPage(TransformerRelayPage.Home); break;
                case "Main menu": button.Click += (_, _) => OpenMenu(); break;
                case "Back": button.Click += (_, _) => Back(); break;
            }
        }
    }

    private void Move(int direction)
    {
        if (_menuOpen)
        {
            _menuIndex = Wrap(_menuIndex + direction, MenuItems.Length);
        }
        else if (_page == TransformerRelayPage.Events && _events.Count > 0)
        {
            _eventIndex = Math.Clamp(_eventIndex + direction, 0, _events.Count - 1);
        }
        else
        {
            var index = Array.FindIndex(MenuItems, item => item.Page == _page);
            _page = MenuItems[Wrap((index < 0 ? 0 : index) + direction, MenuItems.Length)].Page;
            _eventIndex = 0;
        }

        Render();
    }

    private void Enter()
    {
        if (_menuOpen)
        {
            _page = MenuItems[_menuIndex].Page;
            _menuOpen = false;
            _eventIndex = 0;
        }
        else if (_page == TransformerRelayPage.Records)
        {
            _runSelfTest();
            return;
        }
        else if (_page == TransformerRelayPage.Settings)
        {
            _openEngineering();
            return;
        }
        else
        {
            OpenMenu();
            return;
        }

        Render();
    }

    private void Next()
    {
        if (_menuOpen)
        {
            _page = MenuItems[_menuIndex].Page;
            _menuOpen = false;
            Render();
            return;
        }

        Move(1);
    }

    private void Back()
    {
        _menuOpen = false;
        _page = TransformerRelayPage.Home;
        _eventIndex = 0;
        Render();
    }

    private void OpenMenu()
    {
        _menuOpen = true;
        var index = Array.FindIndex(MenuItems, item => item.Page == _page);
        _menuIndex = index < 0 ? 0 : index;
        Render();
    }

    private void OpenPage(TransformerRelayPage page)
    {
        _page = page;
        _menuOpen = false;
        _eventIndex = 0;
        Render();
    }

    private void ObserveSnapshot(TransformerProtectionRuntimeSnapshot snapshot)
    {
        if (_lastRuntimeState != snapshot.State)
        {
            AddEvent("STATE", snapshot.State.ToString().ToUpperInvariant());
            _lastRuntimeState = snapshot.State;
        }

        var protection = snapshot.Protection;
        var active = protection?.ActiveElement;
        if (!string.IsNullOrWhiteSpace(active) && !string.Equals(active, _lastActiveElement, StringComparison.Ordinal))
        {
            AddEvent(protection?.TripLatched == true ? "TRIP" : "PICKUP", active);
            _lastActiveElement = active;
        }

        var blocked = snapshot.State is TransformerRuntimeState.PairBlocked or TransformerRuntimeState.ProtectionBlocked ||
                      protection?.Blocked == true;
        if (_lastBlocked != blocked)
        {
            if (blocked)
                AddEvent("BLOCK", Trim(snapshot.DecisionReason, 34));
            else if (_lastBlocked == true)
                AddEvent("BLOCK CLR", "Pair/protection block cleared");
            _lastBlocked = blocked;
        }
    }

    private void UpdateLamps()
    {
        var snapshot = _snapshot;
        var protection = snapshot?.Protection;
        var differential = protection?.Differential;
        var healthy = snapshot is not null && snapshot.State is TransformerRuntimeState.Ready or TransformerRuntimeState.Pickup or TransformerRuntimeState.TripLatched;
        var pickup = differential?.Restrained87T.Pickup == true || differential?.HighSet87T.Pickup == true ||
                     protection?.RefHighVoltage.Element.Pickup == true || protection?.RefLowVoltage.Element.Pickup == true;
        var blocked = snapshot?.State is TransformerRuntimeState.PairBlocked or TransformerRuntimeState.ProtectionBlocked || protection?.Blocked == true;

        SetLamp(_relay.HealthyLamp.Lens, healthy, _healthyBrush);
        SetLamp(_relay.PickupLamp.Lens, pickup, _warningBrush);
        SetLamp(_relay.TripLamp.Lens, snapshot?.TripLatched == true, _tripBrush);
        SetLamp(_relay.PhaseALamp.Lens,
            differential?.Restrained87T.Pickup == true || differential?.Restrained87T.Operated == true,
            differential?.Restrained87T.Operated == true ? _tripBrush : _warningBrush);
        SetLamp(_relay.PhaseBLamp.Lens,
            differential?.HighSet87T.Pickup == true || differential?.HighSet87T.Operated == true,
            differential?.HighSet87T.Operated == true ? _tripBrush : _warningBrush);
        SetLamp(_relay.PhaseCLamp.Lens,
            protection?.RefHighVoltage.Element.Pickup == true || protection?.RefHighVoltage.Element.Operated == true,
            protection?.RefHighVoltage.Element.Operated == true ? _tripBrush : _warningBrush);
        SetLamp(_relay.EarthLamp.Lens,
            protection?.RefLowVoltage.Element.Pickup == true || protection?.RefLowVoltage.Element.Operated == true,
            protection?.RefLowVoltage.Element.Operated == true ? _tripBrush : _warningBrush);
        SetLamp(_relay.BlockLamp.Lens, blocked, _warningBrush);
    }

    private void SetAllLampsOff()
    {
        foreach (var lens in new[]
                 {
                     _relay.HealthyLamp.Lens, _relay.PickupLamp.Lens, _relay.TripLamp.Lens,
                     _relay.PhaseALamp.Lens, _relay.PhaseBLamp.Lens, _relay.PhaseCLamp.Lens,
                     _relay.EarthLamp.Lens, _relay.BlockLamp.Lens
                 })
            lens.Fill = _ledOffBrush;
    }

    private void UpdateRelayFooter()
    {
        var runtime = _snapshot?.State.ToString().ToUpperInvariant() ?? "WAITING";
        SetTextIfChanged(_relay.RelayFooterText,
            $"{SourceLabel(_sourceMode)} · {_streamCount} SV · {runtime} · ENG {(_engineeringOpen ? "OPEN" : "CLOSED")}");
    }

    private void Render()
    {
        UpdateLamps();
        if (_menuOpen)
        {
            RenderMenu();
            return;
        }

        switch (_page)
        {
            case TransformerRelayPage.Home: RenderHome(); break;
            case TransformerRelayPage.Measurements: RenderMeasurements(); break;
            case TransformerRelayPage.Protection: RenderProtection(); break;
            case TransformerRelayPage.Harmonics: RenderHarmonics(); break;
            case TransformerRelayPage.Ref: RenderRef(); break;
            case TransformerRelayPage.PairTrust: RenderPairTrust(); break;
            case TransformerRelayPage.Settings: RenderSettings(); break;
            case TransformerRelayPage.Events: RenderEvents(); break;
            case TransformerRelayPage.Records: RenderRecords(); break;
            case TransformerRelayPage.Diagnostics: RenderDiagnostics(); break;
        }
    }

    private void RenderMenu()
    {
        var start = Math.Clamp(_menuIndex - 2, 0, Math.Max(0, MenuItems.Length - 5));
        var lines = MenuItems.Skip(start).Take(5)
            .Select((item, offset) => start + offset == _menuIndex ? $"> {item.Label}" : $"  {item.Label}");
        SetLcd("MAIN MENU", string.Join(Environment.NewLine, lines), "▲/▼ SELECT · OK OPEN · X HOME");
    }

    private void RenderHome()
    {
        var snapshot = _snapshot;
        var measurement = snapshot?.Measurement;
        var phases = snapshot?.Protection?.Differential.Phases;
        if (measurement is null || phases is null)
        {
            SetLcd("TRANSFORMER SINGLE LINE",
                "HV --CT--[ 87T ]--CT-- LV\n" +
                "          |     |\n" +
                "        NGR     NGR\n\n" +
                "WAITING FOR PAIRED CURRENT",
                "F1 MEAS · F2 EVENT · F3 RECORD · F4 ENG");
            return;
        }

        var phaseA = phases.Single(item => item.Phase == TransformerPhase.A);
        var phaseB = phases.Single(item => item.Phase == TransformerPhase.B);
        var phaseC = phases.Single(item => item.Phase == TransformerPhase.C);
        var hv = measurement.HighVoltage;
        var lv = measurement.LowVoltage;

        SetLcd("TRANSFORMER SINGLE LINE · SECONDARY A",
            "HV --CT--[ 87T ]--CT-- LV\n" +
            $"A  {hv.FundamentalCurrentA.PhaseA.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseA.Magnitude,5:0.000}\n" +
            $"B  {hv.FundamentalCurrentA.PhaseB.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseB.Magnitude,5:0.000}\n" +
            $"C  {hv.FundamentalCurrentA.PhaseC.Magnitude,5:0.000}       {lv.FundamentalCurrentA.PhaseC.Magnitude,5:0.000}\n" +
            $"N  {NeutralText(hv),5}       {NeutralText(lv),5}\n" +
            $"Id {phaseA.OperatingCurrentPu:0.00}  {phaseB.OperatingCurrentPu:0.00}  {phaseC.OperatingCurrentPu:0.00} pu",
            "F1 MEAS · F2 EVENT · F3 RECORD · F4 ENG");
    }

    private void RenderMeasurements()
    {
        var phases = _snapshot?.Protection?.Differential.Phases;
        if (phases is null)
        {
            SetLcd("IDIFF / IBIAS · pu", "WAITING FOR ALIGNED HV/LV\n\nF4 ENGINEERING\nPAIR TWO DISTINCT SV", "OK MENU");
            return;
        }

        var a = phases.Single(p => p.Phase == TransformerPhase.A);
        var b = phases.Single(p => p.Phase == TransformerPhase.B);
        var c = phases.Single(p => p.Phase == TransformerPhase.C);
        SetLcd("IDIFF / IBIAS · pu",
            $"A  {a.OperatingCurrentPu,6:0.000} / {a.RestraintCurrentPu,6:0.000}\n" +
            $"B  {b.OperatingCurrentPu,6:0.000} / {b.RestraintCurrentPu,6:0.000}\n" +
            $"C  {c.OperatingCurrentPu,6:0.000} / {c.RestraintCurrentPu,6:0.000}\n" +
            $"TRIP {(_snapshot?.TripLatched == true ? "LATCHED" : "CLEAR")}",
            "F4 ENGINEERING · OK MENU");
    }

    private void RenderProtection()
    {
        var protection = _snapshot?.Protection;
        if (protection is null)
        {
            SetLcd("PROTECTION STATUS", "87T     WAIT\n87T-HS  WAIT\nREF HV  WAIT\nREF LV  WAIT", "F4 ENGINEERING · OK MENU");
            return;
        }

        SetLcd("PROTECTION STATUS",
            $"87T     {Stage(protection.Differential.Restrained87T),-9}\n" +
            $"87T-HS  {Stage(protection.Differential.HighSet87T),-9}\n" +
            $"REF HV  {Stage(protection.RefHighVoltage.Element),-9}\n" +
            $"REF LV  {Stage(protection.RefLowVoltage.Element),-9}",
            Trim(protection.DecisionReason, 36));
    }

    private void RenderHarmonics()
    {
        var phases = _snapshot?.Protection?.Differential.Phases;
        if (phases is null)
        {
            SetLcd("HARMONIC SECURITY", "H2 / H5 UNAVAILABLE\nWAITING FOR PAIRED SV", "OK MENU");
            return;
        }

        var lines = phases.Select(p =>
            $"{p.Phase} H2 {p.SecondHarmonicRatio,5:P0} H5 {p.FifthHarmonicRatio,5:P0} {(p.HarmonicBlocked ? "BLK" : "OK")}");
        SetLcd("HARMONICS H2 / H5", string.Join(Environment.NewLine, lines), "Measured evidence · 87T authority");
    }

    private void RenderRef()
    {
        var protection = _snapshot?.Protection;
        if (protection is null)
        {
            SetLcd("REF HV / LV", "REF HV  WAIT\nREF LV  WAIT\n\nINDEPENDENT NCT REQUIRED", "OK MENU");
            return;
        }

        SetLcd("REF HV / LV",
            $"HV {Stage(protection.RefHighVoltage.Element),-8} Iop {protection.RefHighVoltage.OperatingCurrentPu:0.000}\n" +
            $"   Ib {protection.RefHighVoltage.RestraintCurrentPu:0.000} NCT {(protection.RefHighVoltage.NeutralCurrentAvailable ? "YES" : "NO")}\n" +
            $"LV {Stage(protection.RefLowVoltage.Element),-8} Iop {protection.RefLowVoltage.OperatingCurrentPu:0.000}\n" +
            $"   Ib {protection.RefLowVoltage.RestraintCurrentPu:0.000} NCT {(protection.RefLowVoltage.NeutralCurrentAvailable ? "YES" : "NO")}",
            "Independent neutral-current evidence");
    }

    private void RenderPairTrust()
    {
        var snapshot = _snapshot;
        if (snapshot is null)
        {
            SetLcd("SV PAIR / TRUST",
                $"SOURCE  {SourceLabel(_sourceMode)}\nSTREAMS {_streamCount}\nPAIR    WAITING\n\nF4 SELECT HV / LV",
                "Two distinct SV streams required");
            return;
        }

        var identity = snapshot.PairIdentity;
        SetLcd("SV PAIR / TRUST",
            $"CODE    {Trim(snapshot.Pairing.Code, 18)}\n" +
            $"smpCnt  {(identity is null ? "—" : $"{identity.HighVoltageSampleCounter}/{identity.LowVoltageSampleCounter}")}\n" +
            $"HV KEY  {Trim(snapshot.HighVoltageStreamKey, 17)}\n" +
            $"LV KEY  {Trim(snapshot.LowVoltageStreamKey, 17)}",
            Trim(snapshot.Pairing.Detail, 36));
    }

    private void RenderSettings()
    {
        var settings = _snapshot?.EffectiveSettings ?? new TransformerProtectionSettings();
        var differential = settings.Differential87T;
        SetLcd("ACTIVE SETTINGS",
            $"87T {(differential.Enabled ? "ON " : "OFF")} Is1 {differential.Is1Pu:0.00} K1 {differential.K1:P0}\n" +
            $"Is2 {differential.Is2Pu:0.00} K2 {differential.K2:P0}\n" +
            $"HS {(differential.HighSetEnabled ? differential.HighSetPickupPu.ToString("0.0", CultureInfo.InvariantCulture) : "OFF")}  HARM {differential.HarmonicSecurityMode}\n" +
            $"REF HV {(settings.RefHighVoltage.Enabled ? "ON" : "OFF")} · LV {(settings.RefLowVoltage.Enabled ? "ON" : "OFF")}",
            "OK / F4 EDIT IN ENGINEERING");
    }

    private void RenderEvents()
    {
        if (_events.Count == 0)
        {
            SetLcd("EVENTS", "NO EVENTS RECORDED", "OK MENU");
            return;
        }

        var ordered = _events.AsEnumerable().Reverse().ToArray();
        _eventIndex = Math.Clamp(_eventIndex, 0, ordered.Length - 1);
        var item = ordered[_eventIndex];
        SetLcd($"EVENT {item.Sequence:000} · {item.Code}",
            $"DATE {item.Timestamp:yyyy-MM-dd}\nTIME {item.Timestamp:HH:mm:ss.fff}\n\n{Trim(item.Detail, 30)}",
            $"▲/▼ BROWSE · {_eventIndex + 1}/{ordered.Length}");
    }

    private void RenderRecords()
    {
        if (_selfTest is null)
        {
            SetLcd("RECORDS / SELF-TEST",
                "PUBLIC CORE SELF-TEST\nSTATUS  NOT RUN\n10 DETERMINISTIC CASES\nNO SV REQUIRED",
                "OK / ENTER · RUN SELF-TEST");
            return;
        }

        var failed = _selfTest.Cases.FirstOrDefault(test => !test.Passed);
        SetLcd("SELF-TEST RECORD",
            $"RESULT {(_selfTest.AllPassed ? "PASS" : "FAIL")}\nCASES  {_selfTest.PassedCount}/{_selfTest.Cases.Count}\nSUITE  {Trim(_selfTest.SuiteId, 18)}\nLAST   {(failed is null ? "ALL PASSED" : Trim(failed.Id, 18))}",
            "Software verification only · OK rerun");
    }

    private void RenderDiagnostics()
    {
        var snapshot = _snapshot;
        var security = snapshot?.Protection?.ExternalFaultSecurity;
        SetLcd("DIAGNOSTICS",
            $"SOURCE  {SourceLabel(_sourceMode)}\nSTREAMS {_streamCount}\nRUNTIME {(snapshot?.State.ToString().ToUpperInvariant() ?? "WAITING")}\nP13    {(security is null ? "—" : security.Enabled ? security.AnyBlocked ? "HOLD" : security.AnyArmed ? "ARMED" : "READY" : "OFF")}",
            "Virtual output only · no GOOSE / breaker");
    }

    private void AddEvent(string code, string detail)
    {
        _events.Add(new TransformerRelayEvent(++_sequence, DateTimeOffset.Now, code, detail));
        if (_events.Count > 64)
            _events.RemoveAt(0);
        _eventIndex = 0;
    }

    private void SetLcd(string header, string body, string footer)
    {
        // Avoid invalidating WPF text/layout on every high-rate runtime snapshot when
        // the formatted LCD content did not actually change. This is particularly
        // important for synchronized internal injection where snapshots arrive much
        // faster than a human-readable relay display needs to repaint.
        SetTextIfChanged(_lcdHeader, header);
        SetTextIfChanged(_lcdBody, body);
        SetTextIfChanged(_lcdFooter, footer);
    }

    private static void SetTextIfChanged(TextBlock target, string value)
    {
        if (!string.Equals(target.Text, value, StringComparison.Ordinal))
            target.Text = value;
    }

    private static string NeutralText(TransformerWindingMeasurement measurement)
        => measurement.NeutralCurrentAvailable
            ? measurement.NeutralCurrentA.Magnitude.ToString("0.000", CultureInfo.InvariantCulture)
            : "--";

    private Brush ResolveBrush(string key, Brush fallback)
        => _relay.TryFindResource(key) as Brush ?? fallback;

    private void SetLamp(Ellipse lens, bool active, Brush brush)
    {
        var desired = active ? brush : _ledOffBrush;
        if (!ReferenceEquals(lens.Fill, desired))
            lens.Fill = desired;
    }

    private static string Stage(TransformerElementSnapshot element)
        => element.State switch
        {
            ProtectionStageState.Operated => "OPERATE",
            ProtectionStageState.Timing => "TIMING",
            ProtectionStageState.Blocked => "BLOCKED",
            ProtectionStageState.Ready => "READY",
            _ => element.State.ToString().ToUpperInvariant()
        };

    private static string SourceLabel(ProcessBusSourceMode mode)
        => mode switch
        {
            ProcessBusSourceMode.LiveCapture => "LIVE SV",
            ProcessBusSourceMode.PcapReplay => "PCAP",
            _ => "INTERNAL"
        };

    private static int Wrap(int value, int count)
        => count == 0 ? 0 : (value % count + count) % count;

    private static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";
        var oneLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= max ? oneLine : oneLine[..Math.Max(1, max - 1)] + "…";
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    private enum TransformerRelayPage
    {
        Home,
        Measurements,
        Protection,
        Harmonics,
        Ref,
        PairTrust,
        Settings,
        Events,
        Records,
        Diagnostics
    }

    private sealed record TransformerRelayEvent(int Sequence, DateTimeOffset Timestamp, string Code, string Detail);
}
