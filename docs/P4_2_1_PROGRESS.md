# P4.2.1 UI Stability and One-Click Fault Injection — progress

Updated: 2026-08-05

## Objective

Remove visible text churn and steady-state flicker from the Internal virtual-injection workspace while restoring the expected one-click A-G fault command.

## Implementation

- [x] Audit the reported 1520×900 Internal demo layout
- [x] Identify unconditional 80–120 ms WPF status and brush assignments
- [x] Reduce injection status to `STOPPED`, `STARTING`, and `RUNNING`
- [x] Reduce the primary injection subtitle to profile name plus output state
- [x] Keep fingerprints and detailed provenance in tooltips and evidence
- [x] Make the relay footer stable instead of including continuously refreshed injection identity/state text
- [x] Assign START/STOP button state, run icon, stream status, subtitle, tooltip, and footer only when values change
- [x] Cache Advanced Injection profile, fingerprint, output state, and frozen brushes
- [x] Reduce Advanced Injection presentation polling to 250 ms
- [x] Build a deterministic phasor presentation signature
- [x] Assign a new phasor frame only when vectors, display mode, reference, or status change
- [x] Use a 100 ms engineering-instrument refresh cadence without forcing redraws
- [x] Restore `Inject A-G fault` as a one-click command
- [x] Always load the A-G preset instead of toggling back to normal
- [x] Immediately START when the source was stopped
- [x] Apply A-G directly to the active source when already running
- [x] Preserve protection pickup, timer, trust, and trip-latch behavior

## Automated validation

- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] NuGet vulnerability audit
- [x] Initial scope review: four UI/injection files only
- [x] Documentation commit does not modify public-site assets; no static deployment gate required
- [x] Final changed-file scope review: four UI/injection files plus changelog and progress document; no parser, process-bus, or protection-function implementation changes
- [ ] Mark PR ready for review
- [ ] Squash merge to `main`

## Manual Windows QA

These checks require launching the WPF application and remain intentionally open.

- [ ] Confirm the injection header no longer flickers during steady RUNNING output
- [ ] Confirm status badge, stream status, subtitle, and relay footer remain visually stable
- [ ] Confirm phasor labels remain stable for an unchanged 4I+4V profile
- [ ] Confirm phasor updates immediately after RMS, angle, frequency, or display-mode changes
- [ ] Confirm `Inject A-G fault` from STOPPED loads A-G and starts output
- [ ] Confirm `Inject A-G fault` while RUNNING applies A-G without stopping first
- [ ] Confirm START/STOP controls remain interlocked
- [ ] Confirm Advanced Injection status remains stable
- [ ] Confirm Live Npcap and PCAP replay behavior remains unchanged
- [ ] Visual smoke test at 1280×740 and 1520×900

## Next P4.2 stages

- [ ] P4.2.2 Direct and Symmetrical component views
- [ ] P4.2.3 Impedance R–X view and loop solving
- [ ] P4.2.4 Ramp engine and measured pickup search
- [ ] P4.2.5 Prefault/fault/post-fault sequencer
- [ ] P4.2.6 harmonics, DC offset, phase jump, clipping, and CT-saturation approximation
