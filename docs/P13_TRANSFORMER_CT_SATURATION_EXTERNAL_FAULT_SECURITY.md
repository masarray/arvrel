# P13 — Transformer CT Saturation & External-Fault Security

## Objective

P13 adds a vendor-neutral security layer for transformer differential protection during severe external/through faults when unequal CT saturation can create false differential current.

The implementation is deliberately split into two independent stages:

1. **waveform evidence** — measure how far each CT secondary waveform departs from a fitted fundamental sinusoid;
2. **protection context** — block only when that waveform evidence follows a restraint-leading external-fault sequence.

Waveform distortion alone never blocks transformer protection.

## Public engineering basis

Public transformer-differential literature consistently identifies through-fault CT saturation as a major security problem:

- Schneider / MiCOM P64x documentation describes through-fault stability and the need for increased restraint at higher bias current because CT errors and saturation grow during severe external faults.
- SEL's public *Transformer Differential Protection Revisited* paper shows an external-fault security concept in which restraint current rises before false differential current develops as a CT saturates.
- GE Vernova Multilin 845 documentation describes percent-differential restraint designed for through-fault stability under CT saturation and explicitly notes that the characteristic must account for AC and DC saturation.

P13 uses those public principles only. It does **not** reproduce proprietary vendor filters, state machines, thresholds, equations, timing constants, or internal product logic.

## Architecture

```text
HV SV waveform ----\
                    -> P9 pair / synchronization
LV SV waveform ----/
                           |
                           v
                  P10 H1 / H2 / H5
                           |
                           +----> P13 CT waveform evidence
                           |       - fundamental-fit residual
                           |       - residual distortion ratio
                           |       - positive/negative peak asymmetry
                           v
                TransformerMeasurementFrame
                           |
                           v
              P13 external-fault coordinator
                 restraint-leading sequence
                           +
                 CT distortion evidence
                           |
                           v
                P8 87T / 87T-HS / REF
```

P11 live/replay runtime does not need a parallel execution path. It already consumes the enriched paired frame from `TransformerHarmonicProcessBusAdapter.AlignAndEstimate(...)`, so live capture and PCAP replay automatically receive the same P13 evidence.

## Waveform estimator

`TransformerCtSaturationEstimator` operates on the latest integer-cycle waveform window.

For one phase it:

1. removes the mean value;
2. least-squares fits the fundamental using orthogonal sine/cosine components;
3. reconstructs the fundamental waveform;
4. computes the RMS residual;
5. reports:
   - fundamental RMS;
   - residual-distortion RMS;
   - residual/fundamental distortion ratio;
   - positive/negative peak asymmetry;
   - evidence reliability.

This is intentionally a measurable distortion indicator, not a claim that a waveform is physically proven to be CT saturation.

### Why two distortion indicators?

A strongly one-sided saturated waveform often has both high residual distortion and peak asymmetry.

A heavily but approximately symmetrically clipped waveform can have strong residual distortion while peak asymmetry remains small. P13 therefore allows a configurable `SevereDistortionRatioThreshold` that can qualify evidence without the asymmetry condition.

## External-fault arming

For each compensated transformer phase, P13 observes the standard 87T quantities already used by the relay:

```text
Idiff = |IHV + ILV|
Ibias = 0.5 * (|IHV| + |ILV|)
```

An external-fault candidate is armed only when all configured conditions are met:

```text
Ibias >= MinimumBiasPu
AND
ΔIbias >= MinimumBiasIncreasePu
AND
Idiff / Ibias <= MaximumInitialDifferentialToBiasRatio
```

This represents the generic physical sequence expected for a through fault before unequal CT saturation becomes dominant: large through current appears first while differential current is still comparatively small.

## Blocking rule

The restrained 87T path is blocked only when:

```text
external-fault candidate is armed
AND
CT saturation/distortion evidence becomes significant
```

Once asserted, the security block is held for `SecurityHold` so brief waveform recovery or alternating saturation does not chatter the protection decision.

The hold is independent for HV and LV CT evidence.

## CT evidence qualification

A winding/phase is considered saturation-suspect when its evidence is reliable and either:

```text
DistortionRatio >= SevereDistortionRatioThreshold
```

or:

```text
DistortionRatio >= DistortionRatioThreshold
AND
PeakAsymmetry >= PeakAsymmetryThreshold
```

