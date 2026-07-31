using System.Collections.Concurrent;
using System.Text.Json;
using Arvrel.Protection;
#if ARIEC61850_SIBLING
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
using AR.Iec61850.Transports;
using AR.Iec61850.Transports.Npcap;
#endif

namespace Arvrel.ProcessBus;

public sealed class SmvProcessBusController : IDisposable
{
    private readonly ConcurrentDictionary<string, SmvStreamRuntime> _streams = new(StringComparer.Ordinal);
    private readonly object _profileGate = new();
    private readonly ProtectionSettings _settings;
    private IReadOnlyList<SampledValuesPublisherProfile> _profiles = Array.Empty<SampledValuesPublisherProfile>();
    private SmvMeasurementContext _measurementContext = SmvMeasurementContext.Default;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private bool _disposed;

    public SmvProcessBusController(ProtectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public event EventHandler<ProcessBusEventArgs>? EventRaised;

    public bool IsCapturing => _captureTask is { IsCompleted: false };

    public SmvMeasurementContext MeasurementContext => _measurementContext;

    public IReadOnlyList<ProcessBusAdapterInfo> GetAdapters()
    {
#if ARIEC61850_SIBLING
        return NpcapAdapterCatalog.ListAdapters()
            .Select(adapter => new ProcessBusAdapterInfo(
                adapter.Index,
                adapter.Name,
                string.IsNullOrWhiteSpace(adapter.Description) ? adapter.Name : adapter.Description,
                adapter.MacAddress?.ToString() ?? string.Empty,
                adapter.Name))
            .ToArray();
#else
        return Array.Empty<ProcessBusAdapterInfo>();
#endif
    }

    public void SetMeasurementContext(SmvMeasurementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        _measurementContext = context;
        foreach (var runtime in _streams.Values)
            runtime.SetMeasurementContext(context);
        Raise("MEASUREMENT_CONTEXT", $"CT context updated: {context.CtPrimaryA:0.###}/{context.CtSecondaryA:0.###} A, {context.NominalFrequencyHz:0} Hz.");
    }

    public IReadOnlyList<SmvStreamInfo> GetStreams(bool primaryDisplay, bool replay)
        => _streams.Values
            .Select(runtime => runtime.Snapshot(DateTimeOffset.UtcNow, primaryDisplay, replay).Stream)
            .OrderBy(stream => stream.SvId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stream => stream.AppId)
            .ToArray();

    public SmvRuntimeSnapshot? GetSnapshot(string? key, bool primaryDisplay, bool replay)
    {
        if (string.IsNullOrWhiteSpace(key) || !_streams.TryGetValue(key, out var runtime))
            return null;
        return runtime.Snapshot(DateTimeOffset.UtcNow, primaryDisplay, replay);
    }

    public void ResetProtection()
    {
        foreach (var runtime in _streams.Values)
            runtime.ResetProtection();
        Raise("TRIP_RESET", "Virtual trip latch and protection timers reset.");
    }

    public void Clear()
    {
        _streams.Clear();
        Raise("CLEAR", "Process-bus runtime state cleared.");
    }

    public async Task ImportSclAsync(string path, CancellationToken cancellationToken = default)
    {
#if ARIEC61850_SIBLING
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("SCL path is empty.", nameof(path));

        var profiles = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = new SclParser().Load(path);
            return SampledValuesPublisherProfile.CreateMany(document);
        }, cancellationToken).ConfigureAwait(false);

