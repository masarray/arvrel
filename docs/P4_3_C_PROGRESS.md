# P4.3-C — Compact Advanced Injection layout

Updated: 2026-08-05

## Objective

Make the modeless Advanced Injection Laboratory denser, clearer, and closer to a professional secondary-injection work surface while preserving the shared Direct editor and all runtime behavior.

## Layout contract

- [x] Reduce default window size from 1180×760 to 1040×680
- [x] Reduce minimum window size from 960×620 to 880×540
- [x] Enable layout rounding and device-pixel snapping
- [x] Replace the tall title area and separate footer with one compact command header
- [x] Keep title and engineering subtitle visible without oversized typography
- [x] Present active profile in the command header
- [x] Keep P4.3-B Play and Stop icons in the command header
- [x] Present STOPPED / STARTING / RUNNING as one compact state badge
- [x] Retain a compact EDITOR authority badge
- [x] Reduce Direct host margin from 12 px to 6 px
- [x] Reduce outer tab margins and tab padding
- [x] Give the shared 4I+4V editor more usable vertical area
- [x] Preserve the modeless single-editor ownership contract

## Behavior boundary

- [x] No change to Start/Stop command routing
- [x] No change to relay-reset/source functional separation
- [x] No change to profile validation or auto apply
- [x] No change to virtual waveform generation
- [x] No change to DFT, phasor, protection, process-bus, trust, or evidence behavior
- [x] P4.3-D Angle context menu remains out of scope

## Automated validation

- [ ] Restore
- [ ] Windows application build
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] Virtual-injection tests
- [ ] NuGet vulnerability audit
- [ ] Static-site validation
- [ ] Changed-file scope review
- [ ] Ready-for-review and squash merge

## Manual Windows QA

These checks require launching the Windows WPF application and remain intentionally open until verified on Windows.

- [ ] Verify compact layout at the 1040×680 default size
- [ ] Verify usability at the 880×540 minimum size
- [ ] Confirm title, profile, Play/Stop, status, and EDITOR badge do not overlap
- [ ] Confirm every tab label remains visible
- [ ] Confirm 4I+4V grid remains fully editable
- [ ] Confirm Clear injection and Reset relay remain visible
- [ ] Confirm disabled future tabs remain visually quiet
- [ ] Confirm STOPPED / STARTING / RUNNING badge changes once per runtime transition
- [ ] Confirm Advanced Window remains modeless and Main Window remains interactive
- [ ] Confirm closing/reopening returns the same shared editor instance
