# P4.3-A — Functional separation progress

Updated: 2026-08-05

## Equipment contract

- [x] Virtual injection source and protection relay are separate equipment domains
- [x] START/STOP remains the only virtual-source output authority
- [x] Relay reset does not call `StopInjection`, `Restart`, `Reset`, or apply a nominal source preset
- [x] Relay reset retains configured injection profile and configured fingerprint
- [x] Relay reset retains effective output profile and output fingerprint
- [x] Relay reset retains source state: STOPPED, STARTING, or RUNNING
- [x] Relay reset clears protection timers and virtual-trip latch
- [x] Relay reset clears pickup/trip evidence cursors and relay annunciation
- [x] Relay is immediately rendered against the source that is already present
- [x] A persistent injected fault can pick up and re-trip the reset relay after the configured delay
- [x] Live/Replay relay reset leaves the selected process-bus source unchanged

## UI command coverage

- [x] Main waveform-footer `Reset`
- [x] Relay-faceplate `RESET TRIP`
- [x] Simple injection editor `Reset relay`
- [x] Modeless Advanced Injection `Reset relay`
- [x] Legacy coupled handlers are prevented from executing after the relay-only authority handles the command

## Automated validation

- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] Relay-reset source-retention regression test
- [x] Persistent-fault re-trip regression test
- [x] NuGet vulnerability audit
- [x] Static-site validation
- [x] Changed-file scope review: one WPF authority, one test file, this progress document
- [x] Ready-for-review and squash merge through PR #46
- [x] Main commit `cb90b178d836ca97ae3a186229acb67673e44dc3`

## Manual Windows QA

These checks require launching the Windows WPF application and remain intentionally open.

- [ ] Start A-G injection and allow the relay to trip
- [ ] Press relay-faceplate `RESET TRIP`
- [ ] Confirm source remains RUNNING and waveform/phasor remain energized
- [ ] Confirm the relay picks up and re-trips after its configured delay
- [ ] Repeat through the Advanced Injection `Reset relay` command
- [ ] Confirm STOP still forces 0 V / 0 A and remains independent from relay reset
- [ ] Confirm Live Npcap and PCAP replay reset only protection state
