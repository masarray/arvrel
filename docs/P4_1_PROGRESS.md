# P4.1 Virtual Injection Run/Stop Interlock — progress

Updated: 2026-08-04

## Objective

Make Internal virtual injection behave like a secondary-injection test source: values are configured and armed while stopped, START energizes the source, and STOP forces every effective voltage and current output to zero without erasing the configured table.

## Implementation

- [x] Review ARSVIN publisher run/stop command pattern
- [x] Separate configured profile from effective output profile
- [x] Default virtual output state is STOPPED
- [x] STOP forces effective 4I+4V output to 0 V / 0 A
- [x] Configured RMS, angle, frequency, enablement, and neutral provenance remain armed while stopped
- [x] START validates the complete editor before energizing
- [x] START and STOP controls are mutually interlocked
- [x] Main Run control follows the same Start injection / Stop injection state
- [x] Legacy A-G button now arms/applies a preset without silently starting a stopped source
- [x] Auto-apply while stopped updates armed values only
- [x] Auto-apply while running rebuilds one coherent nominal cycle
- [x] STOP drives a zero measurement through the protection engine so pickup drops out
- [x] Existing trip latch remains until relay reset
- [x] Relay operation remains governed by measured current, active pickup setting, element delay, and trust permission
- [x] Default analysis workspace changed to DUAL
- [x] Evidence schema records configured and effective profiles, fingerprints, output state, and state-change timestamp
- [x] Deterministic runtime tests added for stopped zero output, start gating, stop behavior, exact pickup threshold, configured delay, and trip-latch retention

## Automated validation gates

- [x] Static public-site validation
- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection run/stop tests
- [x] Exact pickup-current and configured-delay test
- [x] NuGet vulnerability audit
- [x] Changed-file scope review: nine expected P4.1 files; no live/replay/parser/protection-function implementation modification
- [x] Squash merge PR #36 to `main`
- [x] Merge commit `00f73e34b403ef3c939088f2256ce62064b6f275`

## Manual Windows QA

These checks require launching the WPF application on Windows and remain intentionally open.

- [ ] Verify DUAL is selected on startup
- [ ] Verify START enabled and STOP disabled at startup
- [ ] Verify START disables itself and enables STOP
- [ ] Verify STOP disables itself and enables START
- [ ] Verify table values remain visible while stopped but phasor/waveform/current readouts are zero
- [ ] Verify changing values while stopped does not energize output
- [ ] Verify changing values while running auto-applies after validation and rebuild
- [ ] Verify current below pickup does not trip
- [ ] Verify current at/above pickup trips only after the configured delay
- [ ] Verify STOP removes pickup while preserving a latched trip
- [ ] Verify Reset relay clears the latch without changing the configured profile
- [ ] Verify live Npcap and PCAP replay remain unchanged

## Safety boundary

The internal source remains uncalibrated software laboratory injection. START/STOP semantics improve operator realism but do not make ARVREL a calibrated Omicron replacement, IEC 60255 test set, commissioning acceptance instrument, hard-real-time source, physical trip source, or switching authority.
