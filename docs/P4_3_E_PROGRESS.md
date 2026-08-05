# P4.3-E — Relay LED visual unification

Updated: 2026-08-05

## Objective

Make every relay indicator use the same physical LED language as the TRIP indicator while retaining each indicator's engineering color and state meaning.

## Visual contract

- [x] HEALTHY, PICKUP, TRIP, PHASE A/B/C, EARTH FAULT, and SMV BLOCK use one circular lens presentation
- [x] Relay-faceplate indicators use one deterministic 12×12 px diameter
- [x] Common edge thickness, radial opacity mask, and state-dependent glow
- [x] HEALTHY remains green while healthy
- [x] PICKUP, EARTH FAULT, and SMV BLOCK retain amber warning color
- [x] TRIP retains red trip color
- [x] PHASE A/B/C retain blue phase indication
- [x] OFF indicators retain the same lens and bezel without a bright active glow
- [x] Top application-health indicator uses a compact 8×8 variant of the same lens/glow behavior

## Rendering contract

- [x] Frozen brushes and effects are allocated once
- [x] No per-frame brush or DropShadowEffect allocation
- [x] Presentation reacts only when an indicator Fill value changes
- [x] Reference checks prevent redundant Stroke and Effect assignment
- [x] Fill-change observers are removed when the Main Window closes
- [x] Existing protection and SMV state logic remains the sole authority for LED color/state

## Behavior boundary

- [x] No changes to protection algorithms
- [x] No changes to pickup, trip, latch, reset, or block semantics
- [x] No changes to virtual injection, waveform, DFT, phasor, process bus, trust, or evidence
- [x] No changes to P4.3-A/B/C/D behavior

## Automated validation

- [ ] Restore
- [ ] Windows application build
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] Virtual-injection tests
- [ ] Relay LED source coverage test
- [ ] NuGet vulnerability audit
- [ ] Static-site validation
- [ ] Changed-file scope review
- [ ] Ready-for-review and squash merge

## Manual Windows QA

- [ ] HEALTHY appears as a green illuminated LED with the same circular lens as TRIP
- [ ] PICKUP, TRIP, PHASE A/B/C, EARTH FAULT, and SMV BLOCK share identical diameter and bezel geometry
- [ ] Active glow follows each indicator color
- [ ] OFF indicators remain clearly visible without appearing illuminated
- [ ] LED transitions do not flicker
- [ ] Glow does not clip inside the relay indicator panel
- [ ] Top health dot remains compact and aligned
- [ ] Layout remains correct at 1520×900 and minimum window size
