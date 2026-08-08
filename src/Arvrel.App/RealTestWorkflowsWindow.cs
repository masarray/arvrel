using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.App;

public sealed class RealTestWorkflowsWindow : Window
{
    private readonly RealTestWorkflowEngine _engine;
    private readonly ComboBox _workflowCombo;
    private readonly ComboBox _signalCombo;
    private readonly ComboBox _feedbackCombo;
    private readonly ComboBox _faultPresetCombo;
    private readonly TextBox _startText;
    private readonly TextBox _endText;
    private readonly TextBox _stepText;
    private readonly TextBox _dwellText;
    private readonly TextBox _baselineText;
    private readonly TextBox _resetText;
    private readonly TextBox _resultsText;
    private readonly Button _runButton;
    private readonly Button _cancelButton;
    private CancellationTokenSource? _cancellation;

    public RealTestWorkflowsWindow(
        DeterministicLabScenario source,
        ClosedLoopVirtualTestBench bench)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bench);
        _engine = new RealTestWorkflowEngine(source, bench);

        Title = "ARVREL — Real Test Workflows";
        Width = 920;
        Height = 700;
        MinWidth = 820;
        MinHeight = 620;
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
        var headerStack = new StackPanel();
        header.Child = headerStack;
        headerStack.Children.Add(new TextBlock
        {
            Text = "REAL TEST WORKFLOWS",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#172033")
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "Ramp, pulse ramp, pickup/dropout search and state sequencing operate through the P0 virtual wiring and TESTSET BI feedback only.",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 10.5,
            Foreground = Brush("#64748B")
        });

        var setup = new Border
        {
            Margin = new Thickness(12, 10, 12, 0),
            Padding = new Thickness(12, 10, 12, 10),
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(5)
        };
        Grid.SetRow(setup, 1);
        root.Children.Add(setup);

        var setupGrid = new Grid();
        for (var index = 0; index < 4; index++)
            setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = index % 2 == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
        for (var index = 0; index < 5; index++)
            setupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        setup.Child = setupGrid;

        _workflowCombo = new ComboBox
        {
            ItemsSource = new[] { "Step ramp", "Pulse ramp", "Pickup / dropout search", "State sequence" },
            SelectedIndex = 0,
            Height = 30,
            Margin = new Thickness(8, 3, 16, 3)
        };
        AddField(setupGrid, "WORKFLOW", _workflowCombo, 0, 0);

        _signalCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<VirtualInjectionSignal>(),
            SelectedItem = VirtualInjectionSignal.PhaseACurrent,
            Height = 30,
            Margin = new Thickness(8, 3, 0, 3)
        };
        AddField(setupGrid, "SIGNAL", _signalCombo, 0, 2);

        _startText = Field("2.0");
        AddField(setupGrid, "START RMS", _startText, 1, 0);
        _endText = Field("6.0");
        AddField(setupGrid, "END RMS", _endText, 1, 2);

        _stepText = Field("0.25");
        AddField(setupGrid, "STEP RMS", _stepText, 2, 0);
        _dwellText = Field("10");
        AddField(setupGrid, "DWELL / PULSE ms", _dwellText, 2, 2);

        _baselineText = Field("1.0");
        AddField(setupGrid, "BASELINE RMS", _baselineText, 3, 0);
        _resetText = Field("20");
        AddField(setupGrid, "RESET ms", _resetText, 3, 2);

        _feedbackCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<RealTestFeedback>(),
            SelectedItem = RealTestFeedback.Pickup,
            Height = 30,
            Margin = new Thickness(8, 3, 16, 3)
        };
        AddField(setupGrid, "STOP / ADVANCE", _feedbackCombo, 4, 0);

        _faultPresetCombo = new ComboBox
        {
            ItemsSource = VirtualInjectionPresets.Names,
            SelectedItem = "Three-phase fault",
            Height = 30,
            Margin = new Thickness(8, 3, 0, 3)
        };
        AddField(setupGrid, "FAULT STATE", _faultPresetCombo, 4, 2);

        var results = new Border
        {
            Margin = new Thickness(12, 10, 12, 10),
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(1, 1, 1, 1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 10, 10, 10)
        };
        Grid.SetRow(results, 2);
        root.Children.Add(results);
        var resultsGrid = new Grid();
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        results.Child = resultsGrid;
        resultsGrid.Children.Add(new TextBlock
        {
            Text = "TEST-SET RESULT / EVIDENCE",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B"),
            Margin = new Thickness(2, 0, 0, 8)
        });
        _resultsText = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 10.5,
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#DCE4EA"),
            BorderThickness = new Thickness(1, 1, 1, 1),
            Padding = new Thickness(10, 8, 10, 8),
            Text = "Ready. Select a workflow and run it against the closed-loop virtual relay."
        };
        Grid.SetRow(_resultsText, 1);
        resultsGrid.Children.Add(_resultsText);

        var footer = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#D6E0E8"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 8, 12, 8)
        };
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Child = footerGrid;
        footerGrid.Children.Add(new TextBlock
        {
            Text = "P1 · TESTSET BI AUTHORITY · 0.25 ms deterministic simulation",
            Foreground = Brush("#64748B"),
            FontSize = 9.5,
            VerticalAlignment = VerticalAlignment.Center
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(actions, 1);
        footerGrid.Children.Add(actions);
        _cancelButton = new Button
        {
            Content = "Cancel",
            IsEnabled = false,
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 6, 0)
        };
        _cancelButton.Click += (_, _) => _cancellation?.Cancel();
        actions.Children.Add(_cancelButton);
        _runButton = new Button
        {
            Content = "Run test",
            Height = 30,
            Padding = new Thickness(14, 0, 14, 0),
            FontWeight = FontWeights.SemiBold,
            Background = Brush("#2E6F9E"),
            Foreground = Brushes.White,
            BorderBrush = Brush("#245C84")
        };
        _runButton.Click += RunButton_Click;
        actions.Children.Add(_runButton);

        _workflowCombo.SelectionChanged += (_, _) => RefreshWorkflowFields();
        RefreshWorkflowFields();
        Closed += (_, _) => _cancellation?.Cancel();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        _runButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _resultsText.Text = "Running deterministic closed-loop workflow…";

        try
        {
            var token = _cancellation.Token;
            var result = await Task.Run<object>(() => RunSelectedWorkflow(token), token);
            _resultsText.Text = FormatResult(result);
        }
        catch (OperationCanceledException)
        {
            _resultsText.Text = "CANCELLED · virtual output returned to stopped state.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            _resultsText.Text = $"INVALID TEST DEFINITION\n{ex.Message}";
        }
        finally
        {
            _cancelButton.IsEnabled = false;
            _runButton.IsEnabled = true;
        }
    }

    private object RunSelectedWorkflow(CancellationToken token)
    {
        var signal = (VirtualInjectionSignal)(_signalCombo.SelectedItem ?? VirtualInjectionSignal.PhaseACurrent);
        var feedback = (RealTestFeedback)(_feedbackCombo.SelectedItem ?? RealTestFeedback.Pickup);
        var start = Number(_startText.Text, "Start RMS");
        var end = Number(_endText.Text, "End RMS");
        var step = Number(_stepText.Text, "Step RMS");
        var dwell = Milliseconds(_dwellText.Text, "Dwell / pulse");
        var baseline = Number(_baselineText.Text, "Baseline RMS");
        var reset = Milliseconds(_resetText.Text, "Reset");

        return _workflowCombo.SelectedIndex switch
        {
            0 => _engine.RunRamp(new StepRampDefinition("Operator step ramp", signal, start, end, step, dwell, feedback), token),
            1 => _engine.RunPulseRamp(new PulseRampDefinition("Operator pulse ramp", signal, baseline, start, end, step, dwell, reset, feedback), token),
            2 => _engine.RunPickupDropoutSearch(new PickupDropoutSearchDefinition("Operator pickup/dropout search", signal, Math.Min(start, end), Math.Max(start, end), step, dwell), token),
            _ => _engine.RunStateSequence(BuildStateSequence(dwell, reset, feedback), token)
        };
    }

    private StateSequenceDefinition BuildStateSequence(TimeSpan faultDuration, TimeSpan postDuration, RealTestFeedback feedback)
    {
        var preset = _faultPresetCombo.SelectedItem as string ?? "Three-phase fault";
        var normal = VirtualInjectionPresets.Create("Normal balanced");
        var fault = VirtualInjectionPresets.Create(preset);
        return new StateSequenceDefinition(
            "Operator state sequence",
            new[]
            {
                new RealTestState("Pre-fault", normal, TimeSpan.FromMilliseconds(100)),
                new RealTestState("Fault", fault, faultDuration, feedback),
                new RealTestState("Post-fault", normal, postDuration)
            });
    }

    private static string FormatResult(object result)
    {
        var builder = new StringBuilder();
        switch (result)
        {
            case StepRampResult ramp:
                builder.AppendLine($"{ramp.Name} · {ramp.Outcome}");
                builder.AppendLine($"Stop feedback : {ramp.StopOn}");
                builder.AppendLine($"Detected RMS  : {Format(ramp.DetectedAtRms)}");
                builder.AppendLine($"Detected time : {Format(ramp.DetectedAtTime)}");
                AppendObservations(builder, ramp.Observations);
                break;
            case PulseRampResult pulse:
                builder.AppendLine($"{pulse.Name} · {pulse.Outcome}");
                builder.AppendLine($"Pulses        : {pulse.PulsesApplied}");
                builder.AppendLine($"Detected RMS  : {Format(pulse.DetectedAtRms)}");
                builder.AppendLine($"Detected time : {Format(pulse.DetectedAtTime)}");
                AppendObservations(builder, pulse.Observations);
                break;
            case PickupDropoutSearchResult search:
                builder.AppendLine($"{search.Name} · {search.Outcome}");
                builder.AppendLine($"Pickup RMS    : {Format(search.PickupRms)}");
                builder.AppendLine($"Dropout RMS   : {Format(search.DropoutRms)}");
                builder.AppendLine($"Dropout ratio : {(search.DropoutRatio is { } ratio ? ratio.ToString("0.000", CultureInfo.InvariantCulture) : "—")}");
                AppendObservations(builder, search.Observations);
                break;
            case StateSequenceResult sequence:
                builder.AppendLine($"{sequence.Name} · {sequence.Outcome}");
                builder.AppendLine();
                builder.AppendLine("STATE                 START ms    END ms      EXIT          PICKUP  TRIP");
                foreach (var state in sequence.States)
                {
                    builder.AppendLine($"{state.Name,-20} {state.StartedAt.TotalMilliseconds,9:0.000}  {state.EndedAt.TotalMilliseconds,9:0.000}  {state.ExitReason,-12}  {(state.PickupObserved ? "YES" : "NO"),-6}  {(state.TripObserved ? "YES" : "NO")}");
                }
                AppendObservations(builder, sequence.Observations);
                break;
        }
        return builder.ToString();
    }

    private static void AppendObservations(StringBuilder builder, IReadOnlyList<RealTestObservation> observations)
    {
        builder.AppendLine();
        builder.AppendLine("OBSERVATIONS");
        builder.AppendLine("TIME ms     STAGE                    CMD RMS     RELAY RMS   BI2  BI1");
        foreach (var point in observations.TakeLast(120))
        {
            builder.AppendLine($"{point.Elapsed.TotalMilliseconds,8:0.000}   {point.Stage,-23}  {point.CommandedRms,8:0.###}   {point.RelayMeasuredRms,8:0.###}   {(point.PickupInput ? 1 : 0),3}  {(point.TripInput ? 1 : 0),3}");
        }
    }

    private void RefreshWorkflowFields()
    {
        var pulse = _workflowCombo.SelectedIndex == 1;
        var sequence = _workflowCombo.SelectedIndex == 3;
        _baselineText.IsEnabled = pulse;
        _resetText.IsEnabled = pulse || sequence;
        _signalCombo.IsEnabled = !sequence;
        _startText.IsEnabled = !sequence;
        _endText.IsEnabled = !sequence;
        _stepText.IsEnabled = !sequence;
        _faultPresetCombo.IsEnabled = sequence;
    }

    private static TextBox Field(string text)
        => new()
        {
            Text = text,
            Height = 30,
            Margin = new Thickness(8, 3, 16, 3),
            Padding = new Thickness(7, 3, 7, 3),
            VerticalContentAlignment = VerticalAlignment.Center
        };

    private static void AddField(Grid grid, string label, Control control, int row, int column)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 8.8,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(text, row);
        Grid.SetColumn(text, column);
        grid.Children.Add(text);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column + 1);
        grid.Children.Add(control);
    }

    private static double Number(string text, string name)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException($"{name} must be a finite number using '.' as decimal separator.");
        return value;
    }

    private static TimeSpan Milliseconds(string text, string name)
    {
        var value = Number(text, name);
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name, "Duration must be greater than zero.");
        return TimeSpan.FromMilliseconds(value);
    }

    private static string Format(double? value)
        => value is { } number ? number.ToString("0.###", CultureInfo.InvariantCulture) : "—";

    private static string Format(TimeSpan? value)
        => value is { } duration ? $"{duration.TotalMilliseconds:0.000} ms" : "—";

    private static SolidColorBrush Brush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }
}