The defaults are intentionally generic engineering starting points, not vendor-equivalent values:

```text
MinimumBiasPu                       2.00 pu
MinimumBiasIncreasePu               0.50 pu
MaximumInitialDifferentialToBias    0.20
ArmingDuration                      80 ms
SecurityHold                        120 ms
DistortionRatioThreshold            0.12
PeakAsymmetryThreshold              0.08
SevereDistortionRatioThreshold      0.25
SuperviseHighSet                    true
SuperviseRef                        true
```

`ExternalFaultSecurity.Enabled` remains **false by default** so P13 cannot silently change an existing settings group. Enabling the feature is an explicit engineering decision.

## Internal-fault security boundary

The most important P13 invariant is:

> CT waveform distortion without a restraint-leading external-fault sequence does not block 87T.

An internal fault normally creates differential current at the same time as the fault current rises. In that case `Idiff / Ibias` is not small enough to arm the external-fault supervisor, so even a distorted waveform remains available to the differential element.

This behavior is covered by deterministic regression tests.

## 87T high-set

P13 can supervise 87T-HS during an armed external fault with CT saturation evidence.

`SuperviseHighSet = true` is the default within the P13 settings object because severe unequal saturation can create a large false differential quantity.

This is separate from `HighSetBypassesHarmonicSecurity`. A high-set element may bypass harmonic inrush security while still being supervised by explicit external-fault CT-saturation security.

An engineer can explicitly set `SuperviseHighSet = false` when the application philosophy requires the unrestrained element to remain independent.

## REF supervision

When `SuperviseRef = true`, winding-specific CT saturation evidence can supervise the corresponding REF element:

- HV saturation evidence can block `87N-HV`;
- LV saturation evidence can block `87N-LV`.

The existing REF measurement boundary is unchanged. REF still requires an independent neutral CT; P13 does not promote phase residual into neutral-current evidence.

## Timer behavior

When external-fault CT saturation security blocks an element:

- restrained 87T pickup is removed;
- its definite-time accumulator resets;
- supervised 87T-HS pickup is removed;
- supervised REF pickup and timer reset.

No hidden pickup time is allowed to accumulate behind a security block.

## Evidence model

P13 adds evidence fields to runtime snapshots without changing existing constructor signatures.

Per differential phase, evidence includes:

- external-fault arm state;
- HV/LV saturation-suspect state;
- HV/LV active security hold;
- HV/LV distortion ratio;
- HV/LV peak asymmetry.

`TransformerProtectionSnapshot.ExternalFaultSecurity` provides the aggregate state and REF supervision flags for evidence export and later practitioner UI work.

## Validation coverage

P13 deterministic tests cover:

- pure fundamental waveform;
- one-sided clipping;
- symmetric clipping;
- amplitude invariance;
- latest-window behavior;
- low-current evidence reliability;
- non-finite waveform rejection;
- paired-SV evidence enrichment;
- restraint-leading through-fault arming;
- delayed CT saturation producing false Idiff;
- dynamic block and security hold;
- distorted internal fault remaining trippable;
- 87T-HS supervision and explicit bypass configuration;
- REF HV supervision from HV CT evidence;
- legacy behavior when P13 is disabled;
- invalid security-setting rejection.

## Intentional non-goals

P13 does not claim:

- physical CT flux reconstruction;
- CT knee-point or burden modeling;
- IEC 61869 CT model conformance;
- IEC 60255 type-test conformance;
- proprietary MiCOM `CTSat` / `NoGap` equivalence;
- SEL EFD equivalence;
- GE saturation-algorithm equivalence;
- adaptive frequency tracking beyond the existing coherent SV assumptions;
- cross-phase external-fault blocking;
- physical trip output.

## Recommended next layer

P14 should expose P13 evidence and settings in the practitioner workspace:

```text
External-fault security
├─ enable
├─ minimum Ibias
├─ ΔIbias arming
├─ maximum initial Idiff/Ibias
├─ distortion/asymmetry thresholds
├─ arming / hold timers
├─ supervise 87T-HS
└─ supervise REF

Live evidence
├─ EXT FAULT ARMED
├─ CT SAT HV / LV
├─ distortion ratio
├─ peak asymmetry
└─ SECURITY HOLD
```

UI work should remain presentation-only and must not duplicate the P13 state machine.
