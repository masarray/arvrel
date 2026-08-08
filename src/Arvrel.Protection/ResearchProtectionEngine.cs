using Arvrel.Protection.Algorithms;

namespace Arvrel.Protection;

/// <summary>
/// Dual-mode protection host used by the ARVREL laboratory surfaces. The native
/// <see cref="ProtectionEngine"/> always runs as the standard reference oracle.
/// Validated custom definitions run in parallel from the exact same measurement
/// frame and can replace only their own virtual 50/51 output after explicit activation.
/// </summary>
public sealed class ResearchProtectionEngine
{
    private ProtectionSettings _settings;
    private readonly ProtectionEngine _standard;
    private readonly AlgorithmDualModeHost _custom = new();
    private readonly Dictionary<string, PickupEvidence> _pickupEvidence = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _standardHashes = new(StringComparer.OrdinalIgnoreCase);
    private string _standardHashFingerprint = string.Empty;
    private string _activeSignature = string.Empty;
    private bool _tripLatched;
    private ProtectionOperationEvidence? _latchedOperation;

    public ResearchProtectionEngine(ProtectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        _standard = new ProtectionEngine(_settings);
        _activeSignature = ActiveSignature(AlgorithmRuntimeRegistry.Snapshot());
    }

    public ProtectionSettings Settings => _settings;

    public void UpdateSettings(ProtectionSettings settings, bool keepTripLatch = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
        _standard.UpdateSettings(settings, keepTripLatch: false);
        _custom.Reset();
        _standardHashes.Clear();
        _standardHashFingerprint = string.Empty;
        _pickupEvidence.Clear();
        if (!keepTripLatch)
        {
            _tripLatched = false;
            _latchedOperation = null;
        }
        _activeSignature = ActiveSignature(AlgorithmRuntimeRegistry.Snapshot());
    }

    public ProtectionSnapshot Evaluate(MeasurementFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame.SmvTrust);
        var runtime = AlgorithmRuntimeRegistry.Snapshot();
        ObserveActivationTransition(runtime);

        var standard = _standard.Evaluate(frame);
        var customResults = _custom.Evaluate(frame, _settings);
        var comparisons = BuildComparisons(frame, standard, customResults, runtime);

        if (customResults.Count == 0)
            return standard with
            {
                AlgorithmComparisons = comparisons,
                AlgorithmRuntime = runtime
            };

        var phase50 = ResolveEffective("50P-1", standard.Phase50, customResults, runtime);
        var phase51 = ResolveEffective("51P", standard.Phase51, customResults, runtime);
        var earth50 = ResolveEffective("50N", standard.Earth50, customResults, runtime);
        var earth51 = ResolveEffective("51N", standard.Earth51, customResults, runtime);

        var phase50Trip = EffectiveTripRequested("50P-1", standard.Phase50, customResults, runtime, frame.SmvTrust);
        var phase51Trip = EffectiveTripRequested("51P", standard.Phase51, customResults, runtime, frame.SmvTrust);
        var earth50Trip = EffectiveTripRequested("50N", standard.Earth50, customResults, runtime, frame.SmvTrust);
        var earth51Trip = EffectiveTripRequested("51N", standard.Earth51, customResults, runtime, frame.SmvTrust);
        var tripRequested = standard.Feeder.TripRequested || phase50Trip || phase51Trip || earth50Trip || earth51Trip;

        var legacy = new[] { phase50, earth50, phase51, earth51 };
        var legacyOperation = legacy.Any(element => element.Operated);
        var blocked = standard.Feeder.Blocked ||
                      legacy.Any(element => element.State == ProtectionStageState.Blocked) ||
                      (legacyOperation && !frame.SmvTrust.AllowsTrip);

        UpdatePickupEvidence(frame.Timestamp, legacy);
        var operatedElement = ResolveFirstElement(legacy, operated: true) ?? NormalizeElement(standard.Feeder.ActiveElement);
        var pickupElement = ResolveFirstElement(legacy, operated: false) ??
                            (standard.Feeder.ActiveElement == "READY" ? null : NormalizeElement(standard.Feeder.ActiveElement));
        var activeElement = operatedElement ?? FormatPickupElement(pickupElement) ?? "READY";

        if (tripRequested && !_tripLatched)
        {
            var tripElement = operatedElement ?? pickupElement ?? "UNKNOWN";
            _latchedOperation = IsLegacyElement(tripElement)
                ? CaptureOperationEvidence(tripElement, frame.Timestamp, legacy, frame)
                : standard.LatchedOperation;
        }
        if (tripRequested)
            _tripLatched = true;

