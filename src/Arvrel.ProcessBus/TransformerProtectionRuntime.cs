using Arvrel.Protection;

namespace Arvrel.ProcessBus;

public enum TransformerRuntimeState
{
    WaitingForPair,
    PairBlocked,
    ProtectionBlocked,
    Ready,
    Pickup,
    TripLatched
}

public sealed record TransformerProtectionRuntimeConfiguration(
    string HighVoltageStreamKey,
    string LowVoltageStreamKey,
    TransformerNameplate Nameplate,
    TransformerWindingEngineering HighVoltageEngineering,
    TransformerWindingEngineering LowVoltageEngineering,
    TransformerProtectionSettings ProtectionSettings)
{
    public TransformerSvPairingSettings PairingSettings { get; init; } = new();
    public TransformerHarmonicEstimatorSettings HarmonicSettings { get; init; } = new();

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(HighVoltageStreamKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(LowVoltageStreamKey);
        if (string.Equals(HighVoltageStreamKey, LowVoltageStreamKey, StringComparison.Ordinal))
            throw new ArgumentException("HV and LV transformer inputs must use two distinct SV stream keys.");

        ArgumentNullException.ThrowIfNull(Nameplate);
        ArgumentNullException.ThrowIfNull(HighVoltageEngineering);
        ArgumentNullException.ThrowIfNull(LowVoltageEngineering);
        ArgumentNullException.ThrowIfNull(ProtectionSettings);
        ArgumentNullException.ThrowIfNull(PairingSettings);
        ArgumentNullException.ThrowIfNull(HarmonicSettings);

        Nameplate.Validate();
        HighVoltageEngineering.Validate(nameof(HighVoltageEngineering));
        LowVoltageEngineering.Validate(nameof(LowVoltageEngineering));
        ProtectionSettings.Validate();
        PairingSettings.Validate();
        HarmonicSettings.Validate();

        _ = TransformerEngineeringAdapter.Build(Nameplate, HighVoltageEngineering, LowVoltageEngineering);
    }
}

public sealed record TransformerRuntimePairIdentity(
    ushort HighVoltageSampleCounter,
    ushort LowVoltageSampleCounter,
    DateTimeOffset HighVoltageCaptureTimestamp,
    DateTimeOffset LowVoltageCaptureTimestamp);

public sealed record TransformerProtectionRuntimeSnapshot(
    DateTimeOffset Timestamp,
    ProcessBusSourceMode SourceMode,
    TransformerRuntimeState State,
    string HighVoltageStreamKey,
    string LowVoltageStreamKey,
    TransformerEngineeringPlan Engineering,
    TransformerProtectionSettings EffectiveSettings,
    string EffectiveSettingsFingerprint,
    TransformerSvPairDiagnostics Pairing,
    TransformerRuntimePairIdentity? PairIdentity,
    TransformerMeasurementFrame? Measurement,
    TransformerProtectionSnapshot? Protection,
    bool EvaluatedNewPair,
    string DecisionReason)
{
    public bool TripLatched => Protection?.TripLatched == true;
    public bool TripRequested => Protection?.TripRequested == true;
}

public sealed record TransformerProtectionRuntimeEvidence(
    DateTimeOffset ExportedAt,
    ProcessBusSourceMode SourceMode,
    string HighVoltageStreamKey,
    string LowVoltageStreamKey,
    TransformerNameplate Nameplate,
    TransformerVectorGroup VectorGroup,
    TransformerWindingEngineering HighVoltageEngineering,
    TransformerWindingEngineering LowVoltageEngineering,
    TransformerProtectionSettings EffectiveSettings,
    string EffectiveSettingsFingerprint,
    TransformerSvPairDiagnostics Pairing,
    TransformerRuntimePairIdentity? PairIdentity,
    TransformerMeasurementFrame? Measurement,
    TransformerProtectionSnapshot? Protection,
    string DecisionReason);

public sealed class TransformerProtectionRuntimeSnapshotChangedEventArgs : EventArgs
{
    public TransformerProtectionRuntimeSnapshotChangedEventArgs(TransformerProtectionRuntimeSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public TransformerProtectionRuntimeSnapshot Snapshot { get; }
}

/// <summary>
/// Stateful transformer-protection runtime core. It consumes two process-bus runtime
/// snapshots, applies P9 pairing plus P10 H2/H5 estimation, then evaluates the P8
/// transformer protection engine with engineering-derived compensation settings.
/// The class never emits a physical trip; TripRequested/TripLatched remain virtual
/// protection evidence only.
/// </summary>
public sealed class TransformerProtectionRuntime
{
    private readonly object _gate = new();
    private TransformerProtectionRuntimeConfiguration _configuration;
    private TransformerEngineeringPlan _engineering;
    private TransformerProtectionSettings _effectiveSettings;
    private TransformerProtectionEngine _engine;
    private TransformerRuntimePairIdentity? _lastEvaluatedPair;
    private TransformerProtectionRuntimeSnapshot _snapshot;

