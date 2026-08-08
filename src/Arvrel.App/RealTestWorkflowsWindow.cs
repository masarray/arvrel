using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Laboratory;
using Arvrel.Protection;
using CoreScenario = Arvrel.Application.Laboratory.DeterministicLabScenario;

namespace Arvrel.App;

public sealed class RealTestWorkflowsWindow : Window
{
    private readonly CoreScenario _source;
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
    private bool _workflowRunning;
    private bool _closeAfterRun;

    public RealTestWorkflowsWindow(CoreScenario source, ClosedLoopVirtualTestBench bench)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bench);
        _source = source;
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
            Text = "Ramp, pulse ramp, pickup/dropout search and state sequencing use the P0 virtual wiring and TESTSET BI feedback only.",
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

        _workflowCombo = Combo(new[] { "Step ramp", "Pulse ramp", "Pickup / dropout search", "State sequence" }, 0, new Thickness(8, 3, 16, 3));
        AddField(setupGrid, "WORKFLOW", _workflowCombo, 0, 0);
        _signalCombo = Combo(Enum.GetValues<VirtualInjectionSignal>(), VirtualInjectionSignal.PhaseACurrent, new Thickness(8, 3, 0, 3));
        AddField(setupGrid, "SIGNAL", _signalCombo, 0, 2);

        _startText = Field("2.0");
        AddField(setupGrid, "START RMS", _startText, 1, 0);
        _endText = Field("6.0", rightMargin: false);
        AddField(setupGrid, "END RMS", _endText, 1, 2);
        _stepText = Field("0.25");
        AddField(setupGrid, "STEP RMS", _stepText, 2, 0);
        _dwellText = Field("10", rightMargin: false);
        AddField(setupGrid, "DWELL / PULSE ms", _dwellText, 2, 2);
        _baselineText = Field("1.0");
        AddField(setupGrid, "BASELINE RMS", _baselineText, 3, 0);
        _resetText = Field("20", rightMargin: false);
        AddField(setupGrid, "RESET / POST ms", _resetText, 3, 2);

        _feedbackCombo = Combo(Enum.GetValues<RealTestFeedback>(), RealTestFeedback.Pickup, new Thickness(8, 3, 16, 3));
        AddField(setupGrid, "STOP / ADVANCE", _feedbackCombo, 4, 0);
        _faultPresetCombo = Combo(VirtualInjectionPresets.Names, "Three-phase fault", new Thickness(8, 3, 0, 3));
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
        Closing += WorkflowWindow_Closing;
        Closed += (_, _) => _cancellation?.Cancel();
        RefreshWorkflowFields();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        _workflowRunning = true;
        _runButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _resultsText.Text = "Running deterministic closed-loop workflow…";

        try
        {
            var request = CaptureRequest();
            var token = _cancellation.Token;
            var result = await Task.Run(() => RunRequest(request, token), token);
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
            _workflowRunning = false;
            _cancelButton.IsEnabled = false;
            _runButton.IsEnabled = true;
            if (_closeAfterRun)
            {
                _closeAfterRun = false;
                Dispatcher.BeginInvoke(new Action(Close));
            }
        }
    }

    private void WorkflowWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_workflowRunning)
            return;

        e.Cancel = true;
        _closeAfterRun = true;
        _cancellation?.Cancel();
        _resultsText.Text = "Cancelling active workflow and de-energizing the virtual source…";
    }

    private WorkflowRequest CaptureRequest()
    {
        var workflowIndex = _workflowCombo.SelectedIndex;
        var sequence = workflowIndex == 3;
        var pulse = workflowIndex == 1;
        return new WorkflowRequest(
            workflowIndex,
            (VirtualInjectionSignal)(_signalCombo.SelectedItem ?? VirtualInjectionSignal.PhaseACurrent),
            (RealTestFeedback)(_feedbackCombo.SelectedItem ?? RealTestFeedback.Pickup),
            sequence ? 0 : Number(_startText.Text, "Start RMS"),
            sequence ? 0 : Number(_endText.Text, "End RMS"),
            sequence ? 1 : Number(_stepText.Text, "Step RMS"),
            Milliseconds(_dwellText.Text, "Dwell / pulse"),
            pulse ? Number(_baselineText.Text, "Baseline RMS") : 0,
            pulse || sequence ? Milliseconds(_resetText.Text, "Reset / post") : TimeSpan.FromMilliseconds(20),
            _faultPresetCombo.SelectedItem as string ?? "Three-phase fault",
            _source.ActiveProfile);
    }

    private object RunRequest(WorkflowRequest request, CancellationToken token)
        => request.WorkflowIndex switch
        {
            0 => _engine.RunRamp(new StepRampDefinition("Operator step ramp", request.Signal, request.Start, request.End, request.Step, request.Dwell, request.Feedback), token),
            1 => _engine.RunPulseRamp(new PulseRampDefinition("Operator pulse ramp", request.Signal, request.Baseline, request.Start, request.End, request.Step, request.Dwell, request.Reset, request.Feedback), token),
            2 => _engine.RunPickupDropoutSearch(new PickupDropoutSearchDefinition("Operator pickup/dropout search", request.Signal, Math.Min(request.Start, request.End), Math.Max(request.Start, request.End), request.Step, request.Dwell), token),
            _ => _engine.RunStateSequence(BuildStateSequence(request), token)
        };

    private static StateSequenceDefinition BuildStateSequence(WorkflowRequest request)
    {
        var context = request.SourceContext;
        var normal = (VirtualInjectionPresets.Create("Normal balanced", context.FrequencyHz) with
        {
            CurrentTransformer = context.CurrentTransformer
        }).Normalize();
        var fault = (VirtualInjectionPresets.Create(request.FaultPreset, context.FrequencyHz) with
        {
            CurrentTransformer = context.CurrentTransformer
        }).Normalize();
        return new StateSequenceDefinition(
            "Operator state sequence",
            new[]
            {
                new RealTestState("Pre-fault", normal, TimeSpan.FromMilliseconds(100)),
                new RealTestState("Fault", fault, request.Dwell, request.Feedback),
                new RealTestState("Post-fault", normal, request.Reset)
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

    private static ComboBox Combo<T>(IEnumerable<T> values, object selected, Thickness margin)
    {
        var combo = new ComboBox
        {
            ItemsSource = values,
            Height = 30,
            Margin = margin
        };
        if (selected is int index)
            combo.SelectedIndex = index;
        else
            combo.SelectedItem = selected;
        return combo;
    }

    private static TextBox Field(string text, bool rightMargin = true)
        => new()
        {
            Text = text,
            Height = 30,
            Margin = rightMargin ? new Thickness(8, 3, 16, 3) : new Thickness(8, 3, 0, 3),
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
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException($"{name} must be a finite number using '.' as decimal separator.");
        return value;
    }

    private static TimeSpan Milliseconds(string text, string name)
    {
        var value = Number(text, name);
        if (value <= 0)
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

    private sealed record WorkflowRequest(
        int WorkflowIndex,
        VirtualInjectionSignal Signal,
        RealTestFeedback Feedback,
        double Start,
        double End,
        double Step,
        TimeSpan Dwell,
        double Baseline,
        TimeSpan Reset,
        string FaultPreset,
        VirtualInjectionProfile SourceContext);
}
