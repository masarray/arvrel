using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App.Controls.Transformer;

/// <summary>
/// Operator-facing virtual front panel for the Transformer Differential IED.
/// This control is presentation only. It consumes the authoritative transformer
/// runtime snapshot and never evaluates, restrains, blocks, or trips protection.
/// </summary>
public sealed class TransformerVirtualRelayControl : UserControl
{
    private static readonly Brush BodyBrush = Brush("#43515B");
    private static readonly Brush BodyDarkBrush = Brush("#2C3841");
    private static readonly Brush BezelBrush = Brush("#25323B");
    private static readonly Brush LcdBrush = Brush("#DDE5DD");
    private static readonly Brush LcdTextBrush = Brush("#293A36");
    private static readonly Brush MutedBrush = Brush("#73848F");
    private static readonly Brush HealthyBrush = Brush("#44A266");
    private static readonly Brush WarningBrush = Brush("#D89A37");
    private static readonly Brush TripBrush = Brush("#D75650");
    private static readonly Brush AccentBrush = Brush("#3E91C5");
    private static readonly Brush LedOffBrush = Brush("#66737C");

    private static readonly (TransformerRelayPage Page, string Label)[] MenuItems =
    [
        (TransformerRelayPage.Home, "HOME"),
        (TransformerRelayPage.Measurements, "MEASUREMENTS"),
        (TransformerRelayPage.Protection, "PROTECTION"),
        (TransformerRelayPage.Harmonics, "HARMONICS H2 / H5"),
        (TransformerRelayPage.Ref, "REF HV / LV"),
        (TransformerRelayPage.PairTrust, "SV PAIR / TRUST"),
        (TransformerRelayPage.Settings, "SETTINGS"),
        (TransformerRelayPage.Events, "EVENTS"),
        (TransformerRelayPage.Records, "RECORDS / SELF-TEST"),
        (TransformerRelayPage.Diagnostics, "DIAGNOSTICS")
    ];

    private readonly TextBlock _lcdHeader;
    private readonly TextBlock _lcdBody;
    private readonly TextBlock _lcdFooter;
    private readonly TextBlock _sourceText;
    private readonly TextBlock _runtimeText;
    private readonly Ellipse _healthyLed;
    private readonly Ellipse _pickupLed;
    private readonly Ellipse _tripLed;
    private readonly Ellipse _restrainedLed;
    private readonly Ellipse _highSetLed;
    private readonly Ellipse _refHvLed;
    private readonly Ellipse _refLvLed;
    private readonly Ellipse _blockLed;
    private readonly List<TransformerRelayEvent> _events = [];

    private TransformerProtectionRuntimeSnapshot? _snapshot;
    private TransformerPublicSelfTestReport? _selfTest;
    private ProcessBusSourceMode _sourceMode = ProcessBusSourceMode.InternalDemo;
    private int _streamCount;
    private bool _practitionerOpen;
    private TransformerRelayPage _page = TransformerRelayPage.Home;
    private bool _menuOpen;
    private int _menuIndex;
    private int _eventIndex;
    private int _sequence;
    private TransformerRuntimeState? _lastRuntimeState;
    private string? _lastActiveElement;
    private bool? _lastBlocked;

