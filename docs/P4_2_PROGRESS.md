# P4.2 Advanced Injection Laboratory — progress

Updated: 2026-08-04

## Objective

Create a modeless Advanced Injection Window that becomes the only visible injection editor while open. The Main Window remains the virtual relay under test and continues to show waveform, phasor, pickup, timing, annunciation, trip latch, and evidence.

## P4.2.0 modeless-window foundation

- [x] Create modeless `AdvancedInjectionWindow` using `Show()`, not `ShowDialog()`
- [x] Enforce one Advanced Injection Window instance
- [x] Keep Main Window interactive while the advanced window is open
- [x] Reuse the existing Direct 4I+4V editor rather than cloning an editor or runtime
- [x] Move the editor from Main Window to the advanced window on open
- [x] Force Main Window to DUAL before transferring editor authority
- [x] Hide the Main Window `INJECT` tab while advanced editor authority is active
- [x] Prevent internal calls from reopening the Main Window injection workspace while advanced authority is active
- [x] Preserve WAVE, DUAL, and PHASOR Main Window views
- [x] Add `ADVANCED` / `FOCUS INJECTION` header action
- [x] Restore the same editor to Main Window on advanced-window close
- [x] Restore the `INJECT` tab after close
- [x] Keep Main Window in DUAL after close
- [x] Close the advanced window automatically when leaving Internal demo
- [x] Suppress injection-running prompt during owner shutdown and source change
- [x] Prompt on manual close while injection is running:
  - stop output and close;
  - keep output running and close;
  - cancel close.
- [x] Synchronize configured profile, fingerprint, and output state in the advanced-window footer
- [x] Reserve disabled navigation stages for Symmetrical, Impedance, Ramp, Sequencer, and advanced Waveform without claiming them implemented

## Automated validation gates

- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] NuGet vulnerability audit
- [x] Static public-site validation
- [x] Final changed-file scope review: five expected P4.2.0 files; no parser, process-bus, or protection-function implementation changes
- [x] Mark PR #38 ready for review
- [x] Squash merge PR #38 to `main`
- [x] Merge commit `a7cf1f3da43d660cdc778452ca5653a56332837a`

## Manual Windows QA

These checks require launching the WPF application and remain intentionally open.

- [ ] Verify DUAL is selected at startup
- [ ] Verify `ADVANCED` opens a separate non-blocking window
- [ ] Verify Main Window remains fully interactive
- [ ] Verify the Main Window `INJECT` tab disappears immediately
- [ ] Verify the Main Window injection workspace cannot be reopened while advanced authority is active
- [ ] Verify WAVE, DUAL, and PHASOR switching does not hide the editor inside the advanced window
- [ ] Verify `FOCUS INJECTION` restores a minimized or background advanced window
- [ ] Verify DataGrid editing, validation, preset selection, START, and STOP after WPF reparenting
- [ ] Verify manual close while stopped restores the same values and tab
- [ ] Verify manual close while running: Stop and close
- [ ] Verify manual close while running: Keep running and close
- [ ] Verify manual close while running: Cancel
- [ ] Verify switching to Live Npcap or PCAP replay closes the advanced window safely
- [ ] Verify closing Main Window does not leave a hidden advanced window or prompt
- [ ] Verify live/replay behavior remains unchanged

## Next P4.2 stages

- [ ] P4.2.1 refine modeless-window visual and interaction QA
- [ ] P4.2.2 Direct and Symmetrical component views
- [ ] P4.2.3 Impedance R–X view and loop solving
- [ ] P4.2.4 Ramp engine and measured pickup search
- [ ] P4.2.5 Prefault/fault/post-fault sequencer
- [ ] P4.2.6 harmonics, DC offset, phase jump, clipping, and CT-saturation approximation

## Safety boundary

The modeless Advanced Injection Window changes editor authority and workflow only. It does not create a calibrated analog output, IEC 60255 test-set claim, physical trip source, hard-real-time source, or Omicron-equivalent certification claim.
