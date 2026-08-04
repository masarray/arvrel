# P4 Virtual Injection Laboratory — progress

Updated: 2026-08-04

## Implementation

- [x] Branch created from current `main`
- [x] Immutable 4I+4V injection profile
- [x] Per-channel RMS, angle, enable, and provenance model
- [x] Common synchronous injected-frequency validation
- [x] Fixed 4 kHz nominal measurement grid
- [x] Off-nominal response remains visible through the existing DFT
- [x] Culture-invariant injection fingerprint
- [x] Synthetic complete-window sample generator
- [x] Existing single-bin DFT used for measured phasors
- [x] Explicit IN/VN and calculated residual fallback
- [x] Atomic profile application in testable core runtime
- [x] Invalid profile retains the last valid source
- [x] Coherent-cycle pickup/trip restraint after changes
- [x] Internal scenario refactored away from fixed A-G values
- [x] Validated table editor
- [x] Debounced auto apply
- [x] Injection + phasor workspace
- [x] Waveform and phasor remain available
- [x] Built-in fault/protection presets
- [x] Clear injection without clearing trip latch
- [x] Reset relay while retaining injection
- [x] Reset all returns balanced nominal source
- [x] Internal evidence schema v3
- [x] Core deterministic generator and runtime tests
- [x] Engineering behavior document added
- [x] README and changelog updated without overstating released packages

## Automated validation gates

- [x] Static public-site validation
- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection generator tests
- [x] Virtual-injection atomic/coherence runtime tests
- [x] NuGet vulnerability audit
- [x] Changed-file scope review: 13 expected files, no live/replay engine modification
- [x] Feature PR marked ready for review
- [x] Release-candidate packaging correctly deferred: repository policy runs it only on a future `release/*` branch

## Manual visual and interaction QA

These checks require launching the Windows WPF application; they cannot be honestly marked complete from source/CI inspection alone.

- [ ] Visual smoke test at 1280×740 minimum window
- [ ] Visual smoke test at 1520×900 default window
- [ ] Verify DataGrid keyboard editing and validation feedback
- [ ] Verify preset → custom edit → invalid edit → recovery sequence
- [ ] Verify phasor, LCD, annunciation, operation evidence, and export interaction
- [ ] Verify live Npcap and PCAP replay remain visually and operationally unchanged

## Merge gate

- [x] Review changed-file scope
- [x] Resolve all compiler and CI findings
- [x] Update changelog, README, engineering contract, and checklist
- [x] Mark PR ready for review
- [x] Squash merge PR #34 to `main`
- [x] Merge commit `0d22f09aefcc9d7ef9b943dda94c9160761da0da`