        var reportedElement = _tripLatched && _latchedOperation is not null
            ? _latchedOperation.Element
            : activeElement;
        var decisionReason = blocked
            ? $"TRIP BLOCKED · {frame.SmvTrust.Code} · {frame.SmvTrust.Detail}"
            : _tripLatched
                ? $"TRIP LATCHED · {reportedElement}"
                : activeElement == "READY"
                    ? "Measurements stable · no pickup"
                    : $"OPERATING · {activeElement}";

        var phaseCustomActive = IsActive(runtime, "50P-1") || IsActive(runtime, "51P");
        var earthCustomActive = IsActive(runtime, "50N") || IsActive(runtime, "51N");
        var minimumPhasePickup = Math.Min(
            phase50.Pickup ? phase50.PickupSetting : double.PositiveInfinity,
            phase51.Pickup ? phase51.PickupSetting : double.PositiveInfinity);
        var phaseA = phaseCustomActive && double.IsFinite(minimumPhasePickup)
            ? frame.PhaseA >= minimumPhasePickup
            : standard.PhaseAPickup;
        var phaseB = phaseCustomActive && double.IsFinite(minimumPhasePickup)
            ? frame.PhaseB >= minimumPhasePickup
            : standard.PhaseBPickup;
        var phaseC = phaseCustomActive && double.IsFinite(minimumPhasePickup)
            ? frame.PhaseC >= minimumPhasePickup
            : standard.PhaseCPickup;
        var earth = earthCustomActive
            ? earth50.Pickup || earth51.Pickup || standard.Feeder.DirectionalEarth67N.Pickup || standard.Feeder.ResidualOvervoltage59N.Pickup
            : standard.EarthPickup;