    public TransformerVirtualRelayControl()
    {
        MinWidth = 620;
        MinHeight = 570;
        Focusable = true;

        _lcdHeader = LcdText(12.5, FontWeights.SemiBold);
        _lcdBody = LcdText(11.2, FontWeights.Normal);
        _lcdFooter = LcdText(9.2, FontWeights.Normal);
        _sourceText = new TextBlock
        {
            Foreground = Brush("#D7E2E8"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 9.2,
            Text = "INTERNAL DEMO · 0 SV"
        };
        _runtimeText = new TextBlock
        {
            Foreground = Brush("#D7E2E8"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 9.2,
            Text = "RUNTIME WAITING"
        };

        _healthyLed = Led();
        _pickupLed = Led();
        _tripLed = Led();
        _restrainedLed = Led();
        _highSetLed = Led();
        _refHvLed = Led();
        _refLvLed = Led();
        _blockLed = Led();

        Content = BuildDevice();
        AddEvent("READY", "Transformer virtual relay front panel initialized");
        Render();
    }

    public event EventHandler? OpenPractitionerRequested;
    public event EventHandler? RunSelfTestRequested;
    public event EventHandler? ResetRequested;

    public TransformerProtectionRuntimeSnapshot? Snapshot => _snapshot;
    public TransformerPublicSelfTestReport? SelfTestReport => _selfTest;

    public void UpdateEnvironment(ProcessBusSourceMode sourceMode, int streamCount, bool practitionerOpen)
    {
        _sourceMode = sourceMode;
        _streamCount = Math.Max(0, streamCount);
        _practitionerOpen = practitionerOpen;
        _sourceText.Text = $"{SourceLabel(sourceMode)} · {_streamCount} SV";
        Render();
    }

    public void UpdateSnapshot(TransformerProtectionRuntimeSnapshot? snapshot)
    {
        _snapshot = snapshot;
        if (snapshot is not null)
            ObserveSnapshot(snapshot);
        UpdateLeds();
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
        _practitionerOpen = false;
        AddEvent("ENGINEER", "Practitioner workspace closed; last relay evidence retained");
        Render();
    }

    private FrameworkElement BuildDevice()
    {
        var outer = new Border
        {
            Background = Brush("#D4DCE1"),
            BorderBrush = Brush("#A8B5BD"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13)
        };

        var body = new Border
        {
            Background = BodyBrush,
            BorderBrush = Brush("#71808A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(15)
        };
        outer.Child = body;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Child = root;

        root.Children.Add(BuildHeader());
        var main = BuildMainPanel();
        Grid.SetRow(main, 1);
        root.Children.Add(main);
        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return outer;
    }

    private FrameworkElement BuildHeader()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var identity = new StackPanel();
        identity.Children.Add(new TextBlock
        {
            Text = "ARVREL 87T",
            Foreground = Brush("#F3F7F9"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        identity.Children.Add(new TextBlock
        {
            Text = "TRANSFORMER DIFFERENTIAL RELAY",
            Foreground = Brush("#B9C8D0"),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 0)
        });
        identity.Children.Add(new TextBlock
        {
            Text = "87T · 87T-HS · REF HV/LV · H2/H5",
            Foreground = Brush("#8FA4B0"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.8,
            Margin = new Thickness(0, 3, 0, 0)
        });
        grid.Children.Add(identity);

        var badge = new Border
        {
            Background = Brush("#EAF0F3"),
            BorderBrush = Brush("#8D9AA3"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(9, 6, 9, 6),
            VerticalAlignment = VerticalAlignment.Top
        };
        var badgeStack = new StackPanel();
        badgeStack.Children.Add(new TextBlock
        {
            Text = "TR-87T",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = Brush("#263640")
        });
        badgeStack.Children.Add(new TextBlock
        {
            Text = "IEC 61850 SV",
            FontSize = 8.5,
            Foreground = Brush("#526572")
        });
        badge.Child = badgeStack;
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);
        return grid;
    }

    private FrameworkElement BuildMainPanel()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.72, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var operatorPanel = new Grid();
        operatorPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(235) });
        operatorPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        operatorPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(operatorPanel);

        var lcdBezel = new Border
        {
            Background = BezelBrush,
            BorderBrush = Brush("#71828C"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7)
        };
        var lcd = new Border
        {
            Background = LcdBrush,
            BorderBrush = Brush("#83928C"),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 12, 14, 10)
        };
        lcdBezel.Child = lcd;
        var lcdStack = new StackPanel();
        _lcdHeader.Margin = new Thickness(0, 0, 0, 8);
        _lcdBody.Height = 143;
        _lcdBody.LineHeight = 17;
        _lcdBody.TextWrapping = TextWrapping.NoWrap;
        _lcdFooter.Margin = new Thickness(0, 8, 0, 0);
        _lcdFooter.TextTrimming = TextTrimming.CharacterEllipsis;
        lcdStack.Children.Add(_lcdHeader);
        lcdStack.Children.Add(_lcdBody);
        lcdStack.Children.Add(new Border
        {
            Height = 1,
            Background = Brush("#A6B2AD"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        lcdStack.Children.Add(_lcdFooter);
        lcd.Child = lcdStack;
        operatorPanel.Children.Add(lcdBezel);

        var nav = new UniformGrid
        {
            Rows = 2,
            Columns = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 11, 0, 0)
        };
        nav.Children.Add(Key("▲", "Up", (_, _) => Move(-1)));
        nav.Children.Add(Key("OK", "Enter", (_, _) => Enter()));
        nav.Children.Add(Key("▶", "Next", (_, _) => Next()));
        nav.Children.Add(Key("▼", "Down", (_, _) => Move(1)));
        nav.Children.Add(Key("↩", "Cancel", (_, _) => Back()));
        nav.Children.Add(Key("RST", "Reset virtual trip latch", (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty)));
        Grid.SetRow(nav, 1);
        operatorPanel.Children.Add(nav);

        var softKeys = new UniformGrid
        {
            Rows = 1,
            Columns = 5,
            Margin = new Thickness(0, 9, 0, 0)
        };
        softKeys.Children.Add(SoftKey("F1", "MEAS", (_, _) => OpenPage(TransformerRelayPage.Measurements)));
        softKeys.Children.Add(SoftKey("F2", "EVENT", (_, _) => OpenPage(TransformerRelayPage.Events)));
        softKeys.Children.Add(SoftKey("F3", "RECORD", (_, _) => OpenPage(TransformerRelayPage.Records)));
        softKeys.Children.Add(SoftKey("F4", "ENG", (_, _) => OpenPractitionerRequested?.Invoke(this, EventArgs.Empty)));
        softKeys.Children.Add(SoftKey("F5", "RESET", (_, _) => ResetRequested?.Invoke(this, EventArgs.Empty)));
        Grid.SetRow(softKeys, 2);
        operatorPanel.Children.Add(softKeys);

        var annunciation = BuildAnnunciation();
        Grid.SetColumn(annunciation, 2);
        grid.Children.Add(annunciation);
        return grid;
    }

    private FrameworkElement BuildAnnunciation()
    {
        var border = new Border
        {
            Background = Brush("#E5EAED"),
            BorderBrush = Brush("#84939D"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < 8; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

        AddLedRow(grid, 0, "HEALTHY", _healthyLed);
        AddLedRow(grid, 1, "PICKUP", _pickupLed);
        AddLedRow(grid, 2, "TRIP", _tripLed);
        AddLedRow(grid, 3, "87T", _restrainedLed);
        AddLedRow(grid, 4, "87T-HS", _highSetLed);
        AddLedRow(grid, 5, "REF HV", _refHvLed);
        AddLedRow(grid, 6, "REF LV", _refLvLed);
        AddLedRow(grid, 7, "BLOCK", _blockLed);
        border.Child = grid;
        return border;
    }

    private FrameworkElement BuildFooter()
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = "VIRTUAL TRIP OUTPUT ONLY · NO GOOSE · NO BREAKER OUTPUT",
            Foreground = Brush("#D4DEE3"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.8
        });
        left.Children.Add(_sourceText);
        left.Children.Add(_runtimeText);
        grid.Children.Add(left);

        var open = new Button
        {
            Content = "ENGINEERING",
            MinWidth = 102,
            Height = 30,
            Padding = new Thickness(10, 4, 10, 4),
            Background = AccentBrush,
            Foreground = Brushes.White,
            BorderBrush = Brush("#2E6F98"),
            BorderThickness = new Thickness(1),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            ToolTip = "Open paired-SV transformer practitioner workspace"
        };
        open.Click += (_, _) => OpenPractitionerRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);
        return grid;
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
            index = Wrap((index < 0 ? 0 : index) + direction, MenuItems.Length);
            _page = MenuItems[index].Page;
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
            RunSelfTestRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (_page == TransformerRelayPage.Settings)
        {
            OpenPractitionerRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _menuOpen = true;
            var index = Array.FindIndex(MenuItems, item => item.Page == _page);
            _menuIndex = index < 0 ? 0 : index;
        }
        Render();
    }

    private void Next()
    {
        if (_menuOpen)
        {
            _page = MenuItems[_menuIndex].Page;
            _menuOpen = false;
        }
        else
        {
            Move(1);
            return;
        }
        Render();
    }

    private void Back()
    {
        _menuOpen = false;
        _page = TransformerRelayPage.Home;
        _eventIndex = 0;
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
        if (!string.Equals(active, _lastActiveElement, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(active))
        {
            AddEvent(protection?.TripLatched == true ? "TRIP" : "PICKUP", active);
            _lastActiveElement = active;
        }

        var blocked = snapshot.State is TransformerRuntimeState.PairBlocked or TransformerRuntimeState.ProtectionBlocked || protection?.Blocked == true;
        if (_lastBlocked != blocked)
        {
            if (blocked)
                AddEvent("BLOCK", Trim(snapshot.DecisionReason, 46));
            else if (_lastBlocked == true)
                AddEvent("BLOCK CLR", "Protection / pair block cleared");
            _lastBlocked = blocked;
        }
    }

    private void UpdateLeds()
    {
        var snapshot = _snapshot;
        var protection = snapshot?.Protection;
        var diff = protection?.Differential;
        var healthy = snapshot is not null && snapshot.State is TransformerRuntimeState.Ready or TransformerRuntimeState.Pickup or TransformerRuntimeState.TripLatched;
        var pickup = diff?.Restrained87T.Pickup == true || diff?.HighSet87T.Pickup == true ||
                     protection?.RefHighVoltage.Element.Pickup == true || protection?.RefLowVoltage.Element.Pickup == true;
        var blocked = snapshot?.State is TransformerRuntimeState.PairBlocked or TransformerRuntimeState.ProtectionBlocked || protection?.Blocked == true;

        SetLed(_healthyLed, healthy, HealthyBrush);
        SetLed(_pickupLed, pickup, WarningBrush);
        SetLed(_tripLed, snapshot?.TripLatched == true, TripBrush);
        SetLed(_restrainedLed, diff?.Restrained87T.Pickup == true || diff?.Restrained87T.Operated == true,
            diff?.Restrained87T.Operated == true ? TripBrush : WarningBrush);
        SetLed(_highSetLed, diff?.HighSet87T.Pickup == true || diff?.HighSet87T.Operated == true,
            diff?.HighSet87T.Operated == true ? TripBrush : WarningBrush);
        SetLed(_refHvLed, protection?.RefHighVoltage.Element.Pickup == true || protection?.RefHighVoltage.Element.Operated == true,
            protection?.RefHighVoltage.Element.Operated == true ? TripBrush : WarningBrush);
        SetLed(_refLvLed, protection?.RefLowVoltage.Element.Pickup == true || protection?.RefLowVoltage.Element.Operated == true,
            protection?.RefLowVoltage.Element.Operated == true ? TripBrush : WarningBrush);
        SetLed(_blockLed, blocked, WarningBrush);

        _runtimeText.Text = snapshot is null
            ? _practitionerOpen ? "RUNTIME NOT APPLIED" : "RUNTIME WAITING · F4 ENGINEERING"
            : $"RUNTIME {snapshot.State.ToString().ToUpperInvariant()}";
    }

    private void Render()
    {
        UpdateLeds();
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
        var start = Math.Clamp(_menuIndex - 2, 0, Math.Max(0, MenuItems.Length - 6));
        var lines = MenuItems.Skip(start).Take(6)
            .Select((item, offset) => start + offset == _menuIndex ? $"> {item.Label}" : $"  {item.Label}");
        SetLcd("MAIN MENU", string.Join(Environment.NewLine, lines), "▲/▼ SELECT · OK OPEN · ↩ HOME");
    }

    private void RenderHome()
    {
        var snapshot = _snapshot;
        var protection = snapshot?.Protection;
        var state = snapshot?.State.ToString().ToUpperInvariant() ?? "WAITING";
        var pair = snapshot?.Pairing.Code ?? (_streamCount >= 2 ? "NOT CONFIGURED" : "WAITING FOR 2 SV");
        var active = string.IsNullOrWhiteSpace(protection?.ActiveElement) ? "—" : protection.ActiveElement;
        SetLcd("ARVREL TR-87T · HOME",
            $"STATE     {state}\nSOURCE    {SourceLabel(_sourceMode)}\nPAIR      {Trim(pair, 20)}\nACTIVE    {Trim(active, 20)}\nENGINEER  {(_practitionerOpen ? "OPEN" : "CLOSED")}",
            "F1 MEAS · F2 EVENT · F3 RECORD · F4 ENG");
    }

    private void RenderMeasurements()
    {
        var snapshot = _snapshot;
        var phases = snapshot?.Protection?.Differential.Phases;
        if (snapshot?.Measurement is null || phases is null)
        {
            SetLcd("DIFFERENTIAL MEASUREMENTS",
                "WAITING FOR ALIGNED HV/LV\n\nApply runtime in F4 ENGINEERING.\nLive / Replay requires two distinct SV streams.",
                "F4 ENGINEERING · OK MENU");
            return;
        }

        var a = phases.Single(p => p.Phase == TransformerPhase.A);
        var b = phases.Single(p => p.Phase == TransformerPhase.B);
        var c = phases.Single(p => p.Phase == TransformerPhase.C);
        SetLcd("IDIFF / IBIAS · pu",
            $"A   {a.OperatingCurrentPu,6:0.000} / {a.RestraintCurrentPu,6:0.000}\n" +
            $"B   {b.OperatingCurrentPu,6:0.000} / {b.RestraintCurrentPu,6:0.000}\n" +
            $"C   {c.OperatingCurrentPu,6:0.000} / {c.RestraintCurrentPu,6:0.000}\n" +
            $"HV  {Currents(snapshot.Measurement.HighVoltage.FundamentalCurrentA)}\n" +
            $"LV  {Currents(snapshot.Measurement.LowVoltage.FundamentalCurrentA)}",
            "IDIFF / IBIAS · ▲/▼ PAGE · OK MENU");
    }

    private void RenderProtection()
    {
        var p = _snapshot?.Protection;
        if (p is null)
        {
            SetLcd("PROTECTION STATUS", "87T      WAIT\n87T-HS   WAIT\nREF HV   WAIT\nREF LV   WAIT", "F4 ENGINEERING · OK MENU");
            return;
        }

        SetLcd("PROTECTION STATUS",
            $"87T      {Stage(p.Differential.Restrained87T),-10}\n" +
            $"87T-HS   {Stage(p.Differential.HighSet87T),-10}\n" +
            $"REF HV   {Stage(p.RefHighVoltage.Element),-10}\n" +
            $"REF LV   {Stage(p.RefLowVoltage.Element),-10}\n" +
            $"TRIP     {(p.TripLatched ? "LATCHED" : "CLEAR")}",
            Trim(p.DecisionReason, 48));
    }

    private void RenderHarmonics()
    {
        var phases = _snapshot?.Protection?.Differential.Phases;
        if (phases is null)
        {
            SetLcd("HARMONIC SECURITY", "H2 / H5 evidence unavailable.\nWaiting for configured paired SV runtime.", "OK MENU");
            return;
        }

        var lines = phases.Select(p =>
            $"{p.Phase}  H2 {p.SecondHarmonicRatio,6:P1}  H5 {p.FifthHarmonicRatio,6:P1}  {(p.HarmonicBlocked ? "BLOCK" : "clear")}");
        var security = _snapshot!.EffectiveSettings.Differential87T.HarmonicSecurityMode;
        SetLcd($"HARMONICS · {security.ToString().ToUpperInvariant()}", string.Join(Environment.NewLine, lines),
            "Measured ratios · authority: 87T runtime");
    }

    private void RenderRef()
    {
        var p = _snapshot?.Protection;
        if (p is null)
        {
            SetLcd("RESTRICTED EARTH FAULT", "REF HV   WAIT\nREF LV   WAIT\n\nIndependent neutral CT is required for each enabled REF side.", "OK MENU");
            return;
        }

        SetLcd("REF HV / LV",
            $"HV  {Stage(p.RefHighVoltage.Element),-9} Iop {p.RefHighVoltage.OperatingCurrentPu:0.000}\n" +
            $"    Ib {p.RefHighVoltage.RestraintCurrentPu:0.000}  thr {p.RefHighVoltage.ThresholdPu:0.000}\n" +
            $"LV  {Stage(p.RefLowVoltage.Element),-9} Iop {p.RefLowVoltage.OperatingCurrentPu:0.000}\n" +
            $"    Ib {p.RefLowVoltage.RestraintCurrentPu:0.000}  thr {p.RefLowVoltage.ThresholdPu:0.000}\n" +
            $"NCT HV {(p.RefHighVoltage.NeutralCurrentAvailable ? "YES" : "NO")} · LV {(p.RefLowVoltage.NeutralCurrentAvailable ? "YES" : "NO")}",
            "Independent neutral-current evidence only");
    }

    private void RenderPairTrust()
    {
        var snapshot = _snapshot;
        if (snapshot is null)
        {
            SetLcd("SV PAIR / TRUST",
                $"SOURCE   {SourceLabel(_sourceMode)}\nSTREAMS  {_streamCount}\nPAIR     WAITING\n\nSelect distinct HV and LV streams in F4 ENGINEERING.",
                "No inferred pair · OK MENU");
            return;
        }

        var pair = snapshot.Pairing;
        var identity = snapshot.PairIdentity;
        SetLcd("SV PAIR / TRUST",
            $"CODE     {Trim(pair.Code, 18)}\n" +
            $"smpCnt   {(identity is null ? "—" : $"{identity.HighVoltageSampleCounter}/{identity.LowVoltageSampleCounter}")}\n" +
            $"smpSynch {pair.HighVoltageSynchronization}/{pair.LowVoltageSynchronization}\n" +
            $"SKEW     {pair.SignedSampleCounterSkew:+0;-0;0} · {pair.CaptureTimestampSkew.TotalMilliseconds:0.###} ms\n" +
            $"CORR     {pair.PhaseCorrectionDegrees:+0.###;-0.###;0}°",
            Trim(pair.Detail, 48));
    }

    private void RenderSettings()
    {
        var settings = _snapshot?.EffectiveSettings ?? new TransformerProtectionSettings();
        var d = settings.Differential87T;
        SetLcd("ACTIVE SETTINGS",
            $"87T      {(d.Enabled ? "ON" : "OFF")}  Is1 {d.Is1Pu:0.00}\n" +
            $"K1       {d.K1:P0}   Is2 {d.Is2Pu:0.00}\n" +
            $"K2       {d.K2:P0}   HS {(d.HighSetEnabled ? d.HighSetPickupPu.ToString("0.0", CultureInfo.InvariantCulture) : "OFF")}\n" +
            $"REF HV   {(settings.RefHighVoltage.Enabled ? "ON" : "OFF")}   REF LV {(settings.RefLowVoltage.Enabled ? "ON" : "OFF")}\n" +
            $"HARM     {d.HarmonicSecurityMode.ToString().ToUpperInvariant()}",
            "OK opens menu · F4 edit in engineering");
    }

    private void RenderEvents()
    {
        if (_events.Count == 0)
        {
            SetLcd("EVENTS", "NO FACEPLATE EVENTS", "OK MENU");
            return;
        }

        var ordered = _events.AsEnumerable().Reverse().ToArray();
        _eventIndex = Math.Clamp(_eventIndex, 0, ordered.Length - 1);
        var item = ordered[_eventIndex];
        SetLcd($"EVENT {item.Sequence:000} · {item.Code}",
            $"DATE  {item.Timestamp:yyyy-MM-dd}\nTIME  {item.Timestamp:HH:mm:ss.fff}\n\n{Trim(item.Detail, 46)}",
            $"▲/▼ BROWSE · {_eventIndex + 1}/{ordered.Length}");
    }

    private void RenderRecords()
    {
        if (_selfTest is null)
        {
            SetLcd("RECORDS / SELF-TEST",
                "PUBLIC CORE SELF-TEST\nStatus  NOT RUN\nSuite   transformer-public-beta-v1\n\nNo SV required for deterministic packaged-core verification.",
                "OK / ENTER · RUN 10-SCENARIO SELF-TEST");
            return;
        }

        var failed = _selfTest.Cases.FirstOrDefault(test => !test.Passed);
        SetLcd("SELF-TEST RECORD",
            $"RESULT  {(_selfTest.AllPassed ? "PASS" : "FAIL")}\n" +
            $"CASES   {_selfTest.PassedCount}/{_selfTest.Cases.Count}\n" +
            $"SUITE   {_selfTest.SuiteId}\n" +
            $"LAST    {(failed is null ? "ALL CASES PASSED" : Trim(failed.Id, 20))}\n\n" +
            "Software verification only; not calibration or IEC type test.",
            "OK / ENTER · RUN AGAIN · F4 FULL EVIDENCE");
    }

    private void RenderDiagnostics()
    {
        var snapshot = _snapshot;
        var security = snapshot?.Protection?.ExternalFaultSecurity;
        SetLcd("DIAGNOSTICS",
            $"SOURCE   {SourceLabel(_sourceMode)}\n" +
            $"STREAMS  {_streamCount}\n" +
            $"RUNTIME  {(snapshot?.State.ToString().ToUpperInvariant() ?? "WAITING")}\n" +
            $"P13      {(security is null ? "—" : security.Enabled ? security.AnyBlocked ? "HOLD" : security.AnyArmed ? "ARMED" : "READY" : "OFF")}\n" +
            $"SET ID   {(snapshot is null ? "—" : Trim(snapshot.EffectiveSettingsFingerprint.ToUpperInvariant(), 12))}",
            "Virtual-only evidence · no physical outputs");
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
        _lcdHeader.Text = header;
        _lcdBody.Text = body;
        _lcdFooter.Text = footer;
    }

    private static Button Key(string text, string tooltip, RoutedEventHandler handler)
    {
        var button = HardwareButton(text, 54, 38, 4);
        button.ToolTip = tooltip;
        button.Click += handler;
        return button;
    }

    private static Button SoftKey(string key, string label, RoutedEventHandler handler)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = key,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            FontSize = 9.3
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 7.8,
            Foreground = Brush("#B8C6CE")
        });
        var button = HardwareButton(stack, 65, 42, 3);
        button.Click += handler;
        return button;
    }

