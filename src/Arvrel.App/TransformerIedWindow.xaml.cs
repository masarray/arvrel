using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.ProcessBus;
using Arvrel.Protection;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class TransformerIedWindow : Window
{
    private static readonly Brush NeutralBrush = FrozenBrush(102, 117, 130);
    private static readonly Brush HealthyBrush = FrozenBrush(69, 147, 90);
    private static readonly Brush WarningBrush = FrozenBrush(189, 133, 43);
    private static readonly Brush TripBrush = FrozenBrush(196, 73, 70);
    private static readonly Brush AccentBrush = FrozenBrush(46, 111, 158);
    private static readonly Brush HealthySoftBrush = FrozenBrush(234, 245, 236);
    private static readonly Brush WarningSoftBrush = FrozenBrush(251, 242, 227);
    private static readonly Brush TripSoftBrush = FrozenBrush(252, 237, 236);
    private static readonly Brush NeutralSoftBrush = FrozenBrush(239, 243, 245);

    private readonly SmvProcessBusController _controller;
    private readonly DispatcherTimer _refreshTimer;
    private TransformerProcessBusProtectionRuntime? _runtime;
    private TransformerProtectionRuntimeSnapshot? _lastSnapshot;
    private bool _uiReady;

    public TransformerIedWindow(SmvProcessBusController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        InitializeComponent();
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += TransformerIedWindow_Loaded;
        _uiReady = true;
        PreviewCharacteristic();
    }

    private void TransformerIedWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshStreams(preserveSelection: false);
        UpdateSourceMode();
        _refreshTimer.Start();
        StatusText.Text = _controller.GetStreams().Count >= 2
            ? "Select HV/LV streams, verify transformer engineering, then apply the runtime."
            : "Waiting for at least two Sampled Values streams from Live Capture or PCAP Replay.";
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshStreams(preserveSelection: true);
        UpdateSourceMode();
        if (_runtime is null)
            return;

        try
        {
            var snapshot = _runtime.EvaluateCurrent();
            RenderSnapshot(snapshot);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ObjectDisposedException)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void RefreshStreams_Click(object sender, RoutedEventArgs e)
        => RefreshStreams(preserveSelection: true);

    private void StreamSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady)
            return;
        UpdateStreamHint();
    }

    private void RefreshStreams(bool preserveSelection)
    {
        var streams = _controller.GetStreams();
        var previousHv = preserveSelection && HvStreamCombo.SelectedItem is SmvStreamInfo hv ? hv.Key : null;
        var previousLv = preserveSelection && LvStreamCombo.SelectedItem is SmvStreamInfo lv ? lv.Key : null;

        HvStreamCombo.ItemsSource = streams;
        LvStreamCombo.ItemsSource = streams;

        var selectedHv = streams.FirstOrDefault(stream => string.Equals(stream.Key, previousHv, StringComparison.Ordinal));
        var selectedLv = streams.FirstOrDefault(stream => string.Equals(stream.Key, previousLv, StringComparison.Ordinal));
        if (selectedHv is null && streams.Count > 0)
            selectedHv = streams[0];
        if (selectedLv is null && streams.Count > 1)
            selectedLv = streams.First(stream => !string.Equals(stream.Key, selectedHv?.Key, StringComparison.Ordinal));

        if (!ReferenceEquals(HvStreamCombo.SelectedItem, selectedHv))
            HvStreamCombo.SelectedItem = selectedHv;
        if (!ReferenceEquals(LvStreamCombo.SelectedItem, selectedLv))
            LvStreamCombo.SelectedItem = selectedLv;
        UpdateStreamHint();
    }

    private void UpdateStreamHint()
    {
        if (HvStreamCombo.SelectedItem is not SmvStreamInfo hv || LvStreamCombo.SelectedItem is not SmvStreamInfo lv)
        {
            StreamBindingHintText.Text = "Two distinct current streams are required. Start Live Capture or replay a PCAP containing both transformer sides.";
            StreamBindingHintText.Foreground = WarningBrush;
            return;
        }

        if (string.Equals(hv.Key, lv.Key, StringComparison.Ordinal))
        {
            StreamBindingHintText.Text = "HV and LV cannot use the same SV stream.";
            StreamBindingHintText.Foreground = TripBrush;
            return;
        }

        StreamBindingHintText.Text = $"HV {hv.SvId} / APPID 0x{hv.AppId:X4}  ·  LV {lv.SvId} / APPID 0x{lv.AppId:X4}";
        StreamBindingHintText.Foreground = HealthyBrush;
    }

    private void SettingsInput_Changed(object sender, RoutedEventArgs e)
    {
        if (_uiReady)
            PreviewCharacteristic();
    }

    private void PreviewCharacteristic()
    {
        if (!TryBuildProtectionSettings(out var settings, out _))
            return;
        CharacteristicScope.Settings = settings.Differential87T;
        CharacteristicCaptionText.Text =
            $"Is1 {settings.Differential87T.Is1Pu:0.00} · K1 {settings.Differential87T.K1:P0} · " +
            $"Is2 {settings.Differential87T.Is2Pu:0.00} · K2 {settings.Differential87T.K2:P0}";
    }

    private void ApplyRuntime_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configuration = BuildConfiguration();
            if (_runtime is null)
            {
                _runtime = new TransformerProcessBusProtectionRuntime(_controller, configuration);
                _runtime.SnapshotChanged += Runtime_SnapshotChanged;
            }
            else
            {
                _runtime.UpdateConfiguration(configuration, keepTripLatch: false);
            }

            CharacteristicScope.Settings = _runtime.CurrentSnapshot.EffectiveSettings.Differential87T;
            RenderSnapshot(_runtime.EvaluateCurrent());
            StatusText.Text = "Transformer runtime applied. All trip indications remain virtual evidence only.";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(this, ex.Message, "Transformer runtime configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = ex.Message;
        }
    }

    private TransformerProtectionRuntimeConfiguration BuildConfiguration()
    {
        if (HvStreamCombo.SelectedItem is not SmvStreamInfo hv)
            throw new InvalidOperationException("Select the high-voltage Sampled Values stream.");
        if (LvStreamCombo.SelectedItem is not SmvStreamInfo lv)
            throw new InvalidOperationException("Select the low-voltage Sampled Values stream.");
        if (string.Equals(hv.Key, lv.Key, StringComparison.Ordinal))
            throw new InvalidOperationException("HV and LV must use two distinct Sampled Values streams.");

        var nameplate = new TransformerNameplate(
            ParsePositive(RatedPowerText, "Rated power"),
            ParsePositive(HvVoltageText, "HV rated voltage"),
            ParsePositive(LvVoltageText, "LV rated voltage"),
            RequireText(VectorGroupText, "Vector group"));

        var highVoltage = new TransformerWindingEngineering(
            new TransformerCtRatio(
                ParsePositive(HvCtPrimaryText, "HV phase CT primary"),
                ParsePositive(HvCtSecondaryText, "HV phase CT secondary")),
            new TransformerCtRatio(
                ParsePositive(HvNeutralCtPrimaryText, "HV neutral CT primary"),
                ParsePositive(HvNeutralCtSecondaryText, "HV neutral CT secondary")))
        {
            ReversePhasePolarity = ReverseHvPhaseCheck.IsChecked == true
        };
        var lowVoltage = new TransformerWindingEngineering(
            new TransformerCtRatio(
                ParsePositive(LvCtPrimaryText, "LV phase CT primary"),
                ParsePositive(LvCtSecondaryText, "LV phase CT secondary")),
            new TransformerCtRatio(
                ParsePositive(LvNeutralCtPrimaryText, "LV neutral CT primary"),
                ParsePositive(LvNeutralCtSecondaryText, "LV neutral CT secondary")))
        {
            ReversePhasePolarity = ReverseLvPhaseCheck.IsChecked == true
        };

        if (!TryBuildProtectionSettings(out var settings, out var settingsError))
            throw new ArgumentException(settingsError);

        return new TransformerProtectionRuntimeConfiguration(
            hv.Key,
            lv.Key,
            nameplate,
            highVoltage,
            lowVoltage,
            settings);
    }

    private bool TryBuildProtectionSettings(out TransformerProtectionSettings settings, out string error)
    {
        settings = new TransformerProtectionSettings();
        error = string.Empty;
        try
        {
            var harmonicMode = HarmonicModeCombo?.SelectedIndex switch
            {
                0 => TransformerHarmonicSecurityMode.Disabled,
                2 => TransformerHarmonicSecurityMode.Restraint,
                _ => TransformerHarmonicSecurityMode.Blocking
            };

            settings = new TransformerProtectionSettings
            {
                Differential87T = new TransformerDifferentialSettings
                {
                    Enabled = Enable87TCheck?.IsChecked == true,
                    MinimumPickupPu = ParsePositive(Is1Text, "Is1"),
                    Slope1 = ParsePercentage(K1Text, "K1"),
                    SlopeBreakpointPu = ParsePositive(Is2Text, "Is2"),
                    Slope2 = ParsePercentage(K2Text, "K2"),
                    OperateDelay = TimeSpan.FromMilliseconds(ParseNonNegative(OperateDelayText, "87T delay")),
                    HighSetEnabled = EnableHighSetCheck?.IsChecked == true,
                    HighSetPickupPu = ParsePositive(HighSetText, "87T high-set"),
                    HarmonicSecurityMode = harmonicMode,
                    SecondHarmonicThreshold = ParsePercentage(H2ThresholdText, "H2 threshold"),
                    FifthHarmonicThreshold = ParsePercentage(H5ThresholdText, "H5 threshold")
                },
                RefHighVoltage = new RestrictedEarthFaultSettings
                {
                    Enabled = EnableHvRefCheck?.IsChecked == true
                },
                RefLowVoltage = new RestrictedEarthFaultSettings
                {
                    Enabled = EnableLvRefCheck?.IsChecked == true
                }
            };
            settings.Validate();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            error = ex.Message;
            return false;
        }
    }

    private void EvaluateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            StatusText.Text = "Apply transformer runtime settings first.";
            return;
        }

        try
        {
            RenderSnapshot(_runtime.EvaluateCurrent());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ResetRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            StatusText.Text = "No transformer runtime is active.";
            return;
        }
        _runtime.Reset();
        RenderSnapshot(_runtime.CurrentSnapshot);
        StatusText.Text = "Transformer pickup timers and virtual trip latch reset.";
    }

    private void Runtime_SnapshotChanged(object? sender, TransformerProtectionRuntimeSnapshotChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => RenderSnapshot(e.Snapshot), DispatcherPriority.Background);
            return;
        }
        RenderSnapshot(e.Snapshot);
    }

    private void RenderSnapshot(TransformerProtectionRuntimeSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        UpdateSourceMode(snapshot.SourceMode);
        RenderRuntimeState(snapshot);
        RenderPairing(snapshot);
        RenderEngineering(snapshot);
        SettingsFingerprintText.Text = $"SETTINGS {snapshot.EffectiveSettingsFingerprint[..Math.Min(12, snapshot.EffectiveSettingsFingerprint.Length)].ToUpperInvariant()}";
        CharacteristicScope.Settings = snapshot.EffectiveSettings.Differential87T;

        if (snapshot.Protection is null)
        {
            CharacteristicScope.Phases = null;
            ClearProtectionQuantities();
            ProtectionDecisionText.Text = $"  ·  {snapshot.DecisionReason}";
            return;
        }

        var protection = snapshot.Protection;
        CharacteristicScope.Phases = protection.Differential.Phases;
        ProtectionDecisionText.Text = $"  ·  {protection.DecisionReason}";
        RenderPhase(protection.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.A),
            PhaseAIdiffText, PhaseAIbiasText, PhaseAThresholdText, PhaseAH2Text, PhaseAH5Text, PhaseAStateText);
        RenderPhase(protection.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.B),
            PhaseBIdiffText, PhaseBIbiasText, PhaseBThresholdText, PhaseBH2Text, PhaseBH5Text, PhaseBStateText);
        RenderPhase(protection.Differential.Phases.Single(phase => phase.Phase == TransformerPhase.C),
            PhaseCIdiffText, PhaseCIbiasText, PhaseCThresholdText, PhaseCH2Text, PhaseCH5Text, PhaseCStateText);

        SetElementState(RestrainedStateText, protection.Differential.Restrained87T);
        SetElementState(HighSetStateText, protection.Differential.HighSet87T);
        SetElementState(RefHvStateText, protection.RefHighVoltage.Element);
        SetElementState(RefLvStateText, protection.RefLowVoltage.Element);
        RefHvDetailText.Text =
            $"HV  Iop {protection.RefHighVoltage.OperatingCurrentPu:0.000} · Ibias {protection.RefHighVoltage.RestraintCurrentPu:0.000} · thr {protection.RefHighVoltage.ThresholdPu:0.000}";
        RefLvDetailText.Text =
            $"LV  Iop {protection.RefLowVoltage.OperatingCurrentPu:0.000} · Ibias {protection.RefLowVoltage.RestraintCurrentPu:0.000} · thr {protection.RefLowVoltage.ThresholdPu:0.000}";

        if (snapshot.Measurement is not null)
        {
            HvCurrentText.Text = $"HV  {FormatCurrents(snapshot.Measurement.HighVoltage.FundamentalCurrentA)}";
            LvCurrentText.Text = $"LV  {FormatCurrents(snapshot.Measurement.LowVoltage.FundamentalCurrentA)}";
            HarmonicEvidenceText.Text =
                $"HV H2 {FormatRatios(snapshot.Measurement.HighVoltage.SecondHarmonicRatio)} · H5 {FormatRatios(snapshot.Measurement.HighVoltage.FifthHarmonicRatio)}\n" +
                $"LV H2 {FormatRatios(snapshot.Measurement.LowVoltage.SecondHarmonicRatio)} · H5 {FormatRatios(snapshot.Measurement.LowVoltage.FifthHarmonicRatio)}";
        }
    }

    private void RenderRuntimeState(TransformerProtectionRuntimeSnapshot snapshot)
    {
        var (brush, soft, stateText) = snapshot.State switch
        {
            TransformerRuntimeState.TripLatched => (TripBrush, TripSoftBrush, "TRIP LATCHED"),
            TransformerRuntimeState.Pickup => (WarningBrush, WarningSoftBrush, "PICKUP"),
            TransformerRuntimeState.ProtectionBlocked => (WarningBrush, WarningSoftBrush, "PROTECTION BLOCKED"),
            TransformerRuntimeState.PairBlocked => (WarningBrush, WarningSoftBrush, "PAIR BLOCKED"),
            TransformerRuntimeState.Ready => (HealthyBrush, HealthySoftBrush, "READY"),
            _ => (NeutralBrush, NeutralSoftBrush, "WAITING")
        };
        RuntimeStatusLed.Fill = brush;
        RuntimeStatusText.Text = stateText;
        RuntimeStatusText.Foreground = brush;
        TripStateBadge.Background = soft;
        TripStateBadge.BorderBrush = brush;
        TripStateText.Text = stateText;
        TripStateText.Foreground = brush;
        TripReasonText.Text = snapshot.DecisionReason;
        StatusText.Text = snapshot.DecisionReason;
    }

    private void RenderPairing(TransformerProtectionRuntimeSnapshot snapshot)
    {
        var pairing = snapshot.Pairing;
        PairingCodeText.Text = pairing.Code;
        PairingCodeText.Foreground = pairing.Aligned ? HealthyBrush : WarningBrush;
        PairCounterText.Text = snapshot.PairIdentity is null
            ? $"skew {pairing.SignedSampleCounterSkew:+0;-0;0}"
            : $"HV {snapshot.PairIdentity.HighVoltageSampleCounter:0000} · LV {snapshot.PairIdentity.LowVoltageSampleCounter:0000} · Δ {pairing.SignedSampleCounterSkew:+0;-0;0}";
        PairSyncText.Text = $"HV {pairing.HighVoltageSynchronization} · LV {pairing.LowVoltageSynchronization}";
        PairCorrectionText.Text = $"{pairing.PhaseCorrectionDegrees:+0.###;-0.###;0}° · {pairing.CaptureTimestampSkew.TotalMilliseconds:0.###} ms";
        PairingDetailText.Text = pairing.Detail;
        PairIdentityText.Text = snapshot.PairIdentity is null
            ? "—"
            : $"HV smpCnt {snapshot.PairIdentity.HighVoltageSampleCounter} @ {snapshot.PairIdentity.HighVoltageCaptureTimestamp:HH:mm:ss.fff}\n" +
              $"LV smpCnt {snapshot.PairIdentity.LowVoltageSampleCounter} @ {snapshot.PairIdentity.LowVoltageCaptureTimestamp:HH:mm:ss.fff}";
    }

    private void RenderEngineering(TransformerProtectionRuntimeSnapshot snapshot)
    {
        var plan = snapshot.Engineering;
        EngineeringSummaryText.Text = plan.Summary;
        CompensationText.Text =
            $"HV scale {plan.HighVoltageCompensation.CurrentScaleToPu:0.######} pu/A · shift {plan.HighVoltageCompensation.PhaseShiftDegrees:+0.##;-0.##;0}° · Z0 remove {YesNo(plan.HighVoltageCompensation.RemoveZeroSequence)}\n" +
            $"LV scale {plan.LowVoltageCompensation.CurrentScaleToPu:0.######} pu/A · shift {plan.LowVoltageCompensation.PhaseShiftDegrees:+0.##;-0.##;0}° · Z0 remove {YesNo(plan.LowVoltageCompensation.RemoveZeroSequence)}";
        SourceEvidenceText.Text =
            $"{snapshot.SourceMode} · HV {snapshot.HighVoltageStreamKey}\nLV {snapshot.LowVoltageStreamKey}";
        CharacteristicCaptionText.Text =
            $"Is1 {snapshot.EffectiveSettings.Differential87T.Is1Pu:0.00} · K1 {snapshot.EffectiveSettings.Differential87T.K1:P0} · " +
            $"Is2 {snapshot.EffectiveSettings.Differential87T.Is2Pu:0.00} · K2 {snapshot.EffectiveSettings.Differential87T.K2:P0}";
    }

    private static void RenderPhase(
        TransformerDifferentialPhaseSnapshot phase,
        TextBlock idiff,
        TextBlock ibias,
        TextBlock threshold,
        TextBlock h2,
        TextBlock h5,
        TextBlock state)
    {
        idiff.Text = phase.OperatingCurrentPu.ToString("0.000", CultureInfo.InvariantCulture);
        ibias.Text = phase.RestraintCurrentPu.ToString("0.000", CultureInfo.InvariantCulture);
        threshold.Text = phase.ThresholdPu.ToString("0.000", CultureInfo.InvariantCulture);
        h2.Text = phase.SecondHarmonicRatio.ToString("P1", CultureInfo.InvariantCulture);
        h5.Text = phase.FifthHarmonicRatio.ToString("P1", CultureInfo.InvariantCulture);

        if (phase.HighSetOperated)
        {
            state.Text = "87T-HS OPERATE";
            state.Foreground = TripBrush;
        }
        else if (phase.RestrainedOperated)
        {
            state.Text = "87T OPERATE";
            state.Foreground = TripBrush;
        }
        else if (phase.HarmonicBlocked)
        {
            state.Text = "HARMONIC BLOCK";
            state.Foreground = WarningBrush;
        }
        else if (phase.HighSetPickup || phase.RestrainedPickup)
        {
            state.Text = "PICKUP / TIMING";
            state.Foreground = WarningBrush;
        }
        else
        {
            state.Text = "RESTRAINED";
            state.Foreground = HealthyBrush;
        }
    }

    private static void SetElementState(TextBlock text, TransformerElementSnapshot element)
    {
        text.Text = element.State.ToString().ToUpperInvariant();
        text.Foreground = element.State switch
        {
            ProtectionStageState.Operated => TripBrush,
            ProtectionStageState.Timing => WarningBrush,
            ProtectionStageState.Blocked => WarningBrush,
            ProtectionStageState.Ready => HealthyBrush,
            _ => NeutralBrush
        };
    }

    private void ClearProtectionQuantities()
    {
        foreach (var text in new[]
        {
            PhaseAIdiffText, PhaseAIbiasText, PhaseAThresholdText, PhaseAH2Text, PhaseAH5Text,
            PhaseBIdiffText, PhaseBIbiasText, PhaseBThresholdText, PhaseBH2Text, PhaseBH5Text,
            PhaseCIdiffText, PhaseCIbiasText, PhaseCThresholdText, PhaseCH2Text, PhaseCH5Text
        })
            text.Text = "—";
        PhaseAStateText.Text = PhaseBStateText.Text = PhaseCStateText.Text = "WAIT";
        RestrainedStateText.Text = HighSetStateText.Text = RefHvStateText.Text = RefLvStateText.Text = "—";
        HvCurrentText.Text = "HV  —";
        LvCurrentText.Text = "LV  —";
        RefHvDetailText.Text = "HV  —";
        RefLvDetailText.Text = "LV  —";
        HarmonicEvidenceText.Text = "—";
    }

    private void UpdateSourceMode()
        => UpdateSourceMode(_controller.IsReplayMode
            ? ProcessBusSourceMode.PcapReplay
            : _controller.IsRunning
                ? ProcessBusSourceMode.LiveCapture
                : ProcessBusSourceMode.InternalDemo);

    private void UpdateSourceMode(ProcessBusSourceMode mode)
        => SourceModeText.Text = $"SOURCE · {mode.ToString().ToUpperInvariant()}";

    private void ExportEvidence_Click(object sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            MessageBox.Show(this, "Apply the transformer runtime before exporting evidence.", "Transformer evidence", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var evidence = _runtime.CaptureEvidence();
        var dialog = new SaveFileDialog
        {
            Title = "Export transformer IED evidence",
            Filter = "ARVREL transformer evidence (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ARVREL-87T-evidence-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        StatusText.Text = $"Transformer evidence exported to {dialog.FileName}.";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
        if (_runtime is not null)
        {
            _runtime.SnapshotChanged -= Runtime_SnapshotChanged;
            _runtime.Dispose();
            _runtime = null;
        }
    }

    private static double ParsePositive(TextBox? textBox, string label)
    {
        var value = ParseNumber(textBox, label);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(label, $"{label} must be greater than zero.");
        return value;
    }

    private static double ParseNonNegative(TextBox? textBox, string label)
    {
        var value = ParseNumber(textBox, label);
        if (value < 0)
            throw new ArgumentOutOfRangeException(label, $"{label} cannot be negative.");
        return value;
    }

    private static double ParsePercentage(TextBox? textBox, string label)
    {
        var percent = ParseNonNegative(textBox, label);
        if (percent > 500)
            throw new ArgumentOutOfRangeException(label, $"{label} cannot exceed 500%.");
        return percent / 100.0;
    }

    private static double ParseNumber(TextBox? textBox, string label)
    {
        var text = textBox?.Text?.Trim() ?? string.Empty;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var local) && double.IsFinite(local))
            return local;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant) && double.IsFinite(invariant))
            return invariant;
        throw new FormatException($"{label} must be a valid number.");
    }

    private static string RequireText(TextBox textBox, string label)
    {
        var value = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException($"{label} is required.");
        return value;
    }

    private static string FormatCurrents(TransformerPhaseCurrents currents)
        => $"A {currents.PhaseA.Magnitude:0.000} · B {currents.PhaseB.Magnitude:0.000} · C {currents.PhaseC.Magnitude:0.000} A(sec)";

    private static string FormatRatios(TransformerHarmonicRatios ratios)
        => $"A {ratios.PhaseA:P1} / B {ratios.PhaseB:P1} / C {ratios.PhaseC:P1}";

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static Brush FrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