        return standard with
        {
            Phase50 = phase50,
            Phase51 = phase51,
            Earth50 = earth50,
            Earth51 = earth51,
            PhaseAPickup = phaseA,
            PhaseBPickup = phaseB,
            PhaseCPickup = phaseC,
            EarthPickup = earth,
            TripRequested = tripRequested,
            TripLatched = _tripLatched,
            Blocked = blocked,
            ActiveElement = reportedElement,
            DecisionReason = decisionReason,
            LatchedOperation = _latchedOperation,
            AlgorithmComparisons = comparisons,
            AlgorithmRuntime = runtime
        };
    }

    public void Reset()
    {
        _standard.Reset();
        _custom.Reset();
        _pickupEvidence.Clear();
        _tripLatched = false;
        _latchedOperation = null;
        _activeSignature = ActiveSignature(AlgorithmRuntimeRegistry.Snapshot());
    }

    public void ObserveRejectedFrameTimestamp(DateTimeOffset timestamp)
    {
        _standard.ObserveRejectedFrameTimestamp(timestamp);
        _custom.ObserveRejectedFrameTimestamp(timestamp);
    }

    private void ObserveActivationTransition(AlgorithmRuntimeRegistrySnapshot runtime)
    {
        var signature = ActiveSignature(runtime);
        if (string.Equals(signature, _activeSignature, StringComparison.Ordinal))
            return;

        // Changing the executing algorithm is a relay state transition. Timers,
        // integration progress and the virtual trip latch are reset atomically so
        // no state from the previous algorithm can leak into the newly active one.
        _standard.Reset();
        _custom.Reset();
        _pickupEvidence.Clear();
        _tripLatched = false;
        _latchedOperation = null;
        _activeSignature = signature;
    }

    private IReadOnlyList<AlgorithmAbComparisonSnapshot> BuildComparisons(
        MeasurementFrame frame,
        ProtectionSnapshot standard,
        IReadOnlyDictionary<string, AlgorithmExecutionResult> customResults,
        AlgorithmRuntimeRegistrySnapshot runtime)
    {
        if (customResults.Count == 0)
            return Array.Empty<AlgorithmAbComparisonSnapshot>();

        var results = new List<AlgorithmAbComparisonSnapshot>(customResults.Count);
        foreach (var pair in customResults.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var standardElement = StandardElement(pair.Key, standard);
            var standardTrip = StandardTripRequested(standardElement, frame.SmvTrust);
            results.Add(new AlgorithmAbComparisonSnapshot(
                frame.Timestamp,
                pair.Key,
                StandardSourceHash(pair.Key),
                pair.Value.SourceHash,
                IsActive(runtime, pair.Key, pair.Value.SourceHash),
                standardElement,
                pair.Value.Snapshot,
                standardTrip,
                pair.Value.TripRequested));
        }
        return results;
    }

    private string StandardSourceHash(string element)
    {
        var fingerprint = _settings.Fingerprint();
        if (!string.Equals(_standardHashFingerprint, fingerprint, StringComparison.Ordinal))
        {
            _standardHashes.Clear();
            _standardHashFingerprint = fingerprint;
        }
        if (_standardHashes.TryGetValue(element, out var hash))
            return hash;

        hash = AlgorithmPolicyValidator.Validate(AlgorithmSourceCatalog.Build(element, _settings)).ContentHash;
        _standardHashes[element] = hash;
        return hash;
    }

    private static ElementSnapshot ResolveEffective(
        string element,
        ElementSnapshot standard,
        IReadOnlyDictionary<string, AlgorithmExecutionResult> custom,
        AlgorithmRuntimeRegistrySnapshot runtime)
        => custom.TryGetValue(element, out var result) && IsActive(runtime, element, result.SourceHash)
            ? result.Snapshot
            : standard;

    private static bool EffectiveTripRequested(
        string element,
        ElementSnapshot standard,
        IReadOnlyDictionary<string, AlgorithmExecutionResult> custom,
        AlgorithmRuntimeRegistrySnapshot runtime,
        SmvTrustState trust)
        => custom.TryGetValue(element, out var result) && IsActive(runtime, element, result.SourceHash)
            ? result.TripRequested
            : StandardTripRequested(standard, trust);

    private static bool StandardTripRequested(ElementSnapshot snapshot, SmvTrustState trust)
        => snapshot.Operated && trust.AllowsTrip && snapshot.State != ProtectionStageState.Blocked;

    private static ElementSnapshot StandardElement(string element, ProtectionSnapshot snapshot) => element switch
    {
        "50P-1" => snapshot.Phase50,
        "51P" => snapshot.Phase51,
        "50N" => snapshot.Earth50,
        "51N" => snapshot.Earth51,
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported dual-mode element.")
    };

    private static bool IsActive(AlgorithmRuntimeRegistrySnapshot runtime, string element, string? hash = null)
        => runtime.Active.Any(identity =>
            string.Equals(identity.Element, element, StringComparison.OrdinalIgnoreCase) &&
            (hash is null || string.Equals(identity.SourceHash, hash, StringComparison.OrdinalIgnoreCase)));

    private static string ActiveSignature(AlgorithmRuntimeRegistrySnapshot runtime)
        => string.Join('|', runtime.Active
            .OrderBy(identity => identity.Element, StringComparer.Ordinal)
            .Select(identity => $"{identity.Element}:{identity.SourceHash}"));

    private void UpdatePickupEvidence(DateTimeOffset timestamp, IReadOnlyList<ElementSnapshot> elements)
    {
        foreach (var element in elements)
        {
            var code = ElementCode(element.Element);
            if (element.Pickup || element.Operated)
            {
                if (!_pickupEvidence.ContainsKey(code))
                    _pickupEvidence[code] = new PickupEvidence(timestamp, element.OperatingQuantity);
            }
            else
            {
                _pickupEvidence.Remove(code);
            }
        }
    }

    private ProtectionOperationEvidence CaptureOperationEvidence(
        string element,
        DateTimeOffset tripTimestamp,
        IReadOnlyList<ElementSnapshot> elements,
        MeasurementFrame frame)
    {
        var snapshot = elements.First(candidate => ElementCode(candidate.Element) == element);
        var pickup = _pickupEvidence.TryGetValue(element, out var evidence)
            ? evidence
            : new PickupEvidence(tripTimestamp, snapshot.OperatingQuantity);
        var earth = element is "50N" or "51N";
        var phase = !earth;
        var setting = snapshot.PickupSetting;
        return new ProtectionOperationEvidence(
            element,
            pickup.Timestamp,
            tripTimestamp,
            pickup.Quantity,
            snapshot.OperatingQuantity,
            earth ? "3I0" : "I OP",
            "A",
            phase && frame.PhaseA >= setting,
            phase && frame.PhaseB >= setting,
            phase && frame.PhaseC >= setting,
            earth);
    }

    private static string? ResolveFirstElement(IReadOnlyList<ElementSnapshot> elements, bool operated)
    {
        foreach (var element in elements)
        {
            if (operated ? element.Operated : element.Pickup)
                return ElementCode(element.Element);
        }
        return null;
    }

    private static string? FormatPickupElement(string? element) => element switch
    {
        null => null,
        "50P-1" or "50N" => $"{element} PICKUP",
        _ => $"{element} START"
    };

    private static string? NormalizeElement(string? element)
    {
        if (string.IsNullOrWhiteSpace(element) || string.Equals(element, "READY", StringComparison.OrdinalIgnoreCase))
            return null;
        return element.Replace(" PICKUP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" START", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool IsLegacyElement(string element)
        => element is "50P-1" or "51P" or "50N" or "51N";

    private static string ElementCode(ProtectionElementId element) => element switch
    {
        ProtectionElementId.PhaseInstantaneous50P => "50P-1",
        ProtectionElementId.PhaseTime51P => "51P",
        ProtectionElementId.EarthInstantaneous50N => "50N",
        ProtectionElementId.EarthTime51N => "51N",
        _ => "UNKNOWN"
    };

    private readonly record struct PickupEvidence(DateTimeOffset Timestamp, double Quantity);
}
