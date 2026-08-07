using System.Collections.Concurrent;
using Arvrel.Capture;
using Arvrel.ProcessBus.Capture;
using Arvrel.Protection;

#if ARIEC61850_SIBLING
using AR.Iec61850.SampledValues;
using AR.Iec61850.Scl;
#endif

namespace Arvrel.ProcessBus;

public sealed class SmvProcessBusController : IAsyncDisposable
{
    private readonly ProtectionSettings _settings;
    private readonly ILiveCaptureBackend _liveCaptureBackend;
    private readonly ICaptureReplaySource _replaySource;
    private readonly ConcurrentDictionary<string, SmvStreamRuntime> _streams = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SmvIngressContinuityGate> _continuity = new(StringComparer.Ordinal);
    private readonly SmvContinuityNoticeLimiter _continuityNotices = new();
    private readonly object _profileGate = new();
    private SmvMeasurementContext _measurementContext = new();
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private bool _replayMode;

#if ARIEC61850_SIBLING
    private IReadOnlyList<SampledValuesPublisherProfile> _profiles = Array.Empty<SampledValuesPublisherProfile>();
#endif

    public SmvProcessBusController(ProtectionSettings settings)
        : this(
            settings,
            ProcessBusCaptureBackendFactory.CreateDefault(),
            new PcapFileReplaySource())
    {
    }

