using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrP0HmiControl : UserControl
{
    private enum Page { Home, Measured, Control, Events, Network }

    private sealed record IpChoice(string Address, int PrefixLength)
    {
        public override string ToString() => $"{Address}/{PrefixLength}";
    }

    private sealed record NicChoice(
        string Id,
        string Name,
        string Description,
        NetworkInterfaceType Type,
        OperationalStatus Status,
        string Mac,
        string Gateway,
        IReadOnlyList<IpChoice> Addresses)
    {
        public override string ToString() => $"{Name} · {(Status == OperationalStatus.Up ? "UP" : Status.ToString().ToUpperInvariant())}";
    }

    private static readonly Page[] MenuPages = [Page.Measured, Page.Control, Page.Events, Page.Network];
    private readonly Queue<double> _trendHistory = new();
    private Page _page = Page.Home;
    private int _menuIndex;
    private int _trendDivider;
    private AvrSnapshot _snapshot = AvrSnapshot.Ready(new AvrSettings(), 0);
    private AvrSettings _settings = new();
    private AvrIec61850ServerStatus _serverStatus = new();
    private double _frequencyHz = 50;

    public AvrP0HmiControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PopulateAdapters();
            SetPage(Page.Home);
            RenderTapTrack();
            RenderTrend();
            RenderMenuSelection();
        };
    }

    public event Action<string, int>? StartServerRequested;
    public event Action? StopServerRequested;

    public void HandleHardwareKey(string key)
    {
        key = key.ToUpperInvariant();
        if (key == "MENU")
        {
            MenuOverlay.Visibility = Visibility.Visible;
            RenderMenuSelection();
            return;
        }

        if (key == "BACK")
        {
            if (MenuOverlay.Visibility == Visibility.Visible)
                MenuOverlay.Visibility = Visibility.Collapsed;
            else
                SetPage(Page.Home);
            return;
        }

        if (MenuOverlay.Visibility == Visibility.Visible)
        {
            if (key == "LEFT") _menuIndex = (_menuIndex - 1 + MenuPages.Length) % MenuPages.Length;
            else if (key == "RIGHT") _menuIndex = (_menuIndex + 1) % MenuPages.Length;
            else if (key == "ENTER")
            {
                MenuOverlay.Visibility = Visibility.Collapsed;
                SetPage(MenuPages[_menuIndex]);
            }
            RenderMenuSelection();
            return;
        }

        if (key is "LEFT" or "RIGHT")
        {
            var pages = new[] { Page.Home, Page.Measured, Page.Control, Page.Events, Page.Network };
            var index = Array.IndexOf(pages, _page);
            index = key == "RIGHT" ? (index + 1) % pages.Length : (index - 1 + pages.Length) % pages.Length;
            SetPage(pages[index]);
        }
        else if (key == "ENTER" && _page == Page.Home)
        {
            MenuOverlay.Visibility = Visibility.Visible;
            RenderMenuSelection();
        }
    }

    public void UpdateState(
        AvrSnapshot snapshot,
        AvrSettings settings,
        double frequencyHz,
        IReadOnlyCollection<string> eventLines,
        AvrIec61850ServerStatus serverStatus)
    {
        _snapshot = snapshot;
        _settings = settings;
        _frequencyHz = frequencyHz;
        _serverStatus = serverStatus;

        HomeAuthority.Text = snapshot.Authority == AvrControlAuthority.Local ? "LOCAL" : "REMOTE";
        HomeMode.Text = snapshot.Mode == AvrOperatingMode.Automatic ? "AUTO" : "MANUAL";
        HomeState.Text = StateText(snapshot.State).ToUpperInvariant();
        HomeClock.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        TapHero.Text = snapshot.TapPosition.ToString("+00;-00;00", CultureInfo.InvariantCulture);
        TapFeedback.Text = snapshot.PendingTapPosition is int pending ? $"TARGET {pending:+00;-00;00} · {snapshot.MotorTravelRemainingSeconds:0.0}s" : "FEEDBACK VALID";
        TapRange.Text = $"{settings.MinimumTap:+00;-00;00} … {settings.MaximumTap:+00;-00;00}";
        VoltageHero.Text = snapshot.SourceEnergized ? snapshot.MeasuredVoltageV.ToString("0.00", CultureInfo.InvariantCulture) : "0.00";
        HomeSetpoint.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V";
        HomeDeviation.Text = snapshot.SourceEnergized ? $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %" : "—";
        HomeCurrent.Text = snapshot.SourceEnergized ? $"{snapshot.SourceCurrentA:0.000} A" : "0.000 A";
        HomePf.Text = snapshot.SourceEnergized && snapshot.SourceCurrentA > 1e-9 ? snapshot.PowerFactor.ToString("0.000", CultureInfo.InvariantCulture) : "—";
        HomeFreq.Text = snapshot.SourceEnergized ? $"{frequencyHz:0.00} Hz" : "— Hz";
        HomeBand.Text = $"{snapshot.LowerBandVoltageV:0.00} — {snapshot.UpperBandVoltageV:0.00} V";
        HomeOutput.Text = snapshot.RaiseOutput ? "RAISE CONTACT" : snapshot.LowerOutput ? "LOWER CONTACT" : snapshot.TapMoving ? "MOTOR IN TRANSIT" : snapshot.Blocked ? "BLOCKED" : "IDLE";
        CommandCount.Text = $"CMDS {snapshot.OperationCount}";
        TrendState.Text = snapshot.SourceEnergized ? StateText(snapshot.State).ToUpperInvariant() : "SOURCE OFF";
        TimerText.Text = snapshot.DelayTargetSeconds > 0 ? $"{snapshot.DelayElapsedSeconds:0.0}/{snapshot.DelayTargetSeconds:0.0} s" : "—";
        TimerBar.Value = snapshot.DelayTargetSeconds > 0 ? Math.Clamp(snapshot.DelayElapsedSeconds / snapshot.DelayTargetSeconds * 100.0, 0, 100) : 0;
        MotorText.Text = snapshot.TapMoving ? $"MOVING · {snapshot.MotorTravelRemainingSeconds:0.0}s" : "READY";
        MotorBar.Value = snapshot.TapMoving && settings.MotorDriveTravelSeconds > 0 ? Math.Clamp((1 - snapshot.MotorTravelRemainingSeconds / settings.MotorDriveTravelSeconds) * 100.0, 0, 100) : 0;
        FooterAuthority.Text = HomeAuthority.Text;
        FooterMode.Text = HomeMode.Text;
        FooterReason.Text = snapshot.Reason;
        BottomStatus.Text = snapshot.SourceEnergized ? $"{HomeAuthority.Text} · {HomeMode.Text} · {StateText(snapshot.State).ToUpperInvariant()}" : "SOURCE OFF";

        MeasuredU.Text = snapshot.SourceEnergized ? $"{snapshot.MeasuredVoltageV:0.00} V" : "0.00 V";
        MeasuredI.Text = snapshot.SourceEnergized ? $"{snapshot.SourceCurrentA:0.000} A" : "0.000 A";
        MeasuredTap.Text = snapshot.TapPosition.ToString("+00;-00;00", CultureInfo.InvariantCulture);
        MeasuredDev.Text = snapshot.SourceEnergized ? $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %" : "—";
        MeasuredPf.Text = snapshot.SourceEnergized && snapshot.SourceCurrentA > 1e-9 ? $"PF {snapshot.PowerFactor:0.000} · φ {snapshot.CurrentAngleDegrees:+0.0;-0.0;0.0}°" : "—";
        MeasuredF.Text = snapshot.SourceEnergized ? $"{frequencyHz:0.00} Hz" : "— Hz";

        ControlSetBand.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V · ±{settings.TolerancePercent:0.00} %";
        ControlTimers.Text = $"T1 {settings.T1Seconds:0.0}s · T2 {settings.T2Seconds:0.0}s";
        ControlAuthority.Text = $"{HomeAuthority.Text} · {HomeMode.Text}";
        ControlTapRange.Text = $"{settings.MinimumTap:+00;-00;00} … {settings.MaximumTap:+00;-00;00} · {settings.TapStepPercent:0.###}%";
        ControlBlocking.Text = $"U {settings.UndervoltageBlockPercent:0.#}…{settings.OvervoltageBlockPercent:0.#}%\nI< {(settings.UndercurrentBlockingEnabled ? settings.UndercurrentBlockPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "OFF")} · I> {(settings.OvercurrentBlockingEnabled ? settings.OvercurrentBlockPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "OFF")}";
        EventsText.Text = eventLines.Count == 0 ? "No events" : string.Join(Environment.NewLine, eventLines.Reverse());

        UpdateNetworkStatus();
        UpdateHistory();
        RenderTapTrack();
        RenderTrend();
    }

    private void TopTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender == HomeTab) SetPage(Page.Home);
        else if (sender == MeasuredTab) SetPage(Page.Measured);
        else if (sender == ControlTab) SetPage(Page.Control);
        else if (sender == EventsTab) SetPage(Page.Events);
        else if (sender == NetworkTab) SetPage(Page.Network);
    }

    private void SetPage(Page page)
    {
        _page = page;
        HomePage.Visibility = page == Page.Home ? Visibility.Visible : Visibility.Collapsed;
        MeasuredPage.Visibility = page == Page.Measured ? Visibility.Visible : Visibility.Collapsed;
        ControlPage.Visibility = page == Page.Control ? Visibility.Visible : Visibility.Collapsed;
        EventsPage.Visibility = page == Page.Events ? Visibility.Visible : Visibility.Collapsed;
        NetworkPage.Visibility = page == Page.Network ? Visibility.Visible : Visibility.Collapsed;
        HomeTab.Tag = page == Page.Home ? "ACTIVE" : null;
        MeasuredTab.Tag = page == Page.Measured ? "ACTIVE" : null;
        ControlTab.Tag = page == Page.Control ? "ACTIVE" : null;
        EventsTab.Tag = page == Page.Events ? "ACTIVE" : null;
        NetworkTab.Tag = page == Page.Network ? "ACTIVE" : null;
        if (page == Page.Network) UpdateNetworkStatus();
    }

    private void RenderMenuSelection()
    {
        var borders = new[] { MenuMeasured, MenuControl, MenuEvents, MenuNetwork };
        for (var i = 0; i < borders.Length; i++)
        {
            borders[i].Background = i == _menuIndex ? Brush("#DCECF7") : Brushes.White;
            borders[i].BorderBrush = i == _menuIndex ? Brush("#2D78B0") : Brush("#D5DBDE");
            borders[i].BorderThickness = new Thickness(i == _menuIndex ? 2 : 1);
        }
    }

    private void UpdateHistory()
    {
        if (!_snapshot.SourceEnergized)
        {
            if (_trendHistory.Count > 0) _trendHistory.Clear();
            _trendDivider = 0;
            return;
        }
        _trendDivider++;
        if (_trendDivider % 3 != 0) return;
        _trendHistory.Enqueue(_snapshot.MeasuredVoltageV);
        while (_trendHistory.Count > 90) _trendHistory.Dequeue();
    }

    private void CompactTrendCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderTrend();
    private void TapTrackCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderTapTrack();

    private void RenderTapTrack()
    {
        if (!IsLoaded || TapTrackCanvas.ActualWidth <= 1) return;
        var width = TapTrackCanvas.ActualWidth;
        TapTrackLine.X1 = 4;
        TapTrackLine.X2 = Math.Max(4, width - 4);
        var span = Math.Max(1, _settings.MaximumTap - _settings.MinimumTap);
        var ratio = Math.Clamp((_snapshot.TapPosition - _settings.MinimumTap) / (double)span, 0, 1);
        var x = 4 + ratio * Math.Max(1, width - 8);
        Canvas.SetLeft(TapTrackDot, Math.Clamp(x - 5, 0, Math.Max(0, width - 10)));
        Canvas.SetTop(TapTrackDot, 6);
    }

    private void RenderTrend()
    {
        if (!IsLoaded || CompactTrendCanvas.ActualWidth <= 1 || CompactTrendCanvas.ActualHeight <= 1) return;
        var w = CompactTrendCanvas.ActualWidth;
        var h = CompactTrendCanvas.ActualHeight;
        var setpoint = _snapshot.EffectiveSetpointVoltageV > 0 ? _snapshot.EffectiveSetpointVoltageV : _settings.SetpointVoltageV;
        var lower = _snapshot.LowerBandVoltageV > 0 ? _snapshot.LowerBandVoltageV : setpoint * (1 - _settings.TolerancePercent / 100.0);
        var upper = _snapshot.UpperBandVoltageV > 0 ? _snapshot.UpperBandVoltageV : setpoint * (1 + _settings.TolerancePercent / 100.0);
        var values = _trendHistory.ToArray();
        var min = Math.Min(lower - 0.5, values.Length > 0 ? values.Min() - 0.5 : lower - 0.5);
        var max = Math.Max(upper + 0.5, values.Length > 0 ? values.Max() + 0.5 : upper + 0.5);
        if (max - min < 0.1) max = min + 0.1;
        double Y(double v) => Math.Clamp(h - ((v - min) / (max - min) * h), 0, h);
        SetLine(TrendUpper, w, Y(upper));
        SetLine(TrendSet, w, Y(setpoint));
        SetLine(TrendLower, w, Y(lower));
        TrendScale.Text = $"{lower:0.0}     {setpoint:0.0}     {upper:0.0} V";
        var points = new PointCollection();
        if (values.Length > 0)
        {
            var d = Math.Max(1, values.Length - 1);
            for (var i = 0; i < values.Length; i++) points.Add(new Point(i / (double)d * w, Y(values[i])));
            var p = points[^1];
            Canvas.SetLeft(TrendDot, Math.Clamp(p.X - 3.5, 0, Math.Max(0, w - 7)));
            Canvas.SetTop(TrendDot, Math.Clamp(p.Y - 3.5, 0, Math.Max(0, h - 7)));
            TrendDot.Visibility = Visibility.Visible;
        }
        else TrendDot.Visibility = Visibility.Collapsed;
        TrendPath.Points = points;
    }

    private static void SetLine(System.Windows.Shapes.Line line, double width, double y)
    {
        line.X1 = 0; line.X2 = width; line.Y1 = y; line.Y2 = y;
    }

    private void PopulateAdapters()
    {
        var choices = new List<NicChoice>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var props = nic.GetIPProperties();
                var ips = props.UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address))
                    .Select(x => new IpChoice(x.Address.ToString(), x.PrefixLength))
                    .Where(x => !x.Address.StartsWith("169.254.", StringComparison.Ordinal))
                    .ToArray();
                if (ips.Length == 0) continue;
                var gateway = props.GatewayAddresses.Select(x => x.Address).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !x.Equals(IPAddress.Any))?.ToString() ?? "—";
                var rawMac = nic.GetPhysicalAddress().ToString();
                var mac = rawMac.Length >= 12 ? string.Join("-", Enumerable.Range(0, rawMac.Length / 2).Select(i => rawMac.Substring(i * 2, 2))) : rawMac;
                choices.Add(new NicChoice(nic.Id, nic.Name, nic.Description, nic.NetworkInterfaceType, nic.OperationalStatus, mac, gateway, ips));
            }
            catch (NetworkInformationException) { }
        }

        choices = choices.OrderByDescending(x => x.Status == OperationalStatus.Up)
            .ThenBy(x => x.Type == NetworkInterfaceType.Ethernet ? 0 : x.Type == NetworkInterfaceType.Wireless80211 ? 1 : 2)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AdapterCombo.ItemsSource = choices;
        AdapterCombo.SelectedItem = choices.FirstOrDefault(x => x.Status == OperationalStatus.Up && x.Type == NetworkInterfaceType.Ethernet)
            ?? choices.FirstOrDefault(x => x.Status == OperationalStatus.Up)
            ?? choices.FirstOrDefault();
    }

    private void AdapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AdapterCombo.SelectedItem is not NicChoice nic)
        {
            Ipv4Combo.ItemsSource = null;
            UpdateNetworkStatus();
            return;
        }
        Ipv4Combo.ItemsSource = nic.Addresses;
        Ipv4Combo.SelectedIndex = nic.Addresses.Count > 0 ? 0 : -1;
        UpdateNetworkStatus();
    }

    private void Ipv4Combo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateNetworkStatus();
    private void NetworkSelection_Changed(object sender, RoutedEventArgs e) => UpdateNetworkStatus();

    private void StartServerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show("IEC 61850 TCP port must be between 1 and 65535.", "AVR IEC 61850", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var host = SelectedBindAddress();
        StartServerRequested?.Invoke(host, port);
    }

    private void StopServerButton_Click(object sender, RoutedEventArgs e) => StopServerRequested?.Invoke();

    private string SelectedBindAddress()
    {
        if (AllInterfacesCheck.IsChecked == true) return "0.0.0.0";
        return (Ipv4Combo.SelectedItem as IpChoice)?.Address ?? "0.0.0.0";
    }

    private void UpdateNetworkStatus()
    {
        if (!IsLoaded) return;
        var nic = AdapterCombo.SelectedItem as NicChoice;
        var ip = Ipv4Combo.SelectedItem as IpChoice;
        var listenAll = AllInterfacesCheck.IsChecked == true;
        var port = int.TryParse(PortBox.Text, out var p) ? p : 102;
        var bind = listenAll ? "0.0.0.0" : ip?.Address ?? "0.0.0.0";
        var clientIp = ip?.Address ?? nic?.Addresses.FirstOrDefault()?.Address ?? "<this-PC-IPv4>";

        NetServerStatus.Text = !_serverStatus.EngineAvailable ? "ENGINE UNAVAILABLE" : _serverStatus.IsRunning ? "RUNNING" : "STOPPED";
        NetServerStatus.Foreground = _serverStatus.IsRunning ? Brush("#2C8B50") : Brush("#365B70");
        NetServerEndpoint.Text = _serverStatus.IsRunning ? _serverStatus.Endpoint : $"Bind {bind}:{port}";
        NetCounters.Text = $"Active {_serverStatus.ActiveConnections} · Accepted {_serverStatus.AcceptedConnections}\nRequests {_serverStatus.ServedRequests} · Reports {_serverStatus.ReportsSent}\nControls {_serverStatus.AcceptedControls}/{_serverStatus.RejectedControls}";
        NetLastActivity.Text = _serverStatus.LastActivity;
        NetClientEndpoint.Text = $"{clientIp}:{port}";
        NetAdapterDetails.Text = nic is null ? "No active IPv4 adapter detected." : $"{nic.Description}\nLink {(nic.Status == OperationalStatus.Up ? "UP" : nic.Status.ToString().ToUpperInvariant())} · {nic.Type}\nMAC {nic.Mac}\nIPv4 {(ip is null ? "—" : ip)} · Gateway {nic.Gateway}";
        NetDiagnostics.Text = _serverStatus.IsRunning ? $"Listening · use {clientIp}:{_serverStatus.Port} from IEDScout/SAS" : "Select the commissioning LAN adapter, then START SERVER.";

        var locked = _serverStatus.IsRunning;
        AdapterCombo.IsEnabled = !locked && !listenAll;
        Ipv4Combo.IsEnabled = !locked && !listenAll;
        AllInterfacesCheck.IsEnabled = !locked;
        PortBox.IsEnabled = !locked;
        StartServerButton.IsEnabled = !locked && _serverStatus.EngineAvailable;
        StopServerButton.IsEnabled = locked;
    }

    private static string StateText(AvrControlState state) => state switch
    {
        AvrControlState.SourceOff => "Source off",
        AvrControlState.InBand => "In band",
        AvrControlState.TimingRaise => "Timing raise",
        AvrControlState.TimingLower => "Timing lower",
        AvrControlState.TapMoving => "Tap moving",
        AvrControlState.TapLimit => "Tap limit",
        AvrControlState.Blocked => "Blocked",
        AvrControlState.Manual => "Manual",
        _ => state.ToString()
    };

    private static Brush Brush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}