# P8 Transformer Differential IED

## Objective

Introduce a vendor-neutral two-winding transformer protection IED domain model without pretending that ARVREL's existing single 4I+4V feeder measurement frame is sufficient for transformer differential protection.

This phase implements the protection core and its deterministic regression surface. Live/replay process-bus pairing and practitioner UI wiring are intentionally separate integration work because both transformer sides must be time-aligned and trusted before 87T is allowed to operate.

## Implemented protection functions

| Function | ARVREL code | Status | Operating quantities |
| --- | --- | --- | --- |
| Biased transformer differential | 87T | Implemented | Paired compensated HV/LV phase currents |
| Unrestrained differential high-set | 87T-HS | Implemented | Differential phase current |
| Restricted earth fault, HV winding | 87N-HV / REF HV | Implemented | HV phase residual + independent HV neutral CT |
| Restricted earth fault, LV winding | 87N-LV / REF LV | Implemented | LV phase residual + independent LV neutral CT |

The capability profile also reserves explicit extension points for 24, 49, winding 50/51 backup, 50BF, 63 mechanical protection inputs and 86 lockout logic. They are marked **Planned**, not implemented.

## Why the transformer engine is separate

The existing feeder pipeline evaluates one `MeasurementFrame` carrying one set of phase currents, residual current and optional 4I+4V phasors. A correct transformer differential element needs at least two current domains that represent opposite transformer boundaries.

P8 therefore introduces:

```text
TransformerMeasurementFrame
├─ HighVoltage : TransformerWindingMeasurement
└─ LowVoltage  : TransformerWindingMeasurement

paired / aligned frame
→ explicit current compensation
→ 87T biased differential + 87T-HS
→ winding-local REF HV / REF LV
→ trust permission
→ virtual trip latch
```

This prevents a dangerous shortcut where two sides are inferred from one SV stream.

## Current reference convention

For 87T, the engine uses:

```text
Idiff = | IHV + ILV |
Ibias = 0.5 × ( |IHV| + |ILV| )
```

The acquisition/engineering layer must therefore align current polarity, phase reference and current base before operation. `TransformerWindingCompensation` exposes those transforms explicitly:

- `CurrentScaleToPu` converts source amperes to a common per-unit current base;
- `PhaseShiftDegrees` aligns the winding phase reference;
- `ReversePolarity` handles CT / protected-zone reference direction;
- `RemoveZeroSequence` can remove zero-sequence current from the transformer differential path.

ARVREL does **not** yet infer these transforms automatically from a transformer vector-group name. That mapping is a separate adapter concern and should be validated with deterministic vector-group cases before becoming automatic.

REF deliberately uses the local winding current path without 87T zero-sequence removal.

## 87T percentage-restraint characteristic

The restrained element uses a continuous dual-slope characteristic:

```text
for Ibias <= breakpoint:
  Ithreshold = Imin + slope1 × Ibias

for Ibias > breakpoint:
  Ithreshold = Imin
             + slope1 × breakpoint
             + slope2 × (Ibias - breakpoint)
```

Operation is phase-segregated. The settings include pickup hysteresis and definite operate delay.

The default setting object is safe: all transformer trip functions are disabled until explicitly enabled.

## Inrush and overexcitation security

The 87T model accepts per-phase harmonic ratios from the future paired measurement adapter and supports three policies:

- **Disabled** — no harmonic security;
- **Blocking** — configured second- or fifth-harmonic threshold blocks the restrained 87T stage;
- **Restraint** — harmonic ratios increase the 87T operate threshold using configurable gains.

Second harmonic is intended for magnetizing-inrush security. Fifth harmonic provides an overexcitation-oriented security input. The high-set stage can be configured to bypass harmonic security.

Harmonic estimation itself is intentionally outside the operate characteristic so it can later be backed by a validated waveform estimator rather than hidden inside protection logic.

## REF HV and REF LV

Each winding has an independent low-impedance biased REF element.

Conceptually:

```text
Iresidual = IA + IB + IC
Ineutral  = independent neutral CT current
Iop       = | Iresidual + Ineutral |
Ibias     = 0.5 × ( |Iresidual| + |Ineutral| )
Ithreshold = Imin + slope × Ibias
```

The sign convention is configurable through neutral polarity. An independent neutral CT channel is mandatory. If the neutral current is unavailable, the corresponding REF element enters `Blocked` and cannot request a virtual trip. ARVREL does not silently substitute the calculated phase residual for the missing neutral CT because that would destroy the REF comparison principle.

## Trust behavior

The two winding measurements retain independent `SmvTrustState` values.

- 87T / 87T-HS require measurement and pickup permission from both sides.
- 87T / 87T-HS require trip permission from both sides.
- REF HV follows HV-side trust.
- REF LV follows LV-side trust.
- An operated element with trip permission removed is reported blocked and does not issue `TripRequested`.
- The transformer engine retains a virtual trip latch until reset.

All outputs remain virtual. P8 introduces no physical contact, GOOSE trip, MMS control or breaker actuation path.

## IEC 61850 modeling direction

The IED capability profile identifies `PDIF` as the differential-protection logical-node family for future IEC 61850 model integration. P8 does not claim to provide a complete IEC 61850 server model or SCL template for the transformer IED.

## Deterministic validation included

The regression tests cover:

1. safe defaults with every transformer trip function disabled;
2. secure compensated through-current / external-fault behavior;
3. 87T operation for an internal phase fault;
4. second-harmonic inrush blocking;
5. fifth-harmonic overexcitation blocking;
6. harmonic-restraint threshold increase;
7. 87T-HS operation with configurable harmonic bypass;
8. REF HV operation for an internal ground fault;
9. REF HV restraint for external through-current;
10. independent REF LV operation;
11. secure REF blocking when the neutral CT is missing;
12. SMV trust preventing virtual trip while pickup/operate evidence remains visible;
13. transformer-setting fingerprint changes;
14. invalid dual-slope settings rejection;
15. capability-profile implementation status.

These are deterministic software regression cases. They are not IEC 60255 type tests, calibrated relay tests, transformer commissioning evidence or vendor-equivalence claims.

## Integration sequence after this PR

The next implementation layers should remain separate and reviewable:

1. **Paired process-bus adapter** — bind two SV current sources, enforce sample/frequency/time alignment, expose skew and trust diagnostics.
2. **Transformer engineering adapter** — nameplate, CT ratios, winding nominal current, vector-group compensation and polarity checks.
3. **Harmonic estimator** — produce validated H2/H5 ratios from paired current windows.
4. **Practitioner UI** — IED selector, transformer nameplate/CT settings, 87T characteristic view, REF HV/LV settings and measurements.
5. **Evidence export** — paired source identities, compensation fingerprint, per-phase Idiff/Ibias, harmonic security and REF quantities.
6. **CT saturation security** — couple the existing CT research model into operate/secure transformer scenarios without weakening trust gating.

This ordering keeps acquisition correctness separate from protection math and prevents UI convenience from defining the algorithm contract.

## Research basis

The design shape follows common modern transformer-relay practice rather than a vendor-specific clone:

- two-winding percentage differential with a dual-slope restrained characteristic;
- separate high-set differential stage;
- harmonic blocking/restraint for inrush and overexcitation security;
- two independent restricted-earth-fault functions for transformer winding zones;
- explicit measurement trust and CT/transformer compensation boundaries.

The implementation remains vendor-neutral and is intended for ARVREL laboratory and research workflows.
