using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _testSetTimingUxInitialized;
    private DispatcherTimer? _testSetTimingUxTimer;
    private Border? _testSetTimingPanel;
    private Ellipse? _testSetBi2Lamp;
    private Ellipse? _testSetBi1Lamp;
    private TextBlock? _testSetBi2State;
    private TextBlock? _testSetBi1State;
    private TextBlock? _testSetBi2Edge;
    private TextBlock? _testSetBi1Edge;
    private TextBlock? _testSetTimerValue;
    private TextBlock? _testSetTimingRail;
    private long _tripCaptureDisplayRunId = -1;
    private WaveformFrame? _tripCaptureWaveform;
    private PhasorDisplayFrame? _tripCapturePhasor;
    private PhasorDisplayMode _tripCapturePhasorMode;
    private bool _tripCaptureDisplayAttached;

    internal void InitializeTestSetTimingAndTripCaptureUx()
    {
        if (_testSetTimingUxInitialized)
            return;
        if (!_productReadyInjectionUxInitialized ||
            _virtualInjectionView is null ||
            _phasorScope is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeTestSetTimingAndTripCaptureUx));
            return;
        }

        var footer = _virtualInjectionView.Children
            .OfType<Grid>()
            .FirstOrDefault(child => Grid.GetRow(child) == 2);
        if (footer is null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(InitializeTestSetTimingAndTripCaptureUx));
            return;
        }

        _testSetTimingUxInitialized = true;
        if (_virtualInjectionView.RowDefinitions.Count >= 3)
            _virtualInjectionView.RowDefinitions[2].Height = new GridLength(58);

        // The existing footer uses column 0 for provenance/CT observability and
        // column 1 for action buttons. Insert a dedicated middle timing column and
        // shift existing right-side children so the BI strip never overlays the CT
        // control regardless of bootstrap order.
        if (footer.ColumnDefinitions.Count >= 2)
        {
            footer.ColumnDefinitions[0].Width = GridLength.Auto;
            footer.ColumnDefinitions.Insert(1, new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            foreach (UIElement child in footer.Children.Cast<UIElement>().ToArray())
            {
                var column = Grid.GetColumn(child);
                if (column >= 1)
                    Grid.SetColumn(child, column + 1);
            }
        }

        _testSetTimingPanel = BuildTestSetTimingPanel();
        Grid.SetColumn(_testSetTimingPanel, footer.ColumnDefinitions.Count >= 3 ? 1 : 0);
        footer.Children.Add(_testSetTimingPanel);

        _testSetTimingUxTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _testSetTimingUxTimer.Tick += (_, _) => RefreshTestSetTimingUx();
        _testSetTimingUxTimer.Start();

        if (!_tripCaptureDisplayAttached)
        {
            CompositionTarget.Rendering += TripCaptureDisplay_Rendering;
            _tripCaptureDisplayAttached = true;
        }

        Closed += (_, _) =>
        {
            _testSetTimingUxTimer?.Stop();
            if (_tripCaptureDisplayAttached)
            {
                CompositionTarget.Rendering -= TripCaptureDisplay_Rendering;
                _tripCaptureDisplayAttached = false;
            }
        };

        RefreshTestSetTimingUx();
    }

    private Border BuildTestSetTimingPanel()
    {
        var border = new Border
        {
            Background = TestSetBrush("#F7FAFC"),
            BorderBrush = TestSetBrush("#D7E1E8"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ToolTip = "Virtual test-set binary inputs. Times are latched from injection START to the accepted BI rising edge."
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        border.Child = root;

        var label = new TextBlock
        {
            Text = "TESTSET BI",
            FontSize = 8.8,
            FontWeight = FontWeights.SemiBold,
            Foreground = TestSetBrush("#607383"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetRow(label, 0);
        root.Children.Add(label);

        var bi2 = BuildBinaryInputCluster("BI2", "PICKUP", out _testSetBi2Lamp, out _testSetBi2State, out _testSetBi2Edge);
        Grid.SetRow(bi2, 0);
        Grid.SetColumn(bi2, 2);
        root.Children.Add(bi2);

        var bi1 = BuildBinaryInputCluster("BI1", "TRIP", out _testSetBi1Lamp, out _testSetBi1State, out _testSetBi1Edge);
        Grid.SetRow(bi1, 0);
        Grid.SetColumn(bi1, 4);
        root.Children.Add(bi1);

        _testSetTimerValue = new TextBlock
        {
            Text = "TIMER IDLE",
            FontSize = 10.2,
            FontWeight = FontWeights.SemiBold,
            Foreground = TestSetBrush("#243746"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(14, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(_testSetTimerValue, 0);
        Grid.SetColumn(_testSetTimerValue, 5);
        root.Children.Add(_testSetTimerValue);

        _testSetTimingRail = new TextBlock
        {
            Text = "T0 · waiting for timed injection",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.3,
            Foreground = TestSetBrush("#607383"),
            Margin = new Thickness(0, 3, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = "Chronological timing rail. BI2 is generic ANY PICKUP; operated-element pickup and trip are shown separately."
        };
        Grid.SetRow(_testSetTimingRail, 1);
        Grid.SetColumn(_testSetTimingRail, 0);
        Grid.SetColumnSpan(_testSetTimingRail, 6);
        root.Children.Add(_testSetTimingRail);

        return border;
    }

    private static StackPanel BuildBinaryInputCluster(
        string input,
        string function,
        out Ellipse lamp,
        out TextBlock state,
        out TextBlock edge)
    {
        var cluster = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        lamp = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = TestSetBrush("#CBD5DD"),
            Stroke = TestSetBrush("#8797A4"),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        cluster.Children.Add(lamp);

        var name = new TextBlock
        {
            Text = $"{input} {function}",
            FontSize = 9.2,
            FontWeight = FontWeights.SemiBold,
            Foreground = TestSetBrush("#304552"),
            VerticalAlignment = VerticalAlignment.Center
        };
        cluster.Children.Add(name);

        state = new TextBlock
        {
            Text = "OFF",
            FontSize = 8.8,
            FontWeight = FontWeights.SemiBold,
            Foreground = TestSetBrush("#718391"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0)
        };
        cluster.Children.Add(state);

        edge = new TextBlock
        {
            Text = "↑ —",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 8.8,
            Foreground = TestSetBrush("#607383"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        cluster.Children.Add(edge);

        return cluster;
    }

    private void RefreshTestSetTimingUx()
    {
        if (!_testSetTimingUxInitialized ||
            _testSetTimingPanel is null ||
            _testSetBi2Lamp is null ||
            _testSetBi1Lamp is null ||
            _testSetBi2State is null ||
            _testSetBi1State is null ||
            _testSetBi2Edge is null ||
            _testSetBi1Edge is null ||
            _testSetTimerValue is null ||
            _testSetTimingRail is null)
            return;

        var testSet = _closedLoopBench?.TestSetSnapshot;
        if (testSet is null)
        {
            SetBinaryInputPresentation(_testSetBi2Lamp, _testSetBi2State, false, false);
            SetBinaryInputPresentation(_testSetBi1Lamp, _testSetBi1State, false, true);
            _testSetBi2Edge.Text = "↑ —";
            _testSetBi1Edge.Text = "↑ —";
            _testSetTimerValue.Text = "TIMER IDLE";
            _testSetTimingRail.Text = "T0 · waiting for timed injection";
            _testSetTimingPanel.Background = TestSetBrush("#F7FAFC");
            _testSetTimingPanel.BorderBrush = TestSetBrush("#D7E1E8");
            return;
        }

        SetBinaryInputPresentation(_testSetBi2Lamp, _testSetBi2State, testSet.PickupInput, false);
        SetBinaryInputPresentation(_testSetBi1Lamp, _testSetBi1State, testSet.TripInput, true);
        _testSetBi2Edge.Text = testSet.PickupTime is { } pickup
            ? $"↑ {pickup.TotalMilliseconds:0.000} ms"
            : "↑ —";
        _testSetBi1Edge.Text = testSet.TripTime is { } trip
            ? $"↑ {trip.TotalMilliseconds:0.000} ms"
            : "↑ —";

        _testSetTimerValue.Text = testSet.TimerState switch
        {
            VirtualTestSetTimerState.Armed when testSet.ElapsedTime is { } elapsed
                => $"TIMER {elapsed.TotalMilliseconds:0.000} ms · ARMED",
            VirtualTestSetTimerState.Completed when testSet.TripTime is { } measured
                => $"OUTPUT OFF · FROZEN CAPTURE · BI1 {measured.TotalMilliseconds:0.000} ms",
            VirtualTestSetTimerState.Blocked
                => "NOT ARMED · BI ACTIVE",
            _ => "TIMER IDLE"
        };

        _testSetTimerValue.Foreground = testSet.TimerState switch
        {
            VirtualTestSetTimerState.Completed => TestSetBrush("#8B3A3A"),
            VirtualTestSetTimerState.Blocked => TestSetBrush("#A56B16"),
            VirtualTestSetTimerState.Armed => TestSetBrush("#1E6A45"),
            _ => TestSetBrush("#607383")
        };

        _testSetTimingPanel.Background = testSet.TimerState switch
        {
            VirtualTestSetTimerState.Completed => TestSetBrush("#EEF4F7"),
            VirtualTestSetTimerState.Blocked => TestSetBrush("#FFF8E8"),
            _ => TestSetBrush("#F7FAFC")
        };
        _testSetTimingPanel.BorderBrush = testSet.TimerState switch
        {
            VirtualTestSetTimerState.Completed => TestSetBrush("#9DB2C0"),
            VirtualTestSetTimerState.Blocked => TestSetBrush("#D5B56D"),
            _ => TestSetBrush("#D7E1E8")
        };

        _testSetTimingRail.Text = BuildClosedLoopTimingRail(testSet, _snapshot);
        _testSetTimingRail.Foreground = testSet.TimerState == VirtualTestSetTimerState.Completed
            ? TestSetBrush("#344F60")
            : TestSetBrush("#607383");

        var detail = BuildClosedLoopTimingDetail(testSet, _snapshot);
        var captureSemantics = TryGetTripCaptureFrameOffsetMicroseconds(_closedLoopBench?.TripCapture, out var captureOffsetUs)
            ? $"\nBI1 accepted edge is timer authority. Frozen display frame is +{captureOffsetUs} µs after BI1 on the relay processing grid."
            : string.Empty;
        _testSetTimingPanel.ToolTip =
            $"Timer state: {testSet.TimerState}\n" +
            $"Run: {testSet.TestRunId}\n" +
            $"First ANY-pickup source: {FirstAnyPickupSourceFor(testSet)}\n" +
            $"BI2 accepted: {(testSet.PickupInput ? "ON" : "OFF")} · raw BO2: {(testSet.PickupContactRaw ? "ON" : "OFF")}\n" +
            $"BI1 accepted: {(testSet.TripInput ? "ON" : "OFF")} · raw BO1: {(testSet.TripContactRaw ? "ON" : "OFF")}\n" +
            detail +
            captureSemantics +
            (string.IsNullOrWhiteSpace(testSet.ArmBlockReason)
                ? string.Empty
                : $"\n{testSet.ArmBlockReason}");
    }

    private static void SetBinaryInputPresentation(Ellipse lamp, TextBlock state, bool active, bool trip)
    {
        lamp.Fill = active
            ? TestSetBrush(trip ? "#D84A4A" : "#2E9D62")
            : TestSetBrush("#CBD5DD");
        lamp.Stroke = active
            ? TestSetBrush(trip ? "#A83232" : "#23784B")
            : TestSetBrush("#8797A4");
        state.Text = active ? "ON" : "OFF";
        state.Foreground = active
            ? TestSetBrush(trip ? "#A83232" : "#23784B")
            : TestSetBrush("#718391");
    }

    private void TripCaptureDisplay_Rendering(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0 ||
            _scenario.IsRunning ||
            _closedLoopBench?.TripCapture is not { } capture ||
            _phasorScope is null)
        {
            _tripCaptureDisplayRunId = -1;
            _tripCaptureWaveform = null;
            _tripCapturePhasor = null;
            return;
        }

        if (_tripCaptureDisplayRunId != capture.TestRunId ||
            _tripCapturePhasor is null ||
            _tripCapturePhasorMode != _phasorDisplayMode)
        {
            _tripCaptureDisplayRunId = capture.TestRunId;
            _tripCapturePhasorMode = _phasorDisplayMode;
            _tripCaptureWaveform = new WaveformFrame(
                capture.Source.Waveform.PhaseA,
                capture.Source.Waveform.PhaseB,
                capture.Source.Waveform.PhaseC,
                capture.Source.Waveform.Residual,
                capture.Source.Waveform.FrequencyHz,
                _pickupPosition,
                _tripPosition);
            _tripCapturePhasor = PhasorDisplayProjector.Project(
                capture.Source.Measurement.Phasors,
                _phasorDisplayMode);
        }

        if (_tripCaptureWaveform is not null)
        {
            SmvScope.Frame = _tripCaptureWaveform with
            {
                PickupPosition = _pickupPosition,
                TripPosition = _tripPosition
            };
        }
        if (_tripCapturePhasor is not null)
            _phasorScope.Frame = _tripCapturePhasor;
    }

    private static Brush TestSetBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

internal static class TestSetTimingAndTripCaptureUxBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        window.ContentRendered -= Window_ContentRendered;
        window.ContentRendered += Window_ContentRendered;
    }

    private static void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
            window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(window.InitializeTestSetTimingAndTripCaptureUx));
    }
}
