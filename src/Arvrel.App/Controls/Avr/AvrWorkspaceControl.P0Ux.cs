using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private sealed record P0Ipv4Choice(string Address, int PrefixLength)
    {
        public override string ToString() => $"{Address}/{PrefixLength}";
    }

    private sealed record P0NetworkAdapterChoice(
        string Id,
        string Name,
        string Description,
        NetworkInterfaceType Type,
        OperationalStatus Status,
        string MacAddress,
        string Gateway,
        IReadOnlyList<P0Ipv4Choice> Addresses)
    {
        public override string ToString()
        {
            var link = Status == OperationalStatus.Up ? "UP" : Status.ToString().ToUpperInvariant();
            return $"{Name} · {link}";
        }
    }

    private bool _p0UxLoaded;
    private bool? _p0LastConfigExpanded;
    private ComboBox? _p0AdapterCombo;
    private ComboBox? _p0Ipv4Combo;
    private CheckBox? _p0AllInterfacesCheck;
    private TextBlock? _p0NetworkDetailText;
    private TextBlock? _p0NetworkEndpointText;
    private TextBlock? _p0NetworkDiagnosticsText;
    private string? _p0SelectedAdapterId;
    private string? _p0SelectedIpv4;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += P0Ux_Loaded;
        Unloaded += P0Ux_Unloaded;
    }

    private void P0Ux_Loaded(object sender, RoutedEventArgs e)
    {
        if (_p0UxLoaded)
            return;

        _p0UxLoaded = true;
        _timer.Tick += P0Ux_Tick;

        // Run after the original Loaded/HMI wiring so the P0 deterministic key
        // handlers become the single owner of ENTER/LEFT/RIGHT/BACK/MENU.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            RewireP0HardwareNavigation();
            ApplyP0HomeLayout(force: true);
            RenameSystemTabToNetwork();
            EnsureP0SystemNetworkCard();
            RefreshP0NetworkDiagnostics();
        }));
    }

    private void P0Ux_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_p0UxLoaded)
            return;

        _timer.Tick -= P0Ux_Tick;
        _p0UxLoaded = false;
    }

    private void P0Ux_Tick(object? sender, EventArgs e)
    {
        ApplyP0HomeLayout(force: false);
        ReplaceStaleHmiCapabilityCopy();

        if (_hmiPage == HmiPage.System)
        {
            EnsureP0SystemNetworkCard();
            RefreshP0NetworkDiagnostics();
        }
    }

    private void RewireP0HardwareNavigation()
    {
        foreach (var button in FindVisualChildren<Button>(this))
        {
            var key = button.Tag?.ToString() ?? button.Content?.ToString();
            if (key is not ("ENTER" or "LEFT" or "RIGHT" or "BACK" or "MENU") &&
                button.Content?.ToString() is not ("ENTER" or "◀" or "▶" or "BACK" or "MENU"))
                continue;

            button.Click -= DeviceFunction_Click;
            button.Click -= HmiHardwareNavigation_Click;
            button.Click -= P0HardwareNavigation_Click;
            button.Click += P0HardwareNavigation_Click;
        }
    }

    private void P0HardwareNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var key = (button.Tag?.ToString() ?? button.Content?.ToString() ?? string.Empty).ToUpperInvariant();
        AddEvent("KEY", $"Front-panel {key} pressed");

        switch (key)
        {
            case "MENU":
                _hmiReturnToMenu = false;
                NavigateHmi(HmiPage.Menu);
                break;
            case "BACK":
                if (_hmiPage == HmiPage.Menu)
                {
                    _hmiReturnToMenu = false;
                    NavigateHmi(HmiPage.Home);
                }
                else if (_hmiReturnToMenu)
                {
                    _hmiReturnToMenu = false;
                    NavigateHmi(HmiPage.Menu);
                }
                else
                {
                    NavigateHmi(HmiPage.Home);
                }
                break;
            case "LEFT":
                MoveHmiSelection(-1);
                break;
            case "RIGHT":
                MoveHmiSelection(+1);
                break;
            case "ENTER":
                if (_hmiPage == HmiPage.Menu)
                {
                    _hmiReturnToMenu = true;
                    NavigateHmi(HmiMenuPages[_hmiMenuIndex]);
                }
                else if (_hmiPage == HmiPage.Home)
                {
                    NavigateHmi(HmiPage.Menu);
                }
                break;
        }

        EnsureP0SystemNetworkCard();
        e.Handled = true;
    }

    private void RenameSystemTabToNetwork()
    {
        if (!_hmiHeaderTabs.TryGetValue(HmiPage.System, out var tab))
            return;

        if (tab.Child is TextBlock text)
            text.Text = "Network";
    }

    private void ApplyP0HomeLayout(bool force)
    {
        if (!force && _p0LastConfigExpanded == _configExpanded)
            return;

        _p0LastConfigExpanded = _configExpanded;

        // Home hierarchy: the measured/tap side must win space over the trend.
        // The legacy design was 0.95* : 1.35* and made tap position unreadable
        // whenever the configuration rail was open.
        var split = FindHomeSplitGrid();
        if (split is not null && split.ColumnDefinitions.Count >= 3)
        {
            split.ColumnDefinitions[0].Width = new GridLength(_configExpanded ? 1.42 : 1.26, GridUnitType.Star);
            split.ColumnDefinitions[1].Width = new GridLength(_configExpanded ? 8 : 10);
            split.ColumnDefinitions[2].Width = new GridLength(_configExpanded ? 0.72 : 0.92, GridUnitType.Star);
        }

        LcdTapText.FontSize = _configExpanded ? 31 : 35;
        LcdTapText.MinWidth = _configExpanded ? 72 : 92;
        LcdTapText.HorizontalAlignment = HorizontalAlignment.Center;
        if (LcdTapText.Parent is Border tapChrome)
        {
            tapChrome.MinWidth = _configExpanded ? 82 : 104;
            tapChrome.MinHeight = _configExpanded ? 43 : 49;
            tapChrome.Padding = new Thickness(7, 2, 7, 2);
        }

        LcdPendingTapText.MaxWidth = _configExpanded ? 62 : 82;
        LcdPendingTapText.FontSize = _configExpanded ? 7.1 : 7.8;
        LcdTapRangeText.FontSize = _configExpanded ? 7.1 : 7.8;
        LcdMeasuredText.FontSize = _configExpanded ? 22 : 25;

        // The graph remains useful as context, but no longer owns the page.
        TrendCanvas.MinHeight = _configExpanded ? 125 : 145;
        TrendCanvas.MaxHeight = _configExpanded ? 175 : 215;
    }

    private Grid? FindHomeSplitGrid()
    {
        DependencyObject? current = LcdTapText;
        while (current is not null)
        {
            if (current is Grid grid && grid.ColumnDefinitions.Count == 3 && IsDescendantOf(TrendCanvas, grid))
                return grid;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void ReplaceStaleHmiCapabilityCopy()
    {
        if (_hmiPageContent is null)
            return;

        foreach (var text in FindVisualChildren<TextBlock>(_hmiPageContent))
        {
            if (text.Text.Contains("Remote commands are intentionally not mapped", StringComparison.OrdinalIgnoreCase))
            {
                text.Text = "IEC 61850 control: SBO enhanced · REMOTE authority required · AUTO/MANUAL, LTC block, RAISE/LOWER/STOP and selected AVR setting writes are available for SAS commissioning.";
            }
            else if (text.Text.Contains("Process controls and setting writes remain blocked", StringComparison.OrdinalIgnoreCase))
            {
                text.Text = "SAS client: use the selected IPv4 endpoint on TCP/102. MMS browse/read, DataSets, BRCB/URCB, GI/integrity reporting and AVR process controls are available when the matching ARIEC61850 process-control runtime is loaded.";
            }
        }
    }

    private void EnsureP0SystemNetworkCard()
    {
        if (_hmiPage != HmiPage.System || _hmiPageContent is null)
            return;

        RenameSystemTabToNetwork();

        if (_hmiPageContent.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "P0_NETWORK_BINDING")))
            return;

        // Hide the old free-form bind-address editor. It remains alive underneath
        // because the existing Start handler consumes _iecHostBox; the adapter/IP
        // selector below keeps that textbox synchronized.
        if (_iecHostBox?.Parent is FrameworkElement hostPanel && hostPanel.Parent is FrameworkElement oldConfig)
            oldConfig.Visibility = Visibility.Collapsed;

        var card = new Border
        {
            Tag = "P0_NETWORK_BINDING",
            Background = Brushes.White,
            BorderBrush = P0Brush(0xD3, 0xD8, 0xDC),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var root = new StackPanel();
        root.Children.Add(new TextBlock
        {
            Text = "NETWORK BINDING",
            Foreground = P0Brush(0x2B, 0x36, 0x3C),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        });
        root.Children.Add(new TextBlock
        {
            Text = "Choose the physical interface/IP that IEDScout or the SAS will connect to.",
            Foreground = P0Brush(0x6E, 0x7C, 0x84),
            FontSize = 8.8,
            Margin = new Thickness(0, 2, 0, 9)
        });

        _p0AllInterfacesCheck = new CheckBox
        {
            Content = "Listen on all interfaces (0.0.0.0)",
            Foreground = P0Brush(0x38, 0x47, 0x4E),
            FontSize = 9.5,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _p0AllInterfacesCheck.Checked += P0NetworkSelection_Changed;
        _p0AllInterfacesCheck.Unchecked += P0NetworkSelection_Changed;
        root.Children.Add(_p0AllInterfacesCheck);

        var selectors = new Grid();
        selectors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        selectors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        selectors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var adapterPanel = new StackPanel();
        adapterPanel.Children.Add(P0SmallLabel("NETWORK ADAPTER"));
        _p0AdapterCombo = new ComboBox { Height = 28, FontSize = 9.2 };
        _p0AdapterCombo.SelectionChanged += P0Adapter_SelectionChanged;
        adapterPanel.Children.Add(_p0AdapterCombo);
        selectors.Children.Add(adapterPanel);

        var ipPanel = new StackPanel();
        Grid.SetColumn(ipPanel, 2);
        ipPanel.Children.Add(P0SmallLabel("IPv4 ADDRESS"));
        _p0Ipv4Combo = new ComboBox { Height = 28, FontSize = 9.2 };
        _p0Ipv4Combo.SelectionChanged += P0Ipv4_SelectionChanged;
        ipPanel.Children.Add(_p0Ipv4Combo);
        selectors.Children.Add(ipPanel);
        root.Children.Add(selectors);

        var details = new Grid { Margin = new Thickness(0, 9, 0, 0) };
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _p0NetworkDetailText = new TextBlock
        {
            Foreground = P0Brush(0x5D, 0x6B, 0x72),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.2,
            TextWrapping = TextWrapping.Wrap
        };
        details.Children.Add(_p0NetworkDetailText);
        _p0NetworkEndpointText = new TextBlock
        {
            Foreground = P0Brush(0x24, 0x5D, 0x9A),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 9,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right
        };
        Grid.SetColumn(_p0NetworkEndpointText, 2);
        details.Children.Add(_p0NetworkEndpointText);
        root.Children.Add(details);

        var diagnosticGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        diagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        diagnosticGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        _p0NetworkDiagnosticsText = new TextBlock
        {
            Foreground = P0Brush(0x3F, 0x50, 0x58),
            FontSize = 8.7,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        diagnosticGrid.Children.Add(_p0NetworkDiagnosticsText);
        var refresh = new Button
        {
            Content = "Refresh adapters",
            Height = 27,
            Padding = new Thickness(7, 2, 7, 2),
            FontSize = 8.8
        };
        refresh.Click += (_, _) => PopulateP0NetworkAdapters(forceReselect: false);
        Grid.SetColumn(refresh, 2);
        diagnosticGrid.Children.Add(refresh);
        root.Children.Add(diagnosticGrid);

        card.Child = root;
        var insertAt = Math.Min(2, _hmiPageContent.Children.Count);
        _hmiPageContent.Children.Insert(insertAt, card);
        PopulateP0NetworkAdapters(forceReselect: false);
        ReplaceStaleHmiCapabilityCopy();
    }

    private void PopulateP0NetworkAdapters(bool forceReselect)
    {
        if (_p0AdapterCombo is null)
            return;

        var previousId = forceReselect ? null : (_p0AdapterCombo.SelectedItem as P0NetworkAdapterChoice)?.Id ?? _p0SelectedAdapterId;
        var adapters = DiscoverP0NetworkAdapters();
        _p0AdapterCombo.ItemsSource = adapters;

        var selected = adapters.FirstOrDefault(x => string.Equals(x.Id, previousId, StringComparison.OrdinalIgnoreCase))
            ?? adapters.FirstOrDefault(x => x.Status == OperationalStatus.Up && x.Type == NetworkInterfaceType.Ethernet)
            ?? adapters.FirstOrDefault(x => x.Status == OperationalStatus.Up && x.Type == NetworkInterfaceType.Wireless80211)
            ?? adapters.FirstOrDefault(x => x.Status == OperationalStatus.Up)
            ?? adapters.FirstOrDefault();

        _p0AdapterCombo.SelectedItem = selected;
        if (selected is not null)
        {
            _p0SelectedAdapterId = selected.Id;
            PopulateP0Ipv4Addresses(selected);
        }
        else
        {
            _p0Ipv4Combo!.ItemsSource = null;
            _p0SelectedIpv4 = null;
            P0ApplySelectedNetworkBinding();
        }
    }

    private static List<P0NetworkAdapterChoice> DiscoverP0NetworkAdapters()
    {
        var result = new List<P0NetworkAdapterChoice>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            try
            {
                var properties = nic.GetIPProperties();
                var addresses = properties.UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address))
                    .Select(x => new P0Ipv4Choice(x.Address.ToString(), x.PrefixLength))
                    .Where(x => !x.Address.StartsWith("169.254.", StringComparison.Ordinal))
                    .ToArray();

                if (addresses.Length == 0)
                    continue;

                var gateway = properties.GatewayAddresses
                    .Select(x => x.Address)
                    .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(x))
                    ?.ToString() ?? "—";
                var macRaw = nic.GetPhysicalAddress().ToString();
                var mac = macRaw.Length >= 12
                    ? string.Join("-", Enumerable.Range(0, macRaw.Length / 2).Select(i => macRaw.Substring(i * 2, 2)))
                    : macRaw;

                result.Add(new P0NetworkAdapterChoice(
                    nic.Id,
                    nic.Name,
                    nic.Description,
                    nic.NetworkInterfaceType,
                    nic.OperationalStatus,
                    string.IsNullOrWhiteSpace(mac) ? "—" : mac,
                    gateway,
                    addresses));
            }
            catch (NetworkInformationException)
            {
                // Ignore adapters that disappear while Windows is refreshing the stack.
            }
        }

        return result
            .OrderByDescending(x => x.Status == OperationalStatus.Up)
            .ThenBy(x => x.Type == NetworkInterfaceType.Ethernet ? 0 : x.Type == NetworkInterfaceType.Wireless80211 ? 1 : 2)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void P0Adapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_p0AdapterCombo?.SelectedItem is not P0NetworkAdapterChoice adapter)
            return;

        _p0SelectedAdapterId = adapter.Id;
        PopulateP0Ipv4Addresses(adapter);
    }

    private void PopulateP0Ipv4Addresses(P0NetworkAdapterChoice adapter)
    {
        if (_p0Ipv4Combo is null)
            return;

        _p0Ipv4Combo.ItemsSource = adapter.Addresses;
        var selected = adapter.Addresses.FirstOrDefault(x => string.Equals(x.Address, _p0SelectedIpv4, StringComparison.OrdinalIgnoreCase))
            ?? adapter.Addresses.FirstOrDefault();
        _p0Ipv4Combo.SelectedItem = selected;
        _p0SelectedIpv4 = selected?.Address;
        P0ApplySelectedNetworkBinding();
    }

    private void P0Ipv4_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_p0Ipv4Combo?.SelectedItem is P0Ipv4Choice ip)
            _p0SelectedIpv4 = ip.Address;
        P0ApplySelectedNetworkBinding();
    }

    private void P0NetworkSelection_Changed(object sender, RoutedEventArgs e)
        => P0ApplySelectedNetworkBinding();

    private void P0ApplySelectedNetworkBinding()
    {
        var listenAll = _p0AllInterfacesCheck?.IsChecked == true;
        var ip = (_p0Ipv4Combo?.SelectedItem as P0Ipv4Choice)?.Address ?? _p0SelectedIpv4;
        var bind = listenAll ? "0.0.0.0" : string.IsNullOrWhiteSpace(ip) ? "0.0.0.0" : ip;
        if (_iecHostBox is not null)
            _iecHostBox.Text = bind;
        RefreshP0NetworkDiagnostics();
    }

    private void RefreshP0NetworkDiagnostics()
    {
        if (_p0NetworkDetailText is null || _p0NetworkEndpointText is null || _p0NetworkDiagnosticsText is null)
            return;

        var adapter = _p0AdapterCombo?.SelectedItem as P0NetworkAdapterChoice;
        var ip = _p0Ipv4Combo?.SelectedItem as P0Ipv4Choice;
        var listenAll = _p0AllInterfacesCheck?.IsChecked == true;
        var bindAddress = listenAll ? "0.0.0.0" : ip?.Address ?? "0.0.0.0";
        var port = int.TryParse(_iecPortBox?.Text, out var parsedPort) ? parsedPort : 102;
        var status = _iec61850Server.GetStatus();

        if (adapter is null)
        {
            _p0NetworkDetailText.Text = "No active IPv4 adapter detected.";
        }
        else
        {
            var link = adapter.Status == OperationalStatus.Up ? "UP" : adapter.Status.ToString().ToUpperInvariant();
            _p0NetworkDetailText.Text =
                $"{adapter.Description}\nLink {link} · {adapter.Type} · MAC {adapter.MacAddress}\nIPv4 {(ip is null ? "—" : ip)} · Gateway {adapter.Gateway}";
        }

        var clientEndpoint = listenAll
            ? (ip?.Address ?? adapter?.Addresses.FirstOrDefault()?.Address ?? "<this-PC-IPv4>")
            : bindAddress;
        _p0NetworkEndpointText.Text = $"SAS / IEDScout endpoint\n{clientEndpoint}:{port}";

        var engineState = status.EngineAvailable ? "engine ready" : "engine unavailable";
        var serverState = status.IsRunning ? "RUNNING" : "STOPPED";
        _p0NetworkDiagnosticsText.Text =
            $"Server {serverState} · {engineState} · active clients {status.ActiveConnections} · requests {status.ServedRequests} · reports {status.ReportsSent} · controls {status.AcceptedControls}/{status.RejectedControls}";

        var lockBinding = status.IsRunning;
        if (_p0AdapterCombo is not null) _p0AdapterCombo.IsEnabled = !lockBinding && !listenAll;
        if (_p0Ipv4Combo is not null) _p0Ipv4Combo.IsEnabled = !lockBinding && !listenAll;
        if (_p0AllInterfacesCheck is not null) _p0AllInterfacesCheck.IsEnabled = !lockBinding;
    }

    private static TextBlock P0SmallLabel(string text) => new()
    {
        Text = text,
        Foreground = P0Brush(0x6E, 0x7C, 0x84),
        FontSize = 8.2,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 3)
    };

    private static Brush P0Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
