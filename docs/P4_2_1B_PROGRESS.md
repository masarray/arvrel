# P4.2.1b Layout and Global Flicker Stabilization — progress

Updated: 2026-08-05

## Objective

Clean the Internal virtual-injection layout and remove systemic WPF flicker reported in the 1520×900 Main Window.

## Layout implementation

- [x] Audit the reported 1520×900 screenshot
- [x] Remove duplicate START and STOP buttons from the injection editor footer
- [x] Keep the top-right toolbar as the single output START/STOP authority
- [x] Move the Advanced launcher inside the simple INJECT workspace toolbar
- [x] Remove Advanced from the main analysis-tab row
- [x] Hide the Advanced launcher when the modeless window owns the editor
- [x] Keep the modeless Advanced window accessible through the Windows taskbar
- [x] Reserve deterministic center-column width for the analysis controls
- [x] Restore complete `INJECT`, `WAVE`, `DUAL`, and `PHASOR` labels
- [x] Compact the phasor selector and measurement-summary spacing
- [x] Enable layout rounding and device-pixel snapping

## Global flicker stabilization

- [x] Identify the competing 40 ms generic renderer and 250 ms injection presenter
- [x] Detach the unconditional legacy WPF timer handler after Main Window initialization
- [x] Preserve the 40 ms protection-execution cadence
- [x] Build a deterministic signature from displayed measurements and protection state
- [x] Render Internal waveform, measurements, protection cards, relay LCD, LEDs, and footer only when visible state changes
- [x] Restore concise injection labels in the same dispatcher turn as a generic render
- [x] Stop alternating `INTERNAL · GOOD` with `STOPPED`, `STARTING`, or `RUNNING`
- [x] Replace moving synthetic Internal `smpCnt` with stable `4 kHz` source information
- [x] Keep the Internal operator strip limited to frequency, samples/cycle, sample rate, VIRTUAL, and output state
- [x] Keep fingerprints and residual provenance in tooltips and evidence
- [x] Keep the virtual-relay lower-left footer stable at group and revision
- [x] Refresh concise labels immediately after preset and editor-button actions
- [x] Throttle Live/Replay WPF presentation without changing process-bus processing

## Automated validation

- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] NuGet vulnerability audit
- [ ] Final CI after documentation commit
- [ ] Final changed-file scope review
- [ ] Mark PR ready for review
- [ ] Squash merge to `main`

## Manual Windows QA

These checks require launching the WPF application and remain intentionally open.

- [ ] Confirm `INJECT`, `WAVE`, `DUAL`, and `PHASOR` are fully visible at 1520×900
- [ ] Confirm the Advanced launcher appears only inside INJECT
- [ ] Confirm only the top-right toolbar START/STOP control remains
- [ ] Confirm STOPPED does not alternate with GOOD
- [ ] Confirm the injection subtitle remains stable
- [ ] Confirm the virtual-relay lower-left footer remains stable
- [ ] Confirm LCD measurements and LEDs remain stable for unchanged input
- [ ] Confirm waveform and phasor update when injection values actually change
- [ ] Confirm pickup, timing, trip, and dropout remain responsive
- [ ] Confirm Live Npcap and PCAP replay remain operational
- [ ] Visual smoke test at 1280×740 and 1520×900

## Next P4.2 stages

- [ ] P4.2.2 Direct and Symmetrical component views
- [ ] P4.2.3 Impedance R–X view and loop solving
- [ ] P4.2.4 Ramp engine and measured pickup search
- [ ] P4.2.5 Prefault/fault/post-fault sequencer
- [ ] P4.2.6 harmonics, DC offset, phase jump, clipping, and CT-saturation approximation
