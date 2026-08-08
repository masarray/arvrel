using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private enum HmiPage
    {
        Home,
        Measured,
        Control,
        Events,
        System,
        Menu
    }

    private static readonly HmiPage[] HmiCyclePages =
    [
        HmiPage.Home,
        HmiPage.Measured,
        HmiPage.Control,
        HmiPage.Events,
        HmiPage.System
    ];

    private static readonly HmiPage[] HmiMenuPages =
    [
        HmiPage.Measured,
        HmiPage.Control,
        HmiPage.Events,
        HmiPage.System
    ];

    private readonly AvrIec61850ServerHost _iec61850Server = new();
    private readonly Dictionary<string, TextBlock> _hmiDynamicText = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<HmiPage, Border> _hmiHeaderTabs = new();
    private Grid? _hmiScreenRoot;
    private Border? _hmiPageOverlay;
    private StackPanel? _hmiPageContent;
    private HmiPage _hmiPage = HmiPage.Home;
    private int _hmiMenuIndex;
    private bool _hmiNavigationInitialized;
    private bool _hmiReturnToMenu;
    private TextBox? _iecHostBox;
    private TextBox? _iecPortBox;

    private void InitializeHmiNavigation()
    {
        if (_hmiNavigationInitialized || TrendCanvas is null)
            return;

        _hmiScreenRoot = FindHmiScreenRoot(TrendCanvas);
        if (_hmiScreenRoot is null)
            return;

        _hmiPageOverlay = new Border
        {
            Background = HmiBrush(0xF3, 0xF4, 0xF5),
            BorderBrush = HmiBrush(0xD0, 0xD6, 0xDA),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 12, 14, 10),
            Visibility = Visibility.Collapsed
        };
        _hmiPageContent = new StackPanel();
        _hmiPageOverlay.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _hmiPageContent
        };
        Grid.SetRow(_hmiPageOverlay, 1);
        Panel.SetZIndex(_hmiPageOverlay, 20);
        _hmiScreenRoot.Children.Add(_hmiPageOverlay);

        WireHmiHeaderTabs();
        WireHmiHardwareNavigation();
        _hmiNavigationInitialized = true;
        NavigateHmi(HmiPage.Home);
    }

    private void ShutdownHmiNavigation()
    {
        _ = _iec61850Server.StopAsync();
    }

    private Grid? FindHmiScreenRoot(DependencyObject start)
    {
        DependencyObject? current = start;
        while (current is not null)
        {
            if (current is Grid grid && grid.RowDefinitions.Count == 3)
            {
                var top = grid.RowDefinitions[0].Height;
                var bottom = grid.RowDefinitions[2].Height;
                if (top.IsAbsolute && bottom.IsAbsolute && top.Value >= 30 && bottom.Value >= 20)
                    return grid;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void WireHmiHeaderTabs()
    {
        if (_hmiScreenRoot is null)
            return;

        var mapping = new Dictionary<string, HmiPage>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOME"] = HmiPage.Home,
            ["Measured"] = HmiPage.Measured,
            ["Control"] = HmiPage.Control,
            ["Events"] = HmiPage.Events,
            ["System"] = HmiPage.System
        };

        foreach (var text in FindVisualChildren<TextBlock>(_hmiScreenRoot))
        {
            if (!mapping.TryGetValue(text.Text, out var page) || text.Parent is not Border border)
                continue;

            border.Tag = page;
            border.Cursor = Cursors.Hand;
            border.MouseLeftButtonUp += HmiHeaderTab_MouseLeftButtonUp;
            _hmiHeaderTabs[page] = border;
        }
    }

    private void WireHmiHardwareNavigation()
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var label = button.Content?.ToString();
            if (label is not ("ENTER" or "◀" or "▶" or "BACK" or "MENU"))
                continue;

            button.Click -= HmiHardwareNavigation_Click;
            button.Click += HmiHardwareNavigation_Click;
        }
    }

    private void HmiHeaderTab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: HmiPage page })
        {
            _hmiReturnToMenu = false;
            NavigateHmi(page);
            e.Handled = true;
        }
    }

    private void HmiHardwareNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        switch (button.Content?.ToString())
        {
            case "MENU":
                _hmiReturnToMenu = false;
                NavigateHmi(HmiPage.Menu);
                break;
            case "BACK":
                if (_hmiPage == HmiPage.Menu)
                    NavigateHmi(HmiPage.Home);
                else if (_hmiReturnToMenu)
                {
                    _hmiReturnToMenu = false;
                    NavigateHmi(HmiPage.Menu);
                }
                else
                    NavigateHmi(HmiPage.Home);
                break;
            case "◀":
                MoveHmiSelection(-1);
                break;
            case "▶":
                MoveHmiSelection(+1);
                break;
            case "ENTER":
                if (_hmiPage == HmiPage.Menu)
                {
                    _hmiReturnToMenu = true;
                    NavigateHmi(HmiMenuPages[_hmiMenuIndex]);
                }
                else if (_hmiPage == HmiPage.Home)
                    NavigateHmi(HmiPage.Menu);
                break;
        }
    }

    private void MoveHmiSelection(int direction)
    {
        if (_hmiPage == HmiPage.Menu)
        {
            _hmiMenuIndex = (_hmiMenuIndex + direction + HmiMenuPages.Length) % HmiMenuPages.Length;
            BuildHmiMenuPage();
            return;
        }

        var index = Array.IndexOf(HmiCyclePages, _hmiPage);
        if (index < 0)
            index = 0;
        index = (index + direction + HmiCyclePages.Length) % HmiCyclePages.Length;
        _hmiReturnToMenu = false;
        NavigateHmi(HmiCyclePages[index]);
    }

    private void NavigateHmi(HmiPage page)
    {
        if (!_hmiNavigationInitialized || _hmiPageOverlay is null || _hmiPageContent is null)
            return;

        _hmiPage = page;
        _hmiDynamicText.Clear();
        UpdateHmiHeaderHighlight();

        if (page == HmiPage.Home)
        {
            _hmiPageOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        _hmiPageOverlay.Visibility = Visibility.Visible;
        switch (page)
        {
            case HmiPage.Measured:
                BuildMeasuredPage();
                break;
            case HmiPage.Control:
                BuildControlPage();
                break;
            case HmiPage.Events:
                BuildEventsPage();
                break;
            case HmiPage.System:
                BuildSystemPage();
                break;
            case HmiPage.Menu:
                BuildHmiMenuPage();
                break;
        }

        RefreshHmiNavigation();
    }

    private void UpdateHmiHeaderHighlight()
    {
        foreach (var (page, border) in _hmiHeaderTabs)
        {
            var active = page == _hmiPage || (_hmiPage == HmiPage.Menu && page == HmiPage.Home);
            border.Background = active ? HmiBrush(0x24, 0x5D, 0x9A) : HmiBrush(0xE5, 0xE8, 0xEB);
            if (border.Child is TextBlock text)
                text.Foreground = active ? Brushes.White : HmiBrush(0x30, 0x3B, 0x41);
        }
    }

    private void BuildHmiMenuPage()
    {
        if (_hmiPageContent is null)
            return;

        _hmiPageContent.Children.Clear();
        _hmiPageContent.Children.Add(HmiTitle("Main menu", "◀ / ▶ select · ENTER open · BACK home"));

        var labels = new[]
        {
            ("Measured values", "Live U / I / PF / frequency / tap feedback"),
            ("Control parameters", "Voltage band, T1/T2, authority, blocking and OLTC"),
            ("Events", "Recent AVR and IEC 61850 activity"),
            ("Communication / IEC 61850", "MMS server, SAS endpoint and live model")
        };

        for (var i = 0; i < labels.Length; i++)
        {
            var selected = i == _hmiMenuIndex;
            var row = new Border
            {
                Background = selected ? HmiBrush(0xD8, 0xEA, 0xF8) : Brushes.White,
                BorderBrush = selected ? HmiBrush(0x2E, 0x78, 0xB5) : HmiBrush(0xD3, 0xD8, 0xDC),
                BorderThickness = new Thickness(selected ? 2 : 1),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 0, 7),
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand,
                Tag = i
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock
            {
                Text = selected ? "▶" : "•",
                Foreground = selected ? HmiBrush(0x24, 0x5D, 0x9A) : HmiBrush(0x8C, 0x98, 0xA0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            });
            var copy = new StackPanel();
            Grid.SetColumn(copy, 1);
            copy.Children.Add(new TextBlock { Text = labels[i].Item1, Foreground = HmiBrush(0x25, 0x2D, 0x32), FontSize = 12.5, FontWeight = FontWeights.SemiBold });
            copy.Children.Add(new TextBlock { Text = labels[i].Item2, Foreground = HmiBrush(0x6E, 0x7C, 0x84), FontSize = 9.5, Margin = new Thickness(0, 2, 0, 0) });
            grid.Children.Add(copy);
            row.Child = grid;
            row.MouseLeftButtonUp += HmiMenuRow_MouseLeftButtonUp;
            _hmiPageContent.Children.Add(row);
        }
    }

    private void HmiMenuRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int index })
            return;

        _hmiMenuIndex = Math.Clamp(index, 0, HmiMenuPages.Length - 1);
        _hmiReturnToMenu = true;
        NavigateHmi(HmiMenuPages[_hmiMenuIndex]);
        e.Handled = true;
    }

    private void BuildMeasuredPage()
    {
        if (_hmiPageContent is null)
            return;

        _hmiPageContent.Children.Clear();
        _hmiPageContent.Children.Add(HmiTitle("Measured values", "Live AVR terminal and OLTC feedback"));
        var grid = NewCardGrid();
        AddValueCard(grid, 0, 0, "Control voltage", "U", "0.00 V");
        AddValueCard(grid, 0, 1, "Load current", "I", "0.000 A");
        AddValueCard(grid, 1, 0, "Power factor", "PF", "—");
        AddValueCard(grid, 1, 1, "Frequency", "FREQ", "— Hz");
        AddValueCard(grid, 2, 0, "Tap position", "TAP", "00");
        AddValueCard(grid, 2, 1, "Voltage deviation", "DEV", "— %");
        _hmiPageContent.Children.Add(grid);
    }

    private void BuildControlPage()
    {
        if (_hmiPageContent is null)
            return;

        _hmiPageContent.Children.Clear();
        _hmiPageContent.Children.Add(HmiTitle("Control parameters", "Read-only front-panel overview · edit settings in AVR Configuration"));
        var grid = NewCardGrid();
        AddValueCard(grid, 0, 0, "Setpoint", "SETPOINT", "100.00 V");
        AddValueCard(grid, 0, 1, "Bandwidth", "BAND", "±1.00 %");
        AddValueCard(grid, 1, 0, "Automatic timing", "TIMER", "T1 10.0 s · T2 2.0 s");
        AddValueCard(grid, 1, 1, "Control authority", "AUTH", "LOCAL · AUTO");
        AddValueCard(grid, 2, 0, "Tap range", "TAPRANGE", "-08 … +08");
        AddValueCard(grid, 2, 1, "Blocking", "BLOCK", "U 80…120 % · I limits OFF");
        _hmiPageContent.Children.Add(grid);
        _hmiPageContent.Children.Add(HmiInfo("Remote commands are intentionally not mapped to the MMS server yet. The ARIEC61850 server remains read-only except RCB reporting controls."));
    }

    private void BuildEventsPage()
    {
        if (_hmiPageContent is null)
            return;

        _hmiPageContent.Children.Clear();
        _hmiPageContent.Children.Add(HmiTitle("Events", "Controller event trace"));
        var text = new TextBlock
        {
            Foreground = HmiBrush(0x2B, 0x36, 0x3C),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 9.5,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        _hmiDynamicText["EVENTS"] = text;
        _hmiPageContent.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = HmiBrush(0xD3, 0xD8, 0xDC),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12),
            Child = text
        });
    }

    private void BuildSystemPage()
    {
        if (_hmiPageContent is null)
            return;

        _hmiPageContent.Children.Clear();
        _hmiPageContent.Children.Add(HmiTitle("Communication / IEC 61850", "ARIEC61850 sibling MMS server · SAS commissioning endpoint"));

        var statusCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = HmiBrush(0xD3, 0xD8, 0xDC),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var statusGrid = new Grid();
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var statusLeft = new StackPanel();
        statusLeft.Children.Add(HmiSmallLabel("SERVER STATUS"));
        statusLeft.Children.Add(RegisterHmiText("IEC_STATUS", "STOPPED", 16, true));
        statusLeft.Children.Add(RegisterHmiText("IEC_ENDPOINT", "0.0.0.0:102", 10, false));
        statusLeft.Children.Add(new TextBlock { Text = "Model: ARVAVR1 · ATCC1 · YLTC1 · MMXU1", Foreground = HmiBrush(0x6E, 0x7C, 0x84), FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
        statusGrid.Children.Add(statusLeft);
        var statusRight = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        Grid.SetColumn(statusRight, 1);
        statusRight.Children.Add(HmiSmallLabel("LIVE ACTIVITY"));
        statusRight.Children.Add(RegisterHmiText("IEC_STATS", "Connections 0 · Requests 0 · Reports 0", 10, true));
        statusRight.Children.Add(RegisterHmiText("IEC_ACTIVITY", "Server stopped", 8.8, false));
        statusGrid.Children.Add(statusRight);
        statusCard.Child = statusGrid;
        _hmiPageContent.Children.Add(statusCard);

        var config = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        config.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        var hostPanel = new StackPanel();
        hostPanel.Children.Add(HmiSmallLabel("BIND ADDRESS"));
        _iecHostBox = HmiTextBox("0.0.0.0");
        hostPanel.Children.Add(_iecHostBox);
        config.Children.Add(hostPanel);
        var portPanel = new StackPanel();
        Grid.SetColumn(portPanel, 2);
        portPanel.Children.Add(HmiSmallLabel("TCP PORT"));
        _iecPortBox = HmiTextBox("102");
        portPanel.Children.Add(_iecPortBox);
        config.Children.Add(portPanel);
        _hmiPageContent.Children.Add(config);

        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var start = HmiActionButton("START IEC 61850 SERVER", true);
        start.Click += StartIec61850Server_Click;
        buttons.Children.Add(start);
        var stop = HmiActionButton("STOP SERVER", false);
        stop.Click += StopIec61850Server_Click;
        Grid.SetColumn(stop, 2);
        buttons.Children.Add(stop);
        _hmiPageContent.Children.Add(buttons);

        _hmiPageContent.Children.Add(HmiInfo($"SAS client: connect to this PC IPv4 on TCP/102. Local IPv4: {LocalIpv4Summary()}. Supported P0: MMS association, browse/GetNameList, Read, DataSet, URCB/BRCB enable + GI/integrity reports. Process controls and setting writes remain blocked by the read-only server guard."));
    }

    private void StartIec61850Server_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var host = _iecHostBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(host))
                host = "0.0.0.0";
            if (!int.TryParse(_iecPortBox?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
                throw new FormatException("IEC 61850 TCP port is not a valid integer.");

            _iec61850Server.Start(host, port);
            AddEvent("IEC61850", $"MMS server started · {host}:{port}");
            RefreshHmiNavigation();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SocketException or FormatException)
        {
            AddEvent("IEC ERR", ex.Message);
            ShowWarning(ex.Message, "AVR IEC 61850 server");
            RefreshHmiNavigation();
        }
    }

    private async void StopIec61850Server_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _iec61850Server.StopAsync();
            AddEvent("IEC61850", "MMS server stopped");
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            AddEvent("IEC ERR", ex.Message);
        }
        finally
        {
            RefreshHmiNavigation();
        }
    }

    private void RefreshHmiNavigation()
    {
        if (!_hmiNavigationInitialized)
            return;

        if (_hmiPage == HmiPage.Measured)
        {
            SetHmiText("U", _snapshot.SourceEnergized ? $"{_snapshot.MeasuredVoltageV:0.00} V" : "0.00 V");
            SetHmiText("I", _snapshot.SourceEnergized ? $"{_snapshot.SourceCurrentA:0.000} A" : "0.000 A");
            SetHmiText("PF", _snapshot.SourceEnergized && _snapshot.SourceCurrentA > 1e-9 ? _snapshot.PowerFactor.ToString("0.000", CultureInfo.InvariantCulture) : "—");
            SetHmiText("FREQ", _snapshot.SourceEnergized ? $"{_frequencyHz:0.00} Hz" : "— Hz");
            SetHmiText("TAP", _snapshot.TapPosition.ToString("+00;-00;00", CultureInfo.InvariantCulture));
            SetHmiText("DEV", _snapshot.SourceEnergized ? $"{_snapshot.DeviationPercent:+0.00;-0.00;0.00} %" : "— %");
        }
        else if (_hmiPage == HmiPage.Control)
        {
            SetHmiText("SETPOINT", $"{_snapshot.EffectiveSetpointVoltageV:0.00} V");
            SetHmiText("BAND", $"±{_settings.TolerancePercent:0.00} %");
            SetHmiText("TIMER", $"T1 {_settings.T1Seconds:0.0} s · T2 {_settings.T2Seconds:0.0} s");
            SetHmiText("AUTH", $"{(_snapshot.Authority == AvrControlAuthority.Local ? "LOCAL" : "REMOTE")} · {(_snapshot.Mode == AvrOperatingMode.Automatic ? "AUTO" : "MANUAL")}");
            SetHmiText("TAPRANGE", $"{_settings.MinimumTap:+00;-00;00} … {_settings.MaximumTap:+00;-00;00}");
            SetHmiText("BLOCK", $"U {_settings.UndervoltageBlockPercent:0.#}…{_settings.OvervoltageBlockPercent:0.#} % · I< {(_settings.UndercurrentBlockingEnabled ? _settings.UndercurrentBlockPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "OFF")} · I> {(_settings.OvercurrentBlockingEnabled ? _settings.OvercurrentBlockPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "OFF")}");
        }
        else if (_hmiPage == HmiPage.Events)
        {
            SetHmiText("EVENTS", _events.Count == 0 ? "No events" : string.Join(Environment.NewLine, _events.Reverse()));
        }
        else if (_hmiPage == HmiPage.System)
        {
            var status = _iec61850Server.GetStatus();
            SetHmiText("IEC_STATUS", !status.EngineAvailable ? "ENGINE UNAVAILABLE" : status.IsRunning ? "RUNNING" : "STOPPED");
            SetHmiText("IEC_ENDPOINT", status.Endpoint);
            SetHmiText("IEC_STATS", $"Active {status.ActiveConnections} · Accepted {status.AcceptedConnections} · Requests {status.ServedRequests} · Reports {status.ReportsSent}");
            SetHmiText("IEC_ACTIVITY", status.LastActivity);
        }
    }

    private void PublishIec61850Snapshot()
    {
        _iec61850Server.Publish(_snapshot, _settings, _frequencyHz);
    }

    private Grid NewCardGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (i < 2)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        }
        return grid;
    }

    private void AddValueCard(Grid grid, int logicalRow, int logicalColumn, string title, string key, string value)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = HmiBrush(0xD3, 0xD8, 0xDC),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(11, 8, 11, 8)
        };
        var panel = new StackPanel();
        panel.Children.Add(HmiSmallLabel(title.ToUpperInvariant()));
        var valueText = RegisterHmiText(key, value, 18, true);
        valueText.Margin = new Thickness(0, 3, 0, 0);
        panel.Children.Add(valueText);
        card.Child = panel;
        Grid.SetRow(card, logicalRow * 2);
        Grid.SetColumn(card, logicalColumn * 2);
        grid.Children.Add(card);
    }

    private TextBlock HmiTitle(string title, string subtitle)
    {
        var text = new TextBlock
        {
            Text = title,
            Foreground = HmiBrush(0x25, 0x2D, 0x32),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        };
        var panel = new StackPanel();
        panel.Children.Add(text);
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = HmiBrush(0x6E, 0x7C, 0x84),
            FontSize = 9.2,
            Margin = new Thickness(0, -14, 0, 10)
        });

        var container = new TextBlock { Visibility = Visibility.Collapsed };
        var host = new StackPanel();
        host.Children.Add(panel);
        host.Children.Add(container);
        return new TextBlock
        {
            Text = $"{title}\n{subtitle}",
            Foreground = HmiBrush(0x25, 0x2D, 0x32),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 19,
            Margin = new Thickness(0, 0, 0, 10)
        };
    }

    private TextBlock HmiInfo(string message)
        => new()
        {
            Text = message,
            Foreground = HmiBrush(0x5D, 0x6B, 0x73),
            Background = HmiBrush(0xE9, 0xEE, 0xF1),
            FontSize = 8.8,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 8, 0, 0)
        };

    private static TextBlock HmiSmallLabel(string text)
        => new()
        {
            Text = text,
            Foreground = HmiBrush(0x72, 0x80, 0x88),
            FontSize = 8.3,
            FontWeight = FontWeights.SemiBold
        };

    private TextBlock RegisterHmiText(string key, string text, double fontSize, bool bold)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = HmiBrush(0x2E, 0x69, 0xA0),
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap
        };
        _hmiDynamicText[key] = block;
        return block;
    }

    private void SetHmiText(string key, string value)
    {
        if (_hmiDynamicText.TryGetValue(key, out var text))
            text.Text = value;
    }

    private static TextBox HmiTextBox(string value)
        => new()
        {
            Text = value,
            Height = 28,
            Padding = new Thickness(7, 3, 7, 3),
            BorderBrush = HmiBrush(0xB8, 0xC2, 0xC8),
            Background = Brushes.White,
            Foreground = HmiBrush(0x25, 0x2D, 0x32),
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        };

    private static Button HmiActionButton(string text, bool primary)
        => new()
        {
            Content = text,
            Height = 31,
            Padding = new Thickness(9, 3, 9, 3),
            Background = primary ? HmiBrush(0x24, 0x6D, 0xA5) : HmiBrush(0xE8, 0xEC, 0xEF),
            Foreground = primary ? Brushes.White : HmiBrush(0x2A, 0x36, 0x3C),
            BorderBrush = primary ? HmiBrush(0x1F, 0x5D, 0x8D) : HmiBrush(0xB8, 0xC2, 0xC8),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold
        };

    private static string LocalIpv4Summary()
    {
        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToArray();
            return addresses.Length == 0 ? "not detected" : string.Join(", ", addresses);
        }
        catch (SocketException)
        {
            return "not detected";
        }
    }

    private static SolidColorBrush HmiBrush(byte r, byte g, byte b)
        => new(Color.FromRgb(r, g, b));
}