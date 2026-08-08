using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.Protection;

namespace Arvrel.App;

/// <summary>
/// Production HMI for the P6 virtual protection relay. This layer owns only
/// relay-local navigation and presentation; protection state remains authoritative
/// in the existing protection/laboratory services.
/// </summary>
public partial class MainWindow
{
    private static readonly RelayMenuEntry[] RelayRootMenu =
    {
        new("MEASUREMENTS", Submenu: RelayMenuLevel.Measurements),
        new("PROTECTION", Submenu: RelayMenuLevel.Protection),
        new("RECORDS", Submenu: RelayMenuLevel.Records),
        new("SETTINGS", Page: RelayLcdPage.Settings),
        new("DIAGNOSTICS", Submenu: RelayMenuLevel.Diagnostics)
    };

    private static readonly RelayMenuEntry[] RelayMeasurementsMenu =
    {
        new("OVERVIEW", Page: RelayLcdPage.Measurements),
        new("CURRENT RMS", Page: RelayLcdPage.CurrentMeasurements),
        new("VOLTAGE RMS", Page: RelayLcdPage.VoltageMeasurements),
        new("SEQUENCE", Page: RelayLcdPage.SequenceMeasurements)
    };

    private static readonly RelayMenuEntry[] RelayProtectionMenu =
    {
        new("OVERCURRENT 50/51", Page: RelayLcdPage.Protection),
        new("FEEDER PROTECTION", Page: RelayLcdPage.Feeder),
        new("LAST TRIP", Page: RelayLcdPage.TripRecord)
    };

    private static readonly RelayMenuEntry[] RelayRecordsMenu =
    {
        new("EVENTS", Page: RelayLcdPage.Events),
        new("LAST TRIP", Page: RelayLcdPage.TripRecord)
    };

    private static readonly RelayMenuEntry[] RelayDiagnosticsMenu =
    {
        new("PROCESS BUS", Page: RelayLcdPage.Diagnostics),
        new("DEVICE INFO", Page: RelayLcdPage.DeviceInfo)
    };

    private static readonly RelayLcdPage[] RelayTopPages =
    {
        RelayLcdPage.Measurements,
        RelayLcdPage.Protection,
        RelayLcdPage.Feeder,
        RelayLcdPage.Events,
        RelayLcdPage.TripRecord,
        RelayLcdPage.Diagnostics,
        RelayLcdPage.Settings
    };

    private readonly RelayOperationRecorder _relayOperationRecorder = new();
    private readonly List<RelayFaceplateEvent> _relayFaceplateEvents = new();
    private readonly Stack<RelayLcdPage> _relayPageHistory = new();
    private DispatcherTimer? _relayFaceplateTimer;
    private TextBlock? _relayLcdHeader;
    private TextBlock? _relayLcdBody;
    private TextBlock? _relayLcdFooter;
    private RelayLcdPage _relayLcdPage = RelayLcdPage.Measurements;
    private RelayLcdPage _relayFavoritePage = RelayLcdPage.Measurements;
    private RelayMenuLevel _relayMenuLevel = RelayMenuLevel.None;
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

        _relayLcdHeader = LcdText(10.4, FontWeights.SemiBold);
        _relayLcdHeader.Margin = new Thickness(0, 0, 0, 6);
        _relayLcdHeader.TextTrimming = TextTrimming.CharacterEllipsis;

        _relayLcdBody = LcdText(9.65, FontWeights.Normal);
        _relayLcdBody.LineHeight = 15.4;
        _relayLcdBody.Height = 108;
        _relayLcdBody.TextWrapping = TextWrapping.NoWrap;
        _relayLcdBody.TextTrimming = TextTrimming.CharacterEllipsis;

        _relayLcdFooter = LcdText(8.25, FontWeights.Medium);
        _relayLcdFooter.TextTrimming = TextTrimming.CharacterEllipsis;

