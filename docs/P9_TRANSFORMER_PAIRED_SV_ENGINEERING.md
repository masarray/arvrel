# P9 Transformer Paired SV and Engineering Layer

## Objective

P9 connects the modular transformer-protection domain to a physically meaningful two-stream engineering model without yet exposing a practitioner UI or claiming live 87T operation.

The layer has two responsibilities:

1. align one HV and one LV IEC 61850 Sampled Values stream into a common electrical sample reference;
2. derive deterministic CT/current compensation from transformer nameplate, CT ratios and a standard two-winding vector group.

## Standard 87T characteristic vocabulary

ARVREL keeps a vendor-neutral continuous dual-slope percentage-biased characteristic while publishing the familiar transformer-differential aliases:

| ARVREL setting | Familiar notation | Meaning |
| --- | --- | --- |
| `MinimumPickupPu` | `Is1` | minimum differential pickup |
| `Slope1` | `K1` | low-bias percentage slope |
| `SlopeBreakpointPu` | `Is2` | bias-current breakpoint |
| `Slope2` | `K2` | high-bias percentage slope |

For bias current `Ibias`, the threshold is continuous:

```text
Ibias <= Is2:
  Ioperate = Is1 + K1 * Ibias

Ibias > Is2:
  Ioperate = Is1 + K1 * Is2 + K2 * (Ibias - Is2)
```

This is intentionally the generic two-slope shape familiar from transformer differential relays such as MiCOM P64x and SEL-787. ARVREL does not copy vendor-specific internal logic, CT-saturation detectors, adaptive restraint or proprietary security methods.

## Paired SV alignment

`TransformerProcessBusAdapter` always reads both selected runtimes in the **secondary-current domain**. CT conversion is not performed by the process-bus layer.

Alignment checks:

- HV and LV must be different SV streams;
- samples/cycle must match;
- nominal frequency must agree within a configurable tolerance;
- default policy requires `smpSynch = 2` on both sides;
- `smpCnt` separation must remain within a configurable sample limit;
- capture timestamp skew must remain below a guard limit so a repeated counter after wrap cannot be mistaken for a fresh pair.

A one-sample `smpCnt` offset is not silently ignored. The adapter converts the offset into an explicit phasor correction:

```text
phase correction = -counter skew * 360 / samples-per-cycle
```

The correction aligns the LV fundamental current phasor to the HV sample reference before a `TransformerMeasurementFrame` is emitted.

The resulting diagnostics expose:

- alignment state and code;
- signed sample-counter skew;
- applied phase correction;
- capture timestamp skew;
- both `smpSynch` values;
- samples/cycle and frequency.

## Synchronization policy

The default is deliberately conservative for transformer differential work:

```text
HV smpSynch = 2
AND
LV smpSynch = 2
```

A controlled laboratory can opt into matching non-zero local synchronization values, but this is not the default.

The adapter does not infer synchronized sampling from Ethernet arrival time alone. `smpCnt` and `smpSynch` are the primary alignment evidence; capture time is a stale/wrap guard and diagnostic.

## Current phasor estimation

The adapter uses the same one-cycle fundamental estimator already used by ARVREL:

```text
newest complete one-cycle current window
→ arithmetic mean removal
→ nominal-frequency single-bin DFT
→ complex RMS fundamental phasor
```

Voltage channels are not required for transformer-current pairing.

REF remains conservative: when the existing runtime cannot prove that the residual channel is an independent neutral CT, `NeutralCurrentAvailable` remains false. ARVREL never upgrades calculated `IA+IB+IC` into REF neutral-current evidence.

## Transformer engineering adapter

`TransformerEngineeringAdapter` accepts:

- transformer MVA;
- HV and LV rated line-line kV;
- two-winding IEC vector group;
- HV and LV phase CT ratios;
- optional independent neutral CT ratios;
- explicit phase/neutral polarity overrides.

Rated winding current is calculated as:

```text
Irated = S / (sqrt(3) * VLL)
```

A CT-secondary phasor is converted to the common winding per-unit current base by:

```text
CurrentScaleToPu = CT ratio / Irated
```

### Vector groups

The first automatic mapper supports conventional two-winding Y/D combinations and same-connection Y/Y or D/D groups using IEC clock notation, for example:

- `Dyn11` → LV compensation angle `-30°`;
- `YNd1` → LV compensation angle `+30°`;
- `Yyn0` → `0°`;
- `Dd0` → `0°`.

For a delta/wye boundary, zero-sequence current is removed from the non-delta side in the differential path. REF still uses the local uncompensated residual path.

Zig-zag groups are parsed but automatic compensation is intentionally rejected. They require an explicit engineering model rather than a guessed generic matrix.

## CT polarity convention

Automatic compensation assumes each phase CT reference is positive **into the protected transformer zone** on its own winding side. If imported SV polarity differs, the engineering input exposes an explicit reversal.

This keeps vector-group phase compensation separate from installation-specific CT polarity.

## Neutral CT scaling

REF neutral CTs may use a different ratio from the phase CTs. The engineering plan derives:

```text
NeutralCurrentScale = neutral CT ratio / phase CT ratio
```

and carries an independent neutral-polarity override into the REF setting.

## Intentional boundary

P9 does **not** yet connect the aligned pair directly to live 87T trip evaluation.

The next required layer is a validated H2/H5 waveform estimator. Running a harmonic-secured 87T from live SV while silently substituting zero harmonic content would be unsafe and misleading.

Therefore the integration order remains:

```text
paired SV alignment       [P9]
→ transformer/CT engineering [P9]
→ H2/H5 estimator
→ live/replay transformer protection runtime
→ practitioner UI / characteristic view
→ paired evidence export
→ CT-saturation security scenarios
```

## Validation

P9 regression tests cover:

- Is1/K1/Is2/K2 continuous dual-slope semantics;
- Dyn11 and YNd1 clock mapping;
- rated current and CT-to-pu scaling;
- delta/wye zero-sequence rule;
- Dyn11 through-current compensation stability;
- independent neutral-CT scaling;
- conservative zig-zag rejection;
- same-counter global SV alignment;
- one-sample smpCnt phasor correction;
- excess sample skew block;
- unsynchronized stream block;
- controlled local-sync laboratory mode;
- stale repeated-counter capture-skew block;
- current-only REF-neutral restraint;
- samples/cycle mismatch block.

## Safety and claims

- virtual-only output remains authoritative;
- no physical/GOOSE/MMS trip path is introduced;
- no IEC 60255 conformance, calibration or type-test claim is made;
- no claim is made that ARVREL reproduces MiCOM, SEL or any other vendor implementation;
- generic slope terminology is used only to make the research model familiar and auditable.
