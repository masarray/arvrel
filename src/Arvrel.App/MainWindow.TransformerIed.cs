using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls.Transformer;
using Arvrel.App.Controls.VirtualRelay;
using Arvrel.Application.Ied;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.App;

public partial class MainWindow
{
    private bool _unifiedIedSelectorInstalled;
    private bool _unifiedIedSelectorLoadHooked;
    private Border? _transformerLanding;
    private VirtualRelayControl? _transformerFaceplate;
    private TransformerVirtualRelayPresenter? _transformerFaceplatePresenter;
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

        // Transformer reuses both the exact P6/OCR hardware and the complete OCR
        // operator workspace. Only the relay presentation and protection projection differ.
        ShowTransformerLanding();
    }

    private void EnsureTransformerLanding()
    {
        if (_transformerLanding is not null || Content is not Grid root)
            return;

        _transformerFaceplate = new VirtualRelayControl
        {
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _transformerFaceplatePresenter = new TransformerVirtualRelayPresenter(
            _transformerFaceplate,
            LedOffBrush,
            HealthyBrush,
            WarningBrush,
            TripBrush,
            OpenTransformerIedWorkspace,
            RunTransformerFaceplateSelfTest,
            ResetTransformerFaceplateRuntime);

        // This hidden fallback is not the normal Transformer operator surface. The
        // normal path mounts the shared VirtualRelayControl into the OCR relay host so
        // waveform, injection, phasor and operation evidence remain exactly in place.
        _transformerLanding = new Border
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(8),
            Visibility = Visibility.Collapsed,
            Child = _transformerFaceplate
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

    private void ShowTransformerLanding()
    {
        EnsureTransformerLanding();

        if (_protectionToolbar is not null)
            _protectionToolbar.Visibility = Visibility.Visible;
        if (_protectionWorkspace is not null)
            _protectionWorkspace.Visibility = Visibility.Visible;
        if (_avrToolbar is not null)
            _avrToolbar.Visibility = Visibility.Collapsed;
        if (_avrWorkspace is not null)
            _avrWorkspace.Visibility = Visibility.Collapsed;
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Collapsed;

        OperatingModeCombo.Visibility = Visibility.Visible;
        if (_topHealthBadge is not null)
            _topHealthBadge.Visibility = Visibility.Visible;

        // Normal operator path: keep the complete OCR waveform/injection/phasor
        // workspace and replace only the physical relay child in its right-hand host.
        var mountedInSharedWorkspace = MountTransformerFaceplateIntoOcrWorkspace();
        if (!mountedInSharedWorkspace && _transformerLanding is not null)
        {
            // Defensive fallback only. It preserves access to the Transformer relay if
            // a future shell refactor makes the OCR host temporarily unresolvable.
            if (_protectionWorkspace is not null)
                _protectionWorkspace.Visibility = Visibility.Collapsed;
            _transformerLanding.Child = _transformerFaceplate;
            _transformerLanding.Visibility = Visibility.Visible;
            OperatingModeCombo.Visibility = Visibility.Collapsed;
        }

        _transformerFaceplateTimer?.Start();
        RefreshTransformerFaceplateEnvironment();
        if (_transformerLastSnapshot is not null)
        {
            _transformerFaceplatePresenter?.UpdateSnapshot(_transformerLastSnapshot);
            RenderTransformerOperationWorkspace();
        }

        Title = "ARVREL — Transformer Differential IED Lab";
        if (_labSubtitleText is not null)
            _labSubtitleText.Text = "Transformer differential virtual relay · paired Sampled Values laboratory";
        EngineModeText.Text = "IED · TR-87T";
        StatusText.Text = mountedInSharedWorkspace
            ? "Transformer Differential selected. OCR waveform / injection / phasor workspace is reused; F4 opens paired-SV engineering and evidence."
            : "Transformer Differential selected. Shared relay fallback active; F4 opens paired-SV engineering and evidence.";
        AddEvent("IED", mountedInSharedWorkspace
            ? "Transformer Differential · complete OCR operator workspace reused"
            : "Transformer Differential · shared relay fallback selected");
    }

    private void HideTransformerLanding()
    {
        _transformerFaceplateTimer?.Stop();
        RestoreOcrRelayIntoSharedWorkspace();
        if (_transformerLanding is not null)
            _transformerLanding.Visibility = Visibility.Collapsed;

        if (_transformerWorkspaceWindow is { IsVisible: true })
            _transformerWorkspaceWindow.Close();
    }

    private void TransformerFaceplateTimer_Tick(object? sender, EventArgs e)
        => RefreshTransformerFaceplateEnvironment();

    private void RefreshTransformerFaceplateEnvironment()
    {
        if (_transformerFaceplatePresenter is null)
            return;

        var mode = _processBus.IsReplayMode
            ? ProcessBusSourceMode.PcapReplay
            : _processBus.IsRunning
                ? ProcessBusSourceMode.LiveCapture
                : ProcessBusSourceMode.InternalDemo;
        var streamCount = _processBus.GetStreams().Count;
        var engineeringOpen = _transformerWorkspaceWindow is { IsVisible: true };
        _transformerFaceplatePresenter.UpdateEnvironment(mode, streamCount, engineeringOpen);
    }

    private void RunTransformerFaceplateSelfTest()
    {
        var report = TransformerPublicSelfTest.RunAll();
        _transformerFaceplatePresenter?.UpdateSelfTest(report);
        StatusText.Text = report.AllPassed
            ? $"Transformer deterministic self-test PASS · {report.PassedCount}/{report.Cases.Count}."
            : $"Transformer deterministic self-test FAIL · {report.PassedCount}/{report.Cases.Count}; open engineering workspace for full evidence.";
        AddEvent(report.AllPassed ? "87T TEST" : "87T TEST FAIL",
            $"{report.PassedCount}/{report.Cases.Count} · {report.SuiteId}");
    }

    private void ResetTransformerFaceplateRuntime()
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
        // P15 deliberately allows this window to open with no discovered SV streams
        // for deterministic packaged-core self-test. Applying the live/replay runtime
        // still requires two distinct HV/LV streams inside TransformerIedWindow.
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
        window.InitializeP17FaceplateBridge();
        window.FaceplateSnapshotChanged += TransformerWorkspace_FaceplateSnapshotChanged;
        window.Closed += TransformerWorkspace_Closed;
        _transformerWorkspaceWindow = window;
        RefreshTransformerFaceplateEnvironment();

        // Non-modal by design: the shared physical relay remains visible while
        // paired-SV settings and engineering evidence are inspected.
        window.Show();
        window.Activate();
    }

    private void TransformerWorkspace_FaceplateSnapshotChanged(
        object? sender,
        TransformerProtectionRuntimeSnapshotChangedEventArgs e)
    {
        _transformerLastSnapshot = e.Snapshot;
        _transformerFaceplatePresenter?.UpdateSnapshot(e.Snapshot);
        RenderTransformerOperationWorkspace();
    }

    private void TransformerWorkspace_Closed(object? sender, EventArgs e)
    {
        if (sender is TransformerIedWindow window)
        {
            window.FaceplateSnapshotChanged -= TransformerWorkspace_FaceplateSnapshotChanged;
            window.Closed -= TransformerWorkspace_Closed;
        }

        _transformerWorkspaceWindow = null;
        _transformerFaceplatePresenter?.NoteRuntimeClosed();
        RefreshTransformerFaceplateEnvironment();
    }
}
