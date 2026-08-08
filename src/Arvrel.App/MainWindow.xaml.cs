using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Arvrel.App.Controls;
using Arvrel.App.Infrastructure;
using Arvrel.App.Services;
using Arvrel.ProcessBus;
using Arvrel.Protection;
using Arvrel.Protection.Algorithms;
using Microsoft.Win32;

namespace Arvrel.App;

public partial class MainWindow : Window
{
    private static readonly Brush LedOffBrush = Freeze(new SolidColorBrush(Color.FromRgb(81, 96, 106)));
    private static readonly Brush HealthyBrush = Freeze(new SolidColorBrush(Color.FromRgb(70, 154, 88)));
    private static readonly Brush WarningBrush = Freeze(new SolidColorBrush(Color.FromRgb(196, 139, 43)));
    private static readonly Brush TripBrush = Freeze(new SolidColorBrush(Color.FromRgb(199, 71, 67)));
    private static readonly Brush PhaseBrush = Freeze(new SolidColorBrush(Color.FromRgb(65, 139, 191)));

    private ProtectionSettings _settings = new();
    private readonly ResearchProtectionEngine _internalEngine;
    private readonly DeterministicLabScenario _scenario = new();
    private SmvProcessBusController _processBus;
    private readonly DispatcherTimer _timer;
    private readonly Queue<string> _events = new();
    private ProtectionSnapshot _snapshot;
    private bool _internalRunning;
    private bool _sourceRunning;
    private bool _lastPickup;
    private bool _lastTrip;
    private bool _lastBlocked;
    private double _pickupPosition = double.NaN;
    private double _tripPosition = double.NaN;
    private string? _selectedStreamKey;
    private string? _loadedSclPath;
    private int _streamRefreshDivider;

    public MainWindow()
    {
        InitializeComponent();
        _internalEngine = new ResearchProtectionEngine(_settings);
        _processBus = CreateProcessBus(_settings);
        _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
        Loaded += MainWindow_Loaded;

        EngineModeText.Text = SmvProcessBusController.IsAvailable
            ? "P1.1 · ARIEC61850 SIBLING READY"
            : SiblingEngineStatus.Label;
        StreamCombo.ItemsSource = new[] { new SmvStreamInfo("internal", 0x4000, "MU01", "Internal", null, "GOOD", true) };
        StreamCombo.SelectedIndex = 0;
        AddEvent("READY", "Protection engine initialized");
        AddEvent("HEALTH", "SMV trust guard permits trip");
        UpdateSettingSummaries();
        UpdateOperatingModeUi();
        RenderInitialFrame();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshAdapters();
        UpdateSourceUi();
        UpdateOperatingModeUi();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        try
        {
            switch (SourceCombo.SelectedIndex)
            {
                case 0:
                    ToggleInternalRun();
                    break;
                case 1:
                    await ToggleLiveCaptureAsync().ConfigureAwait(true);
                    break;
                case 2:
                    await ReplayCaptureAsync().ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            AddEvent("ERROR", ex.Message);
            StatusText.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "ARVREL process bus", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunButton.IsEnabled = true;
            UpdateRunButton();
        }
    }

    private void ToggleInternalRun()
    {
        _internalRunning = !_internalRunning;
        StatusText.Text = _internalRunning
            ? "Internal laboratory running. Inject A-G fault to observe pickup, timing, and virtual trip."
            : "Internal laboratory paused; the two-cycle evidence window remains stationary.";
        AddEvent(_internalRunning ? "RUN" : "PAUSE", _internalRunning ? "Internal laboratory started" : "Internal laboratory paused");
        UpdateRunButton();
    }

    private async Task ToggleLiveCaptureAsync()
    {
        if (_sourceRunning)
        {
            await _processBus.StopAsync().ConfigureAwait(true);
            _sourceRunning = false;
            StatusText.Text = "Live Npcap capture stopped.";
            return;
        }

        if (AdapterCombo.SelectedItem is not ProcessBusAdapter adapter)
            throw new InvalidOperationException("Select an Npcap adapter before starting live capture.");

        ResetTransitionMarkers();
        await _processBus.StartLiveAsync(adapter.Selector).ConfigureAwait(true);
        _sourceRunning = true;
        StatusText.Text = $"Listening for IEC 61850 Sampled Values on {adapter.DisplayName}.";
        AddEvent("LIVE", adapter.DisplayName);
    }

    private async Task ReplayCaptureAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open IEC 61850 Sampled Values capture",
            Filter = "Packet capture (*.pcap;*.pcapng)|*.pcap;*.pcapng|Classic PCAP (*.pcap)|*.pcap|PCAPNG (*.pcapng)|*.pcapng|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        ResetTransitionMarkers();
        StatusText.Text = $"Replaying {System.IO.Path.GetFileName(dialog.FileName)}...";
        var count = await _processBus.ReplayAsync(dialog.FileName).ConfigureAwait(true);
        RefreshStreamList(force: true);
        RenderSelectedProcessBusStream();
        StatusText.Text = $"Replay complete · {count:N0} captured frame(s) processed.";
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (SourceCombo.SelectedIndex == 0)
        {
            if (_internalRunning)
            {
                ScenarioStep? last = null;
                for (var index = 0; index < 8; index++)
                {
                    last = _scenario.Advance(TimeSpan.FromMilliseconds(5), _pickupPosition, _tripPosition);
                    _snapshot = _internalEngine.Evaluate(last.Measurement);
                    ObserveTransitions(_snapshot);
                }
                if (last is not null)
                    RenderInternal(last, _snapshot);
            }
            return;
        }

        _streamRefreshDivider++;
        if (_streamRefreshDivider >= 6)
        {
            _streamRefreshDivider = 0;
            RefreshStreamList(force: false);
        }
        RenderSelectedProcessBusStream();
    }