    public TransformerProtectionRuntime(TransformerProtectionRuntimeConfiguration configuration)
    {
        _configuration = ValidateConfiguration(configuration);
        _engineering = TransformerEngineeringAdapter.Build(
            _configuration.Nameplate,
            _configuration.HighVoltageEngineering,
            _configuration.LowVoltageEngineering);
        _effectiveSettings = _engineering.ApplyTo(_configuration.ProtectionSettings);
        _effectiveSettings.Validate();
        _engine = new TransformerProtectionEngine(_effectiveSettings);
        _snapshot = InitialSnapshot(ProcessBusSourceMode.InternalDemo, "Waiting for aligned HV/LV Sampled Values.");
    }

    public event EventHandler<TransformerProtectionRuntimeSnapshotChangedEventArgs>? SnapshotChanged;

    public TransformerProtectionRuntimeConfiguration Configuration
    {
        get
        {
            lock (_gate)
                return _configuration;
        }
    }

    public TransformerProtectionRuntimeSnapshot CurrentSnapshot
    {
        get
        {
            lock (_gate)
                return _snapshot;
        }
    }

    public TransformerProtectionRuntimeSnapshot EvaluateSnapshots(
        SmvRuntimeSnapshot highVoltage,
        SmvRuntimeSnapshot lowVoltage,
        ProcessBusSourceMode sourceMode)
    {
        ArgumentNullException.ThrowIfNull(highVoltage);
        ArgumentNullException.ThrowIfNull(lowVoltage);

        TransformerProtectionRuntimeSnapshot result;
        var changed = false;
        lock (_gate)
        {
            var pair = TransformerHarmonicProcessBusAdapter.AlignAndEstimate(
                highVoltage,
                lowVoltage,
                _configuration.PairingSettings,
                _configuration.HarmonicSettings);
            var identity = BuildIdentity(highVoltage, lowVoltage);
            var timestamp = Latest(highVoltage.Timestamp, lowVoltage.Timestamp);
            var priorProtection = _snapshot.Protection;

            if (!pair.IsAligned || pair.Measurement is null)
            {
                // Never let definite-time pickup bridge an interval for which a valid
                // paired transformer measurement did not exist. Before a virtual trip
                // latches, rebuild the deterministic engine so its next valid frame
                // starts from delta=0. A latched trip is historical evidence and stays
                // frozen until explicit Reset().
                if (priorProtection?.TripLatched != true)
                    _engine = new TransformerProtectionEngine(_effectiveSettings);
                _lastEvaluatedPair = null;

                var state = priorProtection?.TripLatched == true
                    ? TransformerRuntimeState.TripLatched
                    : IsWaitingCode(pair.Diagnostics.Code)
                        ? TransformerRuntimeState.WaitingForPair
                        : TransformerRuntimeState.PairBlocked;
                var reason = priorProtection?.TripLatched == true
                    ? $"TRIP LATCHED · transformer input unavailable · {pair.Diagnostics.Code} · {pair.Diagnostics.Detail}"
                    : $"TRANSFORMER INPUT BLOCKED · {pair.Diagnostics.Code} · {pair.Diagnostics.Detail}";

                result = new TransformerProtectionRuntimeSnapshot(
                    timestamp,
                    sourceMode,
                    state,
                    _configuration.HighVoltageStreamKey,
                    _configuration.LowVoltageStreamKey,
                    _engineering,
                    _effectiveSettings,
                    _effectiveSettings.Fingerprint(),
                    pair.Diagnostics,
                    identity,
                    null,
                    priorProtection,
                    false,
                    reason);
                changed = !Equals(_snapshot, result);
                _snapshot = result;
            }
            else if (_lastEvaluatedPair is not null && Equals(_lastEvaluatedPair, identity))
            {
                result = _snapshot with
                {
                    Timestamp = timestamp,
                    SourceMode = sourceMode,
                    Pairing = pair.Diagnostics,
                    PairIdentity = identity,
                    EvaluatedNewPair = false
                };
                changed = !Equals(_snapshot, result);
                _snapshot = result;
            }
            else if (priorProtection?.TripLatched == true)
            {
                // Freeze the protection decision after a virtual trip latch. Continue
                // updating aligned measurement evidence without changing the operated
                // element until the operator explicitly resets the runtime.
                _lastEvaluatedPair = identity;
                result = new TransformerProtectionRuntimeSnapshot(
                    pair.Measurement.Timestamp,
                    sourceMode,
                    TransformerRuntimeState.TripLatched,
                    _configuration.HighVoltageStreamKey,
                    _configuration.LowVoltageStreamKey,
                    _engineering,
                    _effectiveSettings,
                    _effectiveSettings.Fingerprint(),
                    pair.Diagnostics,
                    identity,
                    pair.Measurement,
                    priorProtection,
                    true,
                    priorProtection.DecisionReason);
                changed = true;
                _snapshot = result;
            }
            else
            {
                var protection = _engine.Evaluate(pair.Measurement);
                _lastEvaluatedPair = identity;
                var state = ResolveState(protection);
                var reason = protection.TripLatched
                    ? protection.DecisionReason
                    : protection.Blocked
                        ? protection.DecisionReason
                        : state == TransformerRuntimeState.Pickup
                            ? protection.DecisionReason
                            : $"RUNTIME READY · {protection.DecisionReason}";

                result = new TransformerProtectionRuntimeSnapshot(
                    pair.Measurement.Timestamp,
                    sourceMode,
                    state,
                    _configuration.HighVoltageStreamKey,
                    _configuration.LowVoltageStreamKey,
                    _engineering,
                    _effectiveSettings,
                    _effectiveSettings.Fingerprint(),
                    pair.Diagnostics,
                    identity,
                    pair.Measurement,
                    protection,
                    true,
                    reason);
                changed = true;
                _snapshot = result;
            }
        }

        if (changed)
            SnapshotChanged?.Invoke(this, new TransformerProtectionRuntimeSnapshotChangedEventArgs(result));
        return result;
    }

