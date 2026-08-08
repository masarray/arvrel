using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls.Transformer;
using Arvrel.Application.Ied;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _unifiedIedSelectorInstalled;
    private bool _unifiedIedSelectorLoadHooked;
    private Border? _transformerLanding;
    private TransformerVirtualRelayControl? _transformerFaceplate;
    private TransformerIedWindow? _transformerWorkspaceWindow;
    private TransformerProtectionRuntimeSnapshot? _transformerLastSnapshot;
    private DispatcherTimer? _transformerFaceplateTimer;

    private enum UnifiedIedKind
    {
        ProtectionRelay,
        AutomaticVoltageRegulator,
        TransformerDifferential
    }

    private sealed record UnifiedIedChoice(
        UnifiedIedKind Kind,
        string DisplayName,
        string Function)
    {
        // WPF normally honors DisplayMemberPath, but SelectionBox rendering can fall
        // back to ToString when a control template is replaced by a platform/theme.
        // Keep the public selector readable under both paths.
        public override string ToString() => DisplayName;
    }

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
    /// P16 established the common OCR / AVR / Transformer selector. P17 keeps that
    /// selector and replaces the transformer placeholder landing page with a real
    /// operator-facing virtual relay front panel.
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

        // P17 intentionally does not pop the engineering window automatically.
        // The front panel is now the first-class operator view; F4 / ENGINEERING
        // opens the practitioner workspace only when the operator asks for it.
        ShowTransformerLanding();
    }

    private void EnsureTransformerLanding()
    {
        if (_transformerLanding is not null || Content is not Grid root)
            return;

        _transformerFaceplate = new TransformerVirtualRelayControl
        {
            Margin = new Thickness(12, 14, 14, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _transformerFaceplate.OpenPractitionerRequested += TransformerFaceplate_OpenPractitionerRequested;
        _transformerFaceplate.RunSelfTestRequested += TransformerFaceplate_RunSelfTestRequested;
        _transformerFaceplate.ResetRequested += TransformerFaceplate_ResetRequested;

        var shell = new Grid();
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(292) });
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var operatorRail = BuildTransformerOperatorRail();
        Grid.SetColumn(operatorRail, 0);
        shell.Children.Add(operatorRail);
        Grid.SetColumn(_transformerFaceplate, 1);
        shell.Children.Add(_transformerFaceplate);

        _transformerLanding = new Border
        {
            Background = BrushFrom("#F3F6F8"),
            BorderBrush = BrushFrom("#CBD4DA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(11),
            Visibility = Visibility.Collapsed,
            Child = shell
        };

        Grid.SetRow(_transformerLanding, 2);
        root.Children.Add(_transformerLanding);

        _transformerFaceplateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _transformerFaceplateTimer.Tick += TransformerFaceplateTimer_Tick;
        RefreshTransformerFaceplateEnvironment();
    }

    private FrameworkElement BuildTransformerOperatorRail()
    {
        var border = new Border
        {
            Background = BrushFrom("#F8FAFB"),
            BorderBrush = BrushFrom("#D6DEE3"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(18, 18, 16, 16)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "TRANSFORMER DIFFERENTIAL IED",
            FontSize = 9.8,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#667985")
        });
        stack.Children.Add(new TextBlock
        {
            Text = "87T · 87T-HS\nREF HV / LV",
            FontSize = 21,
            LineHeight = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#17232D"),
            Margin = new Thickness(0, 6, 0, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Operator front panel",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#3B6F91"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Use the relay LCD, navigation keys and F1–F5 exactly as the primary operator surface. Protection decisions shown here come from the existing transformer runtime; the panel contains no second 87T algorithm.",
            FontSize = 10.5,
            Foreground = BrushFrom("#526572"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        stack.Children.Add(TransformerRailBoundary(
            "VIRTUAL OUTPUT ONLY",
            "No physical trip · no GOOSE · no breaker output"));
        stack.Children.Add(TransformerRailBoundary(
            "PAIRED SV RUNTIME",
            "Live / Replay protection requires two distinct HV/LV Sampled Values streams."));
        stack.Children.Add(TransformerRailBoundary(
            "PUBLIC SELF-TEST",
            "10 deterministic core scenarios can run from F3 → ENTER with no SV required."));

        var engineeringButton = new Button
        {
            Content = "Open engineering workspace",
            MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 15, 0, 0),
            Padding = new Thickness(9, 5, 9, 5)
        };
        if (TryFindResource("PrimaryButton") is Style primaryButton)
            engineeringButton.Style = primaryButton;
        engineeringButton.Click += TransformerLanding_Open_Click;
        stack.Children.Add(engineeringButton);

        var testButton = new Button
        {
            Content = "Run 10-scenario self-test",
            MinHeight = 30,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 7, 0, 0),
            Padding = new Thickness(9, 5, 9, 5)
        };
        if (TryFindResource("CompactButton") is Style compactButton)
            testButton.Style = compactButton;
        testButton.Click += (_, _) => RunTransformerFaceplateSelfTest();
        stack.Children.Add(testButton);

        stack.Children.Add(new TextBlock
        {
            Text = "Shortcuts\nF1 Measurements · F2 Events · F3 Records/Self-test · F4 Engineering · F5 Reset",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 9.2,
            Foreground = BrushFrom("#667985"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 15, 0, 0)
        });

        border.Child = stack;
        return border;
    }

    private static Border TransformerRailBoundary(string heading, string detail)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 9.1,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#526572")
        });
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 9.7,
            Foreground = BrushFrom("#667985"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });
        return new Border
        {
            BorderBrush = BrushFrom("#D7E0E5"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(9, 7, 9, 7),
            Margin = new Thickness(0, 11, 0, 0),
            Child = stack
        };
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

        _transformerFaceplateTimer?.Start();
        RefreshTransformerFaceplateEnvironment();
        if (_transformerLastSnapshot is not null)
            _transformerFaceplate?.UpdateSnapshot(_transformerLastSnapshot);

        Title = "ARVREL — Transformer Differential IED Lab";
        if (_labSubtitleText is not null)
            _labSubtitleText.Text = "Transformer differential virtual relay · paired Sampled Values laboratory";
        EngineModeText.Text = "IED · TR-87T";
        StatusText.Text = "Transformer Differential selected. Virtual relay front panel is active; F4 opens paired-SV engineering and evidence.";
        AddEvent("IED", "Transformer Differential · TR-87T virtual relay selected");
    }

    private void HideTransformerLanding()
    {
        _transformerFaceplateTimer?.Stop();
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Collapsed;

        // A practitioner window belongs to the transformer IED session. Closing it on
        // IED change prevents a hidden transformer runtime from surviving under OCR/AVR.
        if (_transformerWorkspaceWindow is { IsVisible: true })
            _transformerWorkspaceWindow.Close();
    }

    private void TransformerFaceplateTimer_Tick(object? sender, EventArgs e)
        => RefreshTransformerFaceplateEnvironment();

    private void RefreshTransformerFaceplateEnvironment()
    {
        if (_transformerFaceplate is null)
            return;

        var mode = _processBus.IsReplayMode
            ? ProcessBusSourceMode.PcapReplay
            : _processBus.IsRunning
                ? ProcessBusSourceMode.LiveCapture
                : ProcessBusSourceMode.InternalDemo;
        var streamCount = _processBus.GetStreams().Count;
        var practitionerOpen = _transformerWorkspaceWindow is { IsVisible: true };
        _transformerFaceplate.UpdateEnvironment(mode, streamCount, practitionerOpen);
    }

    private void TransformerLanding_Open_Click(object sender, RoutedEventArgs e)
        => OpenTransformerIedWorkspace();

    private void TransformerFaceplate_OpenPractitionerRequested(object? sender, EventArgs e)
        => OpenTransformerIedWorkspace();

    private void TransformerFaceplate_RunSelfTestRequested(object? sender, EventArgs e)
        => RunTransformerFaceplateSelfTest();

    private void RunTransformerFaceplateSelfTest()
    {
        var report = TransformerPublicSelfTest.RunAll();
        _transformerFaceplate?.UpdateSelfTest(report);
        StatusText.Text = report.AllPassed
            ? $"Transformer deterministic self-test PASS · {report.PassedCount}/{report.Cases.Count}."
            : $"Transformer deterministic self-test FAIL · {report.PassedCount}/{report.Cases.Count}; open engineering workspace for full evidence.";
        AddEvent(report.AllPassed ? "87T TEST" : "87T TEST FAIL",
            $"{report.PassedCount}/{report.Cases.Count} · {report.SuiteId}");
    }

    private void TransformerFaceplate_ResetRequested(object? sender, EventArgs e)
    {
        if (_transformerWorkspaceWindow is not null &&
            _transformerWorkspaceWindow.TryResetRuntimeFromFaceplate(out var message))
        {
            StatusText.Text = message;
            AddEvent("87T RESET", message);
            return;
        }

        StatusText.Text = "No active transformer runtime to reset. Open engineering, bind HV/LV streams and apply the runtime first.";
    }

    private void OpenTransformerIedWorkspace()
    {
        // P15 deliberately allows the practitioner window to open with Internal Demo
        // or with no discovered SV streams. That path is used for deterministic packaged-
        // core self-test. Applying the live/replay runtime still requires two distinct
        // HV/LV streams and remains guarded by TransformerIedWindow.BuildConfiguration.
        if (_transformerWorkspaceWindow is { IsVisible: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            existing.Focus();
            return;
        }

        var window = new TransformerIedWindow(_processBus) { Owner = this };
        window.InitializeP14PractitionerUi();
        window.InitializeP15PublicTestUi();
        window.FaceplateSnapshotChanged += TransformerWorkspace_FaceplateSnapshotChanged;
        window.Closed += TransformerWorkspace_Closed;
        _transformerWorkspaceWindow = window;
        RefreshTransformerFaceplateEnvironment();

        // P17 uses a non-modal engineering window so the physical-style front panel
        // stays visible and operable while detailed paired-SV engineering is open.
        window.Show();
        window.Activate();
    }

    private void TransformerWorkspace_FaceplateSnapshotChanged(
        object? sender,
        TransformerProtectionRuntimeSnapshotChangedEventArgs e)
    {
        _transformerLastSnapshot = e.Snapshot;
        _transformerFaceplate?.UpdateSnapshot(e.Snapshot);
    }

    private void TransformerWorkspace_Closed(object? sender, EventArgs e)
    {
        if (sender is TransformerIedWindow window)
        {
            window.FaceplateSnapshotChanged -= TransformerWorkspace_FaceplateSnapshotChanged;
            window.Closed -= TransformerWorkspace_Closed;
        }

        _transformerWorkspaceWindow = null;
        _transformerFaceplate?.NoteRuntimeClosed();
        RefreshTransformerFaceplateEnvironment();
    }
}
