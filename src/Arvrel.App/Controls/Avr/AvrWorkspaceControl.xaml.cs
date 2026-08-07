using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl : UserControl
{
    private static readonly Brush LedOffBrush = Freeze(new SolidColorBrush(Color.FromRgb(81, 96, 106)));
    private static readonly Brush HealthyBrush = Freeze(new SolidColorBrush(Color.FromRgb(63, 142, 88)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(180, 122, 34)));
    private static readonly Brush AlarmBrush = Freeze(new SolidColorBrush(Color.FromRgb(184, 73, 69)));
    private static readonly Brush HealthyBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(234, 245, 236)));
    private static readonly Brush WarningBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(251, 242, 227)));
    private static readonly Brush AlarmBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(249, 232, 231)));

    private AvrSettings _settings = new();
    private readonly AvrSimulationEngine _engine = new(new AvrSettings());
    private AvrSnapshot _snapshot = AvrSnapshot.Ready(new AvrSettings(), 0);
    private readonly DispatcherTimer _timer;
    private readonly Queue<string> _events = new();
    private bool _running;
    private int _lastTapPosition;
    private AvrControlState _lastState = AvrControlState.InBand;

    public AvrWorkspaceControl()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        Loaded += AvrWorkspaceControl_Loaded;
        Unloaded += AvrWorkspaceControl_Unloaded;
        AddEvent("READY", "AVR simulator initialized");
    }

    public event EventHandler? RunStateChanged;

    public bool IsRunning => _running;
    public AvrSettings Settings => _settings;
    public AvrSnapshot Snapshot => _snapshot;

    public void ToggleRun()
    {
        _running = !_running;
        AddEvent(_running ? "RUN" : "PAUSE", _running ? "Automatic voltage regulation started" : "AVR simulation paused");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    public void ResetSimulator()
    {
        _running = false;
        _engine.Reset();
        SourceVoltageSlider.Value = _settings.NominalVoltageV;
        _snapshot = _engine.Advance(TimeSpan.Zero, SourceVoltageSlider.Value);
        _lastTapPosition = _snapshot.TapPosition;
        _lastState = _snapshot.State;
        AddEvent("RESET", "AVR mode, timer, tap position and operation counter reset");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderSnapshot(_snapshot);
    }

    public void FocusConfiguration()
    {
        SetpointBox.Focus();
        SetpointBox.SelectAll();
    }

    private void AvrWorkspaceControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_timer.IsEnabled)
            _timer.Start();

        _snapshot = _engine.Advance(TimeSpan.Zero, SourceVoltageSlider.Value);
        _lastTapPosition = _snapshot.TapPosition;
        _lastState = _snapshot.State;
        RenderSnapshot(_snapshot);
    }

    private void AvrWorkspaceControl_Unloaded(object sender, RoutedEventArgs e)
        => _timer.Stop();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_running || !IsVisible)
            return;

        _snapshot = _engine.Advance(_timer.Interval, SourceVoltageSlider.Value);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void SourceLow_Click(object sender, RoutedEventArgs e)
        => SourceVoltageSlider.Value = _settings.NominalVoltageV * 0.95;

    private void SourceNominal_Click(object sender, RoutedEventArgs e)
        => SourceVoltageSlider.Value = _settings.NominalVoltageV;

    private void SourceHigh_Click(object sender, RoutedEventArgs e)
        => SourceVoltageSlider.Value = _settings.NominalVoltageV * 1.05;

    private void SourceVoltageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SourceVoltageText is not null)
            SourceVoltageText.Text = $"{e.NewValue:0.00} V";

        if (!IsLoaded)
            return;

        RenderCurrent();
    }

    private void ApplySettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = _settings with
            {
                SetpointVoltageV = ParseDouble(SetpointBox, "Setpoint"),
                TolerancePercent = ParseDouble(ToleranceBox, "Tolerance"),
                T1Seconds = ParseDouble(T1Box, "T1 delay"),
                T2Seconds = ParseDouble(T2Box, "T2 delay"),
                MinimumTap = ParseInt(MinTapBox, "Minimum tap"),
                MaximumTap = ParseInt(MaxTapBox, "Maximum tap"),
                TapStepPercent = ParseDouble(TapStepBox, "Tap step"),
                FastT2Enabled = FastT2Check.IsChecked == true,
                LineDropCompensationEnabled = LdcCheck.IsChecked == true,
                LineDropCompensationPercent = ParseDouble(LdcBox, "Line-drop compensation")
            };

            settings.Validate();
            _settings = settings;
            _engine.ApplySettings(settings, keepTapPosition: true);
            AddEvent("SETTINGS", $"Uset {settings.SetpointVoltageV:0.##} V · ±{settings.TolerancePercent:0.##}% · T1 {settings.T1Seconds:0.##} s");
            RenderCurrent();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            var owner = Window.GetWindow(this);
            if (owner is null)
                MessageBox.Show(ex.Message, "AVR settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                MessageBox.Show(owner, ex.Message, "AVR settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AutoMode_Click(object sender, RoutedEventArgs e)
    {
        _engine.SetMode(AvrOperatingMode.Automatic);
        AddEvent("MODE", "AUTO selected");
        RenderCurrent();
    }

    private void ManualMode_Click(object sender, RoutedEventArgs e)
    {
        _engine.SetMode(AvrOperatingMode.Manual);
        AddEvent("MODE", "MANUAL selected · automatic tap commands inhibited");
        RenderCurrent();
    }

    private void RaiseTap_Click(object sender, RoutedEventArgs e)
        => ExecuteManualTap(+1);

    private void LowerTap_Click(object sender, RoutedEventArgs e)
        => ExecuteManualTap(-1);

    private void ExecuteManualTap(int direction)
    {
        if (_engine.Mode != AvrOperatingMode.Manual)
        {
            AddEvent("MAN REQ", "Select MANUAL before direct tap operation");
            RenderCurrent();
            return;
        }

        var moved = direction > 0 ? _engine.RaiseTap() : _engine.LowerTap();
        AddEvent(
            moved ? "TAP" : "LIMIT",
            moved
                ? $"Manual {(direction > 0 ? "RAISE" : "LOWER")} · tap {_engine.TapPosition:+0;-0;0}"
                : direction > 0 ? "Maximum tap reached" : "Minimum tap reached");
        if (moved)
            _lastTapPosition = _engine.TapPosition;
        RenderCurrent();
    }

    private void RenderCurrent()
    {
        _snapshot = _engine.Advance(TimeSpan.Zero, SourceVoltageSlider.Value);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void ObserveTransition(AvrSnapshot snapshot)
    {
        if (snapshot.TapPosition != _lastTapPosition)
        {
            AddEvent(
                snapshot.RaiseOutput ? "RAISE" : snapshot.LowerOutput ? "LOWER" : "TAP",
                $"Tap changed {_lastTapPosition:+0;-0;0} → {snapshot.TapPosition:+0;-0;0} · U {snapshot.MeasuredVoltageV:0.00} V");
            _lastTapPosition = snapshot.TapPosition;
        }

        if (snapshot.State != _lastState)
        {
            if (snapshot.State is AvrControlState.Blocked or AvrControlState.TapLimit)
                AddEvent(snapshot.State == AvrControlState.Blocked ? "BLOCK" : "LIMIT", snapshot.Reason);
            _lastState = snapshot.State;
        }
    }

    private void RenderSnapshot(AvrSnapshot snapshot)
    {
        SourceVoltageText.Text = $"{snapshot.SourceVoltageV:0.00} V";
        MeasuredVoltageText.Text = $"{snapshot.MeasuredVoltageV:0.00} V";
        SetpointValueText.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V";
        DeviationValueText.Text = $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %";
        TapValueText.Text = $"{snapshot.TapPosition:+0;-0;0}";
        OperationCountText.Text = $"OPS {snapshot.OperationCount}";
        ProfileText.Text = _settings.ProfileName;

        DelayText.Text = snapshot.DelayTargetSeconds > 0
            ? $"{snapshot.DelayElapsedSeconds:0.0} / {snapshot.DelayTargetSeconds:0.0} s"
            : "—";
        DelayProgress.Value = snapshot.DelayTargetSeconds > 0
            ? Math.Clamp(snapshot.DelayElapsedSeconds / snapshot.DelayTargetSeconds * 100.0, 0, 100)
            : 0;

        LcdModeText.Text = $"{(snapshot.Mode == AvrOperatingMode.Automatic ? "AUTO" : "MAN")} · {StateLabel(snapshot.State)}";
        LcdMeasuredText.Text = $"{snapshot.MeasuredVoltageV:0.00} V";
        LcdSetpointText.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V";
        LcdDeviationText.Text = $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %";
        LcdTapText.Text = $"{snapshot.TapPosition:+0;-0;0}";
        LcdReasonText.Text = snapshot.Reason;

        StateBadgeText.Text = StateLabel(snapshot.State).ToUpperInvariant();
        var stateBrush = snapshot.State switch
        {
            AvrControlState.Blocked => AlarmBrush,
            AvrControlState.TapLimit or AvrControlState.TimingRaise or AvrControlState.TimingLower => WarningBrush,
            _ => HealthyBrush
        };
        StateBadgeText.Foreground = stateBrush;
        StateBadge.Background = snapshot.State switch
        {
            AvrControlState.Blocked => AlarmBadgeBrush,
            AvrControlState.TapLimit or AvrControlState.TimingRaise or AvrControlState.TimingLower => WarningBadgeBrush,
            _ => HealthyBadgeBrush
        };
        StateBadge.BorderBrush = stateBrush;

        AutoLed.Fill = snapshot.Mode == AvrOperatingMode.Automatic ? HealthyBrush : LedOffBrush;
        RaiseLed.Fill = snapshot.State == AvrControlState.TimingRaise || snapshot.RaiseOutput ? WarningBrush : LedOffBrush;
        LowerLed.Fill = snapshot.State == AvrControlState.TimingLower || snapshot.LowerOutput ? WarningBrush : LedOffBrush;
        BlockLed.Fill = snapshot.Blocked ? AlarmBrush : LedOffBrush;
        LimitLed.Fill = snapshot.State == AvrControlState.TapLimit ? WarningBrush : LedOffBrush;

        AutoModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Automatic ? FontWeights.SemiBold : FontWeights.Normal;
        ManualModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Manual ? FontWeights.SemiBold : FontWeights.Normal;
        EventTraceText.Text = string.Join(Environment.NewLine, _events);
    }

    private void AddEvent(string code, string message)
    {
        _events.Enqueue($"{DateTime.Now:HH:mm:ss}  {code,-8} {message}");
        while (_events.Count > 8)
            _events.Dequeue();

        if (EventTraceText is not null)
            EventTraceText.Text = string.Join(Environment.NewLine, _events);
    }

    private static string StateLabel(AvrControlState state) => state switch
    {
        AvrControlState.InBand => "In band",
        AvrControlState.TimingRaise => "Timing raise",
        AvrControlState.TimingLower => "Timing lower",
        AvrControlState.TapLimit => "Tap limit",
        AvrControlState.Blocked => "Blocked",
        AvrControlState.Manual => "Manual",
        _ => state.ToString()
    };

    private static double ParseDouble(TextBox box, string label)
    {
        if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return value;

        throw new FormatException($"{label} is not a valid number.");
    }

    private static int ParseInt(TextBox box, string label)
    {
        if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) ||
            int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return value;

        throw new FormatException($"{label} is not a valid integer.");
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
