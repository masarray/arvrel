using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl
{
    private sealed record ConfigIpChoice(string Address, int PrefixLength)
    {
        public override string ToString() => $"{Address}/{PrefixLength}";
    }

    private sealed record ConfigNicChoice(
        string Name,
        string Description,
        OperationalStatus Status,
        string Mac,
        string Gateway,
        IReadOnlyList<ConfigIpChoice> Addresses)
    {
        public override string ToString() => $"{Name} · {(Status == OperationalStatus.Up ? "UP" : Status.ToString().ToUpperInvariant())}";
    }

    private ComboBox? _configNicCombo;
    private ComboBox? _configIpCombo;
    private TextBox? _configPortBox;
    private CheckBox? _configAllInterfaces;
    private TextBlock? _configEndpointText;
    private TextBlock? _configNicDetails;
    private TextBlock? _configServerStatus;
    private TextBlock? _configServerCounters;
    private Button? _configStartServerButton;
    private Button? _configStopServerButton;
    private bool _configNetworkInstalled;

    private void InstallNativeConfigurationNetworkTab()
    {
        if (_configNetworkInstalled || ConfigurationTabs is null)
            return;

        var tab = new TabItem { Header = "Network" };
        if (TryFindResource("ConfigTab") is Style configTabStyle)
            tab.Style = configTabStyle;

        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(Label("IEC 61850 / MMS", 10, true));
        root.Children.Add(Label("Full communication configuration for IEDScout / SAS commissioning.", 8.5, false, "#8FA7B8", new Thickness(0, 3, 0, 12)));

        root.Children.Add(Label("NETWORK ADAPTER", 9, true, "#8FA7B8"));
        _configNicCombo = new ComboBox { Height = 30, Margin = new Thickness(0, 4, 0, 9), FontSize = 9 };
        _configNicCombo.SelectionChanged += ConfigNicCombo_SelectionChanged;
        root.Children.Add(_configNicCombo);

        root.Children.Add(Label("IPv4 ADDRESS", 9, true, "#8FA7B8"));
        _configIpCombo = new ComboBox { Height = 30, Margin = new Thickness(0, 4, 0, 9), FontSize = 9 };
        _configIpCombo.SelectionChanged += (_, _) => RefreshConfigurationNetworkStatus();
        root.Children.Add(_configIpCombo);

        var portGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        portGrid.ColumnDefinitions.Add(new ColumnDefinition());
        portGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        portGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        portGrid.Children.Add(Label("MMS TCP PORT", 9, true, "#8FA7B8", new Thickness(0, 6, 0, 0)));
        _configPortBox = new TextBox
        {
            Text = "102",
            Height = 29,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = Brush("#0E1B26"),
            Foreground = Brush("#E9F1F6"),
            BorderBrush = Brush("#314657")
        };
        Grid.SetColumn(_configPortBox, 2);
        portGrid.Children.Add(_configPortBox);
        root.Children.Add(portGrid);

        _configAllInterfaces = new CheckBox
        {
            Content = "Listen on all interfaces (0.0.0.0)",
            Foreground = Brush("#E9F1F6"),
            FontSize = 9,
            Margin = new Thickness(0, 2, 0, 12)
        };
        _configAllInterfaces.Checked += (_, _) => RefreshConfigurationNetworkStatus();
        _configAllInterfaces.Unchecked += (_, _) => RefreshConfigurationNetworkStatus();
        root.Children.Add(_configAllInterfaces);

        var endpointCard = new Border
        {
            Background = Brush("#0E1B26"),
            BorderBrush = Brush("#314657"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var endpointStack = new StackPanel();
        endpointStack.Children.Add(Label("SAS / IEDSCOUT ENDPOINT", 8.5, true, "#8FA7B8"));
        _configEndpointText = Label("<select IPv4>:102", 18, true, "#63BCE8", new Thickness(0, 4, 0, 7));
        _configNicDetails = Label("Select the commissioning LAN adapter.", 8.5, false, "#8FA7B8");
        endpointStack.Children.Add(_configEndpointText);
        endpointStack.Children.Add(_configNicDetails);
        endpointCard.Child = endpointStack;
        root.Children.Add(endpointCard);

        var buttons = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        _configStartServerButton = ActionButton("START SERVER");
        _configStartServerButton.Click += ConfigStartServer_Click;
        _configStopServerButton = ActionButton("STOP");
        _configStopServerButton.Click += ConfigStopServer_Click;
        Grid.SetColumn(_configStopServerButton, 2);
        buttons.Children.Add(_configStartServerButton);
        buttons.Children.Add(_configStopServerButton);
        root.Children.Add(buttons);

        _configServerStatus = Label("STOPPED", 10, true, "#8FA7B8");
        _configServerCounters = Label("Active 0 · Requests 0 · Reports 0", 8.5, false, "#8FA7B8", new Thickness(0, 4, 0, 0));
        root.Children.Add(_configServerStatus);
        root.Children.Add(_configServerCounters);

        tab.Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root
        };
        ConfigurationTabs.Items.Add(tab);
        _configNetworkInstalled = true;
        PopulateConfigurationAdapters();
        RefreshConfigurationNetworkStatus();
    }

    private void PopulateConfigurationAdapters()
    {
        if (_configNicCombo is null) return;
        var choices = new List<ConfigNicChoice>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            try
            {
                var props = nic.GetIPProperties();
                var addresses = props.UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address))
                    .Select(x => new ConfigIpChoice(x.Address.ToString(), x.PrefixLength))
                    .Where(x => !x.Address.StartsWith("169.254.", StringComparison.Ordinal))
                    .ToArray();
                if (addresses.Length == 0) continue;
                var gateway = props.GatewayAddresses.Select(x => x.Address)
                    .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !x.Equals(IPAddress.Any))?.ToString() ?? "—";
                var rawMac = nic.GetPhysicalAddress().ToString();
                var mac = rawMac.Length >= 12
                    ? string.Join("-", Enumerable.Range(0, rawMac.Length / 2).Select(i => rawMac.Substring(i * 2, 2)))
                    : rawMac;
                choices.Add(new ConfigNicChoice(nic.Name, nic.Description, nic.OperationalStatus, mac, gateway, addresses));
            }
            catch (NetworkInformationException) { }
        }

        _configNicCombo.ItemsSource = choices
            .OrderByDescending(x => x.Status == OperationalStatus.Up)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_configNicCombo.Items.Count > 0) _configNicCombo.SelectedIndex = 0;
    }

    private void ConfigNicCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_configIpCombo is null) return;
        var nic = _configNicCombo?.SelectedItem as ConfigNicChoice;
        _configIpCombo.ItemsSource = nic?.Addresses;
        if (_configIpCombo.Items.Count > 0) _configIpCombo.SelectedIndex = 0;
        RefreshConfigurationNetworkStatus();
    }

    private async void ConfigStartServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var host = ConfigurationBindAddress();
            if (!int.TryParse(_configPortBox?.Text, out var port) || port is < 1 or > 65535)
                throw new FormatException("MMS TCP port must be between 1 and 65535.");
            _iec61850Server.Start(host, port);
            AddEvent("IEC61850", $"MMS server started · {host}:{port}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or SocketException or FormatException)
        {
            AddEvent("IEC ERR", ex.Message);
            ShowWarning(ex.Message, "AVR IEC 61850 server");
        }
        finally
        {
            RefreshConfigurationNetworkStatus();
            RefreshP0NativeHmi();
            await Task.CompletedTask;
        }
    }

    private async void ConfigStopServer_Click(object sender, RoutedEventArgs e)
    {
        await _iec61850Server.StopAsync();
        AddEvent("IEC61850", "MMS server stopped");
        RefreshConfigurationNetworkStatus();
        RefreshP0NativeHmi();
    }

    private string ConfigurationBindAddress()
    {
        if (_configAllInterfaces?.IsChecked == true) return "0.0.0.0";
        return (_configIpCombo?.SelectedItem as ConfigIpChoice)?.Address
            ?? throw new InvalidOperationException("Select a network adapter and IPv4 address first.");
    }

    private void RefreshConfigurationNetworkStatus()
    {
        if (!_configNetworkInstalled) return;
        var status = _iec61850Server.GetStatus();
        var running = status.IsRunning;
        var nic = _configNicCombo?.SelectedItem as ConfigNicChoice;
        var ip = _configIpCombo?.SelectedItem as ConfigIpChoice;
        var host = _configAllInterfaces?.IsChecked == true ? "0.0.0.0" : ip?.Address ?? "<select IPv4>";
        var portText = int.TryParse(_configPortBox?.Text, out var port) ? port.ToString() : "102";

        if (_configEndpointText is not null)
            _configEndpointText.Text = running ? status.Endpoint : $"{host}:{portText}";
        if (_configNicDetails is not null)
            _configNicDetails.Text = nic is null
                ? "No active IPv4 adapter detected."
                : $"{nic.Description}\nLink {nic.Status.ToString().ToUpperInvariant()} · GW {nic.Gateway} · MAC {nic.Mac}";
        if (_configServerStatus is not null)
        {
            _configServerStatus.Text = running ? $"RUNNING · {status.Endpoint}" : "STOPPED";
            _configServerStatus.Foreground = running ? Brush("#66C878") : Brush("#8FA7B8");
        }
        if (_configServerCounters is not null)
            _configServerCounters.Text = $"Active {status.ActiveConnections} · Accepted {status.AcceptedConnections}\nRequests {status.ServedRequests} · Reports {status.ReportsSent}\nControls {status.AcceptedControls}/{status.RejectedControls}";
        if (_configStartServerButton is not null) _configStartServerButton.IsEnabled = !running;
        if (_configStopServerButton is not null) _configStopServerButton.IsEnabled = running;
        if (_configNicCombo is not null) _configNicCombo.IsEnabled = !running;
        if (_configIpCombo is not null) _configIpCombo.IsEnabled = !running;
        if (_configPortBox is not null) _configPortBox.IsEnabled = !running;
        if (_configAllInterfaces is not null) _configAllInterfaces.IsEnabled = !running;
    }

    private static TextBlock Label(string text, double size, bool bold, string color = "#E9F1F6", Thickness? margin = null)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Brush(color),
            Margin = margin ?? new Thickness(0),
            TextWrapping = TextWrapping.Wrap
        };

    private static Button ActionButton(string text)
        => new()
        {
            Content = text,
            Height = 30,
            Foreground = Brushes.White,
            Background = Brush("#176A9D"),
            BorderBrush = Brush("#43A1D6"),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

    private static SolidColorBrush Brush(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));
}
