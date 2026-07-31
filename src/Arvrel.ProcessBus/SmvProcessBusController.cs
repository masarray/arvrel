using System.Collections.Concurrent;
using Arvrel.Protection;

#if ARIEC61850_SIBLING
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
using AR.Iec61850.Transports;
using AR.Iec61850.Transports.Npcap;
#endif

namespace Arvrel.ProcessBus;

public sealed class SmvProcessBusController : IAsyncDisposable
{
    private readonly ProtectionSettings _settings;
    private readonly ConcurrentDictionary<string, SmvStreamRuntime> _streams = new(StringComparer.Ordinal);
    private readonly object _profileGate = new();
    private SmvMeasurementContext _measurementContext = new();
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private bool _replayMode;

#if ARIEC61850_SIBLING
    private IReadOnlyList<SampledValuesPublisherProfile> _profiles = Array.Empty<SampledValuesPublisherProfile>();
#endif

    public SmvProcessBusController(ProtectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

#if ARIEC61850_SIBLING
    public static readonly bool IsAvailable = true;
#else
    public static readonly bool IsAvailable = false;
#endif

    public event EventHandler<ProcessBusEventArgs>? EventRaised;

    public bool IsRunning => _captureTask is { IsCompleted: false };
    public bool IsReplayMode => _replayMode;
    public string SclSummary { get; private set; } = "No SCL loaded";
    public SmvMeasurementContext MeasurementContext => _measurementContext;

    public IReadOnlyList<ProcessBusAdapter> ListAdapters()
    {
#if ARIEC61850_SIBLING
        try
        {
            return NpcapAdapterCatalog.ListAdapters()
                .Select(adapter => new ProcessBusAdapter(
                    adapter.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(adapter.Description)
                        ? $"{adapter.Index}. {adapter.Name}"
                        : $"{adapter.Index}. {adapter.Description}",
                    adapter.MacAddress?.ToString() ?? "No MAC"))
                .ToArray();
        }
        catch (Exception ex)
        {
            Raise("ADAPTER", $"Npcap adapter discovery failed: {ex.Message}");
            return Array.Empty<ProcessBusAdapter>();
        }
#else
        return Array.Empty<ProcessBusAdapter>();
#endif
    }

    public void LoadScl(string path)
    {
#if ARIEC61850_SIBLING
        var document = new SclParser().Load(path);
        var profiles = SampledValuesPublisherProfile.CreateMany(document);
        lock (_profileGate)
            _profiles = profiles;
        SclSummary = $"{Path.GetFileName(path)} · {profiles.Count} SV stream(s)";
        Raise("SCL", $"Loaded {profiles.Count} SampledValueControl profile(s) from {Path.GetFileName(path)}.");
#else
        throw new NotSupportedException("The sibling ARIEC61850 engine was not available at build time.");
#endif
    }

    public void SetMeasurementContext(SmvMeasurementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        _measurementContext = context;
        foreach (var runtime in _streams.Values)
            runtime.SetMeasurementContext(context);
        Raise("CT", $"Measurement context set to {context.CtPrimaryA:0.###}/{context.CtSecondaryA:0.###} A at {context.NominalFrequencyHz:0.###} Hz.");
    }

    public async Task StartLiveAsync(string adapterSelector, CancellationToken cancellationToken = default)
    {
#if ARIEC61850_SIBLING
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterSelector);
        await StopAsync().ConfigureAwait(false);
        _streams.Clear();
        _replayMode = false;
        _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _captureCancellation.Token;
        _captureTask = Task.Run(() => CaptureLoopAsync(adapterSelector, token), CancellationToken.None);
        Raise("LIVE", $"Listening on Npcap adapter {adapterSelector}.");
        await Task.Yield();
#else
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("Live Npcap capture requires the sibling ARIEC61850 engine.");
#endif
    }

