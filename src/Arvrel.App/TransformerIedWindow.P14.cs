using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

/// <summary>
/// P14 presentation/configuration layer for P13 CT-saturation and external-fault
/// security. It consumes P13 settings and snapshots and deliberately contains no
/// replacement saturation classifier, external-fault detector, or protection engine.
/// </summary>
public partial class TransformerIedWindow
{
    private bool _p14Initialized;

    private CheckBox? _p14EnabledCheck;
    private TextBox? _p14MinimumBiasText;
    private TextBox? _p14MinimumBiasIncreaseText;
    private TextBox? _p14InitialDiffBiasText;
    private TextBox? _p14ArmingDurationText;
    private TextBox? _p14SecurityHoldText;
    private TextBox? _p14DistortionText;
    private TextBox? _p14AsymmetryText;
    private TextBox? _p14SevereDistortionText;
    private CheckBox? _p14SuperviseHighSetCheck;
    private CheckBox? _p14SuperviseRefCheck;

    private TextBlock? _p14SecurityStateText;
    private TextBlock? _p14SecurityReasonText;
    private TextBlock? _p14CtSummaryText;
    private TextBlock? _p14EvidenceReliabilityText;
    private TextBlock? _p14PhaseAText;
    private TextBlock? _p14PhaseBText;
    private TextBlock? _p14PhaseCText;
    private TextBlock? _p14HoldText;
    private TextBlock? _p14AppliedPolicyText;

    /// <summary>
    /// Called by the transformer-Ied entry point after this window's constructor has
    /// completed and before ShowDialog. This avoids adding another WPF lifecycle override
    /// to the P12 window while guaranteeing all named P12 controls and _refreshTimer exist.
    /// </summary>
    internal void InitializeP14PractitionerUi()
    {
        if (_p14Initialized)
            return;

        if (ApplyRuntimeButton.Parent is not Panel configurationPanel)
            throw new InvalidOperationException("Transformer practitioner configuration host is unavailable.");
        if (HarmonicEvidenceText.Parent is not Panel evidencePanel)
            throw new InvalidOperationException("Transformer practitioner evidence host is unavailable.");

        var applyIndex = configurationPanel.Children.IndexOf(ApplyRuntimeButton);
        if (applyIndex < 0)
            throw new InvalidOperationException("Transformer runtime apply control is unavailable.");
        configurationPanel.Children.Insert(applyIndex, BuildP14ConfigurationSection());

        var harmonicIndex = evidencePanel.Children.IndexOf(HarmonicEvidenceText);
        if (harmonicIndex < 0)
            throw new InvalidOperationException("Transformer harmonic evidence anchor is unavailable.");
        evidencePanel.Children.Insert(harmonicIndex + 1, BuildP14EvidenceSection());

        // Do not add a second post-apply handler. P13 settings must be part of the
        // complete configuration before the first evaluation after Apply.
        ApplyRuntimeButton.Click -= ApplyRuntime_Click;
        ApplyRuntimeButton.Click += ApplyRuntimeWithP14_Click;

        // P12 remains the sole evaluator. This handler only renders _lastSnapshot after
        // P12's earlier-registered timer callback has evaluated the current pair.
        _refreshTimer.Tick += P14RefreshTimer_Tick;

        _p14Initialized = true;
        RenderP14(_lastSnapshot);
    }

    private UIElement BuildP14ConfigurationSection()
    {
        var root = new StackPanel { Margin = new Thickness(0, 11, 0, 0) };
        root.Children.Add(new Border
        {
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 0, 0, 10)
        });

