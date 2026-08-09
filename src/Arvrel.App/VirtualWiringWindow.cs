using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Application.Laboratory;

namespace Arvrel.App;

public sealed class VirtualWiringWindow : Window
{
    private readonly ClosedLoopVirtualTestBench _bench;
    private readonly TextBlock _fingerprintText;
    private readonly TextBlock _pickupText;
    private readonly TextBlock _tripText;
    private readonly TextBlock _outputText;
    private readonly DispatcherTimer _timer;

    public VirtualWiringWindow(ClosedLoopVirtualTestBench bench)
    {
        _bench = bench ?? throw new ArgumentNullException(nameof(bench));
        Title = "ARVREL — Virtual Test Bench Wiring";
        Width = 820;
        Height = 640;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#F3F6F9");
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        var header = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        root.Children.Add(header);
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Child = headerGrid;

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = "VIRTUAL TEST BENCH WIRING",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#172033")
        });
        title.Children.Add(new TextBlock
        {
            Text = "Injector and relay are separate black-box devices. Only connected virtual terminals can exchange signals.",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10.5,
            Foreground = Brush("#64748B")
        });
        headerGrid.Children.Add(title);

        var restore = new Button
        {
            Content = "Restore default wiring",
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Reconnect all analog and binary feedback wires"
        };
        restore.Click += (_, _) => RestoreDefaultWiring();
        Grid.SetColumn(restore, 1);
        headerGrid.Children.Add(restore);

        var timing = new Border
        {
            Margin = new Thickness(12, 10, 12, 0),
            Padding = new Thickness(12, 9, 12, 9),
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5)
        };
        Grid.SetRow(timing, 1);
        root.Children.Add(timing);
        var timingGrid = new Grid();
        for (var index = 0; index < 3; index++)
            timingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timing.Child = timingGrid;

        _pickupText = CreateTimingValue("BI2 PICKUP", 0, timingGrid);
        _tripText = CreateTimingValue("BI1 TRIP", 1, timingGrid);
        _outputText = CreateTimingValue("INJECTOR OUTPUT", 2, timingGrid);

        var scroll = new ScrollViewer
        {
            Margin = new Thickness(12, 10, 12, 10),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);
        var wiringStack = new StackPanel();
        scroll.Content = wiringStack;

        wiringStack.Children.Add(SectionLabel("ANALOG SECONDARY WIRING · 4V + 4I"));
        foreach (var wire in _bench.Topology.AnalogWires)
            wiringStack.Children.Add(CreateAnalogWireRow(wire));

        wiringStack.Children.Add(SectionLabel("BINARY FEEDBACK · RELAY OUTPUT → TEST-SET INPUT", new Thickness(0, 14, 0, 6)));
        foreach (var wire in _bench.Topology.BinaryWires)
            wiringStack.Children.Add(CreateBinaryWireRow(wire));

        var footer = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 9, 14, 9)
        };
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Child = footerGrid;

        var identity = new StackPanel();
        identity.Children.Add(new TextBlock
        {
            Text = "TOPOLOGY FINGERPRINT",
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B")
        });
        _fingerprintText = new TextBlock
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10,
            Foreground = Brush("#172033"),
            Margin = new Thickness(0, 2, 0, 0)
        };
        identity.Children.Add(_fingerprintText);
        footerGrid.Children.Add(identity);

        var boundary = new TextBlock
        {
            Text = "VIRTUAL ONLY · NO PHYSICAL I/O",
            Foreground = Brush("#C48B2B"),
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(boundary, 1);
        footerGrid.Children.Add(boundary);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        RefreshStatus();
    }

    private FrameworkElement CreateAnalogWireRow(VirtualAnalogWire wire)
        => CreateWireRow(
            wire.Id,
            wire.SourceTerminal,
            wire.DestinationTerminal,
            wire.Signal.ToString(),
            "ANALOG",
            wire.Connected);

    private FrameworkElement CreateBinaryWireRow(VirtualBinaryWire wire)
        => CreateWireRow(
            wire.Id,
            wire.SourceTerminal,
            wire.DestinationTerminal,
            wire.Signal == VirtualBinarySignal.Trip ? "TRIP CONTACT" : "PICKUP CONTACT",
            "BINARY",
            wire.Connected);

    private FrameworkElement CreateWireRow(
        string wireId,
        string source,
        string destination,
        string signal,
        string kind,
        bool connected)
    {
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#DCE4EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(10, 7, 10, 7)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
        border.Child = grid;

        grid.Children.Add(new TextBlock
        {
            Text = kind,
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = kind == "BINARY" ? Brush("#C48B2B") : Brush("#2E6F9E"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var sourceText = new TextBlock
        {
            Text = source,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.5,
            Foreground = Brush("#172033"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sourceText, 1);
        grid.Children.Add(sourceText);

        var arrow = new TextBlock
        {
            Text = "→",
            FontSize = 18,
            Foreground = Brush("#7A8B98"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(arrow);

        var destinationText = new TextBlock
        {
            Text = destination,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.5,
            Foreground = Brush("#172033"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(destinationText, 3);
        grid.Children.Add(destinationText);

        var toggle = new CheckBox
        {
            Content = signal,
            IsChecked = connected,
            FontSize = 9.5,
            Foreground = Brush("#425466"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = wireId
        };
        toggle.Checked += (_, _) => SetConnection(wireId, true);
        toggle.Unchecked += (_, _) => SetConnection(wireId, false);
        Grid.SetColumn(toggle, 4);
        grid.Children.Add(toggle);
        return border;
    }

    private void SetConnection(string wireId, bool connected)
    {
        _bench.SetWireConnected(wireId, connected);
        RefreshStatus();
    }

    private void RestoreDefaultWiring()
    {
        foreach (var wire in _bench.Topology.AnalogWires.ToArray())
            _bench.SetWireConnected(wire.Id, true);
        foreach (var wire in _bench.Topology.BinaryWires.ToArray())
            _bench.SetWireConnected(wire.Id, true);

        // Rebuild the visual rows so every checkbox reflects the restored state.
        var replacement = new VirtualWiringWindow(_bench) { Owner = Owner };
        replacement.Show();
        Close();
    }

    private void RefreshStatus()
    {
        var snapshot = _bench.TestSetSnapshot;
        _pickupText.Text = snapshot.PickupTime is { } pickup
            ? $"{pickup.TotalMilliseconds:0.000} ms · BI2={(snapshot.PickupInput ? 1 : 0)}"
            : $"— · BI2={(snapshot.PickupInput ? 1 : 0)}";
        _tripText.Text = snapshot.TripTime is { } trip
            ? $"{trip.TotalMilliseconds:0.000} ms · BI1={(snapshot.TripInput ? 1 : 0)}"
            : $"— · BI1={(snapshot.TripInput ? 1 : 0)}";
        _outputText.Text = snapshot.OutputRunning ? "RUNNING" : snapshot.TripDetectedAt is not null ? "STOPPED BY BI1" : "STOPPED";
        _fingerprintText.Text = _bench.Topology.Fingerprint()[..20] + "…";
    }

    private static TextBlock CreateTimingValue(string label, int column, Grid parent)
    {
        var stack = new StackPanel { Margin = column == 0 ? new Thickness(0) : new Thickness(12, 0, 0, 0) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 8.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B")
        });
        var value = new TextBlock
        {
            Text = "—",
            Margin = new Thickness(0, 2, 0, 0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#172033")
        };
        stack.Children.Add(value);
        Grid.SetColumn(stack, column);
        parent.Children.Add(stack);
        return value;
    }

    private static TextBlock SectionLabel(string text, Thickness? margin = null)
        => new()
        {
            Text = text,
            Margin = margin ?? new Thickness(0, 0, 0, 6),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B")
        };

    private static SolidColorBrush Brush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }
}