    private void InjectFault_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        _scenario.FaultActive = !_scenario.FaultActive;
        AddEvent(_scenario.FaultActive ? "FAULT" : "CLEAR", _scenario.FaultActive ? "A-G fault scenario injected" : "Fault scenario removed");
        StatusText.Text = _scenario.FaultActive
            ? "A-G fault active. Phase A and residual current are ramping into the protection elements."
            : "Fault removed. Observe dropout and keep the latched trip until reset.";
        if (!_internalRunning)
            ToggleInternalRun();
    }

    private void DegradeSmv_Click(object sender, RoutedEventArgs e)
    {
        if (SourceCombo.SelectedIndex != 0)
            return;

        _scenario.SmvDegraded = !_scenario.SmvDegraded;
        AddEvent(_scenario.SmvDegraded ? "SMV WARN" : "SMV GOOD", _scenario.SmvDegraded ? "smpCnt discontinuity: trip permission removed" : "SMV trust restored");
        StatusText.Text = _scenario.SmvDegraded
            ? "SMV degraded. Measurement and pickup stay visible, but any new trip request is blocked."
            : "SMV trust restored; trip permission is available again.";
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        ResetTransitionMarkers();
        if (SourceCombo.SelectedIndex == 0)
        {
            _internalEngine.Reset();
            _scenario.Reset();
            _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
            RenderInitialFrame();
            AddEvent("RESET", "Internal trip latch, timers, and scenario reset");
            StatusText.Text = "Reset complete. Laboratory returned to healthy nominal current.";
        }
        else
        {
            _processBus.ResetProtection(_selectedStreamKey);
            AddEvent("RESET", "Selected live/replay relay state reset");
            RenderSelectedProcessBusStream();
        }
    }

    private void OperatingModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            UpdateOperatingModeUi();
    }

    private void UpdateOperatingModeUi()
    {
        if (AlgorithmButton is null)
            return;
        var research = OperatingModeCombo.SelectedIndex == 1;
        AlgorithmButton.Visibility = research ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded)
        {
            StatusText.Text = research
                ? "Research mode active. Standard and custom algorithms can run A/B on the same virtual relay measurements."
                : "Practitioner mode active. Configure and operate the relay through native settings.";
            AddEvent("MODE", research ? "Research algorithm laboratory exposed" : "Practitioner relay workflow active");
        }
    }

    private void OpenAlgorithmEditor_Click(object sender, RoutedEventArgs e)
    {
        if (OperatingModeCombo.SelectedIndex != 1)
        {
            MessageBox.Show(this, "Switch to Research mode to expose active algorithm source and the deterministic algorithm laboratory.", "ARVREL operating mode", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var before = AlgorithmRuntimeRegistry.Snapshot();
        new AlgorithmEditorWindow(_settings) { Owner = this }.ShowDialog();
        var after = AlgorithmRuntimeRegistry.Snapshot();
        if (after.Generation != before.Generation)
        {
            ResetTransitionMarkers();
            var latest = after.AuditTrail.LastOrDefault(item => item.Generation > before.Generation);
            if (latest is not null)
                AddEvent(latest.Action, $"{latest.Element} · {latest.Version ?? "standard"}");
            StatusText.Text = after.HasActiveCustom
                ? "Custom research algorithm active · STANDARD remains shadow reference · VIRTUAL OUTPUT ONLY."
                : after.Staged.Count > 0
                    ? "Custom research definition staged and shadowing the standard algorithm."
                    : "Standard protection algorithm active.";
        }
    }

    private async void ProtectionSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProtectionSettingsWindow(_settings, _processBus.MeasurementContext) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } settings)
            return;

        try
        {
            settings.Validate();
            _settings = settings;
            _internalEngine.UpdateSettings(settings, keepTripLatch: false);
            _scenario.Reset();
            _snapshot = ProtectionSnapshot.Ready(DateTimeOffset.UtcNow);
            ResetTransitionMarkers();
            await RecreateProcessBusAsync().ConfigureAwait(true);
            UpdateSettingSummaries();
            RenderInitialFrame();
            AddEvent("SETTINGS", $"{settings.GroupName} rev {settings.Revision} · {settings.Fingerprint()[..12]}");
            StatusText.Text = $"Protection settings applied · {settings.GroupName} revision {settings.Revision}. Timers and virtual trip latch reset.";
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, "Protection settings apply failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MeasurementContext_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new MeasurementContextWindow(_processBus.MeasurementContext) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } context)
        {
            _processBus.SetMeasurementContext(context);
            StatusText.Text = $"CT context applied · {context.CtPrimaryA:0.###}/{context.CtSecondaryA:0.###} A · {context.NominalFrequencyHz:0.###} Hz.";
            RenderSelectedProcessBusStream();
        }
    }

    private void ImportScl_Click(object sender, RoutedEventArgs e)
    {
        if (!SmvProcessBusController.IsAvailable)
        {
            MessageBox.Show(this, "Importing SCL requires the sibling ARIEC61850 repository at C:\\Git\\ARIEC61850.", "ARVREL SCL", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select IEC 61850 SCL file",
            Filter = "IEC 61850 SCL (*.scd;*.cid;*.icd;*.iid)|*.scd;*.cid;*.icd;*.iid|XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _processBus.LoadScl(dialog.FileName);
            _loadedSclPath = dialog.FileName;
            SclStatusText.Text = _processBus.SclSummary.ToUpperInvariant();
            StatusText.Text = $"SCL loaded · {_processBus.SclSummary}. Existing and new streams will be matched by APPID, destination, VLAN, svID, and dataset evidence.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "SCL import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (SourceCombo.SelectedIndex != 0 && !SmvProcessBusController.IsAvailable)
        {
            SourceCombo.SelectedIndex = 0;
            MessageBox.Show(this, "Live Npcap and PCAP replay require the sibling ARIEC61850 checkout.\n\nExpected layout:\nC:\\Git\\ARIEC61850\nC:\\Git\\arvrel", "ARVREL process-bus engine", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_sourceRunning)
        {
            await _processBus.StopAsync().ConfigureAwait(true);
            _sourceRunning = false;
        }
        _internalRunning = false;
        ResetTransitionMarkers();
        UpdateSourceUi();
        UpdateRunButton();
    }

    private void StreamCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StreamCombo.SelectedItem is SmvStreamInfo stream)
        {
            if (!string.Equals(_selectedStreamKey, stream.Key, StringComparison.Ordinal))
                ResetTransitionMarkers();
            _selectedStreamKey = stream.Key;
            RenderSelectedProcessBusStream();
        }
    }

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
            RenderSelectedProcessBusStream();
    }

    private void ExportEvidence_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export ARVREL evidence",
            Filter = "ARVREL evidence JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ARVREL-evidence-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        object evidence;
        if (SourceCombo.SelectedIndex == 0)
        {
            var algorithmRuntime = AlgorithmRuntimeRegistry.Snapshot();
            evidence = new
            {
                schemaVersion = 3,
                exportedAt = DateTimeOffset.Now,
                application = "ARVREL",
                operatingMode = OperatingModeCombo.SelectedIndex == 1 ? "Research" : "Practitioner",
                sourceMode = "Internal deterministic laboratory",
                measurement = new { phaseA = _scenario.FaultActive ? 8.4 : 1.0, phaseB = 1.02, phaseC = 0.98 },
                protection = _snapshot,
                protectionSettings = _settings,
                settingsFingerprint = _settings.Fingerprint(),
                algorithmMode = algorithmRuntime.Mode,
                algorithmRuntime,
                events = _events.Reverse().ToArray()
            };
        }
        else
        {
            var snapshot = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
            evidence = new ProcessBusEvidence(
                DateTimeOffset.Now,
                "ARVREL",
                SourceCombo.SelectedIndex == 1 ? "Live Npcap" : "PCAP/PCAPNG replay",
                snapshot,
                _settings,
                _processBus.MeasurementContext,
                _events.Reverse().ToArray());
        }

        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        AddEvent("EXPORT", System.IO.Path.GetFileName(dialog.FileName));
        StatusText.Text = $"Evidence exported to {dialog.FileName}.";
    }

    private SmvProcessBusController CreateProcessBus(ProtectionSettings settings)
    {
        var controller = new SmvProcessBusController(settings);
        controller.EventRaised += ProcessBus_EventRaised;
        return controller;
    }

    private async Task RecreateProcessBusAsync()
    {
        var previous = _processBus;
        var context = previous.MeasurementContext;
        previous.EventRaised -= ProcessBus_EventRaised;
        await previous.DisposeAsync().ConfigureAwait(true);

        _processBus = CreateProcessBus(_settings);
        _processBus.SetMeasurementContext(context);
        if (!string.IsNullOrWhiteSpace(_loadedSclPath) && File.Exists(_loadedSclPath))
            _processBus.LoadScl(_loadedSclPath);

        _sourceRunning = false;
        _selectedStreamKey = SourceCombo.SelectedIndex == 0 ? "internal" : null;
        RefreshAdapters();
        UpdateSourceUi();
        SclStatusText.Text = _processBus.SclSummary == "No SCL loaded" ? "NO SCL" : _processBus.SclSummary.ToUpperInvariant();
    }

    private void RefreshAdapters()
    {
        var adapters = _processBus.ListAdapters();
        AdapterCombo.ItemsSource = adapters;
        AdapterCombo.SelectedIndex = adapters.Count > 0 ? 0 : -1;
        if (SmvProcessBusController.IsAvailable && adapters.Count == 0)
            StatusText.Text = "No Npcap adapter found. Install Npcap and refresh the application.";
    }

    private void RefreshStreamList(bool force)
    {
        var streams = _processBus.GetStreams();
        if (!force && streams.Count == StreamCombo.Items.Count && streams.Any(stream => stream.Key == _selectedStreamKey))
            return;

        var previousKey = _selectedStreamKey;
        StreamCombo.ItemsSource = streams;
        var selected = streams.FirstOrDefault(stream => string.Equals(stream.Key, previousKey, StringComparison.Ordinal)) ?? streams.FirstOrDefault();
        StreamCombo.SelectedItem = selected;
        _selectedStreamKey = selected?.Key;
    }

    private void RenderInitialFrame()
    {
        var step = _scenario.Advance(TimeSpan.Zero, _pickupPosition, _tripPosition);
        RenderInternal(step, _snapshot);
    }

    private void RenderInternal(ScenarioStep step, ProtectionSnapshot snapshot)
    {
        SmvScope.Frame = step.Waveform with { PickupPosition = _pickupPosition, TripPosition = _tripPosition };
        RenderMeasurements(step.Measurement, snapshot);
        FrequencyText.Text = "50.000 Hz";
        SamplesPerCycleText.Text = "  ·  80 samples/cycle";
        SampleCounterText.Text = $"  ·  smpCnt {_scenario.SampleCounter:0000}";
        SyncText.Text = "  ·  smpSynch 2";
        SyncText.Foreground = HealthyBrush;
        FpsText.Text = "  ·  deterministic";
        StreamHealthText.Text = "  ·  INTERNAL · GOOD";
        WaveformSubtitleText.Text = "Stationary deterministic evidence window · pickup and trip markers remain visible";
        RelayFooterText.Text = $"{_settings.GroupName} rev {_settings.Revision} · deterministic SMV health gate active{AlgorithmRuntimeSuffix(snapshot.AlgorithmRuntime)}";
    }

    private void RenderSelectedProcessBusStream()
    {
        if (SourceCombo.SelectedIndex == 0)
            return;

        var snapshot = _processBus.GetSnapshot(_selectedStreamKey, ViewCombo.SelectedIndex == 1);
        if (string.IsNullOrWhiteSpace(snapshot.Stream.Key))
        {
            StreamHealthText.Text = "  ·  WAITING FOR SV";
            WaveformSubtitleText.Text = SourceCombo.SelectedIndex == 1
                ? "Listening for EtherType 0x88BA on the selected Npcap adapter"
                : "Open a PCAP or PCAPNG file to discover Sampled Values streams";
            return;
        }

        ObserveTransitions(snapshot.Protection);
        SmvScope.Frame = new WaveformFrame(
            snapshot.Waveform.PhaseA,
            snapshot.Waveform.PhaseB,
            snapshot.Waveform.PhaseC,
            snapshot.Waveform.Residual,
            snapshot.Waveform.FrequencyHz,
            _pickupPosition,
            _tripPosition);
        RenderMeasurements(snapshot.Measurement, snapshot.Protection);
        FrequencyText.Text = $"{snapshot.Waveform.FrequencyHz:0.000} Hz";
        SamplesPerCycleText.Text = $"  ·  {snapshot.SamplesPerCycle} samples/cycle";
        SampleCounterText.Text = $"  ·  smpCnt {snapshot.SampleCounter:0000}";
        SyncText.Text = $"  ·  smpSynch {snapshot.SampleSynchronization}";
        SyncText.Foreground = snapshot.SampleSynchronization > 0 ? HealthyBrush : WarningBrush;
        FpsText.Text = $"  ·  {snapshot.FramesPerSecond:0.0} fps";
        StreamHealthText.Text = $"  ·  {snapshot.Stream.Health} · APPID 0x{snapshot.Stream.AppId:X4}";
        StreamHealthText.Foreground = snapshot.Stream.Health switch
        {
            "GOOD" => HealthyBrush,
            "WARN" => WarningBrush,
            _ => TripBrush
        };
        WaveformSubtitleText.Text = $"{snapshot.MappingSummary} · {snapshot.ScalingSummary} · gaps {snapshot.SequenceGapCount} · quality {snapshot.InvalidQualityCount}";
        RelayFooterText.Text = $"{_settings.GroupName} rev {_settings.Revision} · {snapshot.TrustSummary}{AlgorithmRuntimeSuffix(snapshot.Protection.AlgorithmRuntime)}";
        LcdHeaderText.Text = ViewCombo.SelectedIndex == 1 ? "PRIMARY CURRENT" : "SECONDARY CURRENT";
    }

    private void RenderMeasurements(MeasurementFrame measurement, ProtectionSnapshot snapshot)
    {
        IaValueText.Text = FormatCurrent(measurement.PhaseA);
        IbValueText.Text = FormatCurrent(measurement.PhaseB);
        IcValueText.Text = FormatCurrent(measurement.PhaseC);
        ResidualValueText.Text = FormatCurrent(measurement.Residual);
        LcdIaText.Text = IaValueText.Text;
        LcdIbText.Text = IbValueText.Text;
        LcdIcText.Text = IcValueText.Text;
        LcdResidualText.Text = ResidualValueText.Text;

        SetElement(Phase50StateText, Phase50Progress, snapshot.Phase50);
        SetElement(Phase51StateText, Phase51Progress, snapshot.Phase51);
        SetElement(Earth50StateText, Earth50Progress, snapshot.Earth50);
        SetElement(Earth51StateText, Earth51Progress, snapshot.Earth51);

        var customActive = snapshot.AlgorithmRuntime.HasActiveCustom;
        ProtectionReasonText.Text = customActive
            ? $"  ·  CUSTOM ACTIVE · VIRTUAL ONLY · {snapshot.DecisionReason}"
            : $"  ·  {snapshot.DecisionReason}";
        var permitted = snapshot.SmvTrust.AllowsTrip;
        PermissionText.Text = permitted ? "TRIP PERMITTED" : "TRIP BLOCKED";
        PermissionText.Foreground = permitted ? HealthyBrush : WarningBrush;
        PermissionBadge.Background = permitted ? BrushFrom("#EAF5EC") : BrushFrom("#FBF2E3");
        PermissionBadge.BorderBrush = permitted ? BrushFrom("#B9D8BF") : BrushFrom("#E2C58F");

        var pickup = snapshot.Phase50.Pickup || snapshot.Phase51.Pickup || snapshot.Earth50.Pickup || snapshot.Earth51.Pickup;
        HealthyLed.Fill = snapshot.SmvTrust.AllowsMeasurement ? HealthyBrush : WarningBrush;
        TopHealthLed.Fill = customActive ? WarningBrush : snapshot.SmvTrust.AllowsTrip ? HealthyBrush : WarningBrush;
        TopHealthText.Text = customActive ? "CUSTOM ACTIVE" : snapshot.SmvTrust.AllowsTrip ? "LAB READY" : "TRIP BLOCKED";
        PickupLed.Fill = pickup ? WarningBrush : LedOffBrush;
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
                : pickup
                    ? $"PICKUP\n{snapshot.ActiveElement}"
                    : snapshot.SmvTrust.AllowsMeasurement
                        ? $"READY · {_settings.GroupName.ToUpperInvariant()}"
                        : $"MEASUREMENT BLOCKED\n{snapshot.SmvTrust.Code}";
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

    private void ResetTransitionMarkers()
    {
        _pickupPosition = double.NaN;
        _tripPosition = double.NaN;
        _lastPickup = false;
        _lastTrip = false;
        _lastBlocked = false;
    }

    private void UpdateSourceUi()
    {
        var internalMode = SourceCombo.SelectedIndex == 0;
        AdapterCombo.IsEnabled = SourceCombo.SelectedIndex == 1;
        InjectFaultButton.IsEnabled = internalMode;
        DegradeSmvButton.IsEnabled = internalMode;
        if (internalMode)
        {
            StreamCombo.ItemsSource = new[] { new SmvStreamInfo("internal", 0x4000, "MU01", "Internal", null, "GOOD", true) };
            StreamCombo.SelectedIndex = 0;
            _selectedStreamKey = "internal";
            RenderInitialFrame();
            StatusText.Text = "Internal deterministic laboratory selected.";
        }
        else
        {
            StreamCombo.ItemsSource = Array.Empty<SmvStreamInfo>();
            _selectedStreamKey = null;
            StreamHealthText.Text = "  ·  WAITING FOR SV";
            StatusText.Text = SourceCombo.SelectedIndex == 1
                ? "Select an Npcap adapter, import SCL, then start live capture."
                : "Select Run to open a PCAP or PCAPNG capture.";
        }
    }

    private void UpdateSettingSummaries()
    {
        Phase50SettingText.Text = _settings.PhaseInstantaneousEnabled
            ? $"50P-1 · {_settings.PhaseInstantaneousPickupA:0.###} A / {_settings.PhaseInstantaneousDelay.TotalMilliseconds:0.###} ms"
            : "50P-1 · DISABLED";
        Earth50SettingText.Text = _settings.EarthInstantaneousEnabled
            ? $"50N · {_settings.EarthInstantaneousPickupA:0.###} A / {_settings.EarthInstantaneousDelay.TotalMilliseconds:0.###} ms"
            : "50N · DISABLED";
        Phase51SettingText.Text = _settings.PhaseTimeEnabled
            ? $"51P · {CurveShortName(_settings.PhaseTimeCurve, _settings.PhaseTimeUserK, _settings.PhaseTimeUserAlpha, _settings.PhaseTimeUserC)} / TMS {_settings.PhaseTimeMultiplier:0.###}"
            : "51P · DISABLED";
        Earth51SettingText.Text = _settings.EarthTimeEnabled
            ? $"51N · {CurveShortName(_settings.EarthTimeCurve, _settings.EarthTimeUserK, _settings.EarthTimeUserAlpha, _settings.EarthTimeUserC)} / TMS {_settings.EarthTimeMultiplier:0.###}"
            : "51N · DISABLED";
        ActiveSettingsStatusText.Text = $"{_settings.GroupName.ToUpperInvariant()} · REV {_settings.Revision} · {_settings.Fingerprint()[..12]}";
    }

    private static string CurveShortName(IecCurveFamily family, double k, double alpha, double c)
        => IecCurveCalculator.GetParameters(family, k, alpha, c).ShortName;

    private void UpdateRunButton()
    {
        switch (SourceCombo.SelectedIndex)
        {
            case 0:
                RunButtonText.Text = _internalRunning ? "Pause lab" : "Run lab";
                RunButtonIcon.Kind = _internalRunning ? LucideIconKind.Pause : LucideIconKind.Play;
                RunButtonIcon.Filled = true;
                break;
            case 1:
                RunButtonText.Text = _sourceRunning ? "Stop capture" : "Start capture";
                RunButtonIcon.Kind = _sourceRunning ? LucideIconKind.CircleStop : LucideIconKind.Radio;
                RunButtonIcon.Filled = _sourceRunning;
                break;
            default:
                RunButtonText.Text = "Open capture";
                RunButtonIcon.Kind = LucideIconKind.FolderOpen;
                RunButtonIcon.Filled = false;
                break;
        }
    }

    private void ProcessBus_EventRaised(object? sender, ProcessBusEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            AddEvent(e.Code, e.Message);
            if (e.Code is "ERROR" or "REPLAY")
                StatusText.Text = e.Message;
        }));
    }

    private static void SetElement(TextBlock stateText, System.Windows.Controls.Primitives.RangeBase progress, ElementSnapshot element)
    {
        stateText.Text = element.State.ToString().ToUpperInvariant();
        stateText.Foreground = element.State switch
        {
            ProtectionStageState.Operated => TripBrush,
            ProtectionStageState.Blocked => WarningBrush,
            ProtectionStageState.Timing or ProtectionStageState.Pickup => WarningBrush,
            ProtectionStageState.Disabled => BrushFrom("#8A98A5"),
            _ => BrushFrom("#657586")
        };
        progress.Value = element.Progress * 100;
    }

    private static string AlgorithmRuntimeSuffix(AlgorithmRuntimeRegistrySnapshot runtime)
        => runtime.HasActiveCustom
            ? " · CUSTOM ACTIVE · VIRTUAL ONLY"
            : runtime.Staged.Count > 0
                ? " · CUSTOM SHADOW"
                : string.Empty;

    private void AddEvent(string code, string detail)
    {
        var normalized = detail.ReplaceLineEndings(" ");
        if (normalized.Length > 76)
            normalized = normalized[..73] + "...";
        _events.Enqueue($"{code,-11}{normalized}");
        while (_events.Count > 6)
            _events.Dequeue();
        EventTraceText.Text = string.Join(Environment.NewLine, _events.Reverse());
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
        _processBus.EventRaised -= ProcessBus_EventRaised;
        _processBus.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private static string FormatCurrent(double value) => value.ToString("0.00 'A'", CultureInfo.InvariantCulture);
    private static Brush BrushFrom(string value) => Freeze((SolidColorBrush)new BrushConverter().ConvertFromString(value)!);

    private static T Freeze<T>(T value) where T : Freezable
    {
        value.Freeze();
        return value;
    }
}
