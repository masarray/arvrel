# P17 — Transformer Differential Virtual Relay Front Panel

## Goal

P17 turns Transformer Differential into a first-class virtual protection relay in the ARVREL main shell. The existing P12–P15 practitioner workspace remains the detailed engineering surface; it is no longer the only visible representation of the IED.

The front panel deliberately follows ARVREL's existing OCR relay visual language rather than copying a proprietary vendor product.

## Operator versus engineer boundary

### Main shell — operator front panel

Selecting **Transformer Differential · 87T / REF** now shows a physical-style virtual relay with:

- ARVREL 87T / TR-87T identity,
- LCD navigation,
- Up / Down / Enter / Next / Cancel / Reset keys,
- F1–F5 shortcuts,
- HEALTHY, PICKUP, TRIP, 87T, 87T-HS, REF HV, REF LV and BLOCK annunciation,
- local event browsing,
- deterministic public self-test record,
- paired-SV trust and diagnostic pages.

### Practitioner workspace — engineering surface

F4 / ENGINEERING opens the existing TransformerIedWindow. It remains responsible for:

- HV/LV stream binding,
- nameplate, CT and vector-group engineering,
- 87T Is1/K1/Is2/K2 settings,
- H2/H5 security settings and evidence,
- REF HV/LV configuration,
- P13 CT-saturation / external-fault security,
- runtime evaluation,
- evidence export,
- P15 public self-test details.

P17 opens this window non-modally and reuses one window instance while it remains open so the operator faceplate stays visible.

## Runtime authority

P17 does not implement a second protection engine.

`TransformerIedWindow` continues to own `TransformerProcessBusProtectionRuntime`. The P17 bridge publishes the already-evaluated `TransformerProtectionRuntimeSnapshot` to the front panel. LED and LCD states are projections of snapshot fields only.

The bridge does not calculate:

- Idiff/Ibias thresholds,
- harmonic blocking/restraint,
- CT-saturation suspicion,
- external-fault arming/hold,
- REF operate quantities,
- trip decisions.

The virtual relay reset key calls the existing runtime `Reset()` path through the practitioner bridge.

## LCD pages

The front panel exposes:

1. Home
2. Measurements
3. Protection
4. Harmonics H2/H5
5. REF HV/LV
6. SV Pair / Trust
7. Settings
8. Events
9. Records / Self-test
10. Diagnostics

Settings are intentionally read-only on the faceplate in P17. F4 opens the practitioner surface for engineering changes.

## Function keys

- **F1** — Measurements
- **F2** — Events
- **F3** — Records / deterministic self-test
- **F4** — Engineering practitioner workspace
- **F5** — Reset transformer pickup timers / virtual trip latch through the existing runtime

On the Records page, Enter runs `TransformerPublicSelfTest.RunAll()` using the same protection core already used by P15.

## Annunciation mapping

- **HEALTHY** — authoritative runtime is Ready, Pickup or TripLatched rather than pair/protection blocked.
- **PICKUP** — any authoritative 87T, 87T-HS or REF element pickup.
- **TRIP** — transformer runtime trip latch.
- **87T** — restrained differential pickup/operation.
- **87T-HS** — high-set differential pickup/operation.
- **REF HV** — HV restricted-earth-fault pickup/operation.
- **REF LV** — LV restricted-earth-fault pickup/operation.
- **BLOCK** — PairBlocked, ProtectionBlocked or authoritative protection blocked state.

No LED state creates or changes a protection decision.

## Source and safety boundary

The front panel can exist in Internal Demo with no SV so a packaged binary can run the deterministic 10-scenario self-test. Live/Replay protection remains guarded by the existing practitioner configuration and requires two distinct HV/LV Sampled Values streams.

All outputs remain virtual evidence only:

- no physical trip,
- no GOOSE trip,
- no breaker output,
- no protection-grade / calibration / IEC type-test claim.

## Selector hardening

The unified selector continues to set `DisplayMemberPath = DisplayName` and P17 also overrides the choice model's `ToString()` to return `DisplayName`. This prevents a WPF theme/control-template fallback from displaying the raw record representation in the selection box.

## Verification

P17 source-contract tests assert:

- the transformer physical-style front panel is present,
- all required annunciation and operator keys exist,
- the faceplate consumes authoritative runtime snapshots,
- no `TransformerProtectionEngine` is created in the P17 UI,
- P15 self-test is reused rather than reimplemented,
- the practitioner window is singleton/non-modal while open,
- OCR/AVR unified-selector routes remain intact,
- virtual-only safety wording remains visible.
