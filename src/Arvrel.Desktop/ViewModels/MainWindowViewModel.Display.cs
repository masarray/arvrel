using System.Windows.Input;
using Arvrel.Application.Laboratory;
using Arvrel.Desktop.Infrastructure;
using Arvrel.ProcessBus;
using Arvrel.Protection;

namespace Arvrel.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private enum OperatorDisplaySource
    {
        InternalLab,
        ProcessBus
    }

    private static readonly RelayAnnunciationSnapshot EmptyAnnunciation = new(
        PickupActive: false,
        TripLatched: false,
        PhaseA: RelayLampState.Off,
        PhaseB: RelayLampState.Off,
        PhaseC: RelayLampState.Off,
        Earth: RelayLampState.Off);

    private static readonly ScenarioWaveform EmptyDisplayWaveform = new(
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        Array.Empty<double>(),
        50,
        80,
        4000);

    private readonly RelayAnnunciationLatch _processBusAnnunciationLatch = new();
    private RelayAnnunciationSnapshot _processBusAnnunciation = EmptyAnnunciation;
    private SmvRuntimeSnapshot _coherentProcessBusSnapshot = SmvRuntimeSnapshot.Empty;
    private ScenarioWaveform _coherentProcessBusWaveform = EmptyDisplayWaveform;
    private OperatorDisplaySource _operatorDisplaySource;
    private string? _displayGuardStreamKey;
    private string _displayHandoverStatus = "INTERNAL LAB display active.";
    private RelayCommand? _activateInternalDisplayCommand;
    private RelayCommand? _activateProcessBusDisplayCommand;

    public bool IsProcessBusDisplayActive => _operatorDisplaySource == OperatorDisplaySource.ProcessBus;
    public bool IsInternalDisplayActive => !IsProcessBusDisplayActive;
    public string ActiveDisplaySourceText => IsProcessBusDisplayActive
        ? _processBus.IsReplayMode ? "PROCESS BUS · REPLAY" : "PROCESS BUS · LIVE"
        : "INTERNAL VIRTUAL TEST SET";
    public string DisplayHandoverStatus => _displayHandoverStatus;
    public string ProcessBusDisplayReadinessText => string.IsNullOrWhiteSpace(_coherentProcessBusSnapshot.Stream.Key)
        ? "Waiting for one coherent two-cycle SV window."
        : $"Coherent display ready · {_coherentProcessBusSnapshot.Stream.SvId} · smpCnt {_coherentProcessBusSnapshot.SampleCounter}";

    public ICommand ActivateInternalDisplayCommand =>
        _activateInternalDisplayCommand ??= new RelayCommand(ActivateInternalDisplay);

    public ICommand ActivateProcessBusDisplayCommand =>
        _activateProcessBusDisplayCommand ??= new RelayCommand(ActivateProcessBusDisplay);

    private MeasurementFrame DisplayMeasurement => IsProcessBusDisplayActive
        ? _coherentProcessBusSnapshot.Measurement
        : _currentTick.Scenario.Measurement;

    private ProtectionSnapshot DisplayProtection => IsProcessBusDisplayActive &&
                                                    !string.IsNullOrWhiteSpace(_processBusSnapshot.Stream.Key)
        ? _processBusSnapshot.Protection
        : _currentTick.Protection;

    private ScenarioWaveform DisplayWaveform => IsProcessBusDisplayActive
        ? _coherentProcessBusWaveform
        : _currentTick.Scenario.Waveform;

    private RelayAnnunciationSnapshot DisplayAnnunciation => IsProcessBusDisplayActive
        ? _processBusAnnunciation
        : _relayAnnunciation;

    private string DisplayActiveElement => DisplayProtection.ActiveElement;
    private string DisplayProfileNameText => IsProcessBusDisplayActive
        ? _processBusSnapshot.Stream.DisplayName
        : ProfileNameText;
    private string DisplayFingerprintText => IsProcessBusDisplayActive
        ? string.IsNullOrWhiteSpace(_processBusSnapshot.Stream.Key)
            ? "SV —"
            : $"SV {StableShortFingerprint(_processBusSnapshot.Stream.Key)}"
        : InjectionFingerprintText;
    private string DisplayProvenanceText => IsProcessBusDisplayActive
        ? $"{_processBusSnapshot.MappingSummary} · {_processBusSnapshot.ScalingSummary}"
        : ProvenanceText;

    private void ActivateProcessBusDisplay()
    {
        if (SelectedProcessBusStream is null)
        {
            SetDisplayHandoverStatus("Select an SV stream before activating the process-bus display.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_coherentProcessBusSnapshot.Stream.Key))
        {
            SetDisplayHandoverStatus("Process-bus display not activated: waiting for a coherent two-cycle SV window.");
            return;
        }

        _operatorDisplaySource = OperatorDisplaySource.ProcessBus;
        UpdateProtectionElementCards(DisplayProtection);
        SetDisplayHandoverStatus($"PROCESS BUS display active · {_coherentProcessBusSnapshot.Stream.DisplayName}. Internal lab state is retained independently.");
        AddEvent("DISPLAY", $"Process bus · {_coherentProcessBusSnapshot.Stream.DisplayName}");
        OnPropertyChanged(string.Empty);
    }

    private void ActivateInternalDisplay()
    {
        _operatorDisplaySource = OperatorDisplaySource.InternalLab;
        UpdateProtectionElementCards(_currentTick.Protection);
        SetDisplayHandoverStatus("INTERNAL LAB display active. Process-bus capture and replay state are retained independently.");
        AddEvent("DISPLAY", "Internal virtual test set");
        OnPropertyChanged(string.Empty);
    }

    private void PrepareProcessBusDisplayStream(string? streamKey)
    {
        if (string.Equals(_displayGuardStreamKey, streamKey, StringComparison.Ordinal))
            return;

        _displayGuardStreamKey = streamKey;
        _coherentProcessBusSnapshot = SmvRuntimeSnapshot.Empty;
        _coherentProcessBusWaveform = EmptyDisplayWaveform;
        _processBusAnnunciationLatch.Reset();
        _processBusAnnunciation = EmptyAnnunciation;

        if (IsProcessBusDisplayActive)
        {
            _operatorDisplaySource = OperatorDisplaySource.InternalLab;
            UpdateProtectionElementCards(_currentTick.Protection);
            SetDisplayHandoverStatus("Selected SV stream changed; display returned to INTERNAL LAB until the new stream builds a coherent window.");
            AddEvent("DISPLAY", "Returned to internal lab after SV stream change");
        }

        OnPropertyChanged(nameof(ProcessBusDisplayReadinessText));
        OnPropertyChanged(string.Empty);
    }

    private void ObserveProcessBusDisplaySnapshot(SmvRuntimeSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.Stream.Key))
            _processBusAnnunciation = _processBusAnnunciationLatch.Observe(snapshot.Protection);

        if (IsCoherentForDisplay(snapshot))
        {
            _coherentProcessBusSnapshot = snapshot;
            _coherentProcessBusWaveform = ConvertWaveform(snapshot.Waveform);
        }

        if (IsProcessBusDisplayActive)
            UpdateProtectionElementCards(DisplayProtection);

        OnPropertyChanged(nameof(ProcessBusDisplayReadinessText));
        if (IsProcessBusDisplayActive)
            OnPropertyChanged(string.Empty);
    }

    private void ResetDisplayedRelay()
    {
        if (!IsProcessBusDisplayActive)
        {
            ResetInternalRelay();
            return;
        }

        _processBus.ResetProtection(SelectedProcessBusStream?.Key);
        _processBusAnnunciationLatch.Reset();
        _processBusAnnunciation = EmptyAnnunciation;
        UpdateProcessBusSnapshot();
        SetDisplayHandoverStatus("Selected process-bus relay state reset. Internal lab state was not changed.");
        AddEvent("PB RESET", SelectedStreamSummary);
        OnPropertyChanged(string.Empty);
    }

    private async Task ToggleDisplayedSourceAsync()
    {
        if (!IsProcessBusDisplayActive)
        {
            ToggleRun();
            return;
        }

        if (_processBus.IsRunning)
        {
            await StopProcessBusAsync();
            return;
        }

        if (SelectedProcessBusAdapter is null)
        {
            SetDisplayHandoverStatus("Select a live adapter in PROCESS BUS before starting a live source.");
            return;
        }

        await StartLiveCaptureAsync();
    }

    private void UpdateProtectionElementCards(ProtectionSnapshot snapshot)
    {
        ProtectionElements[0].Update(snapshot.Phase50);
        ProtectionElements[1].Update(snapshot.Phase51);
        ProtectionElements[2].Update(snapshot.Earth50);
        ProtectionElements[3].Update(snapshot.Earth51);
    }

    private void SetDisplayHandoverStatus(string status)
    {
        _displayHandoverStatus = status;
        OnPropertyChanged(nameof(DisplayHandoverStatus));
        OnPropertyChanged(nameof(ActiveDisplaySourceText));
        OnPropertyChanged(nameof(IsProcessBusDisplayActive));
        OnPropertyChanged(nameof(IsInternalDisplayActive));
        OnPropertyChanged(nameof(RunButtonText));
    }

    private static bool IsCoherentForDisplay(SmvRuntimeSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Stream.Key))
            return false;

        var expectedWindow = Math.Max(2, snapshot.SamplesPerCycle * 2);
        var sampleCount = snapshot.Waveform.PhaseA.Count;
        var completeWindow = sampleCount >= expectedWindow &&
                             snapshot.Waveform.PhaseB.Count == sampleCount &&
                             snapshot.Waveform.PhaseC.Count == sampleCount &&
                             snapshot.Waveform.Residual.Count == sampleCount;
        if (!completeWindow || !snapshot.Measurement.SmvTrust.AllowsMeasurement)
            return false;

        return snapshot.Measurement.SmvTrust.Code is
            "HEALTHY" or
            "SCL_UNBOUND" or
            "SCALING_UNRESOLVED";
    }

    private static ScenarioWaveform ConvertWaveform(SmvWaveformWindow waveform)
    {
        var frequency = double.IsFinite(waveform.FrequencyHz) && waveform.FrequencyHz > 0
            ? waveform.FrequencyHz
            : 50;
        var samplesPerCycle = Math.Max(1, waveform.SamplesPerCycle);
        return new ScenarioWaveform(
            waveform.PhaseA.ToArray(),
            waveform.PhaseB.ToArray(),
            waveform.PhaseC.ToArray(),
            waveform.Residual.ToArray(),
            frequency,
            samplesPerCycle,
            frequency * samplesPerCycle);
    }

    private static string StableShortFingerprint(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 6));
    }
}
