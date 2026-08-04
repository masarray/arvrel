# P4 Virtual Injection Laboratory — progress

Updated: 2026-08-04

## Implementation

- [x] Branch created from current `main`
- [x] Immutable 4I+4V injection profile
- [x] Per-channel RMS, angle, enable, and provenance model
- [x] Common synchronous frequency validation
- [x] Culture-invariant injection fingerprint
- [x] Synthetic complete-window sample generator
- [x] Existing single-bin DFT used for measured phasors
- [x] Explicit IN/VN and calculated residual fallback
- [x] Atomic profile application
- [x] Coherent-cycle pickup/trip restraint after changes
- [x] Internal scenario refactored away from fixed A-G values
- [x] Validated table editor
- [x] Debounced auto apply
- [x] Last-valid profile retained on invalid edits
- [x] Injection + phasor workspace
- [x] Waveform and phasor remain available
- [x] Built-in fault/protection presets
- [x] Clear injection without clearing trip latch
- [x] Reset relay while retaining injection
- [x] Reset all returns balanced nominal source
- [x] Internal evidence schema v3
- [x] Core deterministic tests added
- [x] Engineering behavior document added

## Validation gates

- [ ] Restore
- [ ] Windows application build
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] NuGet vulnerability audit
- [ ] Release-candidate packaging gate where applicable
- [ ] Visual smoke test at 1280×740 minimum window
- [ ] Visual smoke test at 1520×900 default window
- [ ] Verify DataGrid keyboard editing and validation feedback
- [ ] Verify preset → custom edit → invalid edit → recovery sequence
- [ ] Verify phasor, LCD, annunciation, and operation evidence
- [ ] Verify live Npcap and PCAP replay remain unchanged

## Merge gate

- [ ] Review changed-file scope
- [ ] Resolve all CI findings
- [ ] Update release notes and public capability copy
- [ ] Squash merge to `main`