        lcdPanel.Children.Add(_relayLcdHeader);
        lcdPanel.Children.Add(_relayLcdBody);
        lcdPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(157, 170, 165)),
            Margin = new Thickness(0, 6, 0, 5)
        });
        lcdPanel.Children.Add(_relayLcdFooter);

        _relaySettingsFingerprint = _settings.Fingerprint();
        _relayFaceplateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _relayFaceplateTimer.Tick += RelayFaceplateTimer_Tick;
        _relayFaceplateTimer.Start();

        AddRelayFaceplateEvent(DateTimeOffset.Now, "READY", "Relay HMI initialized");
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

    private void RelayFaceplateTimer_Tick(object? sender, EventArgs e)
        => TickRelayFaceplate();

    private void TickRelayFaceplate()
    {
        if (!_relayFaceplateInitialized)
            return;

        var frame = ReadRelayFaceplateFrame();
        if (frame is null)
        {
            RenderRelayLcd(null);
            return;
        }

        if (!string.Equals(_relaySourceIdentity, frame.SourceIdentity, StringComparison.Ordinal))
        {
            _relaySourceIdentity = frame.SourceIdentity;
            _relayOperationRecorder.ResetCurrent();
            _relayPageHistory.Clear();
            _relayLcdPage = RelayLcdPage.Measurements;
            CloseRelayMenu();
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
            NavigateRelayPage(RelayLcdPage.TripRecord, remember: true);
        }

        if (transition.BlockedStarted)
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "BLOCK", frame.Snapshot.SmvTrust.Code);

        if (transition.ResetObserved)
        {
            AddRelayFaceplateEvent(frame.Snapshot.Timestamp, "RESET", "Trip indication reset");
            if (_relayLcdPage == RelayLcdPage.TripRecord)
                NavigateRelayPage(RelayLcdPage.Measurements, remember: false);
        }

        _relayLastFrame = frame;
        UpdateRelayTrustBanner(frame);
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

    internal void HandleRelayHardwareKey(VirtualRelayHardwareKey key)
    {
        if (!_relayFaceplateInitialized)
            return;

        switch (key)
        {
            case VirtualRelayHardwareKey.F1: HandleRelayFunctionKey(0); break;
            case VirtualRelayHardwareKey.F2: HandleRelayFunctionKey(1); break;
            case VirtualRelayHardwareKey.F3: HandleRelayFunctionKey(2); break;
            case VirtualRelayHardwareKey.F4: HandleRelayFunctionKey(3); break;
            case VirtualRelayHardwareKey.F5: HandleRelayFunctionKey(4); break;
            case VirtualRelayHardwareKey.Home: NavigateRelayPage(RelayLcdPage.Measurements); break;
            case VirtualRelayHardwareKey.Menu: OpenRelayMenu(RelayMenuLevel.Root); break;
            case VirtualRelayHardwareKey.Up: RelayNavigateVertical(-1); break;
            case VirtualRelayHardwareKey.Down: RelayNavigateVertical(+1); break;
            case VirtualRelayHardwareKey.Left: RelayNavigateLeft(); break;
            case VirtualRelayHardwareKey.Right: RelayNavigateRight(); break;
            case VirtualRelayHardwareKey.Ok: RelayAccept(); break;
            case VirtualRelayHardwareKey.Back: RelayBack(); break;
            case VirtualRelayHardwareKey.Favorite: NavigateRelayPage(_relayFavoritePage); break;
        }

        RenderRelayLcd(_relayLastFrame);
    }

    private void RelayNavigateVertical(int direction)
    {
        if (_relayMenuOpen)
        {
            var entries = CurrentRelayMenuEntries();
            if (entries.Length > 0)
                _relayMenuIndex = Wrap(_relayMenuIndex + direction, entries.Length);
            return;
        }

        if (_relayLcdPage == RelayLcdPage.Events)
        {
            BrowseRelayEvents(direction < 0 ? +1 : -1);
            return;
        }

        CycleRelayTopPage(direction);
    }

    private void RelayNavigateLeft()
    {
        if (_relayMenuOpen)
            BackRelayMenu();
        else
            CycleRelayTopPage(-1);
    }

    private void RelayNavigateRight()
    {
        if (_relayMenuOpen)
            ActivateRelayMenuSelection();
        else
            CycleRelayTopPage(+1);
    }

    private void RelayAccept()
    {
        if (_relayMenuOpen)
        {
            ActivateRelayMenuSelection();
            return;
        }

        if (_relayLcdPage == RelayLcdPage.Settings)
        {
            OpenRelaySettingsEditor();
            return;
        }

        OpenRelayMenu(MenuForRelayPage(_relayLcdPage));
    }

    private void RelayBack()
    {
        if (_relayMenuOpen)
        {
            BackRelayMenu();
            return;
        }

        if (_relayPageHistory.Count > 0)
        {
            _relayLcdPage = _relayPageHistory.Pop();
            _relayEventIndex = 0;
        }
        else
        {
            _relayLcdPage = RelayLcdPage.Measurements;
        }
    }

    private void HandleRelayFunctionKey(int index)
    {
        if (_relayMenuOpen)
        {
            switch (index)
            {
                case 0: BackRelayMenu(); break;
                case 1: RelayNavigateVertical(-1); break;
                case 2: RelayNavigateVertical(+1); break;
                case 3: ActivateRelayMenuSelection(); break;
                case 4: NavigateRelayPage(RelayLcdPage.Measurements); break;
            }
            return;
        }

        switch (_relayLcdPage)
        {
            case RelayLcdPage.Measurements:
                RelayMeasurementSoftKey(index, RelayLcdPage.CurrentMeasurements, RelayLcdPage.VoltageMeasurements, RelayLcdPage.SequenceMeasurements);
                break;
            case RelayLcdPage.CurrentMeasurements:
                RelayMeasurementSoftKey(index, RelayLcdPage.Measurements, RelayLcdPage.VoltageMeasurements, RelayLcdPage.SequenceMeasurements);
                break;
            case RelayLcdPage.VoltageMeasurements:
                RelayMeasurementSoftKey(index, RelayLcdPage.Measurements, RelayLcdPage.CurrentMeasurements, RelayLcdPage.SequenceMeasurements);
                break;
            case RelayLcdPage.SequenceMeasurements:
                RelayMeasurementSoftKey(index, RelayLcdPage.Measurements, RelayLcdPage.CurrentMeasurements, RelayLcdPage.VoltageMeasurements);
                break;
            case RelayLcdPage.Protection:
            case RelayLcdPage.Feeder:
                HandleProtectionSoftKey(index);
                break;
            case RelayLcdPage.Events:
                HandleEventsSoftKey(index);
                break;
            case RelayLcdPage.TripRecord:
                HandleTripSoftKey(index);
                break;
            case RelayLcdPage.Settings:
                HandleSettingsSoftKey(index);
                break;
            case RelayLcdPage.Diagnostics:
            case RelayLcdPage.DeviceInfo:
                HandleDiagnosticsSoftKey(index);
                break;
        }
    }

    private void RelayMeasurementSoftKey(int index, RelayLcdPage first, RelayLcdPage second, RelayLcdPage third)
    {
        if (index == 0) NavigateRelayPage(first);
        else if (index == 1) NavigateRelayPage(second);
        else if (index == 2) NavigateRelayPage(third);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Events);
        else SetRelayFavorite();
    }

    private void HandleProtectionSoftKey(int index)
    {
        if (index == 0) NavigateRelayPage(RelayLcdPage.Protection);
        else if (index == 1) NavigateRelayPage(RelayLcdPage.Feeder);
        else if (index == 2) NavigateRelayPage(RelayLcdPage.TripRecord);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Events);
        else SetRelayFavorite();
    }

    private void HandleEventsSoftKey(int index)
    {
        if (index == 0) BrowseRelayEvents(+1);
        else if (index == 1) BrowseRelayEvents(-1);
        else if (index == 2) NavigateRelayPage(RelayLcdPage.TripRecord);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Measurements);
        else SetRelayFavorite();
    }

    private void HandleTripSoftKey(int index)
    {
        if (index == 0) NavigateRelayPage(RelayLcdPage.Protection);
        else if (index == 1) NavigateRelayPage(RelayLcdPage.Feeder);
        else if (index == 2) NavigateRelayPage(RelayLcdPage.Events);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Measurements);
        else SetRelayFavorite();
    }

    private void HandleSettingsSoftKey(int index)
    {
        if (index == 0) OpenRelaySettingsEditor();
        else if (index == 1) NavigateRelayPage(RelayLcdPage.Protection);
        else if (index == 2) NavigateRelayPage(RelayLcdPage.Feeder);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Measurements);
        else SetRelayFavorite();
    }

    private void HandleDiagnosticsSoftKey(int index)
    {
        if (index == 0) NavigateRelayPage(RelayLcdPage.Diagnostics);
        else if (index == 1) NavigateRelayPage(RelayLcdPage.DeviceInfo);
        else if (index == 2) NavigateRelayPage(RelayLcdPage.Events);
        else if (index == 3) NavigateRelayPage(RelayLcdPage.Measurements);
        else SetRelayFavorite();
    }

    private void SetRelayFavorite()
    {
        _relayFavoritePage = _relayLcdPage;
        AddRelayFaceplateEvent(DateTimeOffset.Now, "FAVORITE", RelayPageLabel(_relayFavoritePage));
        StatusText.Text = $"Relay favorite page set to {RelayPageLabel(_relayFavoritePage)}.";
    }

    private void OpenRelaySettingsEditor()
        => ProtectionSettings_Click(_p6VirtualRelay ?? this, new RoutedEventArgs());

    private void NavigateRelayPage(RelayLcdPage page, bool remember = true)
    {
        CloseRelayMenu();
        if (page == _relayLcdPage)
            return;

        if (remember)
            _relayPageHistory.Push(_relayLcdPage);
        _relayLcdPage = page;
        _relayEventIndex = 0;
    }

    private void CycleRelayTopPage(int direction)
    {
        var normalized = TopRelayPageFor(_relayLcdPage);
        var current = Array.IndexOf(RelayTopPages, normalized);
        current = Wrap((current < 0 ? 0 : current) + direction, RelayTopPages.Length);
        NavigateRelayPage(RelayTopPages[current]);
    }

    private static RelayLcdPage TopRelayPageFor(RelayLcdPage page)
        => page switch
        {
            RelayLcdPage.CurrentMeasurements or
            RelayLcdPage.VoltageMeasurements or
            RelayLcdPage.SequenceMeasurements => RelayLcdPage.Measurements,
            RelayLcdPage.DeviceInfo => RelayLcdPage.Diagnostics,
            _ => page
        };

    private void OpenRelayMenu(RelayMenuLevel level)
    {
        _relayMenuOpen = true;
        _relayMenuLevel = level == RelayMenuLevel.None ? RelayMenuLevel.Root : level;
        _relayMenuIndex = _relayMenuLevel == RelayMenuLevel.Root
            ? FindRelayRootIndex(_relayLcdPage)
            : FindRelayMenuIndex(_relayMenuLevel, _relayLcdPage);
    }

    private void CloseRelayMenu()
    {
        _relayMenuOpen = false;
        _relayMenuLevel = RelayMenuLevel.None;
        _relayMenuIndex = 0;
    }

    private void BackRelayMenu()
    {
        if (_relayMenuLevel == RelayMenuLevel.Root)
        {
            CloseRelayMenu();
            return;
        }

        _relayMenuLevel = RelayMenuLevel.Root;
        _relayMenuIndex = FindRelayRootIndex(_relayLcdPage);
    }

    private void ActivateRelayMenuSelection()
    {
        var entries = CurrentRelayMenuEntries();
        if (entries.Length == 0)
            return;

        _relayMenuIndex = Math.Clamp(_relayMenuIndex, 0, entries.Length - 1);
        var selected = entries[_relayMenuIndex];
        if (selected.Submenu is { } submenu)
        {
            _relayMenuLevel = submenu;
            _relayMenuIndex = FindRelayMenuIndex(submenu, _relayLcdPage);
            return;
        }

        if (selected.Page is { } page)
            NavigateRelayPage(page);
    }

    private RelayMenuEntry[] CurrentRelayMenuEntries()
        => _relayMenuLevel switch
        {
            RelayMenuLevel.Measurements => RelayMeasurementsMenu,
            RelayMenuLevel.Protection => RelayProtectionMenu,
            RelayMenuLevel.Records => RelayRecordsMenu,
            RelayMenuLevel.Diagnostics => RelayDiagnosticsMenu,
            _ => RelayRootMenu
        };

    private static RelayMenuLevel MenuForRelayPage(RelayLcdPage page)
        => page switch
        {
            RelayLcdPage.Measurements or
            RelayLcdPage.CurrentMeasurements or
            RelayLcdPage.VoltageMeasurements or
            RelayLcdPage.SequenceMeasurements => RelayMenuLevel.Measurements,
            RelayLcdPage.Protection or RelayLcdPage.Feeder => RelayMenuLevel.Protection,
            RelayLcdPage.Events or RelayLcdPage.TripRecord => RelayMenuLevel.Records,
            RelayLcdPage.Diagnostics or RelayLcdPage.DeviceInfo => RelayMenuLevel.Diagnostics,
            _ => RelayMenuLevel.Root
        };

    private static int FindRelayMenuIndex(RelayMenuLevel level, RelayLcdPage page)
    {
        var entries = level switch
        {
            RelayMenuLevel.Measurements => RelayMeasurementsMenu,
            RelayMenuLevel.Protection => RelayProtectionMenu,
            RelayMenuLevel.Records => RelayRecordsMenu,
            RelayMenuLevel.Diagnostics => RelayDiagnosticsMenu,
            _ => RelayRootMenu
        };
        var index = Array.FindIndex(entries, item => item.Page == page);
        return index < 0 ? 0 : index;
    }

    private static int FindRelayRootIndex(RelayLcdPage page)
        => page switch
        {
            RelayLcdPage.Measurements or
            RelayLcdPage.CurrentMeasurements or
            RelayLcdPage.VoltageMeasurements or
            RelayLcdPage.SequenceMeasurements => 0,
            RelayLcdPage.Protection or RelayLcdPage.Feeder => 1,
            RelayLcdPage.Events or RelayLcdPage.TripRecord => 2,
            RelayLcdPage.Settings => 3,
            RelayLcdPage.Diagnostics or RelayLcdPage.DeviceInfo => 4,
            _ => 0
        };

    private void BrowseRelayEvents(int olderDirection)
    {
        if (_relayFaceplateEvents.Count > 0)
            _relayEventIndex = Math.Clamp(_relayEventIndex + olderDirection, 0, _relayFaceplateEvents.Count - 1);
    }

    private void RenderRelayLcd(RelayFaceplateFrame? frame)
    {
        if (_relayLcdHeader is null || _relayLcdBody is null || _relayLcdFooter is null)
            return;

        if (_relayMenuOpen)
        {
            RenderRelayMenu();
            UpdateRelaySoftKeys();
            return;
        }

        switch (_relayLcdPage)
        {
            case RelayLcdPage.Measurements: RenderMeasurementPage(frame); break;
            case RelayLcdPage.CurrentMeasurements: RenderCurrentMeasurementsPage(frame); break;
            case RelayLcdPage.VoltageMeasurements: RenderVoltageMeasurementsPage(frame); break;
            case RelayLcdPage.SequenceMeasurements: RenderSequenceMeasurementsPage(frame); break;
            case RelayLcdPage.Protection: RenderProtectionPage(frame); break;
            case RelayLcdPage.Feeder: RenderFeederProtectionPage(frame); break;
            case RelayLcdPage.Settings: RenderSettingsPage(); break;
            case RelayLcdPage.Events: RenderEventsPage(); break;
            case RelayLcdPage.Diagnostics: RenderDiagnosticsPage(frame); break;
            case RelayLcdPage.DeviceInfo: RenderDeviceInfoPage(frame); break;
            case RelayLcdPage.TripRecord: RenderTripRecordPage(frame); break;
        }

        UpdateRelaySoftKeys();
    }

    private void RenderRelayMenu()
    {
        var entries = CurrentRelayMenuEntries();
        if (entries.Length == 0)
        {
            SetRelayLcd("MAIN MENU", "NO MENU ITEMS", "BACK");
            return;
        }

        _relayMenuIndex = Math.Clamp(_relayMenuIndex, 0, entries.Length - 1);
        var visible = Math.Min(5, entries.Length);
        var start = Math.Clamp(_relayMenuIndex - 2, 0, Math.Max(0, entries.Length - visible));
        var lines = entries.Skip(start).Take(visible)
            .Select((item, offset) => start + offset == _relayMenuIndex ? $"› {item.Label}" : $"  {item.Label}");
        var title = _relayMenuLevel == RelayMenuLevel.Root
            ? "MAIN MENU"
            : $"MENU · {_relayMenuLevel.ToString().ToUpperInvariant()}";
        SetRelayLcd(title, string.Join(Environment.NewLine, lines), "▲▼ SELECT · OK OPEN · ◀ BACK");
    }

    private void RenderMeasurementPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("MEASUREMENTS", "WAITING FOR DATA", "MENU · SOURCE NOT READY");
            return;
        }

        var m = RelayHomeDisplayMeasurement(frame.Measurement, ViewCombo.SelectedIndex == 1);
        var p = m.Phasors;
        var domain = ViewCombo.SelectedIndex == 1 ? "PRIMARY" : "SECONDARY";
        var body = p is null
            ? $"IR {m.PhaseA,8:0.00} A\nIS {m.PhaseB,8:0.00} A\nIT {m.PhaseC,8:0.00} A\n3I0{m.Residual,8:0.00} A"
            : $"I1 {p.PositiveSequenceCurrent.Magnitude,8:0.00} A\n" +
              $"3I0{p.ResidualCurrent.Magnitude,8:0.00} A\n" +
              $"U1 {p.PositiveSequenceVoltage.Magnitude,8:0.00} V\n" +
              $"3U0{p.ResidualVoltage.Magnitude,8:0.00} V\n" +
              $"F  {p.FrequencyHz,8:0.000} Hz";
        SetRelayLcd(
            $"MEASUREMENTS · {domain}",
            body,
            frame.Snapshot.TripLatched ? "TRIP LATCHED · MENU" : "READY · OK MEASUREMENT MENU");
    }

    private void RenderCurrentMeasurementsPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("CURRENT RMS", "WAITING FOR DATA", "HOME · MENU");
            return;
        }

        var m = RelayHomeDisplayMeasurement(frame.Measurement, ViewCombo.SelectedIndex == 1);
        var domain = ViewCombo.SelectedIndex == 1 ? "PRIMARY" : "SECONDARY";
        SetRelayLcd(
            $"CURRENT RMS · {domain}",
            $"IR       {m.PhaseA,8:0.00} A\n" +
            $"IS       {m.PhaseB,8:0.00} A\n" +
            $"IT       {m.PhaseC,8:0.00} A\n" +
            $"3I0      {m.Residual,8:0.00} A",
            "OK MEASUREMENT MENU");
    }

    private void RenderVoltageMeasurementsPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("VOLTAGE RMS", "WAITING FOR DATA", "HOME · MENU");
            return;
        }

        var m = RelayHomeDisplayMeasurement(frame.Measurement, ViewCombo.SelectedIndex == 1);
        if (m.Phasors is not { } p)
        {
            SetRelayLcd("VOLTAGE RMS", "VOLTAGE PHASORS\nUNAVAILABLE", "CHECK SOURCE / SV DATA");
            return;
        }

        var domain = ViewCombo.SelectedIndex == 1 ? "PRIMARY" : "SECONDARY";
        SetRelayLcd(
            $"VOLTAGE RMS · {domain}",
            $"UR       {p.PhaseAVoltage.Magnitude,8:0.00} V\n" +
            $"US       {p.PhaseBVoltage.Magnitude,8:0.00} V\n" +
            $"UT       {p.PhaseCVoltage.Magnitude,8:0.00} V\n" +
            $"3U0      {p.ResidualVoltage.Magnitude,8:0.00} V",
            "OK MEASUREMENT MENU");
    }

    private void RenderSequenceMeasurementsPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("SEQUENCE", "WAITING FOR DATA", "HOME · MENU");
            return;
        }

        var m = RelayHomeDisplayMeasurement(frame.Measurement, ViewCombo.SelectedIndex == 1);
        if (m.Phasors is not { } p)
        {
            SetRelayLcd("SEQUENCE", "SEQUENCE DATA\nUNAVAILABLE", "CHECK SOURCE / SV DATA");
            return;
        }

        SetRelayLcd(
            "SEQUENCE / RESIDUAL",
            $"I1       {p.PositiveSequenceCurrent.Magnitude,8:0.00} A\n" +
            $"3I0      {p.ResidualCurrent.Magnitude,8:0.00} A\n" +
            $"U1       {p.PositiveSequenceVoltage.Magnitude,8:0.00} V\n" +
            $"3U0      {p.ResidualVoltage.Magnitude,8:0.00} V\n" +
            $"FREQ     {p.FrequencyHz,8:0.000} Hz",
            "OK MEASUREMENT MENU");
    }

    private void RenderProtectionPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("OVERCURRENT", "NO ACTIVE DATA", "HOME · MENU");
            return;
        }

        var s = frame.Snapshot;
        SetRelayLcd(
            "OVERCURRENT 50/51",
            $"50P-1 {Stage(s.Phase50),-7} {s.Phase50.Progress * 100,3:0}%\n" +
            $"51P   {Stage(s.Phase51),-7} {s.Phase51.Progress * 100,3:0}%\n" +
            $"50N   {Stage(s.Earth50),-7} {s.Earth50.Progress * 100,3:0}%\n" +
            $"51N   {Stage(s.Earth51),-7} {s.Earth51.Progress * 100,3:0}%",
            s.SmvTrust.AllowsTrip ? "TRIP PERMITTED · OK PROTECTION MENU" : $"TRIP BLOCKED · {s.SmvTrust.Code}");
    }

    private void RenderFeederProtectionPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("FEEDER PROTECTION", "NO ACTIVE DATA", "HOME · MENU");
            return;
        }

        var f = frame.Snapshot.Feeder;
        var footer = frame.Snapshot.SmvTrust.AllowsTrip
            ? DirectionFooter(f)
            : $"TRIP BLOCKED · {frame.Snapshot.SmvTrust.Code}";
        SetRelayLcd(
            "FEEDER PROTECTION",
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
        return "TRIP PERMITTED · OK PROTECTION MENU";
    }

    private static string DirectionLabel(DirectionalDecision decision)
        => !decision.PolarizingAvailable
            ? "NO VPOL"
            : decision.InSelectedDirection ? "SELECTED" : "RESTRAIN";

    private void RenderSettingsPage()
    {
        var phaseCurve = IecCurveCalculator.GetParameters(
            _settings.PhaseTimeCurve,
            _settings.PhaseTimeUserK,
            _settings.PhaseTimeUserAlpha,
            _settings.PhaseTimeUserC).ShortName;
        var earthCurve = IecCurveCalculator.GetParameters(
            _settings.EarthTimeCurve,
            _settings.EarthTimeUserK,
            _settings.EarthTimeUserAlpha,
            _settings.EarthTimeUserC).ShortName;
        var feederEnabled = EnabledFeederFunctions(_settings.Feeder);
        SetRelayLcd(
            $"SETTINGS · {_settings.GroupName}",
            $"REV {_settings.Revision}  {_settings.Fingerprint()[..8]}\n" +
            $"51P {_settings.PhaseTimePickupA:0.###}A {phaseCurve}\n" +
            $"51N {_settings.EarthTimePickupA:0.###}A {earthCurve}\n" +
            $"FEEDER {TrimLine(feederEnabled, 20)}\n" +
            $"VT {_processBus.MeasurementContext.VtPrimaryV:0.#}/{_processBus.MeasurementContext.VtSecondaryV:0.#}V",
            "OK / F1 EDIT SETTINGS");
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
        if (_relayFaceplateEvents.Count == 0)
        {
            SetRelayLcd("EVENTS", "NO EVENTS RECORDED", "OK RECORDS MENU");
            return;
        }

        var events = _relayFaceplateEvents.AsEnumerable().Reverse().ToArray();
        _relayEventIndex = Math.Clamp(_relayEventIndex, 0, events.Length - 1);
        var item = events[_relayEventIndex];
        SetRelayLcd(
            $"EVENT {item.Sequence:000} · {item.Code}",
            $"DATE  {item.Timestamp:yyyy-MM-dd}\n" +
            $"TIME  {item.Timestamp:HH:mm:ss.fff}\n" +
            TrimLine(item.Detail, 27),
            $"▲ OLDER · ▼ NEWER · {_relayEventIndex + 1}/{events.Length}");
    }

    private void RenderDiagnosticsPage(RelayFaceplateFrame? frame)
    {
        if (frame is null)
        {
            SetRelayLcd("PROCESS BUS", "WAITING FOR SV", "OK DIAGNOSTICS MENU");
            return;
        }

        var trust = frame.Snapshot.SmvTrust;
        SetRelayLcd(
            "PROCESS BUS / TRUST",
            $"SOURCE  {TrimLine(frame.SourceSummary, 19)}\n" +
            $"STATE   {trust.Code}\n" +
            $"MEASURE {(trust.AllowsMeasurement ? "YES" : "NO")}\n" +
            $"PICKUP  {(trust.AllowsPickup ? "YES" : "NO")}\n" +
            $"TRIP    {(trust.AllowsTrip ? "YES" : "NO")}",
            TrimLine(frame.Diagnostics, 33));
    }

    private void RenderDeviceInfoPage(RelayFaceplateFrame? frame)
    {
        SetRelayLcd(
            "DEVICE INFORMATION",
            $"ARVREL 650 · ARV-650\n" +
            $"IEC 61850 SAMPLED VALUES\n" +
            $"GROUP {_settings.GroupName} · REV {_settings.Revision}\n" +
            $"SOURCE {TrimLine(frame?.SourceSummary ?? "NO SOURCE", 19)}\n" +
            $"TRUST  {frame?.Snapshot.SmvTrust.Code ?? "N/A"}",
            "VIRTUAL TRIP OUTPUT ONLY");
    }

    private void RenderTripRecordPage(RelayFaceplateFrame? frame)
    {
        var operation = _relayOperationRecorder.Current;
        if (operation?.TripTimestamp is not { } tripAt)
        {
            SetRelayLcd("LAST TRIP", "NO TRIP RECORD", "OK RECORDS MENU");
            return;
        }

        var element = string.IsNullOrWhiteSpace(operation.TripElement)
            ? operation.PickupElement
            : operation.TripElement;
        var operateMs = operation.OperateTime?.TotalMilliseconds ?? 0;
        var fifthLine = DirectionalTripLine(element, frame?.Snapshot.Feeder);
        SetRelayLcd(
            $"TRIP RECORD · {element}",
            $"PICKUP {operation.PickupTimestamp:HH:mm:ss.fff}\n" +
            $"TRIP   {tripAt:HH:mm:ss.fff}\n" +
            $"P→T       {operateMs,7:0.0} ms\n" +
            $"{operation.QuantitySymbol,-5} {operation.TripQuantity,8:0.00} {operation.QuantityUnit}\n" +
            fifthLine,
            "TRIP LATCHED · RESET ON RELAY");
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

    private void UpdateRelaySoftKeys()
    {
        if (_p6VirtualRelay is null)
            return;

        if (_relayMenuOpen)
        {
            _p6VirtualRelay.SetSoftKeys(new[]
            {
                new VirtualRelaySoftKey("BACK"),
                new VirtualRelaySoftKey("UP"),
                new VirtualRelaySoftKey("DOWN"),
                new VirtualRelaySoftKey("OPEN"),
                new VirtualRelaySoftKey("HOME")
            });
            return;
        }

        _p6VirtualRelay.SetSoftKeys(_relayLcdPage switch
        {
            RelayLcdPage.Measurements => MeasurementSoftKeys("I RMS", "U RMS", "SEQ"),
            RelayLcdPage.CurrentMeasurements => MeasurementSoftKeys("OVERVIEW", "U RMS", "SEQ"),
            RelayLcdPage.VoltageMeasurements => MeasurementSoftKeys("OVERVIEW", "I RMS", "SEQ"),
            RelayLcdPage.SequenceMeasurements => MeasurementSoftKeys("OVERVIEW", "I RMS", "U RMS"),
            RelayLcdPage.Protection => ProtectionSoftKeys(active: 0),
            RelayLcdPage.Feeder => ProtectionSoftKeys(active: 1),
            RelayLcdPage.Events => new[]
            {
                new VirtualRelaySoftKey("OLDER"),
                new VirtualRelaySoftKey("NEWER"),
                new VirtualRelaySoftKey("TRIP"),
                new VirtualRelaySoftKey("HOME"),
                new VirtualRelaySoftKey("SET FAV")
            },
            RelayLcdPage.TripRecord => new[]
            {
                new VirtualRelaySoftKey("50/51"),
                new VirtualRelaySoftKey("FEEDER"),
                new VirtualRelaySoftKey("EVENTS"),
                new VirtualRelaySoftKey("HOME"),
                new VirtualRelaySoftKey("SET FAV")
            },
            RelayLcdPage.Settings => new[]
            {
                new VirtualRelaySoftKey("EDIT"),
                new VirtualRelaySoftKey("50/51"),
                new VirtualRelaySoftKey("FEEDER"),
                new VirtualRelaySoftKey("HOME"),
                new VirtualRelaySoftKey("SET FAV")
            },
            RelayLcdPage.Diagnostics => DiagnosticsSoftKeys(active: 0),
            RelayLcdPage.DeviceInfo => DiagnosticsSoftKeys(active: 1),
            _ => MeasurementSoftKeys("I RMS", "U RMS", "SEQ")
        });
    }

    private static VirtualRelaySoftKey[] MeasurementSoftKeys(string first, string second, string third)
        => new[]
        {
            new VirtualRelaySoftKey(first),
            new VirtualRelaySoftKey(second),
            new VirtualRelaySoftKey(third),
            new VirtualRelaySoftKey("EVENTS"),
            new VirtualRelaySoftKey("SET FAV")
        };

    private static VirtualRelaySoftKey[] ProtectionSoftKeys(int active)
        => new[]
        {
            new VirtualRelaySoftKey("50/51", active == 0),
            new VirtualRelaySoftKey("FEEDER", active == 1),
            new VirtualRelaySoftKey("TRIP"),
            new VirtualRelaySoftKey("EVENTS"),
            new VirtualRelaySoftKey("SET FAV")
        };

    private static VirtualRelaySoftKey[] DiagnosticsSoftKeys(int active)
        => new[]
        {
            new VirtualRelaySoftKey("SV", active == 0),
            new VirtualRelaySoftKey("DEVICE", active == 1),
            new VirtualRelaySoftKey("EVENTS"),
            new VirtualRelaySoftKey("HOME"),
            new VirtualRelaySoftKey("SET FAV")
        };

    private void UpdateRelayTrustBanner(RelayFaceplateFrame frame)
    {
        if (_p6VirtualRelay is null)
            return;

        if (frame.Snapshot.TripLatched)
            _p6VirtualRelay.SetLcdTrustState("TRIP LATCH", VirtualRelayLcdTone.Trip);
        else if (!frame.Snapshot.SmvTrust.AllowsTrip)
            _p6VirtualRelay.SetLcdTrustState("SV BLOCK", VirtualRelayLcdTone.Warning);
        else
            _p6VirtualRelay.SetLcdTrustState("SV READY", VirtualRelayLcdTone.Normal);
    }

    private void AddRelayFaceplateEvent(DateTimeOffset timestamp, string code, string detail)
    {
        _relayFaceplateEvents.Add(new(
            _relayFaceplateEvents.Count == 0 ? 1 : _relayFaceplateEvents[^1].Sequence + 1,
            timestamp,
            code,
            detail));
        if (_relayFaceplateEvents.Count > 60)
            _relayFaceplateEvents.RemoveAt(0);
        _relayEventIndex = 0;
    }

    private static string Stage(ElementSnapshot element)
        => element.State switch
        {
            ProtectionStageState.Operated => "TRIP",
            ProtectionStageState.Blocked => "BLOCK",
            ProtectionStageState.Timing => "TIMING",
            ProtectionStageState.Pickup => "PICKUP",
            ProtectionStageState.Disabled => "OFF",
            _ => "READY"
        };

    private static string RelayPageLabel(RelayLcdPage page)
        => page switch
        {
            RelayLcdPage.Measurements => "Measurements overview",
            RelayLcdPage.CurrentMeasurements => "Current RMS",
            RelayLcdPage.VoltageMeasurements => "Voltage RMS",
            RelayLcdPage.SequenceMeasurements => "Sequence measurements",
            RelayLcdPage.Protection => "Overcurrent protection",
            RelayLcdPage.Feeder => "Feeder protection",
            RelayLcdPage.Settings => "Settings",
            RelayLcdPage.Events => "Events",
            RelayLcdPage.Diagnostics => "Process bus diagnostics",
            RelayLcdPage.DeviceInfo => "Device information",
            RelayLcdPage.TripRecord => "Last trip",
            _ => page.ToString()
        };

    private static int Wrap(int value, int count)
        => (value % count + count) % count;

    private static string TrimLine(string value, int length)
    {
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= length
            ? normalized
            : normalized[..Math.Max(1, length - 1)] + "…";
    }

    private enum RelayMenuLevel
    {
        None,
        Root,
        Measurements,
        Protection,
        Records,
        Diagnostics
    }

    private enum RelayLcdPage
    {
        Measurements,
        CurrentMeasurements,
        VoltageMeasurements,
        SequenceMeasurements,
        Protection,
        Feeder,
        Settings,
        Events,
        Diagnostics,
        DeviceInfo,
        TripRecord
    }

    private readonly record struct RelayMenuEntry(
        string Label,
        RelayLcdPage? Page = null,
        RelayMenuLevel? Submenu = null);

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
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.UnloadedEvent,
            new RoutedEventHandler(OnUnloaded));
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