    private static Button HardwareButton(object content, double width, double height, double margin)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Button.BackgroundProperty, BodyDarkBrush));
        style.Setters.Add(new Setter(Button.ForegroundProperty, Brush("#EDF2F5")));
        style.Setters.Add(new Setter(Button.BorderBrushProperty, Brush("#18242C")));
        style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Button.FontFamilyProperty, new FontFamily("Segoe UI")));
        var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Button.BackgroundProperty, Brush("#536570")));
        hover.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        style.Triggers.Add(hover);
        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Button.BackgroundProperty, Brush("#1C2830")));
        style.Triggers.Add(pressed);

        return new Button
        {
            Content = content,
            Width = width,
            Height = height,
            Margin = new Thickness(margin),
            Style = style,
            Padding = new Thickness(4),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
    }

    private static void AddLedRow(Grid grid, int row, string label, Ellipse led)
    {
        var text = new TextBlock
        {
            Text = label,
            Foreground = Brush("#334650"),
            FontSize = 10.3,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(text, row);
        grid.Children.Add(text);
        Grid.SetRow(led, row);
        Grid.SetColumn(led, 1);
        grid.Children.Add(led);
    }

    private static Ellipse Led()
        => new()
        {
            Width = 12,
            Height = 12,
            Fill = LedOffBrush,
            Stroke = Brush("#52616A"),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static void SetLed(Ellipse led, bool on, Brush active)
        => led.Fill = on ? active : LedOffBrush;

    private static TextBlock LcdText(double fontSize, FontWeight weight)
        => new()
        {
            Foreground = LcdTextBrush,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = fontSize,
            FontWeight = weight
        };

    private static string Stage(TransformerElementSnapshot element)
        => element.State switch
        {
            ProtectionStageState.Operated => "OPERATE",
            ProtectionStageState.Timing => "TIMING",
            ProtectionStageState.Blocked => "BLOCKED",
            ProtectionStageState.Ready => "READY",
            _ => element.State.ToString().ToUpperInvariant()
        };

    private static string Currents(TransformerPhaseCurrents currents)
        => $"{Magnitude(currents.PhaseA):0.00}/{Magnitude(currents.PhaseB):0.00}/{Magnitude(currents.PhaseC):0.00}";

    private static double Magnitude(Complex value) => value.Magnitude;

    private static string SourceLabel(ProcessBusSourceMode mode)
        => mode switch
        {
            ProcessBusSourceMode.LiveCapture => "LIVE SV",
            ProcessBusSourceMode.PcapReplay => "PCAP REPLAY",
            _ => "INTERNAL DEMO"
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

    private static Brush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
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
