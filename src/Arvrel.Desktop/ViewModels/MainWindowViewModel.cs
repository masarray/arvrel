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
    private InternalLabTick _currentTick;
    private string _statusText = "Internal deterministic laboratory ready.";

    public MainWindowViewModel()
    {
        var settings = new ProtectionSettings();
        _workspace = new ArvrelWorkspace(settings);
        _processBus = new SmvProcessBusController(settings);
        _currentTick = _workspace.InternalLab.CaptureFrame();

        RunCommand = new RelayCommand(ToggleRun);
        FaultCommand = new RelayCommand(ToggleFault);
        SmvCommand = new RelayCommand(ToggleSmvDegradation);
        ResetCommand = new RelayCommand(Reset);

        ProtectionElements = new ObservableCollection<ProtectionElementViewModel>
        {
            new("50P", "Phase instantaneous"),
            new("51P", "Phase inverse time"),
            new("50N", "Earth instantaneous"),
            new("51N", "Earth inverse time")
        };

        Events = new ObservableCollection<string>();
        ApplyTick(_currentTick);
        AddEvent("READY", "Avalonia shell connected to the portable application core");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RunCommand { get; }
    public ICommand FaultCommand { get; }
    public ICommand SmvCommand { get; }
    public ICommand ResetCommand { get; }

    public ObservableCollection<ProtectionElementViewModel> ProtectionElements { get; }
    public ObservableCollection<string> Events { get; }

    public string ProductTitle => "ARVREL";
    public string ProductSubtitle => "Virtual Protection Relay Laboratory";
    public string ShellVersion => "P5.2 · AVALONIA BASE SHELL";
    public string PlatformText => $"{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture}";
    public string SourceModeText => "INTERNAL LABORATORY";
    public string StatusText => _statusText;

    public string LiveCaptureStatus => _processBus.IsLiveCaptureAvailable
        ? $"{_processBus.LiveCaptureBackendName} live backend ready"
        : _processBus.CaptureAvailabilityMessage;

    public string ReplayStatus => _processBus.IsReplayAvailable
        ? "PCAP/PCAPNG replay decoder ready"
        : "Replay requires the ARIEC61850 decoder at build time";

    public bool IsRunning => _workspace.InternalLab.IsRunning;
    public bool FaultActive => _workspace.InternalLab.Scenario.FaultActive;
    public bool SmvDegraded => _workspace.InternalLab.Scenario.SmvDegraded;
    public bool AllowsTrip => _currentTick.Protection.SmvTrust.AllowsTrip;
    public bool TripLatched => _currentTick.Protection.TripLatched;
    public bool PickupActive =>
        _currentTick.Protection.Phase50.Pickup ||
        _currentTick.Protection.Phase51.Pickup ||
        _currentTick.Protection.Earth50.Pickup ||
        _currentTick.Protection.Earth51.Pickup;

    public string RunButtonText => IsRunning ? "PAUSE" : "RUN";
    public string FaultButtonText => FaultActive ? "CLEAR A-G FAULT" : "INJECT A-G FAULT";
    public string SmvButtonText => SmvDegraded ? "RESTORE SMV TRUST" : "DEGRADE SMV";
    public string OutputStateText => _workspace.InternalLab.Scenario.OutputState.ToUpperInvariant();
    public string TripStateText => TripLatched
        ? $"TRIP LATCHED · {_currentTick.Protection.ActiveElement}"
        : PickupActive
            ? $"PICKUP · {_currentTick.Protection.ActiveElement}"
            : "READY · NO PICKUP";
    public string TrustStateText => AllowsTrip
        ? "SMV TRUST · TRIP PERMITTED"
        : $"SMV TRUST · TRIP BLOCKED · {_currentTick.Protection.SmvTrust.Code}";
    public string DecisionReason => _currentTick.Protection.DecisionReason;

    public string PhaseAText => FormatCurrent(_currentTick.Scenario.Measurement.PhaseA);
    public string PhaseBText => FormatCurrent(_currentTick.Scenario.Measurement.PhaseB);
    public string PhaseCText => FormatCurrent(_currentTick.Scenario.Measurement.PhaseC);
    public string ResidualText => FormatCurrent(_currentTick.Scenario.Measurement.Residual);
    public string FrequencyText => $"{_currentTick.Scenario.Waveform.FrequencyHz:0.000} Hz";
    public string SampleCounterText => $"smpCnt {_workspace.InternalLab.Scenario.SampleCounter:0000}";
    public string WindowText => $"{_currentTick.Scenario.Waveform.SamplesPerCycle} samples/cycle · two-cycle evidence";
    public ScenarioWaveform Waveform => _currentTick.Scenario.Waveform;

    public void Tick()
    {
        var ticks = _workspace.InternalLab.Advance(SimulationSubstep, SimulationIterationsPerUiTick);
        if (ticks.Count == 0)
            return;

        ApplyTick(ticks[^1]);
    }

    public void ToggleRun()
    {
        var running = _workspace.InternalLab.ToggleRunning();
        _statusText = running
            ? "Internal deterministic source running at 4,000 samples per second."
            : "Internal source paused; the latest evidence window remains visible.";
        AddEvent(running ? "RUN" : "PAUSE", _statusText);
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public void ToggleFault()
    {
        var scenario = _workspace.InternalLab.Scenario;
        scenario.FaultActive = !scenario.FaultActive;
        if (!scenario.IsRunning)
            scenario.StartInjection();

        _statusText = scenario.FaultActive
            ? "A-G fault profile active. Observe phase and earth protection operation."
            : "Fault cleared. Normal balanced profile restored.";
        AddEvent(scenario.FaultActive ? "FAULT" : "CLEAR", _statusText);
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public void ToggleSmvDegradation()
    {
        var scenario = _workspace.InternalLab.Scenario;
        scenario.SmvDegraded = !scenario.SmvDegraded;
        _statusText = scenario.SmvDegraded
            ? "SMV continuity degraded. Measurement remains visible while trip permission is removed."
            : "SMV trust restored. Trip permission is available.";
        AddEvent(scenario.SmvDegraded ? "SMV WARN" : "SMV GOOD", _statusText);
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public void Reset()
    {
        _workspace.StopAll();
        _workspace.InternalLab.Reset();
        _statusText = "Laboratory reset to the stopped normal-balanced profile.";
        AddEvent("RESET", _statusText);
        ApplyTick(_workspace.InternalLab.CaptureFrame());
    }

    public async ValueTask DisposeAsync()
    {
        _workspace.StopAll();
        await _processBus.DisposeAsync().ConfigureAwait(false);
    }

    private void ApplyTick(InternalLabTick tick)
    {
        _currentTick = tick;
        ProtectionElements[0].Update(tick.Protection.Phase50);
        ProtectionElements[1].Update(tick.Protection.Phase51);
        ProtectionElements[2].Update(tick.Protection.Earth50);
        ProtectionElements[3].Update(tick.Protection.Earth51);
        OnPropertyChanged(string.Empty);
    }

    private void AddEvent(string code, string detail)
    {
        Events.Insert(0, $"{DateTimeOffset.Now:HH:mm:ss}  {code,-8} {detail}");
        while (Events.Count > 10)
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