    public void UpdateConfiguration(
        TransformerProtectionRuntimeConfiguration configuration,
        bool keepTripLatch = false)
    {
        var validated = ValidateConfiguration(configuration);
        var engineering = TransformerEngineeringAdapter.Build(
            validated.Nameplate,
            validated.HighVoltageEngineering,
            validated.LowVoltageEngineering);
        var effective = engineering.ApplyTo(validated.ProtectionSettings);
        effective.Validate();

        TransformerProtectionRuntimeSnapshot snapshot;
        lock (_gate)
        {
            var priorProtection = keepTripLatch && _snapshot.Protection?.TripLatched == true
                ? _snapshot.Protection
                : null;
            _configuration = validated;
            _engineering = engineering;
            _effectiveSettings = effective;
            _engine = new TransformerProtectionEngine(effective);
            _lastEvaluatedPair = null;
            snapshot = InitialSnapshot(_snapshot.SourceMode, "Transformer runtime configuration updated; waiting for a new aligned pair.");
            if (priorProtection is not null)
            {
                snapshot = snapshot with
                {
                    State = TransformerRuntimeState.TripLatched,
                    Protection = priorProtection,
                    DecisionReason = $"{priorProtection.DecisionReason} · configuration updated with latch preserved"
                };
            }
            _snapshot = snapshot;
        }
        SnapshotChanged?.Invoke(this, new TransformerProtectionRuntimeSnapshotChangedEventArgs(snapshot));
    }

    public void Reset()
    {
        TransformerProtectionRuntimeSnapshot snapshot;
        lock (_gate)
        {
            _engine = new TransformerProtectionEngine(_effectiveSettings);
            _lastEvaluatedPair = null;
            snapshot = InitialSnapshot(_snapshot.SourceMode, "Transformer runtime and virtual trip latch reset.");
            _snapshot = snapshot;
        }
        SnapshotChanged?.Invoke(this, new TransformerProtectionRuntimeSnapshotChangedEventArgs(snapshot));
    }

    public TransformerProtectionRuntimeEvidence CaptureEvidence()
    {
        lock (_gate)
        {
            return new TransformerProtectionRuntimeEvidence(
                DateTimeOffset.UtcNow,
                _snapshot.SourceMode,
                _configuration.HighVoltageStreamKey,
                _configuration.LowVoltageStreamKey,
                _configuration.Nameplate,
                _engineering.VectorGroup,
                _configuration.HighVoltageEngineering,
                _configuration.LowVoltageEngineering,
                _effectiveSettings,
                _effectiveSettings.Fingerprint(),
                _snapshot.Pairing,
                _snapshot.PairIdentity,
                _snapshot.Measurement,
                _snapshot.Protection,
                _snapshot.DecisionReason);
        }
    }

    private TransformerProtectionRuntimeSnapshot InitialSnapshot(ProcessBusSourceMode sourceMode, string reason)
        => new(
            DateTimeOffset.UtcNow,
            sourceMode,
            TransformerRuntimeState.WaitingForPair,
            _configuration.HighVoltageStreamKey,
            _configuration.LowVoltageStreamKey,
            _engineering,
            _effectiveSettings,
            _effectiveSettings.Fingerprint(),
            TransformerSvPairDiagnostics.Unavailable("PAIR_WAITING", reason),
            null,
            null,
            null,
            false,
            reason);