        var heading = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(new TextBlock
        {
            Text = "External-fault / CT security",
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        _p14EnabledCheck = new CheckBox
        {
            Content = "Enable P13",
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Disabled by default. Waveform distortion alone never blocks protection."
        };
        Grid.SetColumn(_p14EnabledCheck, 1);
        heading.Children.Add(_p14EnabledCheck);
        root.Children.Add(heading);

        root.Children.Add(new TextBlock
        {
            Text = "Restraint-leading external-fault context is required before CT distortion can assert a security hold.",
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 9.4,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _p14MinimumBiasText = P14TextBox("2.00");
        _p14MinimumBiasIncreaseText = P14TextBox("0.50");
        root.Children.Add(P14TwoFieldGrid(
            "MIN IBIAS (pu)", _p14MinimumBiasText,
            "MIN ΔIBIAS (pu)", _p14MinimumBiasIncreaseText));

        _p14InitialDiffBiasText = P14TextBox("20");
        _p14ArmingDurationText = P14TextBox("80");
        root.Children.Add(P14TwoFieldGrid(
            "MAX INITIAL IDIFF/IBIAS (%)", _p14InitialDiffBiasText,
            "ARM WINDOW (ms)", _p14ArmingDurationText,
            new Thickness(0, 7, 0, 0)));

        _p14SecurityHoldText = P14TextBox("120");
        _p14DistortionText = P14TextBox("12");
        root.Children.Add(P14TwoFieldGrid(
            "SECURITY HOLD (ms)", _p14SecurityHoldText,
            "DISTORTION (%)", _p14DistortionText,
            new Thickness(0, 7, 0, 0)));

        _p14AsymmetryText = P14TextBox("8");
        _p14SevereDistortionText = P14TextBox("25");
        root.Children.Add(P14TwoFieldGrid(
            "PEAK ASYMMETRY (%)", _p14AsymmetryText,
            "SEVERE DISTORTION (%)", _p14SevereDistortionText,
            new Thickness(0, 7, 0, 0)));

        var supervision = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _p14SuperviseHighSetCheck = new CheckBox
        {
            Content = "Supervise 87T-HS",
            IsChecked = true,
            FontSize = 10.2,
            Margin = new Thickness(0, 0, 13, 0)
        };
        _p14SuperviseRefCheck = new CheckBox
        {
            Content = "Supervise REF",
            IsChecked = true,
            FontSize = 10.2
        };
        supervision.Children.Add(_p14SuperviseHighSetCheck);
        supervision.Children.Add(_p14SuperviseRefCheck);
        root.Children.Add(supervision);
        return root;
    }

    private UIElement BuildP14EvidenceSection()
    {
        var root = new StackPanel { Margin = new Thickness(0, 1, 0, 9) };
        root.Children.Add(P14SectionLabel("EXTERNAL-FAULT / CT SECURITY"));

        var stateGrid = new Grid { Margin = new Thickness(0, 4, 0, 3) };
        stateGrid.ColumnDefinitions.Add(new ColumnDefinition());
        stateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _p14SecurityStateText = new TextBlock
        {
            Text = "DISABLED",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = NeutralBrush
        };
        stateGrid.Children.Add(_p14SecurityStateText);

        _p14HoldText = P14TinyText("HOLD —");
        _p14HoldText.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(_p14HoldText, 1);
        stateGrid.Children.Add(_p14HoldText);
        root.Children.Add(stateGrid);

        _p14CtSummaryText = P14TinyText("CT SAT HV — · LV —");
        _p14EvidenceReliabilityText = P14TinyText("Evidence reliable HV 0/3 · LV 0/3");
        _p14AppliedPolicyText = P14TinyText("Policy —");
        _p14PhaseAText = P14TinyText("A · —");
        _p14PhaseBText = P14TinyText("B · —");
        _p14PhaseCText = P14TinyText("C · —");
        _p14SecurityReasonText = new TextBlock
        {
            Text = "P13 runtime not applied.",
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 9.3,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };

        root.Children.Add(_p14CtSummaryText);
        root.Children.Add(_p14EvidenceReliabilityText);
        root.Children.Add(_p14AppliedPolicyText);
        root.Children.Add(_p14PhaseAText);
        root.Children.Add(_p14PhaseBText);
        root.Children.Add(_p14PhaseCText);
        root.Children.Add(_p14SecurityReasonText);
        return root;
    }

    private void ApplyRuntimeWithP14_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseConfiguration = BuildConfiguration();
            var externalFaultSecurity = ReadP14SecuritySettings();
            var protectionSettings = baseConfiguration.ProtectionSettings with
            {
                Differential87T = baseConfiguration.ProtectionSettings.Differential87T with
                {
                    ExternalFaultSecurity = externalFaultSecurity
                }
            };
            var configuration = baseConfiguration with { ProtectionSettings = protectionSettings };
            configuration.Validate();

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
            var snapshot = _runtime.EvaluateCurrent();
            RenderSnapshot(snapshot);
            RenderP14(snapshot);
            StatusText.Text = externalFaultSecurity.Enabled
                ? "Transformer runtime applied with P13 external-fault / CT saturation security. Trip indications remain virtual evidence only."
                : "Transformer runtime applied. P13 external-fault / CT saturation security is disabled.";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(this, ex.Message, "Transformer runtime configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = ex.Message;
        }
    }

    private TransformerExternalFaultSecuritySettings ReadP14SecuritySettings()
    {
        EnsureP14Controls();
        var settings = new TransformerExternalFaultSecuritySettings
        {
            Enabled = _p14EnabledCheck!.IsChecked == true,
            MinimumBiasPu = ParsePositive(_p14MinimumBiasText, "P13 minimum Ibias"),
            MinimumBiasIncreasePu = ParsePositive(_p14MinimumBiasIncreaseText, "P13 minimum ΔIbias"),
            MaximumInitialDifferentialToBiasRatio = ParsePercentage(_p14InitialDiffBiasText, "P13 maximum initial Idiff/Ibias"),
            ArmingDuration = TimeSpan.FromMilliseconds(ParsePositive(_p14ArmingDurationText, "P13 arm window")),
            SecurityHold = TimeSpan.FromMilliseconds(ParsePositive(_p14SecurityHoldText, "P13 security hold")),
            DistortionRatioThreshold = ParsePercentage(_p14DistortionText, "P13 distortion threshold"),
            PeakAsymmetryThreshold = ParsePercentage(_p14AsymmetryText, "P13 peak asymmetry threshold"),
            SevereDistortionRatioThreshold = ParsePercentage(_p14SevereDistortionText, "P13 severe distortion threshold"),
            SuperviseHighSet = _p14SuperviseHighSetCheck!.IsChecked == true,
            SuperviseRef = _p14SuperviseRefCheck!.IsChecked == true
        };
        settings.Validate();
        return settings;
    }

    private void P14RefreshTimer_Tick(object? sender, EventArgs e)
        => RenderP14(_lastSnapshot);

    private void RenderP14(TransformerProtectionRuntimeSnapshot? snapshot)
    {
        if (!_p14Initialized || _p14SecurityStateText is null)
            return;
        if (snapshot is null)
        {
            RenderP14Waiting();
            return;
        }

        var policy = snapshot.EffectiveSettings.Differential87T.ExternalFaultSecurity;
        _p14AppliedPolicyText!.Text =
            $"Rule Ibias ≥ {policy.MinimumBiasPu:0.00} · Δ ≥ {policy.MinimumBiasIncreasePu:0.00} · Idiff/Ibias ≤ {policy.MaximumInitialDifferentialToBiasRatio:P0} · arm {policy.ArmingDuration.TotalMilliseconds:0} ms";

        if (snapshot.Measurement is not null)
        {
            var hvEvidence = snapshot.Measurement.HighVoltage.CtSaturationEvidence;
            var lvEvidence = snapshot.Measurement.LowVoltage.CtSaturationEvidence;
            _p14EvidenceReliabilityText!.Text =
                $"Evidence reliable HV {hvEvidence.ReliablePhaseCount}/3 · LV {lvEvidence.ReliablePhaseCount}/3";
        }
        else
        {
            _p14EvidenceReliabilityText!.Text = "Evidence reliable HV 0/3 · LV 0/3";
        }

        if (snapshot.Protection is null)
        {
            _p14SecurityStateText.Text = policy.Enabled ? "WAITING FOR PROTECTION" : "DISABLED";
            _p14SecurityStateText.Foreground = policy.Enabled ? WarningBrush : NeutralBrush;
            _p14HoldText!.Text = $"HOLD {policy.SecurityHold.TotalMilliseconds:0} ms configured";
            _p14CtSummaryText!.Text = "CT SAT HV — · LV —";
            _p14PhaseAText!.Text = "A · —";
            _p14PhaseBText!.Text = "B · —";
            _p14PhaseCText!.Text = "C · —";
            _p14SecurityReasonText!.Text = snapshot.DecisionReason;
            return;
        }

        var security = snapshot.Protection.ExternalFaultSecurity;
        var suspectedHv = security.Phases
            .Where(phase => phase.HighVoltageSaturationSuspected)
            .Select(phase => phase.Phase.ToString())
            .ToArray();
        var suspectedLv = security.Phases
            .Where(phase => phase.LowVoltageSaturationSuspected)
            .Select(phase => phase.Phase.ToString())
            .ToArray();
        _p14CtSummaryText!.Text = $"CT SAT HV {P14PhaseList(suspectedHv)} · LV {P14PhaseList(suspectedLv)}";

        if (!security.Enabled)
        {
            _p14SecurityStateText.Text = "DISABLED";
            _p14SecurityStateText.Foreground = NeutralBrush;
        }
        else if (security.AnyBlocked)
        {
            _p14SecurityStateText.Text = "SECURITY HOLD ACTIVE";
            _p14SecurityStateText.Foreground = WarningBrush;
        }
        else if (security.AnyArmed)
        {
            _p14SecurityStateText.Text = "EXT FAULT ARMED";
            _p14SecurityStateText.Foreground = WarningBrush;
        }
        else if (suspectedHv.Length > 0 || suspectedLv.Length > 0)
        {
            _p14SecurityStateText.Text = "CT DISTORTION · NO BLOCK";
            _p14SecurityStateText.Foreground = AccentBrush;
        }
        else
        {
            _p14SecurityStateText.Text = "READY";
            _p14SecurityStateText.Foreground = HealthyBrush;
        }

        _p14HoldText!.Text = security.AnyBlocked
            ? $"HOLD ACTIVE · {policy.SecurityHold.TotalMilliseconds:0} ms configured"
            : $"HOLD clear · {policy.SecurityHold.TotalMilliseconds:0} ms configured";
        _p14SecurityReasonText!.Text = security.Reason;

        RenderP14Phase(security, TransformerPhase.A, _p14PhaseAText!);
        RenderP14Phase(security, TransformerPhase.B, _p14PhaseBText!);
        RenderP14Phase(security, TransformerPhase.C, _p14PhaseCText!);
    }

    private static void RenderP14Phase(
        TransformerExternalFaultSecuritySnapshot security,
        TransformerPhase phase,
        TextBlock target)
    {
        var evidence = security.Phases.FirstOrDefault(item => item.Phase == phase);
        if (evidence is null)
        {
            target.Text = $"{phase} · —";
            return;
        }

        var state = evidence.Blocked
            ? "BLOCK"
            : evidence.Armed
                ? "ARM"
                : evidence.HighVoltageSaturationSuspected || evidence.LowVoltageSaturationSuspected
                    ? "DISTORT"
                    : "READY";
        target.Text =
            $"{phase} · {state} · HV D {evidence.HighVoltageDistortionRatio:P1} ASY {evidence.HighVoltagePeakAsymmetry:P1} {P14CtFlag(evidence.HighVoltageSaturationSuspected, evidence.HighVoltageBlocked)}" +
            $" · LV D {evidence.LowVoltageDistortionRatio:P1} ASY {evidence.LowVoltagePeakAsymmetry:P1} {P14CtFlag(evidence.LowVoltageSaturationSuspected, evidence.LowVoltageBlocked)}";
    }

    private void RenderP14Waiting()
    {
        _p14SecurityStateText!.Text = "WAITING";
        _p14SecurityStateText.Foreground = NeutralBrush;
        _p14HoldText!.Text = "HOLD —";
        _p14CtSummaryText!.Text = "CT SAT HV — · LV —";
        _p14EvidenceReliabilityText!.Text = "Evidence reliable HV 0/3 · LV 0/3";
        _p14AppliedPolicyText!.Text = "Policy —";
        _p14PhaseAText!.Text = "A · —";
        _p14PhaseBText!.Text = "B · —";
        _p14PhaseCText!.Text = "C · —";
        _p14SecurityReasonText!.Text = "P13 runtime not applied.";
    }

    private void EnsureP14Controls()
    {
        if (_p14EnabledCheck is null ||
            _p14MinimumBiasText is null ||
            _p14MinimumBiasIncreaseText is null ||
            _p14InitialDiffBiasText is null ||
            _p14ArmingDurationText is null ||
            _p14SecurityHoldText is null ||
            _p14DistortionText is null ||
            _p14AsymmetryText is null ||
            _p14SevereDistortionText is null ||
            _p14SuperviseHighSetCheck is null ||
            _p14SuperviseRefCheck is null)
        {
            throw new InvalidOperationException("P13 practitioner controls are not initialized.");
        }
    }

    private TextBox P14TextBox(string text)
        => new()
        {
            Text = text,
            Style = (Style)FindResource("DenseTextBox")
        };

    private Grid P14TwoFieldGrid(
        string leftLabel,
        UIElement leftControl,
        string rightLabel,
        UIElement rightControl,
        Thickness? margin = null)
    {
        var grid = new Grid { Margin = margin ?? new Thickness(0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var left = new StackPanel();
        left.Children.Add(P14FieldLabel(leftLabel));
        left.Children.Add(leftControl);
        grid.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(P14FieldLabel(rightLabel));
        right.Children.Add(rightControl);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    private TextBlock P14FieldLabel(string text)
        => new()
        {
            Text = text,
            Style = (Style)FindResource("FieldLabel")
        };

    private TextBlock P14SectionLabel(string text)
        => new()
        {
            Text = text,
            Style = (Style)FindResource("SectionLabel")
        };

    private TextBlock P14TinyText(string text)
        => new()
        {
            Text = text,
            Style = (Style)FindResource("TinyValue"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };

    private static string P14PhaseList(IReadOnlyCollection<string> phases)
        => phases.Count == 0 ? "—" : string.Join(",", phases);

    private static string P14CtFlag(bool suspected, bool blocked)
        => blocked ? "SAT/BLOCK" : suspected ? "SAT" : "clear";
}