    public SmvProcessBusController(
        ProtectionSettings settings,
        ILiveCaptureBackend liveCaptureBackend,
        ICaptureReplaySource replaySource)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _liveCaptureBackend = liveCaptureBackend ?? throw new ArgumentNullException(nameof(liveCaptureBackend));
        _replaySource = replaySource ?? throw new ArgumentNullException(nameof(replaySource));
    }

    public static readonly bool IsAvailable = ProcessBusCaptureBackendFactory.DecoderAvailable;

    public event EventHandler<ProcessBusEventArgs>? EventRaised;
    public event EventHandler<SmvStreamUpdatedEventArgs>? StreamUpdated;

    public bool IsRunning => _captureTask is { IsCompleted: false };
    public bool IsReplayMode => _replayMode;
    public bool IsLiveCaptureAvailable => IsAvailable && _liveCaptureBackend.IsAvailable;
    public bool IsReplayAvailable => IsAvailable;
    public string LiveCaptureBackendId => _liveCaptureBackend.BackendId;
    public string LiveCaptureBackendName => _liveCaptureBackend.DisplayName;
    public string CaptureAvailabilityMessage => IsAvailable
        ? _liveCaptureBackend.AvailabilityMessage
        : "The sibling ARIEC61850 decoder was not available at build time.";
    public string SclSummary { get; private set; } = "No SCL loaded";
    public SmvMeasurementContext MeasurementContext => _measurementContext;

    public IReadOnlyList<ProcessBusAdapter> ListAdapters()
    {
        if (!IsLiveCaptureAvailable)
            return Array.Empty<ProcessBusAdapter>();

        try
        {
            return _liveCaptureBackend.ListAdapters()
                .Select(adapter => new ProcessBusAdapter(
                    adapter.Id,
                    adapter.DisplayName,
                    adapter.Address))
                .ToArray();
        }
        catch (Exception ex)
        {
            Raise("ADAPTER", $"{_liveCaptureBackend.DisplayName} adapter discovery failed: {ex.Message}");
            return Array.Empty<ProcessBusAdapter>();
        }
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
        if (!_liveCaptureBackend.IsAvailable)
            throw new NotSupportedException(_liveCaptureBackend.AvailabilityMessage);

        await StopAsync().ConfigureAwait(false);
        ClearStreams();
        _replayMode = false;
        _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _captureCancellation.Token;
        _captureTask = Task.Run(() => CaptureLoopAsync(adapterSelector, token), CancellationToken.None);
        Raise("LIVE", $"Listening on {_liveCaptureBackend.DisplayName} adapter {adapterSelector}.");
        await Task.Yield();
#else
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("Live capture requires the sibling ARIEC61850 decoder and a supported capture backend.");
#endif
    }

    public async Task<int> ReplayAsync(string path, CancellationToken cancellationToken = default)
    {
#if ARIEC61850_SIBLING
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_replaySource.CanRead(path))
            throw new NotSupportedException($"Capture replay source '{_replaySource.SourceId}' cannot read {Path.GetExtension(path)} files.");

        await StopAsync().ConfigureAwait(false);
        ClearStreams();
        _replayMode = true;
        var processed = await Task.Run(async () =>
        {
            var count = 0;
            await foreach (var packet in _replaySource.ReadAsync(path, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObserveFrame(packet.Timestamp, packet.Frame, replay: true);
                count++;
                if (count % 10_000 == 0)
                    Raise("REPLAY", $"Processed {count:N0} capture frames.");
            }
            return count;
        }, cancellationToken).ConfigureAwait(false);
        Raise("REPLAY", $"Processed {processed:N0} frame(s) from {Path.GetFileName(path)} via {_replaySource.SourceId}.");
        return processed;
#else
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("PCAP replay requires the sibling ARIEC61850 decoder.");
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

    private void ClearStreams()
    {
        _streams.Clear();
        _continuity.Clear();
        _continuityNotices.Clear();
    }

#if ARIEC61850_SIBLING
    private async Task CaptureLoopAsync(string adapterSelector, CancellationToken cancellationToken)
    {
        try
        {
            var options = new CaptureOptions
            {
                Filter = "(ether proto 0x88ba) or (vlan and ether proto 0x88ba)",
                BufferCapacity = 16_384,
                ReadTimeoutMilliseconds = 250
            };

            await foreach (var captured in _liveCaptureBackend
                .CaptureAsync(adapterSelector, options, cancellationToken)
                .ConfigureAwait(false))
            {
                ObserveFrame(captured.Timestamp, captured.Frame, replay: false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop path.
        }
        catch (Exception ex)
        {
            Raise("ERROR", $"{_liveCaptureBackend.DisplayName} capture failed: {ex.Message}");
        }
    }

    private void ObserveFrame(DateTimeOffset timestamp, ReadOnlyMemory<byte> ethernetFrame, bool replay)
    {
        if (!SampledValuesFrameParser.TryParseEthernetFrame(ethernetFrame, out var frame))
            return;

        var asdus = frame.Pdu.Asdus;
        var first = asdus.FirstOrDefault();
        if (first is null)
            return;

        var profile = FindProfile(frame, first);
        var key = BuildKey(frame, first);
        var gate = _continuity.GetOrAdd(key, _ => new SmvIngressContinuityGate());
        var counters = asdus.Select(asdu => asdu.SampleCount).ToArray();
        var wrap = ResolveCounterWrap(first, _measurementContext.NominalFrequencyHz);
        var ingress = gate.ObserveFrame(counters, wrap);

        // Create the runtime before handling a rejected transition so the frame can be
        // represented in stream telemetry and the trust gate without admitting payload
        // samples into the measurement buffers.
        var runtime = _streams.GetOrAdd(key, _ =>
        {
            Raise("STREAM", $"Discovered {first.SvId} · APPID 0x{frame.AppId:X4}.");
            return new SmvStreamRuntime(key, frame, _settings, _measurementContext);
        });

        if (ingress.Decision == SmvIngressDecision.DropDuplicate)
        {
            runtime.ObserveIngressRejection(timestamp, ingress.Decision, ingress.SampleCount, asdus.Count, replay);
            RaiseStreamUpdated(key, timestamp, replay, payloadAccepted: false);
            if (_continuityNotices.ShouldRaise(key, "DUPLICATE", timestamp))
                Raise("SMV DROP", $"{first.SvId} duplicate smpCnt {ingress.SampleCount} discarded before measurement.");
            return;
        }

        if (ingress.Decision == SmvIngressDecision.DropOutOfOrder)
        {
            runtime.ObserveIngressRejection(timestamp, ingress.Decision, ingress.SampleCount, asdus.Count, replay);
            RaiseStreamUpdated(key, timestamp, replay, payloadAccepted: false);
            if (_continuityNotices.ShouldRaise(key, "OUT_OF_ORDER", timestamp))
                Raise("SMV DROP", $"{first.SvId} out-of-order smpCnt {ingress.SampleCount} discarded before measurement.");
            return;
        }

        if (ingress.Decision == SmvIngressDecision.RestartWindow &&
            _continuityNotices.ShouldRaise(key, "GAP", timestamp))
        {
            Raise("SMV RESYNC", $"{first.SvId} smpCnt gap detected near {ingress.SampleCount}; holding coherent display during trust recovery.");
        }

        // Preserve the existing runtime. Communications discontinuities must not
        // clear protection timers or a latched trip. The runtime records a forward
        // gap, removes trip permission through its trust policy, and naturally
        // replaces the one/two-cycle windows with contiguous post-gap samples while
        // the UI holds the last coherent evidence.
        runtime.Observe(timestamp, frame, profile, replay);
        RaiseStreamUpdated(key, timestamp, replay, payloadAccepted: true);
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

    private static ushort ResolveCounterWrap(SampledValueAsdu asdu, double nominalFrequency)
    {
        if (!asdu.SampleRate.HasValue || asdu.SampleRate.Value == 0)
            return 4000;

        var value = asdu.SampleMode switch
        {
            0 => asdu.SampleRate.Value * nominalFrequency,
            1 => asdu.SampleRate.Value,
            _ => asdu.SampleRate.Value is 80 or 96
                ? asdu.SampleRate.Value * nominalFrequency
                : asdu.SampleRate.Value
        };
        return (ushort)Math.Clamp((int)Math.Round(value), 2, ushort.MaxValue);
    }

    private static string BuildKey(SampledValuesFrame frame, SampledValueAsdu asdu)
        => $"{frame.Source}|{frame.Destination}|{frame.Vlan?.VlanId.ToString() ?? "-"}|{frame.AppId:X4}|{asdu.SvId}";
#endif

    private void Raise(string code, string message)
        => EventRaised?.Invoke(this, new ProcessBusEventArgs(code, message, DateTimeOffset.Now));

    private void RaiseStreamUpdated(string streamKey, DateTimeOffset timestamp, bool replay, bool payloadAccepted)
        => StreamUpdated?.Invoke(this, new SmvStreamUpdatedEventArgs(streamKey, timestamp, replay, payloadAccepted));
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

public sealed class SmvStreamUpdatedEventArgs : EventArgs
{
    public SmvStreamUpdatedEventArgs(
        string streamKey,
        DateTimeOffset timestamp,
        bool replay,
        bool payloadAccepted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamKey);
        StreamKey = streamKey;
        Timestamp = timestamp;
        Replay = replay;
        PayloadAccepted = payloadAccepted;
    }

    public string StreamKey { get; }
    public DateTimeOffset Timestamp { get; }
    public bool Replay { get; }
    public bool PayloadAccepted { get; }
}