    public async Task<int> ReplayAsync(string path, CancellationToken cancellationToken = default)
    {
#if ARIEC61850_SIBLING
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await StopAsync().ConfigureAwait(false);
        _streams.Clear();
        _replayMode = true;
        var processed = await Task.Run(() =>
        {
            var count = 0;
            foreach (var packet in PcapPacketReader.Read(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObserveFrame(packet.Timestamp, packet.Frame, replay: true);
                count++;
                if (count % 10_000 == 0)
                    Raise("REPLAY", $"Processed {count:N0} capture frames.");
            }
            return count;
        }, cancellationToken).ConfigureAwait(false);
        Raise("REPLAY", $"Processed {processed:N0} frame(s) from {Path.GetFileName(path)}.");
        return processed;
#else
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("PCAP replay requires the sibling ARIEC61850 engine.");
#endif
    }

    public IReadOnlyList<SmvStreamInfo> GetStreams()
    {
#if ARIEC61850_SIBLING
        var now = DateTimeOffset.UtcNow;
        return _streams.Values
            .Select(runtime => runtime.Snapshot(now, false, _replayMode).Stream)
            .OrderBy(stream => stream.SvId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stream => stream.AppId)
            .ToArray();
#else
        return Array.Empty<SmvStreamInfo>();
#endif
    }

    public SmvRuntimeSnapshot GetSnapshot(string? streamKey, bool primaryDisplay)
    {
#if ARIEC61850_SIBLING
        if (string.IsNullOrWhiteSpace(streamKey) || !_streams.TryGetValue(streamKey, out var runtime))
            return SmvRuntimeSnapshot.Empty;
        return runtime.Snapshot(DateTimeOffset.UtcNow, primaryDisplay, _replayMode);
#else
        return SmvRuntimeSnapshot.Empty;
#endif
    }

    public void ResetProtection(string? streamKey)
    {
#if ARIEC61850_SIBLING
        if (!string.IsNullOrWhiteSpace(streamKey) && _streams.TryGetValue(streamKey, out var selected))
            selected.ResetProtection();
        else
            foreach (var runtime in _streams.Values)
                runtime.ResetProtection();
        Raise("RESET", "Live protection state and trip latch reset.");
#endif
    }

    public async Task StopAsync()
    {
        var cancellation = _captureCancellation;
        var task = _captureTask;
        _captureCancellation = null;
        _captureTask = null;
        if (cancellation is null)
            return;

        cancellation.Cancel();
        try
        {
            if (task is not null)
                await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the operator stops live capture.
        }
        finally
        {
            cancellation.Dispose();
        }
        Raise("STOP", "Process-bus source stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

#if ARIEC61850_SIBLING
    private async Task CaptureLoopAsync(string adapterSelector, CancellationToken cancellationToken)
    {
        try
        {
            using var source = new NpcapProcessBusFrameSource(adapterSelector);
            var options = new ProcessBusCaptureOptions
            {
                Filter = "(ether proto 0x88ba) or (vlan and ether proto 0x88ba)",
                BufferCapacity = 16_384,
                ReadTimeoutMilliseconds = 250
            };

            await foreach (var captured in source.CaptureAsync(options, cancellationToken).ConfigureAwait(false))
                ObserveFrame(captured.Timestamp, captured.Frame, replay: false);
        }
        catch (OperationCanceledException)
        {
            // Normal stop path.
        }
        catch (Exception ex)
        {
            Raise("ERROR", $"Live capture failed: {ex.Message}");
        }
    }

    private void ObserveFrame(DateTimeOffset timestamp, ReadOnlyMemory<byte> ethernetFrame, bool replay)
    {
        if (!SampledValuesFrameParser.TryParseEthernetFrame(ethernetFrame, out var frame))
            return;

        var first = frame.Pdu.Asdus.FirstOrDefault();
        if (first is null)
            return;

        var profile = FindProfile(frame, first);
        var key = BuildKey(frame, first);
        var runtime = _streams.GetOrAdd(key, _ =>
        {
            Raise("STREAM", $"Discovered {first.SvId} · APPID 0x{frame.AppId:X4}.");
            return new SmvStreamRuntime(key, frame, _settings, _measurementContext);
        });
        runtime.Observe(timestamp, frame, profile, replay);
    }

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
        => $"{frame.Source}|{frame.Destination}|{frame.Vlan?.VlanId?.ToString() ?? "-"}|{frame.AppId:X4}|{asdu.SvId}";
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
