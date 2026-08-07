using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl : UserControl
{
    private sealed record InjectionStep(string Label, double TargetPercent, double HoldSeconds);

    private static readonly Brush LedOffBrush = Freeze(new SolidColorBrush(Color.FromRgb(81, 96, 106)));
    private static readonly Brush HealthyBrush = Freeze(new SolidColorBrush(Color.FromRgb(102, 200, 120)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(240, 182, 83)));
    private static readonly Brush AlarmBrush = Freeze(new SolidColorBrush(Color.FromRgb(234, 107, 103)));
    private static readonly Brush HealthyBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(27, 57, 38)));
    private static readonly Brush WarningBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(73, 55, 23)));
    private static readonly Brush AlarmBadgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(73, 31, 31)));

    private AvrSettings _settings = new();
    private readonly AvrSimulationEngine _engine = new(new AvrSettings());
    private AvrSnapshot _snapshot = AvrSnapshot.Ready(new AvrSettings(), 0);
    private readonly DispatcherTimer _timer;
    private readonly Queue<string> _events = new();
    private readonly List<InjectionStep> _scenarioSteps = [];

    private bool _running;
    private bool _configExpanded = true;
    private bool _updatingInjectionUi;
    private int _lastTapPosition;
    private int _scenarioIndex = -1;
    private AvrControlState _lastState = AvrControlState.InBand;
    private double _scenarioElapsedSeconds;
    private double _preDelayRemainingSeconds;
    private double _injectedVoltageV = 100.0;
    private double _injectionTargetVoltageV = 100.0;
    private double _frequencyHz = 50.0;
    private double _phaseDegrees;
    private double _rampVPerSecond = 20.0;
    private double _preDelaySeconds = 0.5;
    private double _dwellSeconds = 12.0;

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
        PopulateSettingsUi();
        PopulateManualSequence();
        AddEvent("READY", "AVR bench simulator initialized");
    }

    public event EventHandler? RunStateChanged;

    public bool IsRunning => _running;
    public AvrSettings Settings => _settings;
    public AvrSnapshot Snapshot => _snapshot;

    public void ToggleRun()
    {
        if (_running)
            PauseInjection();
        else
            StartInjection();
    }

    public void ResetSimulator()
    {
        _running = false;
        _engine.Reset();
        _scenarioSteps.Clear();
        _scenarioIndex = -1;
        _scenarioElapsedSeconds = 0;
        _preDelayRemainingSeconds = 0;
        _injectedVoltageV = _settings.NominalVoltageV;
        _injectionTargetVoltageV = _settings.NominalVoltageV;
        SetInjectionSlider(_injectionTargetVoltageV);
        InjectionVoltageBox.Text = _injectionTargetVoltageV.ToString("0.00", CultureInfo.InvariantCulture);
        TestProfileCombo.SelectedIndex = 0;
        PopulateManualSequence();
        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV);
        _lastTapPosition = _snapshot.TapPosition;
        _lastState = _snapshot.State;
        AddEvent("RESET", "AVR, test sequence, timer and tap position reset");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderSnapshot(_snapshot);
    }

    public void FocusConfiguration()
    {
        SetConfigurationExpanded(true);
        ConfigurationTabs.SelectedIndex = 0;
        SetpointBox.Focus();
        SetpointBox.SelectAll();
    }

    private void AvrWorkspaceControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_timer.IsEnabled)
            _timer.Start();

        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV);
        _lastTapPosition = _snapshot.TapPosition;
        _lastState = _snapshot.State;
        RenderSnapshot(_snapshot);
    }

    private void AvrWorkspaceControl_Unloaded(object sender, RoutedEventArgs e)
        => _timer.Stop();

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
            return;

        if (_running)
            AdvanceInjection(_timer.Interval.TotalSeconds);

        var elapsed = _running ? _timer.Interval : TimeSpan.Zero;
        _snapshot = _engine.Advance(elapsed, _injectedVoltageV);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void StartInjection_Click(object sender, RoutedEventArgs e)
        => StartInjection();

    private void PauseInjection_Click(object sender, RoutedEventArgs e)
        => PauseInjection();

    private void StopInjection_Click(object sender, RoutedEventArgs e)
    {
        if (!_running && _scenarioIndex < 0)
            return;

        _running = false;
        _scenarioIndex = -1;
        _scenarioElapsedSeconds = 0;
        _preDelayRemainingSeconds = 0;
        AddEvent("STOP", "Injection stopped; last source value retained");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    private void StartInjection()
    {
        if (!TryApplyInjectionForm(showError: true))
            return;

        if (_scenarioSteps.Count == 0 && TestProfileCombo.SelectedIndex > 0)
            LoadScenarioForProfile(TestProfileCombo.SelectedIndex);

        if (_scenarioSteps.Count > 0)
        {
            _scenarioIndex = 0;
            _scenarioElapsedSeconds = 0;
            _preDelayRemainingSeconds = _preDelaySeconds;
            ApplyScenarioTarget(_scenarioSteps[0]);
        }
        else
        {
            _scenarioIndex = -1;
            _preDelayRemainingSeconds = _preDelaySeconds;
        }

        _running = true;
        AddEvent(
            "INJECT",
            $"Started · target {_injectionTargetVoltageV:0.00} V · {_frequencyHz:0.00} Hz · ramp {_rampVPerSecond:0.##} V/s");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    private void PauseInjection()
    {
        if (!_running)
            return;

        _running = false;
        AddEvent("PAUSE", $"Injection paused at {_injectedVoltageV:0.00} V");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    private void AdvanceInjection(double elapsedSeconds)
    {
        if (_preDelayRemainingSeconds > 0)
        {
            _preDelayRemainingSeconds = Math.Max(0, _preDelayRemainingSeconds - elapsedSeconds);
            return;
        }

        _injectedVoltageV = MoveToward(
            _injectedVoltageV,
            _injectionTargetVoltageV,
            Math.Max(0.01, _rampVPerSecond) * elapsedSeconds);

        if (_scenarioIndex < 0 || _scenarioIndex >= _scenarioSteps.Count)
            return;

        var step = _scenarioSteps[_scenarioIndex];
        if (Math.Abs(_injectedVoltageV - _injectionTargetVoltageV) > 0.005)
            return;

        _scenarioElapsedSeconds += elapsedSeconds;
        if (_scenarioElapsedSeconds + 1e-9 < step.HoldSeconds)
            return;

        _scenarioIndex++;
        _scenarioElapsedSeconds = 0;
        if (_scenarioIndex >= _scenarioSteps.Count)
        {
            _scenarioIndex = _scenarioSteps.Count - 1;
            _running = false;
            AddEvent("COMPLETE", "Injection sequence completed");
            RunStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        ApplyScenarioTarget(_scenarioSteps[_scenarioIndex]);
        AddEvent("STEP", $"{_scenarioSteps[_scenarioIndex].Label} · target {_injectionTargetVoltageV:0.00} V");
    }

    private void InjectionForm_LostFocus(object sender, RoutedEventArgs e)
        => TryApplyInjectionForm(showError: false);

    private bool TryApplyInjectionForm(bool showError)
    {
        try
        {
            var voltage = ParseDouble(InjectionVoltageBox, "Injected voltage");
            var frequency = ParseDouble(FrequencyBox, "Frequency");
            var phase = ParseDouble(PhaseBox, "Phase");
            var ramp = ParseDouble(RampBox, "Ramp rate");
            var preDelay = ParseDouble(DelayBox, "Pre-delay");
            var dwell = ParseDouble(DwellBox, "Dwell");

            if (voltage < 1 || voltage > 200)
                throw new ArgumentOutOfRangeException(nameof(voltage), "Injected voltage must be between 1 V and 200 V.");
            if (frequency < 40 || frequency > 70)
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be between 40 Hz and 70 Hz.");
            if (phase < -360 || phase > 360)
                throw new ArgumentOutOfRangeException(nameof(phase), "Phase must be between -360° and +360°.");
            if (ramp <= 0 || ramp > 500)
                throw new ArgumentOutOfRangeException(nameof(ramp), "Ramp rate must be greater than 0 and no more than 500 V/s.");
            if (preDelay < 0 || preDelay > 120)
                throw new ArgumentOutOfRangeException(nameof(preDelay), "Pre-delay must be between 0 and 120 seconds.");
            if (dwell <= 0 || dwell > 600)
                throw new ArgumentOutOfRangeException(nameof(dwell), "Dwell must be greater than 0 and no more than 600 seconds.");

            _injectionTargetVoltageV = voltage;
            _frequencyHz = frequency;
            _phaseDegrees = phase;
            _rampVPerSecond = ramp;
            _preDelaySeconds = preDelay;
            _dwellSeconds = dwell;
            SetInjectionSlider(Math.Clamp(voltage, SourceVoltageSlider.Minimum, SourceVoltageSlider.Maximum));

            if (!_running && _scenarioSteps.Count == 0)
                _injectedVoltageV = voltage;

            RenderCurrent();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            if (showError)
                ShowWarning(ex.Message, "AVR injection form");
            return false;
        }
    }

    private void SourceVoltageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingInjectionUi)
            return;

        _injectionTargetVoltageV = e.NewValue;
        if (InjectionVoltageBox is not null)
            InjectionVoltageBox.Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture);
        if (!_running && _scenarioSteps.Count == 0)
            _injectedVoltageV = e.NewValue;

        if (InjectionTargetText is not null)
            InjectionTargetText.Text = $"{_injectionTargetVoltageV:0.00} V";

        if (IsLoaded)
            RenderCurrent();
    }

    private void ScenarioRaise_Click(object sender, RoutedEventArgs e)
    {
        TestProfileCombo.SelectedIndex = 1;
        LoadScenarioForProfile(1);
    }

    private void ScenarioLower_Click(object sender, RoutedEventArgs e)
    {
        TestProfileCombo.SelectedIndex = 2;
        LoadScenarioForProfile(2);
    }

    private void ScenarioBandSweep_Click(object sender, RoutedEventArgs e)
    {
        TestProfileCombo.SelectedIndex = 3;
        LoadScenarioForProfile(3);
    }

    private void ScenarioBlocking_Click(object sender, RoutedEventArgs e)
    {
        TestProfileCombo.SelectedIndex = 4;
        LoadScenarioForProfile(4);
    }

    private void LoadScenarioForProfile(int profileIndex)
    {
        _scenarioSteps.Clear();
        switch (profileIndex)
        {
            case 1:
                _scenarioSteps.Add(new("Baseline", 100, 2));
                _scenarioSteps.Add(new("Low U · expect RAISE", 95, _dwellSeconds));
                _scenarioSteps.Add(new("Return nominal", 100, 3));
                break;
            case 2:
                _scenarioSteps.Add(new("Baseline", 100, 2));
                _scenarioSteps.Add(new("High U · expect LOWER", 105, _dwellSeconds));
                _scenarioSteps.Add(new("Return nominal", 100, 3));
                break;
            case 3:
                _scenarioSteps.Add(new("Below band", 98, 4));
                _scenarioSteps.Add(new("Near lower band", 99.5, 4));
                _scenarioSteps.Add(new("Setpoint", 100, 4));
                _scenarioSteps.Add(new("Near upper band", 100.5, 4));
                _scenarioSteps.Add(new("Above band", 102, 4));
                break;
            case 4:
                _scenarioSteps.Add(new("Nominal", 100, 2));
                _scenarioSteps.Add(new("U< block", Math.Max(50, _settings.UndervoltageBlockPercent - 5), 4));
                _scenarioSteps.Add(new("Recover", 100, 3));
                _scenarioSteps.Add(new("U> block", Math.Min(150, _settings.OvervoltageBlockPercent + 5), 4));
                _scenarioSteps.Add(new("Recover", 100, 3));
                break;
            default:
                PopulateManualSequence();
                return;
        }

        _scenarioIndex = -1;
        _scenarioElapsedSeconds = 0;
        RenderScenarioList();
        AddEvent("PROFILE", $"Loaded {TestProfileCombo.Text}");
    }

    private void PopulateManualSequence()
    {
        _scenarioSteps.Clear();
        _scenarioIndex = -1;
        if (InjectionSequenceList is null)
            return;

        InjectionSequenceList.Items.Clear();
        InjectionSequenceList.Items.Add("MANUAL   Set U/f/phase and press Start injection");
        SequenceStepText.Text = "MANUAL";
    }

    private void RenderScenarioList()
    {
        InjectionSequenceList.Items.Clear();
        for (var i = 0; i < _scenarioSteps.Count; i++)
        {
            var step = _scenarioSteps[i];
            InjectionSequenceList.Items.Add($"{i + 1,2}  {step.Label,-22} {step.TargetPercent,6:0.0}%  {step.HoldSeconds,5:0.0}s");
        }

        SequenceStepText.Text = _scenarioSteps.Count == 0 ? "MANUAL" : $"{_scenarioSteps.Count} STEPS";
    }

    private void ApplyScenarioTarget(InjectionStep step)
    {
        _injectionTargetVoltageV = _settings.NominalVoltageV * (step.TargetPercent / 100.0);
        InjectionVoltageBox.Text = _injectionTargetVoltageV.ToString("0.00", CultureInfo.InvariantCulture);
        SetInjectionSlider(Math.Clamp(_injectionTargetVoltageV, SourceVoltageSlider.Minimum, SourceVoltageSlider.Maximum));
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
                UndervoltageBlockPercent = ParseDouble(UnderBlockBox, "Undervoltage blocking"),
                OvervoltageBlockPercent = ParseDouble(OverBlockBox, "Overvoltage blocking"),
                FastT2Enabled = FastT2Check.IsChecked == true,
                LineDropCompensationEnabled = LdcCheck.IsChecked == true,
                LineDropCompensationPercent = ParseDouble(LdcBox, "Line-drop compensation"),
                VoltageInputMode = ClosedLoopModeRadio.IsChecked == true
                    ? AvrVoltageInputMode.ClosedLoopPlant
                    : AvrVoltageInputMode.BenchInjection
            };

            settings.Validate();
            _settings = settings;
            _engine.ApplySettings(settings, keepTapPosition: true);
            AddEvent(
                "SETTINGS",
                $"Uset {settings.SetpointVoltageV:0.##} V · ±{settings.TolerancePercent:0.##}% · T1 {settings.T1Seconds:0.##} s · {InputModeLabel(settings.VoltageInputMode)}");
            RenderCurrent();
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            ShowWarning(ex.Message, "AVR settings");
        }
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        _settings = new AvrSettings();
        _engine.ApplySettings(_settings, keepTapPosition: false);
        PopulateSettingsUi();
        _scenarioSteps.Clear();
        TestProfileCombo.SelectedIndex = 0;
        PopulateManualSequence();
        AddEvent("DEFAULT", "AVR configuration restored to bench defaults");
        RenderCurrent();
    }

    private void PopulateSettingsUi()
    {
        if (SetpointBox is null)
            return;

        SetpointBox.Text = _settings.SetpointVoltageV.ToString("0.##", CultureInfo.InvariantCulture);
        ToleranceBox.Text = _settings.TolerancePercent.ToString("0.##", CultureInfo.InvariantCulture);
        T1Box.Text = _settings.T1Seconds.ToString("0.##", CultureInfo.InvariantCulture);
        T2Box.Text = _settings.T2Seconds.ToString("0.##", CultureInfo.InvariantCulture);
        MinTapBox.Text = _settings.MinimumTap.ToString(CultureInfo.InvariantCulture);
        MaxTapBox.Text = _settings.MaximumTap.ToString(CultureInfo.InvariantCulture);
        TapStepBox.Text = _settings.TapStepPercent.ToString("0.##", CultureInfo.InvariantCulture);
        UnderBlockBox.Text = _settings.UndervoltageBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        OverBlockBox.Text = _settings.OvervoltageBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        LdcBox.Text = _settings.LineDropCompensationPercent.ToString("0.##", CultureInfo.InvariantCulture);
        FastT2Check.IsChecked = _settings.FastT2Enabled;
        LdcCheck.IsChecked = _settings.LineDropCompensationEnabled;
        BenchModeRadio.IsChecked = _settings.VoltageInputMode == AvrVoltageInputMode.BenchInjection;
        ClosedLoopModeRadio.IsChecked = _settings.VoltageInputMode == AvrVoltageInputMode.ClosedLoopPlant;
        ProfileText.Text = _settings.ProfileName;
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

    private void DeviceFunction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var key = button.Tag?.ToString() ?? button.Content?.ToString() ?? "KEY";
        AddEvent("KEY", $"Front-panel {key} pressed");
        if (string.Equals(key, "MENU", StringComparison.Ordinal))
            FocusConfiguration();
    }

    private void ConfigurePanelToggle_Click(object sender, RoutedEventArgs e)
        => SetConfigurationExpanded(!_configExpanded);

    private void SetConfigurationExpanded(bool expanded)
    {
        _configExpanded = expanded;
        ConfigurationColumn.Width = new GridLength(expanded ? 365 : 38);
        ConfigurationExpanded.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ConfigurationRail.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderCurrent()
    {
        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void ObserveTransition(AvrSnapshot snapshot)
    {
        if (snapshot.TapPosition != _lastTapPosition)
        {
            AddEvent(
                snapshot.RaiseOutput ? "RAISE" : snapshot.LowerOutput ? "LOWER" : "TAP",
                $"Tap {_lastTapPosition:+0;-0;0} → {snapshot.TapPosition:+0;-0;0} · injected U {snapshot.SourceVoltageV:0.00} V");
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

        InjectionTargetText.Text = $"{_injectionTargetVoltageV:0.00} V";
        InjectedVoltageText.Text = $"U = {_injectedVoltageV:0.00} V";
        InjectionStatusText.Text = InjectionStatusLabel();
        InjectionStatusText.Foreground = _running ? HealthyBrush : LedOffBrush;
        InjectionLed.Fill = _running ? HealthyBrush : LedOffBrush;
        InjectionStartButton.IsEnabled = !_running;
        InjectionPauseButton.IsEnabled = _running;

        if (_scenarioSteps.Count > 0 && _scenarioIndex >= 0 && _scenarioIndex < _scenarioSteps.Count)
        {
            SequenceStepText.Text = $"STEP {_scenarioIndex + 1}/{_scenarioSteps.Count}";
            InjectionSequenceList.SelectedIndex = _scenarioIndex;
            if (InjectionSequenceList.SelectedItem is not null)
                InjectionSequenceList.ScrollIntoView(InjectionSequenceList.SelectedItem);
        }
        else if (_scenarioSteps.Count == 0)
        {
            SequenceStepText.Text = "MANUAL";
        }

        DelayText.Text = snapshot.DelayTargetSeconds > 0
            ? $"{snapshot.DelayElapsedSeconds:0.0} / {snapshot.DelayTargetSeconds:0.0} s"
            : "—";
        DelayProgress.Value = snapshot.DelayTargetSeconds > 0
            ? Math.Clamp(snapshot.DelayElapsedSeconds / snapshot.DelayTargetSeconds * 100.0, 0, 100)
            : 0;

        LcdModeText.Text = $"{(snapshot.Mode == AvrOperatingMode.Automatic ? "AUTO" : "MAN")} · {StateLabel(snapshot.State).ToUpperInvariant()}";
        LcdMeasuredText.Text = $"{snapshot.MeasuredVoltageV:0.00}";
        LcdSetpointText.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V";
        LcdDeviationText.Text = $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %";
        LcdTapText.Text = $"{snapshot.TapPosition:+0;-0;0}";
        LcdBandText.Text = $"{snapshot.LowerBandVoltageV:0.00} — {snapshot.UpperBandVoltageV:0.00} V";
        LcdReasonText.Text = snapshot.Reason;
        DeviceClockText.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        DeviceOutputText.Text = snapshot.RaiseOutput
            ? "OUTPUT: RAISE PULSE"
            : snapshot.LowerOutput
                ? "OUTPUT: LOWER PULSE"
                : snapshot.Blocked
                    ? "OUTPUT: BLOCKED"
                    : "OUTPUT: IDLE";

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

        PowerLed.Fill = HealthyBrush;
        RunLed.Fill = _running ? HealthyBrush : LedOffBrush;
        AutoLed.Fill = snapshot.Mode == AvrOperatingMode.Automatic ? HealthyBrush : LedOffBrush;
        RaiseLed.Fill = snapshot.State == AvrControlState.TimingRaise || snapshot.RaiseOutput ? WarningBrush : LedOffBrush;
        LowerLed.Fill = snapshot.State == AvrControlState.TimingLower || snapshot.LowerOutput ? WarningBrush : LedOffBrush;
        BlockLed.Fill = snapshot.Blocked ? AlarmBrush : LedOffBrush;
        LimitLed.Fill = snapshot.State == AvrControlState.TapLimit ? WarningBrush : LedOffBrush;

        AutoModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Automatic ? FontWeights.SemiBold : FontWeights.Normal;
        ManualModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Manual ? FontWeights.SemiBold : FontWeights.Normal;
        EventTraceText.Text = string.Join(Environment.NewLine, _events);
    }

    private string InjectionStatusLabel()
    {
        if (!_running)
            return _scenarioSteps.Count > 0 && _scenarioIndex == _scenarioSteps.Count - 1 ? "COMPLETE / PAUSED" : "READY / PAUSED";
        if (_preDelayRemainingSeconds > 0)
            return $"PRE-DELAY {_preDelayRemainingSeconds:0.0}s";
        if (_scenarioSteps.Count > 0 && _scenarioIndex >= 0 && _scenarioIndex < _scenarioSteps.Count)
            return $"RUN · {_scenarioSteps[_scenarioIndex].Label}";
        return "RUN · MANUAL SOURCE";
    }

    private void AddEvent(string code, string message)
    {
        _events.Enqueue($"{DateTime.Now:HH:mm:ss}  {code,-8} {message}");
        while (_events.Count > 14)
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

    private static string InputModeLabel(AvrVoltageInputMode mode)
        => mode == AvrVoltageInputMode.BenchInjection ? "BENCH" : "CLOSED LOOP";

    private void SetInjectionSlider(double value)
    {
        _updatingInjectionUi = true;
        try
        {
            SourceVoltageSlider.Value = value;
        }
        finally
        {
            _updatingInjectionUi = false;
        }
    }

    private static double MoveToward(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
            return target;
        return current + (Math.Sign(target - current) * maxDelta);
    }

    private void ShowWarning(string message, string title)
    {
        var owner = Window.GetWindow(this);
        if (owner is null)
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

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
