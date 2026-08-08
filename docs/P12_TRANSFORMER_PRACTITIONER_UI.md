# P12 — Transformer IED Practitioner UI

P12 adds the operator-facing workspace for the two-winding transformer differential IED built across P8–P11.

The UI is intentionally a presentation and configuration layer. It does not own protection mathematics, Sampled Values pairing, harmonic estimation, or trip-state timing.

## Architecture

```text
MainWindow process-bus source
        |
        +--> Live Capture
        |        or
        +--> PCAP / PCAPNG Replay
                 |
                 v
       TransformerIedWindow
                 |
                 v
TransformerProcessBusProtectionRuntime   (P11)
        |
        +--> paired HV/LV SV              (P9)
        +--> measured H1/H2/H5            (P10)
        +--> CT/nameplate/vector group    (P9)
        +--> 87T / 87T-HS / REF           (P8)
                 |
                 v
 characteristic + measurements + trust + evidence
```

No physical trip, GOOSE output, MMS control, breaker command, or binary output is added by P12.

## Why a dedicated transformer workspace

The existing ARVREL main window is a single-stream feeder-style relay laboratory. Transformer differential protection has a fundamentally different practitioner workflow:

- two independent SV stream selections;
- transformer nameplate data;
- two sets of CT engineering;
- vector-group compensation;
- percentage-biased differential characteristic;
- harmonics used as security quantities;
- winding-specific REF elements.

P12 therefore opens a dedicated `TransformerIedWindow` instead of adding a large collection of transformer controls to the feeder dashboard.

The main dashboard remains the owner of the process-bus source. The transformer workspace consumes the same `SmvProcessBusController` instance, so it observes the same live capture or completed replay evidence.

## Process-bus input binding

The practitioner selects exact:

- HV SV stream;
- LV SV stream.

The configuration rejects the same stream being used on both sides.

For live operation, the main process-bus capture remains active while the transformer window is open. For replay, the workspace consumes the streams reconstructed by the existing PCAP/PCAPNG replay path.

P12 does not add a second packet decoder or capture implementation.

## Transformer engineering

The left engineering panel exposes:

- rated power in MVA;
- HV rated voltage in kV;
- LV rated voltage in kV;
- IEC vector-group text such as `Dyn11`;
- HV phase CT primary / secondary rating;
- LV phase CT primary / secondary rating;
- HV neutral CT primary / secondary rating;
- LV neutral CT primary / secondary rating;
- explicit HV/LV phase-polarity reversal.

The values are passed to `TransformerEngineeringAdapter` through P11 configuration.

The UI displays the resulting engineering plan, including:

- winding current bases;
- CT-secondary-to-pu scale;
- LV clock-angle compensation;
- automatic zero-sequence removal where applicable.

P12 does not duplicate vector-group or CT scaling calculations in the presentation layer.

## Standard percentage-biased characteristic

The settings panel uses familiar generic terminology:

- `Is1` — minimum differential pickup;
- `K1` — first bias slope;
- `Is2` — bias-current breakpoint;
- `K2` — second bias slope.

The plot uses the public P9/P8 model helper:

```text
TransformerDifferentialSettings.StandardSlopeThresholdPu(Ibias)
```

It does not implement another copy of the dual-slope algorithm.

The characteristic display shows:

- restraint region;
- operate region;
- Is2 breakpoint;
- live phase A/B/C operating points.

The live phase table exposes, per phase:

- Idiff;
- Ibias;
- active threshold;
- H2/H1;
- H5/H1;
- restrained / harmonic-block / pickup / operate state.

This is intended to answer the practitioner question **why did the relay restrain, pick up, block, or operate?** rather than only showing a final trip lamp.

## Harmonic security

P12 exposes the existing transformer-differential harmonic modes:

- Disabled;
- Blocking;
- Restraint.

It also exposes H2 and H5 threshold settings.

Measured harmonic evidence continues to come from P10 waveform estimation. P12 does not calculate H2 or H5 from displayed values.

## 87T-HS and REF

The workspace exposes:

- restrained 87T enable;
- high-set 87T enable and pickup;
- REF HV enable;
- REF LV enable.

The right and center evidence panels show element state and REF operating/restraint/threshold quantities.

The existing security boundary remains unchanged: REF requires evidence of an independent neutral CT channel. A calculated phase residual is not promoted to neutral-CT evidence by the UI.

## Pairing and trust presentation

The practitioner can see:

- pairing diagnostic code;
- HV/LV smpCnt;
- signed smpCnt skew;
- HV/LV smpSynch;
- applied phase correction;
- capture timestamp skew;
- full pairing diagnostic detail.

P11 remains the authority for whether an input pair can be evaluated.

The UI does not override `AllowsMeasurement`, `AllowsPickup`, or `AllowsTrip` trust decisions.

## Runtime decision and virtual trip latch

P12 presents P11 states:

- WaitingForPair;
- PairBlocked;
- ProtectionBlocked;
- Ready;
- Pickup;
- TripLatched.

The operator can explicitly reset the transformer runtime.

The UI does not clear a latched trip automatically when current returns to normal. P11 owns the frozen operated snapshot until reset.

## Evidence export

`Export transformer evidence JSON` serializes the object returned by:

```text
TransformerProcessBusProtectionRuntime.CaptureEvidence()
```

The evidence contains the effective settings fingerprint, engineering plan context, pairing diagnostics, pair identity, H1/H2/H5-enriched measurement, and transformer protection snapshot.

P12 does not recalculate evidence at export time.

## UI density and visual hierarchy

The practitioner workspace intentionally uses:

- compact technical fields;
- restrained typography;
- one dense three-column workspace;
- no large decorative cards;
- monospace engineering values where useful;
- state color only for ready, pickup/block, and trip significance.

The existing ARVREL application resource palette and control styles are reused.

## Validation boundary

P12 validation includes source-contract tests that ensure:

1. two-stream HV/LV selection remains explicit;
2. Is1/K1/Is2/K2 are presented using generic terminology;
3. the characteristic plot calls `StandardSlopeThresholdPu` instead of duplicating slope mathematics;
4. runtime execution uses `TransformerProcessBusProtectionRuntime`;
5. evidence export uses `CaptureEvidence()`;
6. H2/H5, REF, smpCnt/smpSynch, and engineering evidence remain visible to the practitioner;
7. no new `TransformerProtectionEngine` is constructed in UI code.

The WPF application build remains the final XAML/compiler gate.

## Intentional non-goals

P12 does not implement:

- CT-saturation classification or NoGap-style security;
- cross-phase harmonic blocking policy changes;
- additional transformer protection functions such as 24, 49, 50/51 or 63;
- IEC 60255 type-test automation;
- GOOSE trip publication;
- breaker control;
- vendor-equivalence claims.

Those changes should remain separate from practitioner UI work so presentation changes cannot silently alter the protection algorithm.
