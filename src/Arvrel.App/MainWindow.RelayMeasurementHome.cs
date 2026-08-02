using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private readonly TextBlock?[] _relayLcdCurrentValues = new TextBlock?[4];
    private readonly TextBlock?[] _relayLcdVoltageValues = new TextBlock?[4];
    private bool _relayMeasurementHomeInitialized;
    private Grid? _relayLcdContentHost;
    private Grid? _relayMeasurementHomePanel;
    private RelayLcdPhasorScope? _relayLcdHomePhasor;
    private DispatcherTimer? _relayMeasurementHomeTimer;

    internal void InitializeRelayMeasurementHome()
    {
        if (_relayMeasurementHomeInitialized ||
            !_relayFaceplateInitialized ||
            _relayLcdHeader is null ||
            _relayLcdBody is null ||
            _relayLcdFooter is null ||
            _relayLcdHeader.Parent is not Panel lcdPanel)
            return;

        _relayMeasurementHomeInitialized = true;
        lcdPanel.Children.Clear();

        var root = new Grid
        {
            Height = 179,
            ClipToBounds = true
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

        _relayLcdHeader.Margin = new Thickness(0);
        _relayLcdHeader.FontSize = 9.7;
        _relayLcdHeader.VerticalAlignment = VerticalAlignment.Center;
        _relayLcdHeader.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(_relayLcdHeader, 0);
        root.Children.Add(_relayLcdHeader);

        _relayLcdContentHost = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2),
            ClipToBounds = true
        };
        Grid.SetRow(_relayLcdContentHost, 1);
        root.Children.Add(_relayLcdContentHost);

        _relayLcdBody.Margin = new Thickness(0, 2, 0, 0);
        _relayLcdBody.Height = double.NaN;
        _relayLcdBody.FontSize = 9.3;
        _relayLcdBody.LineHeight = 15.2;
        _relayLcdBody.VerticalAlignment = VerticalAlignment.Top;
        _relayLcdContentHost.Children.Add(_relayLcdBody);

        _relayMeasurementHomePanel = BuildRelayMeasurementHomePanel();
        _relayMeasurementHomePanel.Visibility = Visibility.Collapsed;
        _relayLcdContentHost.Children.Add(_relayMeasurementHomePanel);

        var divider = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(166, 178, 173)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(divider, 2);
        root.Children.Add(divider);

        _relayLcdFooter.Margin = new Thickness(0);
        _relayLcdFooter.FontSize = 7.8;
        _relayLcdFooter.VerticalAlignment = VerticalAlignment.Center;
        _relayLcdFooter.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetRow(_relayLcdFooter, 3);
        root.Children.Add(_relayLcdFooter);

        lcdPanel.Children.Add(root);

        _relayMeasurementHomeTimer = _relayFaceplateTimer;
        if (_relayMeasurementHomeTimer is not null)
            _relayMeasurementHomeTimer.Tick += RelayMeasurementHomeTimer_Tick;
        UpdateRelayMeasurementHome();
    }

    internal void StopRelayMeasurementHome()
    {
        if (_relayMeasurementHomeTimer is not null)
            _relayMeasurementHomeTimer.Tick -= RelayMeasurementHomeTimer_Tick;
        _relayMeasurementHomeTimer = null;
    }

    private Grid BuildRelayMeasurementHomePanel()
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.82, GridUnitType.Star) });

        var matrix = new Grid
        {
            Margin = new Thickness(0, 1, 0, 1)
        };
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        matrix.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        matrix.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        for (var index = 0; index < 4; index++)
            matrix.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddMatrixText(matrix, "", 0, 0, TextAlignment.Left, FontWeights.Normal, 7.2);
        AddMatrixText(matrix, "I (A)", 0, 1, TextAlignment.Right, FontWeights.SemiBold, 7.4);
        AddMatrixText(matrix, "U (V)", 0, 2, TextAlignment.Right, FontWeights.SemiBold, 7.4);

        var headerLine = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 78, 96, 117)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            IsHitTestVisible = false
        };
        Grid.SetRow(headerLine, 0);
        Grid.SetColumnSpan(headerLine, 3);
        matrix.Children.Add(headerLine);

        var phaseLabels = new[] { "R", "S", "T", "N" };
        for (var index = 0; index < phaseLabels.Length; index++)
        {
            AddMatrixText(matrix, phaseLabels[index], index + 1, 0, TextAlignment.Left, FontWeights.SemiBold, 8.4);
            _relayLcdCurrentValues[index] = AddMatrixText(
                matrix, "—", index + 1, 1, TextAlignment.Right, FontWeights.Normal, 8.25);
            _relayLcdVoltageValues[index] = AddMatrixText(
                matrix, "—", index + 1, 2, TextAlignment.Right, FontWeights.Normal, 8.25);
        }

        panel.Children.Add(matrix);

        var separator = new Border
        {
            Width = 1,
            Margin = new Thickness(3, 5, 3, 5),
            Background = new SolidColorBrush(Color.FromArgb(80, 78, 96, 117)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(separator, 1);
        panel.Children.Add(separator);

        _relayLcdHomePhasor = new RelayLcdPhasorScope
        {
            Margin = new Thickness(1, 0, 0, 0),
            MinWidth = 72,
            MinHeight = 92,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Frame = PhasorDisplayFrame.Unavailable(PhasorDisplayMode.Current, "No phasor")
        };
        Grid.SetColumn(_relayLcdHomePhasor, 2);
        panel.Children.Add(_relayLcdHomePhasor);

        return panel;
    }

    private TextBlock AddMatrixText(
        Grid parent,
        string text,
        int row,
        int column,
        TextAlignment alignment,
        FontWeight weight,
        double size)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = FindResource("LcdTextBrush") as Brush,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = size,
            FontWeight = weight,
            TextAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = column == 0 ? new Thickness(0) : new Thickness(2, 0, 0, 0)
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        parent.Children.Add(block);
        return block;
    }

    private void RelayMeasurementHomeTimer_Tick(object? sender, EventArgs e)
        => UpdateRelayMeasurementHome();

    private void UpdateRelayMeasurementHome()
    {
        if (!_relayMeasurementHomeInitialized ||
            _relayMeasurementHomePanel is null ||
            _relayLcdBody is null ||
            _relayLcdHeader is null ||
            _relayLcdFooter is null ||
            _relayLcdHomePhasor is null)
            return;

        var showHome = !_relayMenuOpen && _relayLcdPage == RelayLcdPage.Measurements;
        _relayMeasurementHomePanel.Visibility = showHome ? Visibility.Visible : Visibility.Collapsed;
        _relayLcdBody.Visibility = showHome ? Visibility.Collapsed : Visibility.Visible;
        if (!showHome)
            return;

        if (_relayLastFrame is not { } frame)
        {
            _relayLcdHeader.Text = "MEASUREMENTS";
            SetMeasurementValues(null, null);
            _relayLcdHomePhasor.Frame = PhasorDisplayFrame.Unavailable(PhasorDisplayMode.Current, "No data");
            _relayLcdFooter.Text = "WAITING FOR DATA · ENTER MENU";
            return;
        }

        var primary = ViewCombo.SelectedIndex == 1;
        var measurement = RelayHomeDisplayMeasurement(frame.Measurement, primary);
        var overview = RelayMeasurementOverviewProjector.Project(measurement);
        SetMeasurementValues(overview.CurrentsRstn, overview.VoltagesRstn);
        _relayLcdHomePhasor.Frame = overview.CurrentPhasor;

        _relayLcdHeader.Text = primary
            ? "MEASUREMENTS · PRIMARY"
            : "MEASUREMENTS · SECONDARY";
        _relayLcdFooter.Text = frame.Snapshot.TripLatched
            ? "TRIP LATCHED · ENTER MENU"
            : !frame.Snapshot.SmvTrust.AllowsTrip
                ? "TRIP BLOCKED · ENTER MENU"
                : overview.VoltageAvailable
                    ? $"READY · {overview.FrequencyHz:0.000} HZ · ENTER MENU"
                    : "I READY · U UNAVAILABLE";
    }

    private MeasurementFrame RelayHomeDisplayMeasurement(MeasurementFrame measurement, bool primary)
    {
        if (!primary || SourceCombo.SelectedIndex != 0)
            return measurement;

        var context = _processBus.MeasurementContext;
        var currentRatio = context.PrimaryRatio;
        var voltageRatio = context.VoltagePrimaryRatio;
        var phasors = measurement.Phasors;
        return measurement with
        {
            PhaseA = measurement.PhaseA * currentRatio,
            PhaseB = measurement.PhaseB * currentRatio,
            PhaseC = measurement.PhaseC * currentRatio,
            Residual = measurement.Residual * currentRatio,
            Phasors = phasors is null
                ? null
                : new PhasorMeasurementSet(
                    phasors.PhaseACurrent * currentRatio,
                    phasors.PhaseBCurrent * currentRatio,
                    phasors.PhaseCCurrent * currentRatio,
                    phasors.NeutralCurrent * currentRatio,
                    phasors.PhaseAVoltage * voltageRatio,
                    phasors.PhaseBVoltage * voltageRatio,
                    phasors.PhaseCVoltage * voltageRatio,
                    phasors.NeutralVoltage * voltageRatio,
                    phasors.FrequencyHz)
        };
    }

    private void SetMeasurementValues(
        IReadOnlyList<double>? currents,
        IReadOnlyList<double>? voltages)
    {
        for (var index = 0; index < 4; index++)
        {
            if (_relayLcdCurrentValues[index] is { } current)
                current.Text = FormatLcdEngineering(currents is not null && index < currents.Count
                    ? currents[index]
                    : double.NaN);
            if (_relayLcdVoltageValues[index] is { } voltage)
                voltage.Text = FormatLcdEngineering(voltages is not null && index < voltages.Count
                    ? voltages[index]
                    : double.NaN);
        }
    }

    private static string FormatLcdEngineering(double value)
    {
        if (!double.IsFinite(value))
            return "—";

        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000)
            return $"{value / 1_000_000:0.0}M";
        if (absolute >= 1_000)
            return $"{value / 1_000:0.0}k";
        if (absolute >= 100)
            return value.ToString("0.0");
        return value.ToString("0.00");
    }
}

internal static class RelayMeasurementHomeBootstrap
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
        if (sender is not MainWindow window)
            return;

        window.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(window.InitializeRelayMeasurementHome));
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.StopRelayMeasurementHome();
    }
}
