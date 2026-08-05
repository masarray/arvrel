# P4.3-F — Relay LCD phasor stability and hardware depth

Updated: 2026-08-05

## Objective

Keep the small relay-LCD current phasor as stable as the coherent Main Window instrument and give the virtual relay a subtle raised hardware appearance without changing relay or process-bus semantics.

## Relay LCD phasor contract

- [x] Live/replay LCD phasor no longer consumes the raw 80 ms faceplate snapshot directly
- [x] LCD uses a dedicated coherent current-phasor frame accepted by the live display guard
- [x] Accepted LCD frame is independent from the Main Window Current/Voltage/Sequence selector
- [x] Temporary stale/gap states retain the last coherent phasor instead of rotating raw data
- [x] Source changes clear the retained LCD phasor
- [x] Small-display magnitude is canonicalized to 0.01 engineering unit
- [x] Small-display angle is canonicalized to 0.5°
- [x] Frequency is canonicalized to 0.01 Hz
- [x] WPF Frame is assigned only when the canonical presentation signature changes
- [x] Meaningful engineering changes remain visible immediately

## Hardware presentation contract

- [x] Relay body uses a subtle vertical housing gradient
- [x] Relay body is visually raised from the mounting panel with one static shadow
- [x] Indicator panel receives a shallow inset treatment
- [x] LCD bezel receives a deeper recessed edge treatment
- [x] Existing tactile keypad receives additional static depth shadow
- [x] RESET TRIP receives a slightly stronger raised treatment
- [x] No vendor branding or certification implication is introduced
- [x] No layout dimensions or command handlers are changed

## Performance and behavior boundary

- [x] All new brushes/effects are frozen and allocated once
- [x] No new periodic timer is introduced
- [x] No raw packet timing is presented as certified jitter
- [x] No changes to protection, pickup, trip, reset, latch, trust, process bus, waveform, DFT, or evidence semantics
- [x] P4.3-A/B/C/D/E behavior remains unchanged

## Automated validation

- [ ] Restore
- [ ] Windows application build
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] Virtual-injection tests
- [ ] Relay LCD phasor stabilizer tests
- [ ] NuGet vulnerability audit
- [ ] Static-site validation
- [ ] Changed-file scope review
- [ ] Ready-for-review and squash merge

## Manual Windows QA

- [ ] Live Npcap phasor stays fixed while focus changes between ARVREL and ARSVIN
- [ ] Temporary STREAM_STALE/BAD state retains the last coherent LCD phasor
- [ ] Main workspace and relay LCD show the same current-vector orientation
- [ ] Meaningful source-angle changes update both instruments
- [ ] Relay body appears slightly raised, not exaggerated
- [ ] LCD looks recessed into the body
- [ ] Keypad and RESET TRIP look raised and visibly depress when clicked
- [ ] No shadow clipping at 1520×900 or minimum window size
- [ ] No new flicker or material UI-performance regression
