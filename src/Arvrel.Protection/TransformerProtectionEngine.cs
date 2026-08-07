using System.Numerics;

namespace Arvrel.Protection;

/// <summary>
/// Deterministic two-winding transformer protection core. It deliberately consumes
/// a paired HV/LV measurement frame instead of reusing the single-stream feeder
/// MeasurementFrame. An acquisition layer must align both sides before evaluation.
/// </summary>
public sealed class TransformerProtectionEngine
{
    private static readonly TransformerPhase[] Phases =
    [
        TransformerPhase.A,
        TransformerPhase.B,
        TransformerPhase.C
    ];

    private TransformerProtectionSettings _settings;
    private readonly TimeSpan[] _differentialElapsed = new TimeSpan[3];
    private readonly TimeSpan[] _highSetElapsed = new TimeSpan[3];
    private readonly bool[] _differentialPickupLatch = new bool[3];
    private readonly bool[] _highSetPickupLatch = new bool[3];
    private TimeSpan _refHighVoltageElapsed;
    private TimeSpan _refLowVoltageElapsed;
    private bool _refHighVoltagePickupLatch;
    private bool _refLowVoltagePickupLatch;
    private DateTimeOffset? _previousTimestamp;
    private bool _tripLatched;
    private string? _latchedElement;

    public TransformerProtectionEngine(TransformerProtectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public TransformerProtectionSettings Settings => _settings;

    public void UpdateSettings(TransformerProtectionSettings settings, bool keepTripLatch = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
        ResetDynamicState(keepTripLatch);
        _previousTimestamp = null;
    }

    public TransformerProtectionSnapshot Evaluate(TransformerMeasurementFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        frame.Validate();
        var delta = ResolveDelta(frame.Timestamp);

        var highVoltage = _settings.Differential87T.HighVoltageCompensation
            .NormalizeDifferential(frame.HighVoltage.FundamentalCurrentA);
        var lowVoltage = _settings.Differential87T.LowVoltageCompensation
            .NormalizeDifferential(frame.LowVoltage.FundamentalCurrentA);

        var differentialMeasurementAvailable =
            frame.HighVoltage.Trust.AllowsMeasurement &&
            frame.LowVoltage.Trust.AllowsMeasurement;
        var differentialPickupPermitted =
            differentialMeasurementAvailable &&
            frame.HighVoltage.Trust.AllowsPickup &&
            frame.LowVoltage.Trust.AllowsPickup;

        var phaseSnapshots = new TransformerDifferentialPhaseSnapshot[3];
        for (var index = 0; index < Phases.Length; index++)
        {
            var phase = Phases[index];
            phaseSnapshots[index] = EvaluateDifferentialPhase(
                index,
                phase,
                highVoltage.For(phase),
                lowVoltage.For(phase),
                Math.Max(frame.HighVoltage.SecondHarmonicRatio.For(phase), frame.LowVoltage.SecondHarmonicRatio.For(phase)),
                Math.Max(frame.HighVoltage.FifthHarmonicRatio.For(phase), frame.LowVoltage.FifthHarmonicRatio.For(phase)),
                differentialPickupPermitted,
                differentialMeasurementAvailable,
                delta);
        }

        var differential = BuildDifferentialSnapshot(phaseSnapshots, differentialMeasurementAvailable);
        var refHighVoltage = EvaluateRef(
            "87N-HV",
            frame.HighVoltage,
            _settings.Differential87T.HighVoltageCompensation,
            _settings.RefHighVoltage,
            delta,
            ref _refHighVoltageElapsed,
            ref _refHighVoltagePickupLatch);
        var refLowVoltage = EvaluateRef(
            "87N-LV",
            frame.LowVoltage,
            _settings.Differential87T.LowVoltageCompensation,
            _settings.RefLowVoltage,
            delta,
            ref _refLowVoltageElapsed,
            ref _refLowVoltagePickupLatch);

        var differentialOperated = differential.Restrained87T.Operated || differential.HighSet87T.Operated;
        var differentialTripAllowed = frame.HighVoltage.Trust.AllowsTrip && frame.LowVoltage.Trust.AllowsTrip;
        var refHighVoltageTripAllowed = frame.HighVoltage.Trust.AllowsTrip;
        var refLowVoltageTripAllowed = frame.LowVoltage.Trust.AllowsTrip;

        var tripRequested =
            (differentialOperated && differentialTripAllowed) ||
            (refHighVoltage.Element.Operated && refHighVoltageTripAllowed) ||
            (refLowVoltage.Element.Operated && refLowVoltageTripAllowed);

        var blocked =
            (_settings.Differential87T.AnyEnabled && !differentialMeasurementAvailable) ||
            (_settings.RefHighVoltage.Enabled && refHighVoltage.Element.State == ProtectionStageState.Blocked) ||
            (_settings.RefLowVoltage.Enabled && refLowVoltage.Element.State == ProtectionStageState.Blocked) ||
            (differentialOperated && !differentialTripAllowed) ||
            (refHighVoltage.Element.Operated && !refHighVoltageTripAllowed) ||
            (refLowVoltage.Element.Operated && !refLowVoltageTripAllowed);

        var currentActiveElement = ResolveActiveElement(differential, refHighVoltage, refLowVoltage, blocked);
        if (tripRequested && !_tripLatched)
            _latchedElement = ResolveOperatedElement(differential, refHighVoltage, refLowVoltage) ?? currentActiveElement;
        if (tripRequested)
            _tripLatched = true;

        var activeElement = _tripLatched ? _latchedElement ?? currentActiveElement : currentActiveElement;
        var decisionReason = _tripLatched
            ? $"TRIP LATCHED · {activeElement}"
            : blocked
                ? $"PROTECTION BLOCKED · {activeElement}"
                : activeElement == "READY"
                    ? "Transformer measurements stable · no pickup"
                    : $"OPERATING · {activeElement}";

        return new TransformerProtectionSnapshot(
            frame.Timestamp,
            differential,
            refHighVoltage,
            refLowVoltage,
            tripRequested,
            _tripLatched,
            blocked,
            activeElement,
            decisionReason);
    }

    public void Reset()
    {
        ResetDynamicState(keepTripLatch: false);
        _previousTimestamp = null;
    }

    private TransformerDifferentialPhaseSnapshot EvaluateDifferentialPhase(
        int index,
        TransformerPhase phase,
        Complex highVoltage,
        Complex lowVoltage,
        double secondHarmonicRatio,
        double fifthHarmonicRatio,
        bool pickupPermitted,
        bool measurementAvailable,
        TimeSpan delta)
    {
        var settings = _settings.Differential87T;
        var operating = (highVoltage + lowVoltage).Magnitude;
        var restraint = (highVoltage.Magnitude + lowVoltage.Magnitude) / 2;
        var baseThreshold = DifferentialThreshold(settings, restraint);
        var harmonicBlocked = settings.HarmonicSecurityMode == TransformerHarmonicSecurityMode.Blocking &&
                              (secondHarmonicRatio >= settings.SecondHarmonicThreshold ||
                               fifthHarmonicRatio >= settings.FifthHarmonicThreshold);
        var harmonicRestraint = settings.HarmonicSecurityMode == TransformerHarmonicSecurityMode.Restraint
            ? settings.SecondHarmonicRestraintGain * secondHarmonicRatio +
              settings.FifthHarmonicRestraintGain * fifthHarmonicRatio
            : 0;
        var threshold = baseThreshold + harmonicRestraint;

        var restrainedEligible = settings.Enabled && measurementAvailable && pickupPermitted && !harmonicBlocked;
        var restrainedPickup = ResolvePickup(
            _differentialPickupLatch[index],
            operating,
            threshold,
            settings.DropoutRatio,
            restrainedEligible);
        _differentialPickupLatch[index] = restrainedPickup;
        _differentialElapsed[index] = Accumulate(_differentialElapsed[index], restrainedPickup, delta);
        var restrainedOperated = settings.Enabled && restrainedPickup && _differentialElapsed[index] >= settings.OperateDelay;

        var highSetHarmonicBlocked = harmonicBlocked && !settings.HighSetBypassesHarmonicSecurity;
        var highSetEligible = settings.HighSetEnabled && measurementAvailable && pickupPermitted && !highSetHarmonicBlocked;
        var highSetPickup = ResolvePickup(
            _highSetPickupLatch[index],
            operating,
            settings.HighSetPickupPu,
            settings.HighSetDropoutRatio,
            highSetEligible);
        _highSetPickupLatch[index] = highSetPickup;
        _highSetElapsed[index] = Accumulate(_highSetElapsed[index], highSetPickup, delta);
        var highSetOperated = settings.HighSetEnabled && highSetPickup && _highSetElapsed[index] >= settings.HighSetDelay;

        var reason = !measurementAvailable
            ? "Paired HV/LV measurement unavailable."
            : harmonicBlocked && !highSetOperated
                ? $"Harmonic security active · H2 {secondHarmonicRatio:P1} · H5 {fifthHarmonicRatio:P1}."
                : highSetOperated
                    ? $"Unrestrained high-set operate · Idiff {operating:0.000} pu."
                    : restrainedOperated
                        ? $"Biased differential operate · Idiff {operating:0.000} pu > {threshold:0.000} pu."
                        : restrainedPickup || highSetPickup
                            ? $"Differential pickup · Idiff {operating:0.000} pu."
                            : $"Restrained · Idiff {operating:0.000} pu · Ibias {restraint:0.000} pu.";

        return new TransformerDifferentialPhaseSnapshot(
            phase,
            operating,
            restraint,
            threshold,
            secondHarmonicRatio,
            fifthHarmonicRatio,
            harmonicBlocked,
            restrainedPickup,
            restrainedOperated,
            highSetPickup,
            highSetOperated,
            reason);
    }

    private TransformerDifferentialSnapshot BuildDifferentialSnapshot(
        IReadOnlyList<TransformerDifferentialPhaseSnapshot> phases,
        bool measurementAvailable)
    {
        var settings = _settings.Differential87T;
        var restrainedPickup = phases.Any(phase => phase.RestrainedPickup);
        var restrainedOperated = phases.Any(phase => phase.RestrainedOperated);
        var highSetPickup = phases.Any(phase => phase.HighSetPickup);
        var highSetOperated = phases.Any(phase => phase.HighSetOperated);
        var harmonicBlocked = phases.Any(phase => phase.HarmonicBlocked && phase.OperatingCurrentPu >= phase.ThresholdPu);
        var maximumOperating = phases.Max(phase => phase.OperatingCurrentPu);
        var maximumThreshold = phases.Max(phase => phase.ThresholdPu);

        var restrained = BuildElement(
            "87T",
            settings.Enabled,
            restrainedPickup,
            restrainedOperated,
            !measurementAvailable || harmonicBlocked,
            MaximumProgress(_differentialElapsed, settings.OperateDelay),
            maximumOperating,
            maximumThreshold,
            restrainedOperated
                ? "Biased transformer differential operated."
                : harmonicBlocked
                    ? "Restrained by configured harmonic security."
                    : !measurementAvailable
                        ? "Paired HV/LV measurement unavailable."
                        : "Biased transformer differential ready.");

        var highSet = BuildElement(
            "87T-HS",
            settings.HighSetEnabled,
            highSetPickup,
            highSetOperated,
            !measurementAvailable,
            MaximumProgress(_highSetElapsed, settings.HighSetDelay),
            maximumOperating,
            settings.HighSetPickupPu,
            highSetOperated
                ? "Unrestrained transformer differential high-set operated."
                : !measurementAvailable
                    ? "Paired HV/LV measurement unavailable."
                    : "Unrestrained transformer differential high-set ready.");

        return new TransformerDifferentialSnapshot(restrained, highSet, phases);
    }

    private static RestrictedEarthFaultSnapshot EvaluateRef(
        string elementCode,
        TransformerWindingMeasurement measurement,
        TransformerWindingCompensation compensation,
        RestrictedEarthFaultSettings settings,
        TimeSpan delta,
        ref TimeSpan elapsed,
        ref bool pickupLatch)
    {
        if (!settings.Enabled)
        {
            elapsed = TimeSpan.Zero;
            pickupLatch = false;
            return new RestrictedEarthFaultSnapshot(
                BuildElement(elementCode, false, false, false, false, 0, 0, settings.MinimumPickupPu, "Disabled."),
                0,
                0,
                0,
                0,
                settings.MinimumPickupPu,
                measurement.NeutralCurrentAvailable);
        }

        if (!measurement.Trust.AllowsMeasurement || !measurement.NeutralCurrentAvailable)
        {
            elapsed = TimeSpan.Zero;
            pickupLatch = false;
            var reason = !measurement.NeutralCurrentAvailable
                ? "Independent neutral CT current is required for REF."
                : "Measurement blocked by source trust policy.";
            return new RestrictedEarthFaultSnapshot(
                BuildElement(elementCode, true, false, false, true, 0, 0, settings.MinimumPickupPu, reason),
                0,
                0,
                0,
                0,
                settings.MinimumPickupPu,
                measurement.NeutralCurrentAvailable);
        }

        var local = compensation.NormalizeLocal(measurement.FundamentalCurrentA);
        var residual = local.Residual;
        var neutral = compensation.NormalizeNeutral(measurement.NeutralCurrentA) * settings.NeutralCurrentScale;
        if (settings.ReverseNeutralPolarity)
            neutral = -neutral;

        var operating = (residual + neutral).Magnitude;
        var restraint = (residual.Magnitude + neutral.Magnitude) / 2;
        var threshold = settings.MinimumPickupPu + settings.BiasSlope * restraint;
        var eligible = measurement.Trust.AllowsPickup;
        var pickup = ResolvePickup(pickupLatch, operating, threshold, settings.DropoutRatio, eligible);
        pickupLatch = pickup;
        elapsed = Accumulate(elapsed, pickup, delta);
        var operated = pickup && elapsed >= settings.OperateDelay;
        var reasonText = operated
            ? $"REF operate · Iop {operating:0.000} pu > {threshold:0.000} pu."
            : pickup
                ? $"REF pickup · Iop {operating:0.000} pu."
                : $"REF restrained · Iop {operating:0.000} pu · Ibias {restraint:0.000} pu.";

        return new RestrictedEarthFaultSnapshot(
            BuildElement(
                elementCode,
                true,
                pickup,
                operated,
                false,
                Progress(elapsed, settings.OperateDelay),
                operating,
                threshold,
                reasonText),
            residual.Magnitude,
            neutral.Magnitude,
            operating,
            restraint,
            threshold,
            true);
    }

    private static TransformerElementSnapshot BuildElement(
        string code,
        bool enabled,
        bool pickup,
        bool operated,
        bool blocked,
        double progress,
        double operating,
        double threshold,
        string reason)
    {
        var state = !enabled
            ? ProtectionStageState.Disabled
            : blocked
                ? ProtectionStageState.Blocked
                : operated
                    ? ProtectionStageState.Operated
                    : pickup
                        ? ProtectionStageState.Timing
                        : ProtectionStageState.Ready;
        return new TransformerElementSnapshot(
            code,
            state,
            pickup,
            operated,
            Math.Clamp(progress, 0, 1),
            operating,
            threshold,
            reason);
    }

    private static bool ResolvePickup(
        bool previousPickup,
        double operating,
        double threshold,
        double dropoutRatio,
        bool eligible)
    {
        if (!eligible)
            return false;
        return previousPickup
            ? operating >= threshold * dropoutRatio
            : operating >= threshold;
    }

    private static double DifferentialThreshold(TransformerDifferentialSettings settings, double restraint)
    {
        var firstRegion = Math.Min(restraint, settings.SlopeBreakpointPu);
        var secondRegion = Math.Max(0, restraint - settings.SlopeBreakpointPu);
        return settings.MinimumPickupPu + settings.Slope1 * firstRegion + settings.Slope2 * secondRegion;
    }

    private static TimeSpan Accumulate(TimeSpan elapsed, bool pickup, TimeSpan delta)
        => pickup ? elapsed + delta : TimeSpan.Zero;

    private static double MaximumProgress(IReadOnlyList<TimeSpan> elapsed, TimeSpan delay)
        => elapsed.Count == 0 ? 0 : elapsed.Max(value => Progress(value, delay));

    private static double Progress(TimeSpan elapsed, TimeSpan delay)
        => delay <= TimeSpan.Zero ? (elapsed >= delay ? 1 : 0) : elapsed.TotalSeconds / delay.TotalSeconds;

    private static string ResolveActiveElement(
        TransformerDifferentialSnapshot differential,
        RestrictedEarthFaultSnapshot refHighVoltage,
        RestrictedEarthFaultSnapshot refLowVoltage,
        bool blocked)
    {
        if (differential.HighSet87T.Operated) return differential.HighSet87T.Element;
        if (differential.Restrained87T.Operated) return differential.Restrained87T.Element;
        if (refHighVoltage.Element.Operated) return refHighVoltage.Element.Element;
        if (refLowVoltage.Element.Operated) return refLowVoltage.Element.Element;
        if (differential.HighSet87T.Pickup) return $"{differential.HighSet87T.Element} PICKUP";
        if (differential.Restrained87T.Pickup) return $"{differential.Restrained87T.Element} PICKUP";
        if (refHighVoltage.Element.Pickup) return $"{refHighVoltage.Element.Element} PICKUP";
        if (refLowVoltage.Element.Pickup) return $"{refLowVoltage.Element.Element} PICKUP";
        if (blocked)
        {
            if (refHighVoltage.Element.State == ProtectionStageState.Blocked) return "87N-HV BLOCKED";
            if (refLowVoltage.Element.State == ProtectionStageState.Blocked) return "87N-LV BLOCKED";
            if (differential.Restrained87T.State == ProtectionStageState.Blocked) return "87T BLOCKED";
        }
        return "READY";
    }

    private static string? ResolveOperatedElement(
        TransformerDifferentialSnapshot differential,
        RestrictedEarthFaultSnapshot refHighVoltage,
        RestrictedEarthFaultSnapshot refLowVoltage)
    {
        if (differential.HighSet87T.Operated) return differential.HighSet87T.Element;
        if (differential.Restrained87T.Operated) return differential.Restrained87T.Element;
        if (refHighVoltage.Element.Operated) return refHighVoltage.Element.Element;
        if (refLowVoltage.Element.Operated) return refLowVoltage.Element.Element;
        return null;
    }

    private TimeSpan ResolveDelta(DateTimeOffset timestamp)
    {
        var delta = _previousTimestamp is null ? TimeSpan.Zero : timestamp - _previousTimestamp.Value;
        _previousTimestamp = timestamp;
        return delta < TimeSpan.Zero || delta > TimeSpan.FromSeconds(1) ? TimeSpan.Zero : delta;
    }

    private void ResetDynamicState(bool keepTripLatch)
    {
        Array.Fill(_differentialElapsed, TimeSpan.Zero);
        Array.Fill(_highSetElapsed, TimeSpan.Zero);
        Array.Fill(_differentialPickupLatch, false);
        Array.Fill(_highSetPickupLatch, false);
        _refHighVoltageElapsed = TimeSpan.Zero;
        _refLowVoltageElapsed = TimeSpan.Zero;
        _refHighVoltagePickupLatch = false;
        _refLowVoltagePickupLatch = false;
        if (!keepTripLatch)
        {
            _tripLatched = false;
            _latchedElement = null;
        }
    }
}
