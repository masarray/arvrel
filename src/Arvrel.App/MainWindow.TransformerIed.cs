using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Arvrel.Application.Ied;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _unifiedIedSelectorInstalled;
    private bool _unifiedIedSelectorLoadHooked;
    private Border? _transformerLanding;

    private enum UnifiedIedKind
    {
        ProtectionRelay,
        AutomaticVoltageRegulator,
        TransformerDifferential
    }

    private sealed record UnifiedIedChoice(
        UnifiedIedKind Kind,
        string DisplayName,
        string Function);

    private static readonly IReadOnlyList<UnifiedIedChoice> UnifiedIedChoices =
    [
        new(
            UnifiedIedKind.ProtectionRelay,
            "Protection Relay · OCR",
            "50/51 · 50N/51N"),
        new(
            UnifiedIedKind.AutomaticVoltageRegulator,
            "AVR · OLTC Controller",
            "Automatic voltage regulation"),
        new(
            UnifiedIedKind.TransformerDifferential,
            "Transformer Differential · 87T / REF",
            "87T · 87T-HS · REF HV/LV")
    ];

    /// <summary>
    /// P16 upgrades the existing OCR/AVR selector into the authoritative three-IED selector.
    /// The method name is retained for startup compatibility with P12-P15.
    /// </summary>
    public void InitializeTransformerIedEntryPoint()
    {
        if (_unifiedIedSelectorInstalled)
            return;

        if (!IsLoaded)
        {
            if (!_unifiedIedSelectorLoadHooked)
            {
                _unifiedIedSelectorLoadHooked = true;
                Loaded += UnifiedIedSelector_Loaded;
            }

            return;
        }

        // Multi-IED initialization normally runs immediately before this method.
        // Calling it again is idempotent and covers unusual activation ordering.
        if (_iedTypeCombo is null)
            InitializeMultiIedWorkspace();

        InstallUnifiedIedSelector();
    }

    private void UnifiedIedSelector_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UnifiedIedSelector_Loaded;
        _unifiedIedSelectorLoadHooked = false;

        if (_iedTypeCombo is null)
            InitializeMultiIedWorkspace();

        InstallUnifiedIedSelector();
    }

    private void InstallUnifiedIedSelector()
    {
        if (_unifiedIedSelectorInstalled)
            return;

        if (_iedTypeCombo is null)
        {
            StatusText.Text = "Unified IED selector could not resolve the OCR/AVR selector.";
            return;
        }

        // PR #89 owns initial selector construction. P16 takes over only its choice model
        // and selection handler; the existing OCR and AVR workspaces remain untouched.
        _iedTypeCombo.SelectionChanged -= IedTypeCombo_SelectionChanged;
        _iedTypeCombo.ItemsSource = UnifiedIedChoices;
        _iedTypeCombo.DisplayMemberPath = nameof(UnifiedIedChoice.DisplayName);
        TextSearch.SetTextPath(_iedTypeCombo, nameof(UnifiedIedChoice.DisplayName));
        _iedTypeCombo.ToolTip = "Select OCR, AVR / OLTC, or Transformer Differential IED";
        _iedTypeCombo.SelectionChanged += UnifiedIedTypeCombo_SelectionChanged;

        EnsureTransformerLanding();
        _unifiedIedSelectorInstalled = true;
        _iedTypeCombo.SelectedIndex = 0;

        AddEvent("IED", "Unified IED selector ready · OCR + AVR + Transformer Differential");
    }

    private async void UnifiedIedTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_iedTypeCombo?.SelectedItem is not UnifiedIedChoice choice || !_unifiedIedSelectorInstalled)
            return;

        if (choice.Kind == UnifiedIedKind.AutomaticVoltageRegulator)
        {
            HideTransformerLanding();
            _internalRunning = false;
            UpdateRunButton();

            if (_sourceRunning)
            {
                try
                {
                    await _processBus.StopAsync().ConfigureAwait(true);
                    _sourceRunning = false;
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
                {
                    AddEvent("IED WARN", $"Process-bus stop while changing IED: {ex.Message}");
                }
            }

            SelectIed(VirtualIedKind.AutomaticVoltageRegulator);
            return;
        }

        if (_avrWorkspace?.IsRunning == true)
            _avrWorkspace.ToggleRun();

        if (choice.Kind == UnifiedIedKind.ProtectionRelay)
        {
            HideTransformerLanding();
            SelectIed(VirtualIedKind.ProtectionRelay);
            return;
        }

        ShowTransformerLanding();
        OpenTransformerIedWorkspace();
    }

    private void EnsureTransformerLanding()
    {
        if (_transformerLanding is not null || Content is not Grid root)
            return;

        var openButton = new Button
        {
            Content = "Open 87T / REF workspace",
            MinHeight = 31,
            Padding = new Thickness(12, 5, 12, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 13, 0, 0)
        };
        if (TryFindResource("PrimaryButton") is Style primaryButton)
            openButton.Style = primaryButton;
        openButton.Click += TransformerLanding_Open_Click;

        var content = new StackPanel
        {
            Width = 700,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, 22, 24, 22)
        };
        content.Children.Add(new TextBlock
        {
            Text = "TRANSFORMER DIFFERENTIAL IED",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#667985")
        });
        content.Children.Add(new TextBlock
        {
            Text = "87T · 87T-HS · REF HV/LV",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#17232D"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Two-winding paired-SV protection workspace with Is1/K1/Is2/K2 restraint, H2/H5 security, CT-saturation / external-fault evidence, and deterministic public self-test.",
            FontSize = 11.5,
            Foreground = BrushFrom("#526572"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 650,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var boundary = new Border
        {
            BorderBrush = BrushFrom("#D7E0E5"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 14, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        boundary.Child = new TextBlock
        {
            Text = "Virtual protection only · no physical trip / GOOSE / breaker output",
            FontSize = 10.5,
            Foreground = BrushFrom("#667985")
        };
        content.Children.Add(boundary);
        content.Children.Add(openButton);
        content.Children.Add(new TextBlock
        {
            Text = "First public check: RUN 10-SCENARIO SELF-TEST inside the transformer workspace. Live/Replay protection still requires two distinct HV/LV SV streams.",
            FontSize = 10.5,
            Foreground = BrushFrom("#667985"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 650,
            Margin = new Thickness(0, 10, 0, 0)
        });

        _transformerLanding = new Border
        {
            Background = BrushFrom("#F7F9FA"),
            BorderBrush = BrushFrom("#CBD4DA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(11),
            Visibility = Visibility.Collapsed,
            Child = content
        };

        Grid.SetRow(_transformerLanding, 2);
        root.Children.Add(_transformerLanding);
    }

    private void ShowTransformerLanding()
    {
        EnsureTransformerLanding();

        if (_protectionToolbar is not null)
            _protectionToolbar.Visibility = Visibility.Visible;
        if (_protectionWorkspace is not null)
            _protectionWorkspace.Visibility = Visibility.Collapsed;
        if (_avrToolbar is not null)
            _avrToolbar.Visibility = Visibility.Collapsed;
        if (_avrWorkspace is not null)
            _avrWorkspace.Visibility = Visibility.Collapsed;
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Visible;

        OperatingModeCombo.Visibility = Visibility.Collapsed;
        if (_topHealthBadge is not null)
            _topHealthBadge.Visibility = Visibility.Visible;

        Title = "ARVREL — Transformer Differential IED Lab";
        if (_labSubtitleText is not null)
            _labSubtitleText.Text = "Two-winding transformer differential · paired Sampled Values laboratory";
        EngineModeText.Text = "IED · 87T / REF";
        StatusText.Text = "Transformer Differential selected. Open the 87T / REF workspace for self-test, paired-SV engineering, protection and evidence.";
        AddEvent("IED", "Transformer Differential · 87T / REF selected");
    }

    private void HideTransformerLanding()
    {
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Collapsed;
    }

    private void TransformerLanding_Open_Click(object sender, RoutedEventArgs e)
        => OpenTransformerIedWorkspace();

    private void OpenTransformerIedWorkspace()
    {
        // P15 deliberately allows the transformer workspace to open with Internal Demo
        // or with no discovered SV streams. That path is used only for the deterministic
        // packaged-core self-test. Applying the live/replay runtime still requires two
        // distinct HV/LV streams and remains guarded by TransformerIedWindow.BuildConfiguration.
        var window = new TransformerIedWindow(_processBus) { Owner = this };
        window.InitializeP14PractitionerUi();
        window.InitializeP15PublicTestUi();
        window.ShowDialog();
    }
}
