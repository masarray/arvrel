using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Arvrel.Desktop.Infrastructure;
using Arvrel.ProcessBus;

namespace Arvrel.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ConcurrentQueue<ProcessBusEventArgs> _pendingProcessBusEvents = new();
    private bool _processBusWorkspaceInitialized;
    private ProcessBusAdapter? _selectedProcessBusAdapter;
    private SmvStreamInfo? _selectedProcessBusStream;
    private SmvRuntimeSnapshot _processBusSnapshot = SmvRuntimeSnapshot.Empty;
    private string _processBusStatus = "Process-bus workspace not initialized.";
    private string _replayPath = string.Empty;
    private string _sclPath = string.Empty;
    private AsyncRelayCommand? _startLiveCaptureCommand;
    private AsyncRelayCommand? _stopProcessBusCommand;
    private AsyncRelayCommand? _replayCaptureCommand;
    private RelayCommand? _refreshAdaptersCommand;
    private RelayCommand? _loadSclCommand;

    public ObservableCollection<ProcessBusAdapter> ProcessBusAdapters { get; } = new();
    public ObservableCollection<SmvStreamInfo> ProcessBusStreams { get; } = new();

    public ProcessBusAdapter? SelectedProcessBusAdapter
    {
        get => _selectedProcessBusAdapter;
        set
        {
            if (Equals(_selectedProcessBusAdapter, value))
                return;
            _selectedProcessBusAdapter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAdapterSummary));
        }
    }

    public SmvStreamInfo? SelectedProcessBusStream
    {
        get => _selectedProcessBusStream;
        set
        {
            if (Equals(_selectedProcessBusStream, value))
                return;
            _selectedProcessBusStream = value;
            PrepareProcessBusDisplayStream(value?.Key);
            UpdateProcessBusSnapshot();
            OnPropertyChanged(string.Empty);
        }
    }

    public string ReplayPath
    {
        get => _replayPath;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_replayPath, value, StringComparison.Ordinal))
                return;
            _replayPath = value;
            OnPropertyChanged();
        }
    }

    public string SclPath
    {
        get => _sclPath;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_sclPath, value, StringComparison.Ordinal))
                return;
            _sclPath = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshAdaptersCommand =>
        _refreshAdaptersCommand ??= new RelayCommand(RefreshProcessBusAdapters);

    public ICommand StartLiveCaptureCommand =>
        _startLiveCaptureCommand ??= new AsyncRelayCommand(StartLiveCaptureAsync);

    public ICommand StopProcessBusCommand =>
        _stopProcessBusCommand ??= new AsyncRelayCommand(StopProcessBusAsync);

    public ICommand ReplayCaptureCommand =>
        _replayCaptureCommand ??= new AsyncRelayCommand(ReplayCaptureAsync);

    public ICommand LoadSclCommand =>
        _loadSclCommand ??= new RelayCommand(LoadSclFile);

    public string ProcessBusStatus => _processBusStatus;
    public bool ProcessBusRunning => _processBus.IsRunning;
    public string ProcessBusModeText => _processBus.IsRunning
        ? _processBus.IsReplayMode ? "REPLAY RUNNING" : "LIVE CAPTURE RUNNING"
        : _processBus.IsReplayMode ? "REPLAY COMPLETE" : "IDLE";
    public string ProcessBusBackendText =>
        $"{_processBus.LiveCaptureBackendName} · {_processBus.LiveCaptureBackendId}";
    public string ProcessBusCapabilityText => _processBus.IsLiveCaptureAvailable
        ? "Live capture and PCAP replay are available in this build."
        : _processBus.CaptureAvailabilityMessage;
    public string SelectedAdapterSummary => SelectedProcessBusAdapter is null
        ? "No adapter selected"
        : $"{SelectedProcessBusAdapter.DisplayName} · {SelectedProcessBusAdapter.MacAddress}";
    public string ProcessBusSclSummary => _processBus.SclSummary;
    public string SelectedStreamSummary => SelectedProcessBusStream?.DisplayName ?? "No SV stream selected";
    public string SelectedStreamAddress => SelectedProcessBusStream is null
        ? "—"
        : $"DST {SelectedProcessBusStream.Destination} · VLAN {SelectedProcessBusStream.VlanId?.ToString() ?? "—"}";
    public string ProcessBusFrameRateText => $"{_processBusSnapshot.FramesPerSecond:0.0} frames/s";
    public string ProcessBusCounterText =>
        $"frames {_processBusSnapshot.FrameCount:N0} · ASDUs {_processBusSnapshot.AsduCount:N0} · smpCnt {_processBusSnapshot.SampleCounter}";
    public string ProcessBusContinuityText =>
        $"gap {_processBusSnapshot.SequenceGapCount} · duplicate {_processBusSnapshot.DuplicateCount} · out-of-order {_processBusSnapshot.OutOfOrderCount}";
    public string ProcessBusTrustText => _processBusSnapshot.TrustSummary;
    public string ProcessBusMappingText => _processBusSnapshot.MappingSummary;
    public string ProcessBusScalingText => _processBusSnapshot.ScalingSummary;
    public string ProcessBusSourceText => _processBusSnapshot.SourceSummary;

    public void InitializeProcessBusWorkspace()
    {
        if (_processBusWorkspaceInitialized)
            return;

        _processBusWorkspaceInitialized = true;
        _processBus.EventRaised += ProcessBus_EventRaised;
        RefreshProcessBusAdapters();
        RefreshProcessBusStreams();
        SetProcessBusStatus(_processBus.IsReplayAvailable
            ? "Process-bus workspace ready. Select an adapter or capture file."
            : _processBus.CaptureAvailabilityMessage);
    }

    public void TickProcessBus()
    {
        InitializeProcessBusWorkspace();
        DrainProcessBusEvents();
        RefreshProcessBusStreams();
        UpdateProcessBusSnapshot();
        OnPropertyChanged(nameof(ProcessBusRunning));
        OnPropertyChanged(nameof(ProcessBusModeText));
        OnPropertyChanged(nameof(ActiveDisplaySourceText));
        OnPropertyChanged(nameof(RunButtonText));
    }

    public void RefreshProcessBusAdapters()
    {
        var previousSelector = SelectedProcessBusAdapter?.Selector;
        var adapters = _processBus.ListAdapters();

        ProcessBusAdapters.Clear();
        foreach (var adapter in adapters)
            ProcessBusAdapters.Add(adapter);

        SelectedProcessBusAdapter = ProcessBusAdapters.FirstOrDefault(adapter =>
            string.Equals(adapter.Selector, previousSelector, StringComparison.Ordinal))
            ?? ProcessBusAdapters.FirstOrDefault();

        SetProcessBusStatus(adapters.Count == 0
            ? _processBus.CaptureAvailabilityMessage
            : $"Discovered {adapters.Count} live capture adapter(s). Select one before starting capture.");
    }

    public async Task StartLiveCaptureAsync()
    {
        InitializeProcessBusWorkspace();
        if (SelectedProcessBusAdapter is null)
        {
            SetProcessBusStatus("Select a live capture adapter first.");
            return;
        }

        try
        {
            ClearProcessBusSelectionForNewSource();
            SetProcessBusStatus($"Starting live capture on {SelectedProcessBusAdapter.DisplayName}...");
            await _processBus.StartLiveAsync(SelectedProcessBusAdapter.Selector);
            SetProcessBusStatus($"Listening on {SelectedProcessBusAdapter.DisplayName}. Waiting for IEC 61850-9-2 Sampled Values.");
        }
        catch (Exception ex)
        {
            SetProcessBusStatus($"Live capture failed: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(string.Empty);
        }
    }

    public async Task StopProcessBusAsync()
    {
        InitializeProcessBusWorkspace();
        try
        {
            await _processBus.StopAsync();
            SetProcessBusStatus("Process-bus source stopped. Last coherent display and discovered stream telemetry are retained for inspection.");
        }
        catch (Exception ex)
        {
            SetProcessBusStatus($"Unable to stop process-bus source: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(string.Empty);
        }
    }

    public async Task ReplayCaptureAsync()
    {
        InitializeProcessBusWorkspace();
        if (string.IsNullOrWhiteSpace(ReplayPath))
        {
            SetProcessBusStatus("Select a PCAP or PCAPNG capture file first.");
            return;
        }

        if (!File.Exists(ReplayPath))
        {
            SetProcessBusStatus($"Capture file not found: {ReplayPath}");
            return;
        }

        try
        {
            ClearProcessBusSelectionForNewSource();
            SetProcessBusStatus($"Replaying {Path.GetFileName(ReplayPath)}...");
            var frames = await _processBus.ReplayAsync(ReplayPath);
            RefreshProcessBusStreams();
            UpdateProcessBusSnapshot();
            SetProcessBusStatus($"Replay complete: {frames:N0} frame(s) processed from {Path.GetFileName(ReplayPath)}.");
        }
        catch (Exception ex)
        {
            SetProcessBusStatus($"Capture replay failed: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(string.Empty);
        }
    }

    public void LoadSclFile()
    {
        InitializeProcessBusWorkspace();
        if (string.IsNullOrWhiteSpace(SclPath))
        {
            SetProcessBusStatus("Select an SCL file first.");
            return;
        }

        if (!File.Exists(SclPath))
        {
            SetProcessBusStatus($"SCL file not found: {SclPath}");
            return;
        }

        try
        {
            _processBus.LoadScl(SclPath);
            SetProcessBusStatus($"SCL loaded: {_processBus.SclSummary}.");
        }
        catch (Exception ex)
        {
            SetProcessBusStatus($"SCL load failed: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(string.Empty);
        }
    }

    private void RefreshProcessBusStreams()
    {
        var streams = _processBus.GetStreams();
        var unchanged = streams.Count == ProcessBusStreams.Count &&
            streams.Select(stream => (stream.Key, stream.Health, stream.IsSclBound))
                .SequenceEqual(ProcessBusStreams.Select(stream => (stream.Key, stream.Health, stream.IsSclBound)));
        if (unchanged)
            return;

        var selectedKey = SelectedProcessBusStream?.Key;
        ProcessBusStreams.Clear();
        foreach (var stream in streams)
            ProcessBusStreams.Add(stream);

        SelectedProcessBusStream = ProcessBusStreams.FirstOrDefault(stream =>
            string.Equals(stream.Key, selectedKey, StringComparison.Ordinal))
            ?? ProcessBusStreams.FirstOrDefault();
    }

    private void UpdateProcessBusSnapshot()
    {
        _processBusSnapshot = SelectedProcessBusStream is null
            ? SmvRuntimeSnapshot.Empty
            : _processBus.GetSnapshot(SelectedProcessBusStream.Key, primaryDisplay: true);
        ObserveProcessBusDisplaySnapshot(_processBusSnapshot);
        OnPropertyChanged(nameof(SelectedStreamSummary));
        OnPropertyChanged(nameof(SelectedStreamAddress));
        OnPropertyChanged(nameof(ProcessBusFrameRateText));
        OnPropertyChanged(nameof(ProcessBusCounterText));
        OnPropertyChanged(nameof(ProcessBusContinuityText));
        OnPropertyChanged(nameof(ProcessBusTrustText));
        OnPropertyChanged(nameof(ProcessBusMappingText));
        OnPropertyChanged(nameof(ProcessBusScalingText));
        OnPropertyChanged(nameof(ProcessBusSourceText));
        OnPropertyChanged(nameof(ProcessBusSclSummary));
    }

    private void ClearProcessBusSelectionForNewSource()
    {
        SelectedProcessBusStream = null;
        ProcessBusStreams.Clear();
        _processBusSnapshot = SmvRuntimeSnapshot.Empty;
        PrepareProcessBusDisplayStream(null);
    }

    private void ProcessBus_EventRaised(object? sender, ProcessBusEventArgs e)
        => _pendingProcessBusEvents.Enqueue(e);

    private void DrainProcessBusEvents()
    {
        while (_pendingProcessBusEvents.TryDequeue(out var processBusEvent))
        {
            AddEvent($"PB {processBusEvent.Code}", processBusEvent.Message);
            _processBusStatus = processBusEvent.Message;
        }

        OnPropertyChanged(nameof(ProcessBusStatus));
    }

    private void SetProcessBusStatus(string status)
    {
        _processBusStatus = status;
        OnPropertyChanged(nameof(ProcessBusStatus));
        OnPropertyChanged(nameof(ProcessBusRunning));
        OnPropertyChanged(nameof(ProcessBusModeText));
        OnPropertyChanged(nameof(ActiveDisplaySourceText));
        OnPropertyChanged(nameof(RunButtonText));
    }
}