    private static TransformerProtectionRuntimeConfiguration ValidateConfiguration(
        TransformerProtectionRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        return configuration;
    }

    private static TransformerRuntimePairIdentity BuildIdentity(
        SmvRuntimeSnapshot highVoltage,
        SmvRuntimeSnapshot lowVoltage)
        => new(
            highVoltage.SampleCounter,
            lowVoltage.SampleCounter,
            highVoltage.Timestamp,
            lowVoltage.Timestamp);

    private static TransformerRuntimeState ResolveState(TransformerProtectionSnapshot protection)
    {
        if (protection.TripLatched)
            return TransformerRuntimeState.TripLatched;
        if (protection.Blocked)
            return TransformerRuntimeState.ProtectionBlocked;
        if (!string.Equals(protection.ActiveElement, "READY", StringComparison.Ordinal))
            return TransformerRuntimeState.Pickup;
        return TransformerRuntimeState.Ready;
    }

    private static bool IsWaitingCode(string code)
        => code is "PAIR_WAITING" or "PAIR_STREAM_MISSING" or "PAIR_HV_WINDOW_NOT_READY" or "PAIR_LV_WINDOW_NOT_READY";

    private static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;
}

/// <summary>
/// Event-driven bridge between SmvProcessBusController and TransformerProtectionRuntime.
/// It works for both live capture and PCAP replay because the controller raises one
/// stream-update event after every accepted/rejected SV ingress transaction.
/// </summary>
public sealed class TransformerProcessBusProtectionRuntime : IDisposable
{
    private readonly SmvProcessBusController _controller;
    private readonly TransformerProtectionRuntime _runtime;
    private bool _disposed;

    public TransformerProcessBusProtectionRuntime(
        SmvProcessBusController controller,
        TransformerProtectionRuntimeConfiguration configuration)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _runtime = new TransformerProtectionRuntime(configuration);
        _controller.StreamUpdated += OnStreamUpdated;
    }

    public event EventHandler<TransformerProtectionRuntimeSnapshotChangedEventArgs>? SnapshotChanged
    {
        add => _runtime.SnapshotChanged += value;
        remove => _runtime.SnapshotChanged -= value;
    }

    public TransformerProtectionRuntimeConfiguration Configuration => _runtime.Configuration;
    public TransformerProtectionRuntimeSnapshot CurrentSnapshot => _runtime.CurrentSnapshot;

    public TransformerProtectionRuntimeSnapshot EvaluateCurrent()
    {
        ThrowIfDisposed();
        var configuration = _runtime.Configuration;
        var highVoltage = _controller.GetSnapshot(configuration.HighVoltageStreamKey, primaryDisplay: false);
        var lowVoltage = _controller.GetSnapshot(configuration.LowVoltageStreamKey, primaryDisplay: false);
        return _runtime.EvaluateSnapshots(highVoltage, lowVoltage, ResolveSourceMode());
    }

    public void UpdateConfiguration(
        TransformerProtectionRuntimeConfiguration configuration,
        bool keepTripLatch = false)
    {
        ThrowIfDisposed();
        _runtime.UpdateConfiguration(configuration, keepTripLatch);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _runtime.Reset();
    }

    public TransformerProtectionRuntimeEvidence CaptureEvidence()
    {
        ThrowIfDisposed();
        return _runtime.CaptureEvidence();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _controller.StreamUpdated -= OnStreamUpdated;
        _disposed = true;
    }

    private void OnStreamUpdated(object? sender, SmvStreamUpdatedEventArgs e)
    {
        if (_disposed)
            return;
        var configuration = _runtime.Configuration;
        if (!string.Equals(e.StreamKey, configuration.HighVoltageStreamKey, StringComparison.Ordinal) &&
            !string.Equals(e.StreamKey, configuration.LowVoltageStreamKey, StringComparison.Ordinal))
            return;

        try
        {
            _ = EvaluateCurrent();
        }
        catch (Exception)
        {
            // Protection-runtime subscribers must never break the controller capture/replay loop.
            // Deterministic configuration validation occurs before subscription; transient source
            // conditions are represented as runtime snapshots rather than propagated here.
        }
    }

    private ProcessBusSourceMode ResolveSourceMode()
        => _controller.IsReplayMode
            ? ProcessBusSourceMode.PcapReplay
            : _controller.IsRunning
                ? ProcessBusSourceMode.LiveCapture
                : ProcessBusSourceMode.InternalDemo;

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
