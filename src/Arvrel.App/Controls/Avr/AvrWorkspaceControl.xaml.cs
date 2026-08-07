using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.Application.Ied;

namespace Arvrel.App.Controls.Avr;

public partial class AvrWorkspaceControl : UserControl
{
    private sealed record InjectionStep(
        string Label,
        double TargetVoltagePercent,
        double TargetCurrentPercent,
        double HoldSeconds);

    private static readonly Brush LedOffBrush = Freeze(new SolidColorBrush(Color.FromRgb(81, 96, 106)));
    private static readonly Brush HealthyBrush = Freeze(new SolidColorBrush(Color.FromRgb(102, 200, 120)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(240, 182, 83)));
    private static readonly Brush AlarmBrush = Freeze(new SolidColorBrush(Color.FromRgb(234, 107, 103)));

    private AvrSettings _settings = new();
    private readonly AvrSimulationEngine _engine = new(new AvrSettings());
    private AvrSnapshot _snapshot = AvrSnapshot.Ready(new AvrSettings(), 0);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _simulationClock = new();
    private readonly Queue<string> _events = new();
    private readonly List<InjectionStep> _scenarioSteps = [];

    private bool _running;
    private bool _configExpanded = true;
    private bool _updatingInjectionUi;
    private int _lastTapPosition;
    private int? _lastPendingTapPosition;
    private int _scenarioIndex = -1;
    private AvrControlState _lastState = AvrControlState.InBand;
    private TimeSpan _lastSimulationTime;
    private double _scenarioElapsedSeconds;
    private double _preDelayRemainingSeconds;
    private double _injectedVoltageV = 100.0;
    private double _injectionTargetVoltageV = 100.0;
    private double _injectedCurrentA = 1.0;
    private double _injectionTargetCurrentA = 1.0;
    private double _currentAngleDegrees;
    private double _frequencyHz = 50.0;
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
        AddEvent("READY", "AVR U/I bench simulator initialized");
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
        _simulationClock.Reset();
        _lastSimulationTime = TimeSpan.Zero;
        _engine.Reset();
        _scenarioSteps.Clear();
        _scenarioIndex = -1;
        _scenarioElapsedSeconds = 0;
        _preDelayRemainingSeconds = 0;
        _injectedVoltageV = _settings.NominalVoltageV;
        _injectionTargetVoltageV = _settings.NominalVoltageV;
        _injectedCurrentA = _settings.NominalCurrentA;
        _injectionTargetCurrentA = _settings.NominalCurrentA;
        _currentAngleDegrees = 0;
        SetInjectionSlider(_injectionTargetVoltageV);
        InjectionVoltageBox.Text = _injectionTargetVoltageV.ToString("0.00", CultureInfo.InvariantCulture);
        InjectionCurrentBox.Text = _injectionTargetCurrentA.ToString("0.00", CultureInfo.InvariantCulture);
        CurrentAngleBox.Text = "0.0";
        TestProfileCombo.SelectedIndex = 0;
        PopulateManualSequence();
        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV, _injectedCurrentA, _currentAngleDegrees);
        _lastTapPosition = _snapshot.TapPosition;
        _lastPendingTapPosition = _snapshot.PendingTapPosition;
        _lastState = _snapshot.State;
        AddEvent("RESET", "AVR, source, authority, timer and tap feedback reset");
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

        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV, _injectedCurrentA, _currentAngleDegrees);
        _lastTapPosition = _snapshot.TapPosition;
        _lastPendingTapPosition = _snapshot.PendingTapPosition;
        _lastState = _snapshot.State;
        RenderSnapshot(_snapshot);
    }

    private void AvrWorkspaceControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _simulationClock.Stop();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
            return;

        var elapsed = TimeSpan.Zero;
        if (_running)
        {
            var now = _simulationClock.Elapsed;
            elapsed = now - _lastSimulationTime;
            _lastSimulationTime = now;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            AdvanceInjection(elapsed.TotalSeconds);
        }

        _snapshot = _engine.Advance(elapsed, _injectedVoltageV, _injectedCurrentA, _currentAngleDegrees);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void StartInjection_Click(object sender, RoutedEventArgs e) => StartInjection();
    private void PauseInjection_Click(object sender, RoutedEventArgs e) => PauseInjection();

    private void StopInjection_Click(object sender, RoutedEventArgs e)
    {
        if (!_running && _scenarioIndex < 0)
            return;

        _running = false;
        _simulationClock.Stop();
        _scenarioIndex = -1;
        _scenarioElapsedSeconds = 0;
        _preDelayRemainingSeconds = 0;
        AddEvent("STOP", "Injection stopped; last U/I phasor retained");
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
        _simulationClock.Restart();
        _lastSimulationTime = TimeSpan.Zero;
        AddEvent(
            "INJECT",
            $"Started · U {_injectionTargetVoltageV:0.00} V · I {_injectionTargetCurrentA:0.000} A ∠{_currentAngleDegrees:0.0}° · {_frequencyHz:0.00} Hz");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    private void PauseInjection()
    {
        if (!_running)
            return;

        _running = false;
        _simulationClock.Stop();
        AddEvent("PAUSE", $"Injection paused · U {_injectedVoltageV:0.00} V · I {_injectedCurrentA:0.000} A");
        RunStateChanged?.Invoke(this, EventArgs.Empty);
        RenderCurrent();
    }

    private void AdvanceInjection(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0)
            return;

        if (_preDelayRemainingSeconds > 0)
        {
            _preDelayRemainingSeconds = Math.Max(0, _preDelayRemainingSeconds - elapsedSeconds);
            return;
        }

        _injectedVoltageV = MoveToward(
            _injectedVoltageV,
            _injectionTargetVoltageV,
            Math.Max(0.01, _rampVPerSecond) * elapsedSeconds);

        var currentRampAperSecond = Math.Max(0.5, _settings.NominalCurrentA * 5.0);
        _injectedCurrentA = MoveToward(
            _injectedCurrentA,
            _injectionTargetCurrentA,
            currentRampAperSecond * elapsedSeconds);

        if (_scenarioIndex < 0 || _scenarioIndex >= _scenarioSteps.Count)
            return;

        var step = _scenarioSteps[_scenarioIndex];
        if (Math.Abs(_injectedVoltageV - _injectionTargetVoltageV) > 0.005 ||
            Math.Abs(_injectedCurrentA - _injectionTargetCurrentA) > 0.002)
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
            _simulationClock.Stop();
            AddEvent("COMPLETE", "Injection sequence completed");
            RunStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        ApplyScenarioTarget(_scenarioSteps[_scenarioIndex]);
        AddEvent("STEP", $"{_scenarioSteps[_scenarioIndex].Label} · U {_injectionTargetVoltageV:0.00} V · I {_injectionTargetCurrentA:0.000} A");
    }

    private void InjectionForm_LostFocus(object sender, RoutedEventArgs e)
        => TryApplyInjectionForm(showError: false);

    private bool TryApplyInjectionForm(bool showError)
    {
        try
        {
            var voltage = ParseDouble(InjectionVoltageBox, "Injected voltage");
            var current = ParseDouble(InjectionCurrentBox, "Injected current");
            var angle = ParseDouble(CurrentAngleBox, "Current angle");
            var frequency = ParseDouble(FrequencyBox, "Frequency");
            var ramp = ParseDouble(RampBox, "Voltage ramp rate");
            var preDelay = ParseDouble(DelayBox, "Pre-delay");
            var dwell = ParseDouble(DwellBox, "Dwell");

            if (voltage < 1 || voltage > 200)
                throw new ArgumentOutOfRangeException(nameof(voltage), "Injected voltage must be between 1 V and 200 V.");
            if (current < 0 || current > 20)
                throw new ArgumentOutOfRangeException(nameof(current), "Injected secondary current must be between 0 A and 20 A.");
            if (angle < -360 || angle > 360)
                throw new ArgumentOutOfRangeException(nameof(angle), "Current angle must be between -360° and +360°.");
            if (frequency < 40 || frequency > 70)
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency must be between 40 Hz and 70 Hz.");
            if (ramp <= 0 || ramp > 500)
                throw new ArgumentOutOfRangeException(nameof(ramp), "Voltage ramp rate must be greater than 0 and no more than 500 V/s.");
            if (preDelay < 0 || preDelay > 120)
                throw new ArgumentOutOfRangeException(nameof(preDelay), "Pre-delay must be between 0 and 120 seconds.");
            if (dwell <= 0 || dwell > 600)
                throw new ArgumentOutOfRangeException(nameof(dwell), "Dwell must be greater than 0 and no more than 600 seconds.");

            _injectionTargetVoltageV = voltage;
            _injectionTargetCurrentA = current;
            _currentAngleDegrees = angle;
            _frequencyHz = frequency;
            _rampVPerSecond = ramp;
            _preDelaySeconds = preDelay;
            _dwellSeconds = dwell;
            SetInjectionSlider(Math.Clamp(voltage, SourceVoltageSlider.Minimum, SourceVoltageSlider.Maximum));

            if (!_running && _scenarioSteps.Count == 0)
            {
                _injectedVoltageV = voltage;
                _injectedCurrentA = current;
            }

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

    private void InputModel_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || BenchModeRadio is null || ClosedLoopModeRadio is null)
            return;

        var inputMode = ClosedLoopModeRadio.IsChecked == true
            ? AvrVoltageInputMode.ClosedLoopPlant
            : AvrVoltageInputMode.BenchInjection;
        if (_settings.VoltageInputMode == inputMode)
            return;

        _settings = _settings with { VoltageInputMode = inputMode };
        _engine.ApplySettings(_settings, keepTapPosition: true);
        AddEvent("PLANT", inputMode == AvrVoltageInputMode.BenchInjection
            ? "External test-set bench model selected"
            : "Closed-loop transformer + OLTC model selected");
        RenderCurrent();
    }

    private void ScenarioRaise_Click(object sender, RoutedEventArgs e) => SelectScenario(1);
    private void ScenarioLower_Click(object sender, RoutedEventArgs e) => SelectScenario(2);
    private void ScenarioBandSweep_Click(object sender, RoutedEventArgs e) => SelectScenario(3);
    private void ScenarioVoltageBlocking_Click(object sender, RoutedEventArgs e) => SelectScenario(4);
    private void ScenarioUnderCurrent_Click(object sender, RoutedEventArgs e) => SelectScenario(5);
    private void ScenarioOverCurrent_Click(object sender, RoutedEventArgs e) => SelectScenario(6);

    private void SelectScenario(int profileIndex)
    {
        TestProfileCombo.SelectedIndex = profileIndex;
        LoadScenarioForProfile(profileIndex);
    }

    private void LoadScenarioForProfile(int profileIndex)
    {
        _scenarioSteps.Clear();
        EnsureCurrentBlockingForProfile(profileIndex);

        switch (profileIndex)
        {
            case 1:
                _scenarioSteps.Add(new("Baseline", 100, 100, 2));
                _scenarioSteps.Add(new("Low U · expect RAISE", 95, 100, _dwellSeconds));
                _scenarioSteps.Add(new("Return nominal", 100, 100, 3));
                break;
            case 2:
                _scenarioSteps.Add(new("Baseline", 100, 100, 2));
                _scenarioSteps.Add(new("High U · expect LOWER", 105, 100, _dwellSeconds));
                _scenarioSteps.Add(new("Return nominal", 100, 100, 3));
                break;
            case 3:
                _scenarioSteps.Add(new("Below band", 98, 100, 4));
                _scenarioSteps.Add(new("Near lower band", 99.5, 100, 4));
                _scenarioSteps.Add(new("Setpoint", 100, 100, 4));
                _scenarioSteps.Add(new("Near upper band", 100.5, 100, 4));
                _scenarioSteps.Add(new("Above band", 102, 100, 4));
                break;
            case 4:
                _scenarioSteps.Add(new("Nominal", 100, 100, 2));
                _scenarioSteps.Add(new("U< block", Math.Max(50, _settings.UndervoltageBlockPercent - 5), 100, 4));
                _scenarioSteps.Add(new("Recover", 100, 100, 3));
                _scenarioSteps.Add(new("U> block", Math.Min(150, _settings.OvervoltageBlockPercent + 5), 100, 4));
                _scenarioSteps.Add(new("Recover", 100, 100, 3));
                break;
            case 5:
                _scenarioSteps.Add(new("Nominal load", 95, 100, 3));
                _scenarioSteps.Add(new("I< block", 95, Math.Max(0, _settings.UndercurrentBlockPercent - 5), 5));
                _scenarioSteps.Add(new("Current recover", 95, 100, 4));
                break;
            case 6:
                _scenarioSteps.Add(new("Nominal load", 95, 100, 3));
                _scenarioSteps.Add(new("I> block", 95, Math.Min(500, _settings.OvercurrentBlockPercent + 25), 5));
                _scenarioSteps.Add(new("Current recover", 95, 100, 4));
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

    private void EnsureCurrentBlockingForProfile(int profileIndex)
    {
        var underEnabled = profileIndex == 5 || _settings.UndercurrentBlockingEnabled;
        var overEnabled = profileIndex == 6 || _settings.OvercurrentBlockingEnabled;
        if (underEnabled == _settings.UndercurrentBlockingEnabled && overEnabled == _settings.OvercurrentBlockingEnabled)
            return;

        _settings = _settings with
        {
            UndercurrentBlockingEnabled = underEnabled,
            OvercurrentBlockingEnabled = overEnabled
        };
        _engine.ApplySettings(_settings, keepTapPosition: true);
        PopulateSettingsUi();
    }

    private void PopulateManualSequence()
    {
        _scenarioSteps.Clear();
        _scenarioIndex = -1;
        if (InjectionSequenceList is null)
            return;

        InjectionSequenceList.Items.Clear();
        InjectionSequenceList.Items.Add("MANUAL   Set U / I / φ / f and press Start injection");
        SequenceStepText.Text = "MANUAL";
    }

    private void RenderScenarioList()
    {
        InjectionSequenceList.Items.Clear();
        for (var i = 0; i < _scenarioSteps.Count; i++)
        {
            var step = _scenarioSteps[i];
            InjectionSequenceList.Items.Add(
                $"{i + 1,2}  {step.Label,-20} U {step.TargetVoltagePercent,6:0.0}%  I {step.TargetCurrentPercent,6:0.0}%  {step.HoldSeconds,4:0.0}s");
        }
        SequenceStepText.Text = _scenarioSteps.Count == 0 ? "MANUAL" : $"{_scenarioSteps.Count} STEPS";
    }

    private void ApplyScenarioTarget(InjectionStep step)
    {
        _injectionTargetVoltageV = _settings.NominalVoltageV * (step.TargetVoltagePercent / 100.0);
        _injectionTargetCurrentA = _settings.NominalCurrentA * (step.TargetCurrentPercent / 100.0);
        InjectionVoltageBox.Text = _injectionTargetVoltageV.ToString("0.00", CultureInfo.InvariantCulture);
        InjectionCurrentBox.Text = _injectionTargetCurrentA.ToString("0.000", CultureInfo.InvariantCulture);
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
                FastT2Enabled = FastT2Check.IsChecked == true,
                CommandPulseSeconds = ParseDouble(CommandPulseBox, "Command pulse"),
                MotorDriveTravelSeconds = ParseDouble(MotorTravelBox, "Motor travel"),
                MinimumTap = ParseInt(MinTapBox, "Minimum tap"),
                MaximumTap = ParseInt(MaxTapBox, "Maximum tap"),
                TapStepPercent = ParseDouble(TapStepBox, "Tap step"),
                UndervoltageBlockPercent = ParseDouble(UnderBlockBox, "Undervoltage blocking"),
                OvervoltageBlockPercent = ParseDouble(OverBlockBox, "Overvoltage blocking"),
                NominalCurrentA = ParseDouble(NominalCurrentBox, "Nominal current"),
                UndercurrentBlockingEnabled = UnderCurrentCheck.IsChecked == true,
                UndercurrentBlockPercent = ParseDouble(UnderCurrentBox, "Undercurrent blocking"),
                OvercurrentBlockingEnabled = OverCurrentCheck.IsChecked == true,
                OvercurrentBlockPercent = ParseDouble(OverCurrentBox, "Overcurrent blocking"),
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
                $"Uset {settings.SetpointVoltageV:0.##} V · ±{settings.TolerancePercent:0.##}% · T1 {settings.T1Seconds:0.##} s · In {settings.NominalCurrentA:0.###} A");
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
        _engine.SetAuthority(AvrControlAuthority.Local);
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
        CommandPulseBox.Text = _settings.CommandPulseSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        MotorTravelBox.Text = _settings.MotorDriveTravelSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        MinTapBox.Text = _settings.MinimumTap.ToString(CultureInfo.InvariantCulture);
        MaxTapBox.Text = _settings.MaximumTap.ToString(CultureInfo.InvariantCulture);
        TapStepBox.Text = _settings.TapStepPercent.ToString("0.##", CultureInfo.InvariantCulture);
        UnderBlockBox.Text = _settings.UndervoltageBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        OverBlockBox.Text = _settings.OvervoltageBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        NominalCurrentBox.Text = _settings.NominalCurrentA.ToString("0.###", CultureInfo.InvariantCulture);
        UnderCurrentBox.Text = _settings.UndercurrentBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        OverCurrentBox.Text = _settings.OvercurrentBlockPercent.ToString("0.##", CultureInfo.InvariantCulture);
        LdcBox.Text = _settings.LineDropCompensationPercent.ToString("0.##", CultureInfo.InvariantCulture);
        FastT2Check.IsChecked = _settings.FastT2Enabled;
        UnderCurrentCheck.IsChecked = _settings.UndercurrentBlockingEnabled;
        OverCurrentCheck.IsChecked = _settings.OvercurrentBlockingEnabled;
        LdcCheck.IsChecked = _settings.LineDropCompensationEnabled;
        BenchModeRadio.IsChecked = _settings.VoltageInputMode == AvrVoltageInputMode.BenchInjection;
        ClosedLoopModeRadio.IsChecked = _settings.VoltageInputMode == AvrVoltageInputMode.ClosedLoopPlant;
        ProfileText.Text = _settings.ProfileName;
    }

    private void LocalAuthority_Click(object sender, RoutedEventArgs e)
    {
        _engine.SetAuthority(AvrControlAuthority.Local);
        AddEvent("AUTH", "LOCAL control authority selected");
        RenderCurrent();
    }

    private void RemoteAuthority_Click(object sender, RoutedEventArgs e)
    {
        _engine.SetAuthority(AvrControlAuthority.Remote);
        AddEvent("AUTH", "REMOTE control authority selected · local command keys inhibited as applicable");
        RenderCurrent();
    }

    private void AutoMode_Click(object sender, RoutedEventArgs e)
        => SetModeFromFrontPanel(AvrOperatingMode.Automatic);

    private void ManualMode_Click(object sender, RoutedEventArgs e)
        => SetModeFromFrontPanel(AvrOperatingMode.Manual);

    private void SetModeFromFrontPanel(AvrOperatingMode mode)
    {
        if (_engine.Authority == AvrControlAuthority.Remote)
        {
            AddEvent("INHIBIT", $"REMOTE authority · local {(mode == AvrOperatingMode.Automatic ? "AUTO" : "MANUAL")} key ignored");
            RenderCurrent();
            return;
        }

        _engine.SetMode(mode);
        AddEvent("MODE", mode == AvrOperatingMode.Automatic ? "AUTO selected" : "MANUAL selected");
        RenderCurrent();
    }

    private void RaiseTap_Click(object sender, RoutedEventArgs e) => ExecuteManualTap(+1);
    private void LowerTap_Click(object sender, RoutedEventArgs e) => ExecuteManualTap(-1);

    private void ExecuteManualTap(int direction)
    {
        var moved = direction > 0 ? _engine.RaiseTap() : _engine.LowerTap();
        if (moved)
        {
            AddEvent("MAN CMD", direction > 0 ? "Local RAISE command issued" : "Local LOWER command issued");
        }
        else
        {
            var reason = _engine.Authority == AvrControlAuthority.Remote
                ? "REMOTE authority · local tap key inhibited"
                : _engine.Mode != AvrOperatingMode.Manual
                    ? "Select MANUAL before local RAISE / LOWER"
                    : _engine.TapMoving
                        ? "Tap changer already moving"
                        : direction > 0 ? "Maximum tap reached" : "Minimum tap reached";
            AddEvent("INHIBIT", reason);
        }
        RenderCurrent();
    }

    private void DeviceFunction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var key = button.Tag?.ToString() ?? button.Content?.ToString() ?? "KEY";
        AddEvent("KEY", $"Front-panel {key} pressed");
        if (string.Equals(key, "F1", StringComparison.Ordinal))
            ConfigurationTabs.SelectedIndex = 0;
        else if (string.Equals(key, "F2", StringComparison.Ordinal))
            ConfigurationTabs.SelectedIndex = 1;
        else if (string.Equals(key, "F3", StringComparison.Ordinal))
            ConfigurationTabs.SelectedIndex = 3;
    }

    private void ConfigurePanelToggle_Click(object sender, RoutedEventArgs e)
        => SetConfigurationExpanded(!_configExpanded);

    private void SetConfigurationExpanded(bool expanded)
    {
        _configExpanded = expanded;
        ConfigurationColumn.Width = new GridLength(expanded ? 380 : 38);
        ConfigurationExpanded.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ConfigurationRail.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderCurrent()
    {
        _snapshot = _engine.Advance(TimeSpan.Zero, _injectedVoltageV, _injectedCurrentA, _currentAngleDegrees);
        ObserveTransition(_snapshot);
        RenderSnapshot(_snapshot);
    }

    private void ObserveTransition(AvrSnapshot snapshot)
    {
        if (snapshot.PendingTapPosition != _lastPendingTapPosition && snapshot.PendingTapPosition is int pending)
        {
            AddEvent(
                snapshot.RaiseOutput ? "RAISE" : snapshot.LowerOutput ? "LOWER" : "TAP CMD",
                $"Command issued · feedback {_lastTapPosition:+0;-0;0} · target {pending:+0;-0;0}");
        }
        _lastPendingTapPosition = snapshot.PendingTapPosition;

        if (snapshot.TapPosition != _lastTapPosition)
        {
            AddEvent("TAP FB", $"Tap-position feedback {_lastTapPosition:+0;-0;0} → {snapshot.TapPosition:+0;-0;0}");
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
        InjectionTargetText.Text = $"{_injectionTargetVoltageV:0.00} V";
        InjectedSourceText.Text = $"U {_injectedVoltageV:0.00} V · I {_injectedCurrentA:0.000} A";
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

        DelayProgress.Value = snapshot.DelayTargetSeconds > 0
            ? Math.Clamp(snapshot.DelayElapsedSeconds / snapshot.DelayTargetSeconds * 100.0, 0, 100)
            : snapshot.TapMoving && _settings.MotorDriveTravelSeconds > 0
                ? Math.Clamp((1 - snapshot.MotorTravelRemainingSeconds / _settings.MotorDriveTravelSeconds) * 100.0, 0, 100)
                : 0;

        DelayText.Text = snapshot.TapMoving
            ? $"MOTOR {snapshot.MotorTravelRemainingSeconds:0.0}s"
            : snapshot.DelayTargetSeconds > 0
                ? $"{snapshot.DelayElapsedSeconds:0.0} / {snapshot.DelayTargetSeconds:0.0}s"
                : "—";

        AuthorityBadgeText.Text = snapshot.Authority == AvrControlAuthority.Local ? "LOCAL" : "REMOTE";
        ModeBadgeText.Text = snapshot.Mode == AvrOperatingMode.Automatic ? "AUTO" : "MANUAL";
        DeviceStateText.Text = StateLabel(snapshot.State).ToUpperInvariant();
        AuthorityBadgeText.Foreground = snapshot.Authority == AvrControlAuthority.Local ? HealthyBrush : WarningBrush;
        ModeBadgeText.Foreground = snapshot.Mode == AvrOperatingMode.Automatic ? HealthyBrush : WarningBrush;

        LcdMeasuredText.Text = snapshot.MeasuredVoltageV.ToString("0.00", CultureInfo.InvariantCulture);
        LcdSetpointText.Text = $"{snapshot.EffectiveSetpointVoltageV:0.00} V";
        LcdDeviationText.Text = $"{snapshot.DeviationPercent:+0.00;-0.00;0.00} %";
        LcdTapText.Text = snapshot.TapPosition.ToString("+00;-00;00", CultureInfo.InvariantCulture);
        LcdPendingTapText.Text = snapshot.PendingTapPosition is int target
            ? $"TARGET {target:+00;-00;00} · {snapshot.MotorTravelRemainingSeconds:0.0}s"
            : "FEEDBACK VALID";
        LcdBandText.Text = $"{snapshot.LowerBandVoltageV:0.00} — {snapshot.UpperBandVoltageV:0.00} V";
        LcdCurrentText.Text = $"{snapshot.SourceCurrentA:0.000} A";
        LcdPowerText.Text = $"φ {snapshot.CurrentAngleDegrees:+0.0;-0.0;0.0}° · PF {snapshot.PowerFactor:0.000}";
        LcdCommandText.Text = snapshot.RaiseOutput
            ? $"RAISE CONTACT ON · {snapshot.CommandPulseRemainingSeconds:0.00}s"
            : snapshot.LowerOutput
                ? $"LOWER CONTACT ON · {snapshot.CommandPulseRemainingSeconds:0.00}s"
                : snapshot.TapMoving
                    ? "CONTACT OFF · MOTOR IN TRANSIT"
                    : snapshot.Blocked ? "OUTPUT BLOCKED" : "OUTPUT IDLE";
        LcdReasonText.Text = snapshot.Reason;
        DeviceClockText.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        DeviceOutputText.Text = snapshot.RaiseOutput
            ? "OUTPUT: RAISE"
            : snapshot.LowerOutput
                ? "OUTPUT: LOWER"
                : snapshot.TapMoving
                    ? "OUTPUT: MOTOR MOVING"
                    : snapshot.Blocked ? "OUTPUT: BLOCKED" : "OUTPUT: IDLE";

        PowerLed.Fill = HealthyBrush;
        RunLed.Fill = _running ? HealthyBrush : LedOffBrush;
        LocalLed.Fill = snapshot.Authority == AvrControlAuthority.Local ? HealthyBrush : WarningBrush;
        AutoLed.Fill = snapshot.Mode == AvrOperatingMode.Automatic ? HealthyBrush : WarningBrush;
        RaiseLed.Fill = snapshot.RaiseOutput || snapshot.State == AvrControlState.TimingRaise ? WarningBrush : LedOffBrush;
        LowerLed.Fill = snapshot.LowerOutput || snapshot.State == AvrControlState.TimingLower ? WarningBrush : LedOffBrush;
        BlockLed.Fill = snapshot.Blocked ? AlarmBrush : LedOffBrush;
        LimitLed.Fill = snapshot.State == AvrControlState.TapLimit ? WarningBrush : LedOffBrush;

        LocalAuthorityButton.FontWeight = snapshot.Authority == AvrControlAuthority.Local ? FontWeights.Bold : FontWeights.Normal;
        RemoteAuthorityButton.FontWeight = snapshot.Authority == AvrControlAuthority.Remote ? FontWeights.Bold : FontWeights.Normal;
        AutoModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Automatic ? FontWeights.Bold : FontWeights.Normal;
        ManualModeButton.FontWeight = snapshot.Mode == AvrOperatingMode.Manual ? FontWeights.Bold : FontWeights.Normal;
        OperationCountText.Text = $"CMDS {snapshot.OperationCount}";
        ProfileText.Text = _settings.ProfileName;
    }

    private string InjectionStatusLabel()
    {
        if (!_running)
            return _scenarioSteps.Count > 0 && _scenarioIndex == _scenarioSteps.Count - 1 ? "COMPLETE / PAUSED" : "READY / PAUSED";
        if (_preDelayRemainingSeconds > 0)
            return $"PRE-DELAY {_preDelayRemainingSeconds:0.0}s";
        if (_scenarioSteps.Count > 0 && _scenarioIndex >= 0 && _scenarioIndex < _scenarioSteps.Count)
            return $"RUN · {_scenarioSteps[_scenarioIndex].Label}";
        return "RUN · MANUAL U/I SOURCE";
    }

    private void AddEvent(string code, string message)
    {
        _events.Enqueue($"{DateTime.Now:HH:mm:ss}  {code,-9} {message}");
        while (_events.Count > 16)
            _events.Dequeue();

        if (EventTraceText is not null)
            EventTraceText.Text = string.Join(Environment.NewLine, _events);
    }

    private static string StateLabel(AvrControlState state) => state switch
    {
        AvrControlState.InBand => "In band",
        AvrControlState.TimingRaise => "Timing raise",
        AvrControlState.TimingLower => "Timing lower",
        AvrControlState.TapMoving => "Tap moving",
        AvrControlState.TapLimit => "Tap limit",
        AvrControlState.Blocked => "Blocked",
        AvrControlState.Manual => "Manual",
        _ => state.ToString()
    };

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
        return current + Math.Sign(target - current) * maxDelta;
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