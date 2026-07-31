using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Arvrel.App.Infrastructure;
using Arvrel.App.Services;
using Arvrel.Protection;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class MainWindow : Window
{
    private static readonly Brush LedOffBrush = Freeze(new SolidColorBrush(Color.FromRgb(81, 96, 106)));
    private static readonly Brush HealthyBrush = Freeze(new SolidColorBrush(Color.FromRgb(70, 154, 88)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(196, 139, 43)));
    private static readonly Brush TripBrush = Freeze(new SolidColorBrush(Color.FromRgb(199, 71, 67)));
    private static readonly Brush PhaseBrush = Freeze(new SolidColorBrush(Color.FromRgb(65, 139, 191)));

    private readonly ProtectionSettings _settings = new();
    private readonly ProtectionEngine _engine;
    private readonly DeterministicLabScenario _scenario = new();
    private readonly DispatcherTimer _timer;
    private readonly Queue<string> _events = new();
    private ProtectionSnapshot _snapshot;
    private bool _running;
    private bool _lastPickup;
    private bool _lastTrip;
    private bool _lastBlocked;
    private double _pickupPosition = double.NaN;
    private double _tripPosition = double.NaN;

    public MainWindow()
    {
        InitializeComponent();
        _engine = new ProtectionEngine(_settings);
        _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += Timer_Tick;
        EngineModeText.Text = SiblingEngineStatus.Label;
        AddEvent("READY", "Protection engine initialized");
        AddEvent("HEALTH", "SMV trust guard permits trip");
        RenderInitialFrame();
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _running = !_running;
        RunButton.Content = _running ? "Pause lab" : "Run lab";
        StatusText.Text = _running
            ? "Laboratory running. Inject A-G fault to observe pickup, timing and virtual trip."
            : "Laboratory paused; the two-cycle evidence window remains stationary.";
        if (_running)
            _timer.Start();
        else
            _timer.Stop();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        // Evaluate protection with 5 ms frames while rendering UI at 25 fps.
        ScenarioStep? last = null;
        for (var index = 0; index < 8; index++)
        {
            last = _scenario.Advance(TimeSpan.FromMilliseconds(5), _pickupPosition, _tripPosition);
            _snapshot = _engine.Evaluate(last.Measurement);
            ObserveTransitions(_snapshot);
        }

        if (last is not null)
            Render(last, _snapshot);
    }

    private void InjectFault_Click(object sender, RoutedEventArgs e)
    {
        _scenario.FaultActive = !_scenario.FaultActive;
        AddEvent(_scenario.FaultActive ? "FAULT" : "CLEAR", _scenario.FaultActive ? "A-G fault scenario injected" : "Fault scenario removed");
        StatusText.Text = _scenario.FaultActive
            ? "A-G fault active. Phase A and residual current are ramping into the protection elements."
            : "Fault removed. Observe dropout and keep the latched trip until reset.";
        if (!_running)
            RunButton_Click(RunButton, new RoutedEventArgs());
    }

    private void DegradeSmv_Click(object sender, RoutedEventArgs e)
    {
        _scenario.SmvDegraded = !_scenario.SmvDegraded;
        AddEvent(_scenario.SmvDegraded ? "SMV WARN" : "SMV GOOD", _scenario.SmvDegraded ? "smpCnt discontinuity: trip permission removed" : "SMV trust restored");
        StatusText.Text = _scenario.SmvDegraded
            ? "SMV degraded. Measurement and pickup stay visible, but any new trip request is blocked."
            : "SMV trust restored; trip permission is available again.";
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _engine.Reset();
        _scenario.Reset();
        _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        _pickupPosition = double.NaN;
        _tripPosition = double.NaN;
        _lastPickup = false;
        _lastTrip = false;
        _lastBlocked = false;
        AddEvent("RESET", "Trip latch, timers and scenario reset");
        RenderInitialFrame();
        StatusText.Text = "Reset complete. Laboratory returned to healthy nominal current.";
    }

    private void OpenAlgorithmEditor_Click(object sender, RoutedEventArgs e)
    {
        new AlgorithmEditorWindow { Owner = this }.ShowDialog();
    }

    private void ImportScl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select IEC 61850 SCL file",
            Filter = "IEC 61850 SCL (*.scd;*.cid;*.icd;*.iid)|*.scd;*.cid;*.icd;*.iid|XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        AddEvent("SCL", $"Selected {System.IO.Path.GetFileName(dialog.FileName)}");
        StatusText.Text = SiblingEngineStatus.IsAvailable
            ? "SCL selected. P1 binding adapter can now be connected to the sibling ARIEC61850 parser."
            : "SCL selected, but sibling ARIEC61850 was not found at build time; P0 does not parse the file.";
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || SourceCombo.SelectedIndex == 0)
            return;

        var requested = SourceCombo.SelectedIndex == 1 ? "Live Npcap subscriber" : "PCAP replay";
        if (!SiblingEngineStatus.IsAvailable)
        {
            SourceCombo.SelectedIndex = 0;
            MessageBox.Show(
                this,
                $"{requested} requires the sibling ARIEC61850 repository and the P1 subscriber adapter.\n\nExpected layout:\nGit\\ARIEC61850\nGit\\arvrel",
                "ARVREL source boundary",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SourceCombo.SelectedIndex = 0;
        MessageBox.Show(
            this,
            $"The sibling engine is available, but {requested} remains intentionally disabled in this P0 extraction. The integration seam is preserved for P1.",
            "ARVREL P0",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ObserveTransitions(ProtectionSnapshot snapshot)
    {
        var pickup = snapshot.Phase50.Pickup || snapshot.Phase51.Pickup || snapshot.Earth50.Pickup || snapshot.Earth51.Pickup;
        if (pickup && !_lastPickup)
        {
            _pickupPosition = 0.58;
            AddEvent("PICKUP", snapshot.ActiveElement);
        }
        if (snapshot.TripLatched && !_lastTrip)
        {
            _tripPosition = 0.76;
            AddEvent("TRIP", snapshot.ActiveElement);
        }
        if (snapshot.Blocked && !_lastBlocked)
            AddEvent("BLOCK", snapshot.SmvTrust.Code);

        _lastPickup = pickup;
        _lastTrip = snapshot.TripLatched;
        _lastBlocked = snapshot.Blocked;
    }

    private void RenderInitialFrame()
    {
        var step = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition);
        Render(step, _snapshot);
    }

    private void Render(ScenarioStep step, ProtectionSnapshot snapshot)
    {
        SmvScope.Frame = step.Waveform with { PickupPosition = _pickupPosition, TripPosition = _tripPosition };
        IaValueText.Text = FormatCurrent(step.Measurement.PhaseA);
        IbValueText.Text = FormatCurrent(step.Measurement.PhaseB);
        IcValueText.Text = FormatCurrent(step.Measurement.PhaseC);
        ResidualValueText.Text = FormatCurrent(step.Measurement.Residual);
        LcdIaText.Text = IaValueText.Text;
        LcdIbText.Text = IbValueText.Text;
        LcdIcText.Text = IcValueText.Text;
        LcdResidualText.Text = ResidualValueText.Text;
        SampleCounterText.Text = $"  ·  smpCnt {_scenario.SampleCounter:0000}";

        SetElement(Phase50StateText, Phase50Progress, snapshot.Phase50);
        SetElement(Phase51StateText, Phase51Progress, snapshot.Phase51);
        SetElement(Earth50StateText, Earth50Progress, snapshot.Earth50);
        SetElement(Earth51StateText, Earth51Progress, snapshot.Earth51);

        ProtectionReasonText.Text = $"  ·  {snapshot.DecisionReason}";
        var permitted = snapshot.SmvTrust.AllowsTrip;
        PermissionText.Text = permitted ? "TRIP PERMITTED" : "TRIP BLOCKED";
        PermissionText.Foreground = permitted ? HealthyBrush : WarningBrush;
        PermissionBadge.Background = permitted ? BrushFrom("#EAF5EC") : BrushFrom("#FBF2E3");
        PermissionBadge.BorderBrush = permitted ? BrushFrom("#B9D8BF") : BrushFrom("#E2C58F");

        HealthyLed.Fill = snapshot.SmvTrust.AllowsMeasurement ? HealthyBrush : WarningBrush;
        TopHealthLed.Fill = snapshot.SmvTrust.AllowsTrip ? HealthyBrush : WarningBrush;
        TopHealthText.Text = snapshot.SmvTrust.AllowsTrip ? "LAB READY" : "TRIP BLOCKED";
        PickupLed.Fill = _lastPickup ? WarningBrush : LedOffBrush;
        TripLed.Fill = snapshot.TripLatched ? TripBrush : LedOffBrush;
        PhaseALed.Fill = snapshot.PhaseAPickup ? PhaseBrush : LedOffBrush;
        PhaseBLed.Fill = snapshot.PhaseBPickup ? PhaseBrush : LedOffBrush;
        PhaseCLed.Fill = snapshot.PhaseCPickup ? PhaseBrush : LedOffBrush;
        EarthLed.Fill = snapshot.EarthPickup ? WarningBrush : LedOffBrush;
        BlockLed.Fill = snapshot.Blocked || !snapshot.SmvTrust.AllowsTrip ? WarningBrush : LedOffBrush;

        LcdStatusText.Text = snapshot.Blocked
            ? $"TRIP BLOCKED\n{snapshot.SmvTrust.Code}"
            : snapshot.TripLatched
                ? $"TRIP LATCHED\n{snapshot.ActiveElement}"
                : _lastPickup
                    ? $"PICKUP\n{snapshot.ActiveElement}"
                    : "READY · TRIP PERMITTED";
        RelayFooterText.Text = snapshot.SmvTrust.AllowsTrip ? "SMV health gate active" : snapshot.SmvTrust.Detail;
    }

    private static void SetElement(TextBlock stateText, System.Windows.Controls.Primitives.RangeBase progress, ElementSnapshot element)
    {
        stateText.Text = element.State.ToString().ToUpperInvariant();
        stateText.Foreground = element.State switch
        {
            ProtectionStageState.Operated => TripBrush,
            ProtectionStageState.Blocked => WarningBrush,
            ProtectionStageState.Timing or ProtectionStageState.Pickup => WarningBrush,
            _ => BrushFrom("#657586")
        };
        progress.Value = element.Progress * 100;
    }

    private void AddEvent(string code, string detail)
    {
        _events.Enqueue($"{code,-11}{detail}");
        while (_events.Count > 5)
            _events.Dequeue();
        EventTraceText.Text = string.Join(Environment.NewLine, _events.Reverse());
    }

    private static string FormatCurrent(double value) => value.ToString("0.00 'A'", CultureInfo.InvariantCulture);
    private static Brush BrushFrom(string value) => Freeze((SolidColorBrush)new BrushConverter().ConvertFromString(value)!);

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
