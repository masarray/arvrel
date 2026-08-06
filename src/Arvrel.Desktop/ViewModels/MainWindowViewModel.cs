using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Arvrel.Application.Laboratory;
using Arvrel.Application.Workspace;
using Arvrel.Desktop.Infrastructure;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private static readonly TimeSpan SimulationSubstep = TimeSpan.FromMilliseconds(5);
    private const int SimulationIterationsPerUiTick = 8;

    private readonly ArvrelWorkspace _workspace;
    private readonly SmvProcessBusController _processBus;
    private readonly ConcurrentQueue<ProcessBusEventArgs> _processBusEvents = new();
    private InternalLabTick _currentTick;
    private DesktopPresentationSnapshot _presentation;
    private SmvRuntimeSnapshot _externalSnapshot = SmvRuntimeSnapshot.Empty;
    private ProtectionSettings _settings;
    private string _statusText = "Internal deterministic laboratory ready.";
    private string _injectionEditorStatus = "READY · configured source valid";
    private string _settingsEditorStatus = "READY · relay settings active";
    private string _externalSourceStatus = "IDLE · select a PCAP or PCAPNG capture";
    private string? _selectedPreset;
    private string? _replayFilePath;
    private string _frequencyText = "50";
    private SmvStreamInfo? _selectedExternalStream;
    private bool _syncInjectionEditor;
    private bool _syncSettingsEditor;
    private bool _injectionDraftDirty;
    private bool _settingsDraftDirty;
    private bool _replayBusy;
    private int _replayFrameCount;

    public MainWindowViewModel()
    {
        _settings = new ProtectionSettings();
        _workspace = new ArvrelWorkspace(_settings);
        _processBus = new SmvProcessBusController(_settings);
        _processBus.EventRaised += ProcessBus_EventRaised;
        _currentTick = _workspace.InternalLab.CaptureFrame();
        _presentation = CreateInternalPresentation(_currentTick);

        RunCommand = new RelayCommand(ToggleRun);
        FaultCommand = new RelayCommand(InjectAgFault);
        SmvCommand = new RelayCommand(ToggleSmvDegradation);
        ApplyInjectionCommand = new RelayCommand(() => TryApplyInjection(announce: true));
        ClearInjectionCommand = new RelayCommand(ClearInjection);
        ResetRelayCommand = new RelayCommand(ResetRelay);
        ResetLaboratoryCommand = new RelayCommand(ResetLaboratory);
        ApplySettingsCommand = new RelayCommand(ApplySettings);

        PresetNames = VirtualInjectionPresets.Names;
        InjectionChannels = new ObservableCollection<InjectionChannelViewModel>(
            Enum.GetValues<VirtualInjectionSignal>()
                .Select(signal => new InjectionChannelViewModel(signal)));
        foreach (var channel in InjectionChannels)
            channel.PropertyChanged += InjectionChannel_PropertyChanged;

        SettingsEditor = new ProtectionSettingsEditorViewModel();
        SettingsEditor.PropertyChanged += SettingsEditor_PropertyChanged;
        SettingsEditor.Phase50.PropertyChanged += SettingsEditor_PropertyChanged;
        SettingsEditor.Phase51.PropertyChanged += SettingsEditor_PropertyChanged;
        SettingsEditor.Earth50.PropertyChanged += SettingsEditor_PropertyChanged;
        SettingsEditor.Earth51.PropertyChanged += SettingsEditor_PropertyChanged;

        ProtectionElements = new ObservableCollection<ProtectionElementViewModel>
        {
            new("50P", "Phase instantaneous"),
            new("51P", "Phase inverse time"),
            new("50N", "Earth instantaneous"),
            new("51N", "Earth inverse time")
        };

        ExternalStreams = new ObservableCollection<SmvStreamInfo>();
        Events = new ObservableCollection<string>();
        SyncInjectionEditor(_workspace.InternalLab.Scenario.ActiveProfile);
        SyncSettingsEditor();
        ApplyTick(_currentTick);
        AddEvent("READY", "Avalonia relay and injection workspace connected to portable core");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RunCommand { get; }
    public ICommand FaultCommand { get; }
    public ICommand SmvCommand { get; }
    public ICommand ApplyInjectionCommand { get; }
    public ICommand ClearInjectionCommand { get; }
    public ICommand ResetRelayCommand { get; }
    public ICommand ResetLaboratoryCommand { get; }
    public ICommand ApplySettingsCommand { get; }

    public ObservableCollection<InjectionChannelViewModel> InjectionChannels { get; }
    public ObservableCollection<ProtectionElementViewModel> ProtectionElements { get; }
    public ObservableCollection<SmvStreamInfo> ExternalStreams { get; }
    public ObservableCollection<string> Events { get; }
    public IReadOnlyList<string> PresetNames { get; }
    public ProtectionSettingsEditorViewModel SettingsEditor { get; }

    public DesktopPresentationSnapshot CurrentPresentationSnapshot => _presentation;
    public ProtectionSnapshot CurrentProtectionSnapshot => _presentation.Protection;
    public string ProductTitle => "ARVREL";
    public string ProductSubtitle => "Virtual Protection Relay Laboratory";
    public string ShellVersion => "P6.4 · AVALONIA SOURCE PROJECTION";
    public string PlatformText => $"{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture}";
    public string SourceModeText => _presentation.SourceMode;
    public string StatusText => _statusText;
    public string InjectionEditorStatus => _injectionEditorStatus;
    public string SettingsEditorStatus => _settingsEditorStatus;
    public string ExternalSourceStatus => _externalSourceStatus;
    public bool InjectionDraftDirty => _injectionDraftDirty;
    public bool SettingsDraftDirty => _settingsDraftDirty;
    public bool IsReplayBusy => _replayBusy;
    public bool IsExternalSource => _workspace.SourceMode != WorkspaceSourceMode.InternalLaboratory;
    public bool IsReplaySource => _workspace.SourceMode == WorkspaceSourceMode.CaptureReplay;
    public bool IsInternalSource => !IsExternalSource;
    public string ReplayFilePath => _replayFilePath ?? string.Empty;
    public string ReplayFileName => string.IsNullOrWhiteSpace(_replayFilePath)
        ? "No capture selected"
        : Path.GetFileName(_replayFilePath);
    public string ReplayFrameCountText => $"{_replayFrameCount:N0} frame(s) processed";
    public string SelectedStreamText => SelectedExternalStream?.DisplayName ?? "No Sampled Values stream selected";
    public string StreamTrustText => _presentation.TrustSummary;
    public string StreamMappingText => _presentation.MappingSummary;
    public string StreamSourceText => _presentation.SourceSummary;
    public string StreamDiagnosticsText => _presentation.Diagnostics.Count == 0
        ? "No stream diagnostics"
        : string.Join(Environment.NewLine, _presentation.Diagnostics.Take(8));

    public SmvStreamInfo? SelectedExternalStream
    {
        get => _selectedExternalStream;
        set
        {
            if (Equals(_selectedExternalStream, value))
                return;
            _selectedExternalStream = value;
            OnPropertyChanged();
            RefreshExternalSnapshot();
        }
    }

    public string? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (string.Equals(_selectedPreset, value, StringComparison.Ordinal))
                return;
            _selectedPreset = value;
            OnPropertyChanged();
            if (!_syncInjectionEditor && !string.IsNullOrWhiteSpace(value))
                ApplyPreset(value, announce: true);
        }
    }

    public string FrequencyText
    {
        get => _frequencyText;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_frequencyText, value, StringComparison.Ordinal))
                return;
            _frequencyText = value;
            OnPropertyChanged();
            MarkInjectionDraftChanged();
        }
    }

    public string LiveCaptureStatus => _processBus.IsLiveCaptureAvailable
        ? $"{_processBus.LiveCaptureBackendName} live backend ready"
        : _processBus.CaptureAvailabilityMessage;

    public string ReplayStatus => _processBus.IsReplayAvailable
        ? "PCAP/PCAPNG replay decoder ready"
        : "Replay requires the ARIEC61850 decoder at build time";

    public bool IsRunning => IsInternalSource && _workspace.InternalLab.IsRunning;
    public bool FaultActive => IsInternalSource && string.Equals(
        _workspace.InternalLab.Scenario.ActiveProfile.Name,
        "A-G fault",
        StringComparison.Ordinal);
    public bool SmvDegraded => IsInternalSource && _workspace.InternalLab.Scenario.SmvDegraded;
    public bool AllowsTrip => _presentation.Protection.SmvTrust.AllowsTrip;
    public bool TripLatched => _presentation.Protection.TripLatched;
    public bool PickupActive =>
        _presentation.Protection.Phase50.Pickup ||
        _presentation.Protection.Phase51.Pickup ||
        _presentation.Protection.Earth50.Pickup ||
        _presentation.Protection.Earth51.Pickup;

    public string RunButtonText => IsRunning ? "STOP INJECTION" : "START INJECTION";
    public string FaultButtonText => "LOAD + START A-G FAULT";
    public string SmvButtonText => SmvDegraded ? "RESTORE SMV TRUST" : "DEGRADE SMV";
    public string OutputStateText => IsExternalSource
        ? IsReplaySource ? "REPLAY" : "LIVE"
        : _workspace.InternalLab.Scenario.OutputState.ToUpperInvariant();
    public string ProfileNameText => IsExternalSource
        ? _presentation.SourceIdentity
        : _workspace.InternalLab.Scenario.ActiveProfile.Name;
    public string InjectionFingerprintText => IsExternalSource
        ? SelectedExternalStream is null
            ? "STREAM NONE"
            : $"APPID {SelectedExternalStream.AppId:X4}"
        : $"INJ {_workspace.InternalLab.Scenario.InjectionFingerprint[..12]}";
    public string SettingsFingerprintText => $"SET {_settings.Fingerprint()[..12]}";
    public string SettingsGroupText => $"{_settings.GroupName} · REV {_settings.Revision}";
    public string ProvenanceText => IsExternalSource
        ? _presentation.MappingSummary
        : $"IN {_workspace.InternalLab.Scenario.NeutralCurrentProvenance} · " +
          $"VN {_workspace.InternalLab.Scenario.NeutralVoltageProvenance}";

    public string TripStateText => TripLatched
        ? $"TRIP LATCHED · {_presentation.Protection.ActiveElement}"
        : PickupActive
            ? $"PICKUP · {_presentation.Protection.ActiveElement}"
            : "READY · NO PICKUP";
    public string TrustStateText => AllowsTrip
        ? "SMV TRUST · TRIP PERMITTED"
        : $"SMV TRUST · TRIP BLOCKED · {_presentation.Protection.SmvTrust.Code}";
    public string DecisionReason => _presentation.Protection.DecisionReason;

    public string PhaseAText => FormatCurrent(_presentation.Measurement.PhaseA);
    public string PhaseBText => FormatCurrent(_presentation.Measurement.PhaseB);
    public string PhaseCText => FormatCurrent(_presentation.Measurement.PhaseC);
    public string ResidualText => FormatCurrent(_presentation.Measurement.Residual);
    public string FrequencyTextDisplay => $"{_presentation.Waveform.FrequencyHz:0.000} Hz";
    public string SampleCounterText => $"smpCnt {_presentation.SampleCounter:0000}";
    public string WindowText => $"{_presentation.Waveform.SamplesPerCycle} samples/cycle · two-cycle evidence";
    public ScenarioWaveform Waveform => _presentation.Waveform;

    public void Tick()
    {
        DrainProcessBusEvents();
        if (IsExternalSource)
        {
            RefreshExternalSnapshot();
            return;
        }

        var ticks = _workspace.InternalLab.Advance(SimulationSubstep, SimulationIterationsPerUiTick);
        if (ticks.Count == 0)
            return;

        ApplyTick(ticks[^1]);
    }

    public async Task ReplayCaptureAsync(string path)
    {
        if (_replayBusy)
            return;
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".pcap", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".pcapng", StringComparison.OrdinalIgnoreCase))
        {
            _externalSourceStatus = "INVALID · select a .pcap or .pcapng file";
            _statusText = _externalSourceStatus;
            OnPropertyChanged(string.Empty);
            return;
        }

        _replayBusy = true;
        _replayFilePath = path;
        _replayFrameCount = 0;
        _externalSourceStatus = $"REPLAYING · {Path.GetFileName(path)}";
        _statusText = "Capture replay started through the portable process-bus controller.";
        _workspace.SelectSource(WorkspaceSourceMode.CaptureReplay);
        _workspace.SetExternalSourceRunning(true);
        OnPropertyChanged(string.Empty);

        try
        {
            _replayFrameCount = await _processBus.ReplayAsync(path).ConfigureAwait(true);
            _workspace.SetExternalSourceRunning(false);
            RefreshExternalStreams();
            _externalSourceStatus = ExternalStreams.Count == 0
                ? $"COMPLETE · {_replayFrameCount:N0} frame(s) · no SV stream decoded"
                : $"COMPLETE · {_replayFrameCount:N0} frame(s) · {ExternalStreams.Count} stream(s)";
            _statusText = $"Replay complete: {Path.GetFileName(path)}.";
            AddEvent("REPLAY", $"{Path.GetFileName(path)} · {_replayFrameCount:N0} frame(s) · {ExternalStreams.Count} stream(s)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            _workspace.SetExternalSourceRunning(false);
            _externalSourceStatus = $"ERROR · {ex.Message}";
            _statusText = _externalSourceStatus;
            AddEvent("REPLAY ERR", ex.Message);
        }
        finally
        {
            _replayBusy = false;
            DrainProcessBusEvents();
            OnPropertyChanged(string.Empty);
        }
    }

    public async Task SelectInternalSourceAsync()
    {
        await _processBus.StopAsync().ConfigureAwait(true);
        _workspace.SelectSource(WorkspaceSourceMode.InternalLaboratory);
        _externalSnapshot = SmvRuntimeSnapshot.Empty;
        _selectedExternalStream = null;
        ExternalStreams.Clear();
        _externalSourceStatus = "IDLE · internal virtual test set selected";
        ApplyTick(_workspace.InternalLab.CaptureFrame());
        AddEvent("SOURCE", "Internal virtual test set selected");
        OnPropertyChanged(string.Empty);
    }

    public void ToggleRun()
    {
        EnsureInternalSource();
        if (IsRunning)
        {
            _workspace.InternalLab.SetRunning(false);
            _statusText = "Virtual output stopped at 0 V / 0 A; configured values remain armed.";
            _injectionEditorStatus = "STOPPED · configured source retained";
            AddEvent("INJ STOP", $"{ProfileNameText} · configured values retained");
            ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
            return;
        }

        if (_injectionDraftDirty && !TryApplyInjection(announce: false))
            return;

        _workspace.InternalLab.SetRunning(true);
        _statusText = "Virtual output energized. Trip is restrained until one coherent nominal cycle is rebuilt.";
        _injectionEditorStatus = "STARTING · rebuilding coherent cycle";
        AddEvent("INJ START", $"{ProfileNameText} · {_workspace.InternalLab.Scenario.InjectionFingerprint[..12]}");
        ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
    }

    public void InjectAgFault()
    {
        EnsureInternalSource();
        ApplyPreset("A-G fault", announce: false);
        if (!IsRunning)
            _workspace.InternalLab.SetRunning(true);

        _statusText = "A-G fault injection started. Relay operation follows pickup, delay, and trust state.";
        _injectionEditorStatus = "STARTING · A-G fault active";
        AddEvent("FAULT", "A-G preset loaded and virtual output started");
        ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
    }

    public void ToggleSmvDegradation()
    {
        EnsureInternalSource();
        var scenario = _workspace.InternalLab.Scenario;
        scenario.SmvDegraded = !scenario.SmvDegraded;
        _statusText = scenario.SmvDegraded
            ? "SMV continuity degraded. Measurement remains visible while trip permission is removed."
            : "SMV trust restored. Trip permission is available.";
        AddEvent(scenario.SmvDegraded ? "SMV WARN" : "SMV GOOD", _statusText);
        ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
    }

    public bool TryApplyInjection(bool announce)
    {
        EnsureInternalSource();
        if (!InjectionChannelViewModel.TryParseEngineeringDouble(FrequencyText, out var frequency) ||
            !double.IsFinite(frequency) ||
            frequency < VirtualInjectionProfile.MinimumFrequencyHz ||
            frequency > VirtualInjectionProfile.MaximumFrequencyHz)
        {
            SetInjectionInvalid(
                $"Frequency must be {VirtualInjectionProfile.MinimumFrequencyHz:0}–{VirtualInjectionProfile.MaximumFrequencyHz:0} Hz.");
            return false;
        }

        var channels = new Dictionary<VirtualInjectionSignal, VirtualInjectionChannel>();
        foreach (var row in InjectionChannels)
        {
            if (!row.TryCreateChannel(out var channel, out var error))
            {
                SetInjectionInvalid(error);
                return false;
            }
            channels[row.Signal] = channel;
        }

        try
        {
            var profileName = _injectionDraftDirty ? "Custom injection" : SelectedPreset ?? "Custom injection";
            var profile = new VirtualInjectionProfile(
                profileName,
                frequency,
                channels[VirtualInjectionSignal.PhaseAVoltage],
                channels[VirtualInjectionSignal.PhaseBVoltage],
                channels[VirtualInjectionSignal.PhaseCVoltage],
                channels[VirtualInjectionSignal.NeutralVoltage],
                channels[VirtualInjectionSignal.PhaseACurrent],
                channels[VirtualInjectionSignal.PhaseBCurrent],
                channels[VirtualInjectionSignal.PhaseCCurrent],
                channels[VirtualInjectionSignal.NeutralCurrent]).Normalize();

            var changed = _workspace.InternalLab.ApplyProfile(profile);
            _injectionDraftDirty = false;
            OnPropertyChanged(nameof(InjectionDraftDirty));
            if (!VirtualInjectionPresets.Names.Contains(profile.Name))
            {
                _syncInjectionEditor = true;
                _selectedPreset = null;
                OnPropertyChanged(nameof(SelectedPreset));
                _syncInjectionEditor = false;
            }
            _injectionEditorStatus = changed
                ? IsRunning ? "APPLIED · rebuilding coherent cycle" : "APPLIED · source armed"
                : IsRunning ? "RUNNING · source unchanged" : "READY · source unchanged";
            _statusText = changed
                ? $"Injection '{profile.Name}' applied atomically without resetting the relay."
                : "Injection values match the active configured source.";
            if (changed)
                AddEvent("INJECTION", $"{profile.Name} · {profile.Fingerprint()[..12]}");
            ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
            if (announce)
                OnPropertyChanged(string.Empty);
            return true;
        }
        catch (ArgumentException ex)
        {
            SetInjectionInvalid(ex.Message);
            return false;
        }
    }

    public void ClearInjection()
    {
        EnsureInternalSource();
        ApplyPreset("Normal balanced", announce: false);
        _statusText = IsRunning
            ? "Injection cleared to normal balanced values; relay trip latch remains unchanged."
            : "Normal balanced values armed; virtual output remains stopped.";
        AddEvent("INJ CLEAR", "Normal balanced profile restored; relay state retained");
        ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
    }

    public void ResetRelay()
    {
        if (IsExternalSource)
        {
            _processBus.ResetProtection(SelectedExternalStream?.Key);
            RefreshExternalSnapshot();
            _statusText = $"External relay state reset for {SelectedStreamText}.";
            AddEvent("RELAY RESET", $"External source retained · {SelectedStreamText}");
            OnPropertyChanged(string.Empty);
            return;
        }

        var wasRunning = IsRunning;
        var profile = ProfileNameText;
        var fingerprint = _workspace.InternalLab.Scenario.InjectionFingerprint;

        _workspace.InternalLab.ResetProtection();
        _statusText = $"Relay reset complete. Injection '{profile}' remains {(wasRunning ? "RUNNING" : "STOPPED")}.";
        _settingsEditorStatus = "RESET · timers and trip latch cleared";
        AddEvent("RELAY RESET", $"Source retained · {profile} · {fingerprint[..12]}");
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public void ResetLaboratory()
    {
        EnsureInternalSource();
        _workspace.StopAll();
        _workspace.InternalLab.Reset();
        SyncInjectionEditor(_workspace.InternalLab.Scenario.ActiveProfile);
        _statusText = "Laboratory reset to the stopped normal-balanced profile.";
        _injectionEditorStatus = "STOPPED · normal balanced source armed";
        _settingsEditorStatus = "READY · relay settings retained";
        AddEvent("LAB RESET", "Source and relay state returned to normal balanced baseline");
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public void ApplySettings()
    {
        if (IsExternalSource)
        {
            _settingsEditorStatus = "BLOCKED · return to internal source before applying relay settings";
            _statusText = _settingsEditorStatus;
            AddEvent("SET BLOCK", "External process-bus settings projection is read-only in P6.4");
            OnPropertyChanged(string.Empty);
            return;
        }

        if (!SettingsEditor.TryBuild(_settings, out var settings, out var error))
        {
            _settingsEditorStatus = $"INVALID · {error}";
            _statusText = error;
            AddEvent("SET INVALID", error);
            OnPropertyChanged(string.Empty);
            return;
        }

        var sourceFingerprint = _workspace.InternalLab.Scenario.InjectionFingerprint;
        var wasRunning = IsRunning;
        _workspace.InternalLab.ApplySettingsPreservingSource(settings);
        _settings = settings;
        SyncSettingsEditor();
        _settingsDraftDirty = false;
        OnPropertyChanged(nameof(SettingsDraftDirty));
        _settingsEditorStatus = "APPLIED · relay timers and trip latch reset";
        _statusText = $"Protection settings applied · {settings.GroupName} revision {settings.Revision}. Injection remains {(wasRunning ? "RUNNING" : "STOPPED")}.";
        AddEvent("SETTINGS", $"{settings.GroupName} rev {settings.Revision} · {settings.Fingerprint()[..12]}");

        if (!string.Equals(sourceFingerprint, _workspace.InternalLab.Scenario.InjectionFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Applying relay settings unexpectedly changed the injection source.");

        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public async ValueTask DisposeAsync()
    {
        _workspace.StopAll();
        _processBus.EventRaised -= ProcessBus_EventRaised;
        await _processBus.DisposeAsync().ConfigureAwait(false);
    }

    private void ApplyPreset(string preset, bool announce)
    {
        EnsureInternalSource();
        var frequency = TryReadFrequency(out var entered)
            ? entered
            : _workspace.InternalLab.Scenario.ActiveProfile.FrequencyHz;
        var profile = VirtualInjectionPresets.Create(preset, frequency);
        var changed = _workspace.InternalLab.ApplyProfile(profile);
        SyncInjectionEditor(profile);
        _injectionEditorStatus = changed
            ? IsRunning ? "APPLIED · rebuilding coherent cycle" : "APPLIED · source armed"
            : IsRunning ? "RUNNING · preset unchanged" : "READY · preset unchanged";
        _statusText = $"Virtual injection preset '{preset}' applied. Values remain editable.";
        if (changed)
            AddEvent("PRESET", $"{preset} · {profile.Fingerprint()[..12]}");
        ApplyTick(_workspace.InternalLab.Step(TimeSpan.Zero));
        if (announce)
            OnPropertyChanged(string.Empty);
    }

    private void EnsureInternalSource()
    {
        if (IsInternalSource)
            return;

        _workspace.SelectSource(WorkspaceSourceMode.InternalLaboratory);
        _externalSnapshot = SmvRuntimeSnapshot.Empty;
        _selectedExternalStream = null;
        ExternalStreams.Clear();
        _externalSourceStatus = "IDLE · internal virtual test set selected";
        ApplyTick(_workspace.InternalLab.CaptureFrame());
        AddEvent("SOURCE", "Internal virtual test set selected by internal-lab command");
    }

    private void RefreshExternalStreams()
    {
        var selectedKey = _selectedExternalStream?.Key;
        var streams = _processBus.GetStreams();
        ExternalStreams.Clear();
        foreach (var stream in streams)
            ExternalStreams.Add(stream);

        _selectedExternalStream = ExternalStreams.FirstOrDefault(stream =>
            string.Equals(stream.Key, selectedKey, StringComparison.Ordinal)) ?? ExternalStreams.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedExternalStream));
        RefreshExternalSnapshot();
    }

    private void RefreshExternalSnapshot()
    {
        if (!IsExternalSource || string.IsNullOrWhiteSpace(_selectedExternalStream?.Key))
        {
            if (IsExternalSource)
            {
                _externalSnapshot = SmvRuntimeSnapshot.Empty;
                _presentation = DesktopPresentationSnapshot.FromProcessBus(_externalSnapshot);
                UpdateProtectionElements(_presentation.Protection);
                OnPropertyChanged(string.Empty);
            }
            return;
        }

        var snapshot = _processBus.GetSnapshot(_selectedExternalStream.Key, primaryDisplay: false);
        if (string.IsNullOrWhiteSpace(snapshot.Stream.Key))
            return;

        _externalSnapshot = snapshot;
        _presentation = DesktopPresentationSnapshot.FromProcessBus(snapshot);
        UpdateProtectionElements(snapshot.Protection);
        OnPropertyChanged(string.Empty);
    }

    private void ProcessBus_EventRaised(object? sender, ProcessBusEventArgs e)
        => _processBusEvents.Enqueue(e);

    private void DrainProcessBusEvents()
    {
        while (_processBusEvents.TryDequeue(out var item))
            AddEvent(item.Code, item.Message);
    }

    private void SyncInjectionEditor(VirtualInjectionProfile profile)
    {
        _syncInjectionEditor = true;
        try
        {
            foreach (var row in InjectionChannels)
                row.Apply(profile.Channel(row.Signal));
            _frequencyText = profile.FrequencyHz.ToString("0.###", CultureInfo.InvariantCulture);
            _selectedPreset = VirtualInjectionPresets.Names.Contains(profile.Name) ? profile.Name : null;
            _injectionDraftDirty = false;
            OnPropertyChanged(nameof(FrequencyText));
            OnPropertyChanged(nameof(SelectedPreset));
            OnPropertyChanged(nameof(InjectionDraftDirty));
        }
        finally
        {
            _syncInjectionEditor = false;
        }
    }

    private void SyncSettingsEditor()
    {
        _syncSettingsEditor = true;
        try
        {
            SettingsEditor.Apply(_settings);
            _settingsDraftDirty = false;
            OnPropertyChanged(nameof(SettingsDraftDirty));
        }
        finally
        {
            _syncSettingsEditor = false;
        }
    }

    private void InjectionChannel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => MarkInjectionDraftChanged();

    private void MarkInjectionDraftChanged()
    {
        if (_syncInjectionEditor)
            return;
        _injectionDraftDirty = true;
        _injectionEditorStatus = "EDITING · last valid source remains active";
        OnPropertyChanged(nameof(InjectionDraftDirty));
        OnPropertyChanged(nameof(InjectionEditorStatus));
    }

    private void SettingsEditor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncSettingsEditor)
            return;
        _settingsDraftDirty = true;
        _settingsEditorStatus = "EDITING · active relay settings unchanged";
        OnPropertyChanged(nameof(SettingsDraftDirty));
        OnPropertyChanged(nameof(SettingsEditorStatus));
    }

    private bool TryReadFrequency(out double frequency)
        => InjectionChannelViewModel.TryParseEngineeringDouble(FrequencyText, out frequency) &&
           double.IsFinite(frequency) &&
           frequency is >= VirtualInjectionProfile.MinimumFrequencyHz and <= VirtualInjectionProfile.MaximumFrequencyHz;

    private void SetInjectionInvalid(string error)
    {
        _injectionEditorStatus = $"INVALID · {error}";
        _statusText = error;
        AddEvent("INJ INVALID", error);
        OnPropertyChanged(string.Empty);
    }

    private DesktopPresentationSnapshot CreateInternalPresentation(InternalLabTick tick)
        => DesktopPresentationSnapshot.FromInternal(
            tick,
            _workspace.InternalLab.Scenario.SampleCounter,
            _workspace.InternalLab.Scenario.ActiveProfile.Name,
            _workspace.InternalLab.Scenario.OutputState);

    private void ApplyTick(InternalLabTick tick)
    {
        _currentTick = tick;
        _presentation = CreateInternalPresentation(tick);
        UpdateProtectionElements(tick.Protection);
        OnPropertyChanged(string.Empty);
    }

    private void UpdateProtectionElements(ProtectionSnapshot snapshot)
    {
        ProtectionElements[0].Update(snapshot.Phase50);
        ProtectionElements[1].Update(snapshot.Phase51);
        ProtectionElements[2].Update(snapshot.Earth50);
        ProtectionElements[3].Update(snapshot.Earth51);
    }

    private void AddEvent(string code, string detail)
    {
        Events.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss}  {code,-11} {detail}");
        while (Events.Count > 20)
            Events.RemoveAt(Events.Count - 1);
    }

    private static string FormatCurrent(double value)
        => string.Create(CultureInfo.InvariantCulture, $"{value:0.000} A");

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class ProtectionElementViewModel : INotifyPropertyChanged
{
    private ProtectionStageState _state;
    private double _progress;
    private string _detail = "Ready";

    public ProtectionElementViewModel(string code, string label)
    {
        Code = code;
        Label = label;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; }
    public string Label { get; }
    public string StateText => _state.ToString().ToUpperInvariant();
    public double Progress => Math.Clamp(_progress, 0, 1);
    public string Detail => _detail;

    public void Update(ElementSnapshot snapshot)
    {
        _state = snapshot.State;
        _progress = snapshot.Progress;
        _detail = snapshot.Reason;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
