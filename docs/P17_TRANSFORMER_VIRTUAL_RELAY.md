# P17 — Transformer Differential on the Shared OCR Virtual Relay

## Goal

P17 makes Transformer Differential a first-class virtual protection relay in the ARVREL main shell while reusing the **exact P6/OCR relay hardware component** already shipped by ARVREL.

This is not a second visual design and not a Transformer-specific hardware clone. The visual authority is:

- `Controls/VirtualRelay/VirtualRelayControl.xaml`
- `Controls/VirtualRelay/VirtualRelayTokens.xaml`
- `Controls/VirtualRelay/RelayLampControl.xaml`

The Transformer layer only changes presentation text, maps the existing lamps to 87T functions, and renders authoritative Transformer runtime evidence into the existing LCD host.

## Non-negotiable visual boundary

Transformer Differential instantiates `new VirtualRelayControl()` directly.

P17 must not create a separate Transformer relay chassis, operator rail, LCD bezel, button set, annunciator geometry, material palette, shadow model, lamp optics, or scaling policy. Those remain owned by P6/OCR.

Presentation-only substitutions are permitted, for example:

- `650` → `87T`
- `PROCESS BUS PROTECTION RELAY` → `TRANSFORMER DIFFERENTIAL RELAY`
- `PHASE A` → `87T`
- `PHASE B` → `87T-HS`
- `PHASE C` → `REF HV`
- `EARTH` → `REF LV`
- `SMV BLOCK` → `BLOCK`

`VirtualRelayControl.ApplyTextOverrides(...)` exists specifically so another virtual IED can reuse the same physical hardware without copying its XAML.

## Operator versus engineer boundary

### Shared relay hardware — operator surface

The existing OCR hardware provides unchanged:

- outer enclosure and front face,
- status/annunciation module,
- LCD bezel and screen,
- F1–F5 function keys,
- RESET key,
- Home/Menu navigation keys,
- Up/Down/Enter/Next/Cancel/Back controls,
- tactile button behavior,
- relay lamp optics,
- responsive hardware scaling.

Transformer presentation maps its authoritative runtime state to:

- HEALTHY,
- PICKUP,
- TRIP,
- 87T,
- 87T-HS,
- REF HV,
- REF LV,
- BLOCK.

### Practitioner workspace — engineering surface

F4 opens the existing `TransformerIedWindow` non-modally. It remains responsible for:

- HV/LV SV stream binding,
- nameplate, CT and vector-group engineering,
- Is1/K1/Is2/K2,
- 87T-HS,
- H2/H5 security,
- REF HV/LV,
- P13 CT-saturation / external-fault security,
- runtime evaluation,
- evidence export,
- P15 public self-test detail.

## Runtime authority

There is no second protection engine in the faceplate.

`TransformerIedWindow` owns `TransformerProcessBusProtectionRuntime`. P17 initializes the existing `InitializeP17FaceplateBridge()` and publishes already-evaluated `TransformerProtectionRuntimeSnapshot` objects to the shared hardware presenter.

The presenter does not calculate:

- standard slope thresholds,
- Idiff/Ibias protection decisions,
- H2/H5 blocking/restraint decisions,
- CT-saturation suspicion,
- P13 external-fault arming/hold,
- REF operate quantities,
- trip decisions.

RESET calls the existing runtime `Reset()` path.

## LCD pages

The shared OCR LCD host presents Transformer-specific pages:

1. Home
2. Measurements / Idiff-Ibias
3. Protection
4. Harmonics H2/H5
5. REF HV/LV
6. SV Pair / Trust
7. Settings
8. Events
9. Records / Self-test
10. Diagnostics

The faceplate settings page is read-only. F4 / Settings Enter opens engineering.

## Function keys

- **F1** — Measurements
- **F2** — Events
- **F3** — Records / deterministic self-test
- **F4** — Engineering practitioner workspace
- **F5** — reset existing Transformer runtime

On Records, Enter runs `TransformerPublicSelfTest.RunAll()`; no separate test algorithm is implemented in the presenter.

## Safety boundary

All outputs remain virtual evidence only:

- no physical trip,
- no GOOSE trip,
- no breaker output,
- no calibration / IEC type-test claim.

Live/Replay Transformer protection still requires two distinct HV/LV Sampled Values streams.

## Regression contracts

P17 tests must assert that:

- Transformer instantiates the shared `VirtualRelayControl`,
- the former improvised `TransformerVirtualRelayControl.cs` does not exist,
- P6/OCR XAML remains the hardware geometry source,
- Transformer only remaps labels/LCD/lamp state,
- runtime snapshots come from the existing P17 bridge,
- no `TransformerProtectionEngine` is created by the presenter,
- practitioner engineering remains singleton/non-modal,
- the public deterministic self-test is reused.