        lock (_profileGate)
            _profiles = profiles;
        Raise("SCL_LOADED", $"Loaded {profiles.Count} Sampled Values stream profile(s) from {Path.GetFileName(path)}.");
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("SCL import requires the sibling ARIEC61850 repository.");
#endif
    }

    public async Task ReplayAsync(string path, CancellationToken cancellationToken = default)
    {
        StopCapture();
        Clear();
        var count = 0;
        await Task.Run(() =>
        {
            foreach (var packet in PcapPacketReader.Read(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObserveFrame(packet.Timestamp, packet.Frame, replay: true);
                count++;
            }
        }, cancellationToken).ConfigureAwait(false);
        Raise("PCAP_REPLAY", $"Processed {count:N0} Ethernet frame(s) from {Path.GetFileName(path)}.");
    }

    public Task StartCaptureAsync(string adapterSelector, CancellationToken cancellationToken = default)
    {
#if ARIEC61850_SIBLING
        if (string.IsNullOrWhiteSpace(adapterSelector))
            throw new ArgumentException("Adapter selector is empty.", nameof(adapterSelector));
        if (IsCapturing)
            throw new InvalidOperationException("Live capture is already active.");

        Clear();
        _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _captureCts.Token;
        _captureTask = Task.Run(async () =>
        {
            try
            {
                using var source = new NpcapProcessBusFrameSource(adapterSelector);
                var options = new ProcessBusCaptureOptions
                {
                    Filter = "(ether proto 0x88ba) or (vlan and ether proto 0x88ba)",
                    BufferCapacity = 8192,
                    ReadTimeoutMilliseconds = 250
                };
                Raise("CAPTURE_STARTED", "Listening for IEC 61850 Sampled Values frames.");
                await foreach (var captured in source.CaptureAsync(options, token).ConfigureAwait(false))
                    ObserveFrame(captured.Timestamp, captured.Frame, replay: false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Normal stop.
            }
            catch (Exception ex)
            {
                Raise("CAPTURE_ERROR", ex.Message);
                throw;
            }
            finally
            {
                Raise("CAPTURE_STOPPED", "Live capture stopped.");
            }
        }, token);
        return Task.CompletedTask;
#else
        throw new InvalidOperationException("Live Npcap capture requires the sibling ARIEC61850 repository.");
#endif
    }

    public void StopCapture()
    {
        _captureCts?.Cancel();
        _captureCts?.Dispose();
        _captureCts = null;
        _captureTask = null;
    }

    public async Task ExportEvidenceAsync(
        string path,
        string? selectedKey,
        bool primaryDisplay,
        bool replay,
        CancellationToken cancellationToken = default)
    {
        var snapshots = string.IsNullOrWhiteSpace(selectedKey)
            ? _streams.Values.Select(runtime => runtime.Snapshot(DateTimeOffset.UtcNow, primaryDisplay, replay)).ToArray()
            : _streams.TryGetValue(selectedKey, out var selected)
                ? new[] { selected.Snapshot(DateTimeOffset.UtcNow, primaryDisplay, replay) }
                : Array.Empty<SmvRuntimeSnapshot>();

        var document = new SmvEvidenceDocument(
            "arvrel-smv-evidence/v1",
            DateTimeOffset.UtcNow,
            _measurementContext,
            snapshots);
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        Raise("EVIDENCE_EXPORTED", $"Exported {snapshots.Length} stream snapshot(s) to {Path.GetFileName(path)}.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        StopCapture();
        _disposed = true;
    }

    private void ObserveFrame(DateTimeOffset timestamp, ReadOnlyMemory<byte> ethernetFrame, bool replay)
    {
#if ARIEC61850_SIBLING
        if (!SampledValuesFrameParser.TryParseEthernetFrame(ethernetFrame, out var frame))
            return;
        var asdu = frame.Pdu.Asdus.FirstOrDefault();
        if (asdu is null)
            return;
        var key = BuildKey(frame, asdu);
        var runtime = _streams.GetOrAdd(key, _ => new SmvStreamRuntime(key, frame, _settings, _measurementContext));
        var profile = FindProfile(frame, asdu);
        runtime.Observe(timestamp, frame, profile, replay);
#else
        _ = timestamp;
        _ = ethernetFrame;
        _ = replay;
#endif
    }

#if ARIEC61850_SIBLING
    private SampledValuesPublisherProfile? FindProfile(SampledValuesFrame frame, SampledValueAsdu asdu)
    {
        IReadOnlyList<SampledValuesPublisherProfile> profiles;
        lock (_profileGate)
            profiles = _profiles;
        if (profiles.Count == 0)
            return null;

        var addressCandidates = profiles.Where(profile =>
            profile.AppId == frame.AppId &&
            string.Equals(profile.Destination.ToString(), frame.Destination.ToString(), StringComparison.OrdinalIgnoreCase) &&
            profile.Vlan?.VlanId == frame.Vlan?.VlanId).ToArray();
        if (addressCandidates.Length == 0)
            return null;

        var exact = addressCandidates.FirstOrDefault(profile =>
            string.Equals(profile.Stream.SvId, asdu.SvId, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(asdu.DataSetReference) ||
             string.Equals(profile.Stream.DataSetReference, asdu.DataSetReference, StringComparison.Ordinal)));
        return exact ?? addressCandidates.FirstOrDefault(profile =>
            string.Equals(profile.Stream.SvId, asdu.SvId, StringComparison.Ordinal));
    }

    private static string BuildKey(SampledValuesFrame frame, SampledValueAsdu asdu)
        => $"{frame.Source}|{frame.Destination}|{frame.Vlan?.VlanId.ToString() ?? "-"}|{frame.AppId:X4}|{asdu.SvId}";
#endif

    private void Raise(string code, string message)
        => EventRaised?.Invoke(this, new ProcessBusEventArgs(code, message, DateTimeOffset.Now));
}

public sealed class ProcessBusEventArgs : EventArgs
{
    public ProcessBusEventArgs(string code, string message, DateTimeOffset timestamp)
    {
        Code = code;
        Message = message;
        Timestamp = timestamp;
    }

    public string Code { get; }
    public string Message { get; }
    public DateTimeOffset Timestamp { get; }
}
