# P14 — Transformer P13 Practitioner UI

## Objective

P14 exposes the P13 CT-saturation and external-fault security model in the existing transformer practitioner workspace without adding a second protection algorithm or a second runtime.

The implementation is presentation/configuration only:

```text
P9 paired SV + engineering
          ↓
P10 H1/H2/H5
          ↓
P13 CT waveform evidence
          ↓
P13 external-fault security coordinator
          ↓
P8 transformer protection engine
          ↓
P11 live/replay runtime
          ↓
P12 practitioner workspace
          ↓
P14 P13 settings + evidence presentation
```

P14 never calculates a new `Idiff`, `Ibias`, CT-saturation classifier, external-fault candidate, arm timer or security-hold timer.

## Safety boundary

The UI deliberately distinguishes three states that must not be conflated:

1. **CT distortion evidence** — waveform residual distortion / peak asymmetry has been measured.
2. **EXT FAULT ARMED** — P13 has observed the restraint-leading external-fault sequence.
3. **SECURITY HOLD ACTIVE** — the armed sequence was followed by qualifying CT distortion evidence and P13 is actively supervising protection.

The key operator message is explicit:

> Waveform distortion alone never blocks protection.

This preserves the P13 internal-fault security boundary. The UI displays the result of the coordinator; it does not infer a block from waveform quality.

## Configuration exposed

The left-side transformer configuration pane now receives a compact `External-fault / CT security` section before the existing Apply button.

Controls:

- Enable P13;
- minimum `Ibias` in pu;
- minimum `ΔIbias` in pu;
- maximum initial `Idiff / Ibias` ratio in percent;
- arm window in milliseconds;
- security hold in milliseconds;
- distortion ratio threshold in percent;
- peak asymmetry threshold in percent;
- severe distortion threshold in percent;
- supervise 87T-HS;
- supervise REF.

The displayed defaults match the P13 model:

```text
Enabled                         false
MinimumBiasPu                   2.00 pu
MinimumBiasIncreasePu           0.50 pu
MaximumInitial Idiff/Ibias      20%
ArmingDuration                  80 ms
SecurityHold                    120 ms
DistortionRatioThreshold        12%
PeakAsymmetryThreshold          8%
SevereDistortionRatioThreshold  25%
SuperviseHighSet                true
SuperviseRef                    true
```

## Apply-path safety

P12 originally binds `ApplyRuntimeButton` directly to `ApplyRuntime_Click`.

P14 removes that handler at runtime and replaces it with `ApplyRuntimeWithP14_Click`.

This is intentional. Adding a second click handler after the P12 handler would allow this sequence:

```text
operator presses Apply
        ↓
P12 applies configuration with P13 default disabled
        ↓
P12 evaluates current pair
        ↓
P14 applies P13 settings afterwards
```

That one evaluation would violate the intended configuration boundary.

P14 instead performs:

```text
BuildConfiguration()
        ↓
Read P13 practitioner settings
        ↓
Overlay ExternalFaultSecurity settings
        ↓
Validate complete runtime configuration
        ↓
Create/update TransformerProcessBusProtectionRuntime
        ↓
EvaluateCurrent()
```

Therefore the first evaluation after Apply already uses the operator-selected P13 policy.

## Evidence presentation

The existing right-side engineering/evidence pane receives a compact `EXTERNAL-FAULT / CT SECURITY` section.

It presents:

- `READY`;
- `CT DISTORTION · NO BLOCK`;
- `EXT FAULT ARMED`;
- `SECURITY HOLD ACTIVE`;
- CT SAT summary by winding and phase;
- reliable CT evidence phase count for HV and LV;
- applied arm rule;
- configured security hold duration and active/clear state;
- per-phase HV/LV distortion ratio;
- per-phase HV/LV peak asymmetry;
- per-phase SAT versus SAT/BLOCK distinction;
- authoritative P13 reason string.

### Example

```text
EXT FAULT ARMED
CT SAT HV — · LV —
Evidence reliable HV 3/3 · LV 3/3
Rule Ibias ≥ 2.00 · Δ ≥ 0.50 · Idiff/Ibias ≤ 20% · arm 80 ms
A · ARM · HV D 1.2% ASY 0.8% clear · LV D 1.1% ASY 0.7% clear
HOLD clear · 120 ms configured
```

After delayed LV CT saturation:

```text
SECURITY HOLD ACTIVE
CT SAT HV — · LV A
A · BLOCK · HV D 1.2% ASY 0.8% clear · LV D 18.4% ASY 11.2% SAT/BLOCK
HOLD ACTIVE · 120 ms configured
```

A distorted internal fault can instead show:

```text
CT DISTORTION · NO BLOCK
```

when the external-fault sequence is not armed. That distinction is intentionally visible.

## Runtime ownership

P14 reuses:

```text
TransformerProcessBusProtectionRuntime
```

and the existing P12 `_refreshTimer` cadence.

P14's timer callback only renders `_lastSnapshot`, which was already produced by the P11 runtime through the existing P12 evaluation path. It does not call a separate evaluator and cannot advance transformer protection timers by refreshing the UI.

## Snapshot sources

P14 reads:

```text
snapshot.Measurement.HighVoltage.CtSaturationEvidence
snapshot.Measurement.LowVoltage.CtSaturationEvidence
snapshot.Protection.ExternalFaultSecurity
snapshot.EffectiveSettings.Differential87T.ExternalFaultSecurity
```

These are the P13/P11 authority surfaces.

The UI does not contain a copy of `IsCtSaturationSuspected`, the `Ibias` rise detector, or the security hold state machine.

## Settings fingerprint and evidence export

Because P13 settings are already part of `TransformerDifferentialSettings.CanonicalIdentity()`, applying a P14 policy updates the P11 effective settings fingerprint automatically.

The existing P12 evidence export already serializes:

- effective settings;
- settings fingerprint;
- transformer measurement frame;
- transformer protection snapshot.

Therefore exported JSON automatically includes P13 CT evidence and external-fault security state without a new exporter.

## Validation

P14 adds source-contract regression tests that require:

- every intended P13 practitioner setting to be exposed;
- the original P12 Apply handler to be replaced rather than run first;
- P13 settings to be bound before runtime construction/evaluation;
- rendering to consume `snapshot.Protection.ExternalFaultSecurity`;
- rendering to consume `CtSaturationEvidence`;
- `EXT FAULT ARMED`, `CT DISTORTION · NO BLOCK`, `SECURITY HOLD ACTIVE` and CT SAT presentation paths;
- no `TransformerProtectionEngine` construction in P14;
- no copied CT-saturation classifier in P14.

The normal WPF `.NET CI` build remains the compiler/XAML/runtime-integration gate.

## Non-goals

P14 does not add:

- a new CT-saturation algorithm;
- proprietary MiCOM CTSat/NoGap behavior;
- SEL EFD equivalence;
- GE saturation-algorithm equivalence;
- physical CT flux or knee-point modeling;
- IEC 61869 or IEC 60255 conformance claims;
- physical trip, GOOSE, MMS control or breaker output;
- a second protection runtime;
- a second evidence exporter.
