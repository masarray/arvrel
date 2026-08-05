# P4.3-B — Start/Stop icon buttons

Updated: 2026-08-05

## Objective

Give the modeless Advanced Injection Laboratory direct source-output controls without duplicating or bypassing the established virtual-injection runtime authority.

## Command contract

- [x] Add a Play icon command for `Start injection`
- [x] Add a Circle Stop icon command for `Stop injection`
- [x] Keep icons compact and separate from the later P4.3-C layout redesign
- [x] Start command delegates to `StartVirtualInjectionSource()`
- [x] Stop command delegates to `StopVirtualInjectionSource()`
- [x] No direct mutation of `VirtualInjectionRuntime` from the Advanced Injection Window
- [x] No change to relay-reset functional separation from P4.3-A
- [x] No change to injection profile, generator, DFT, protection, process-bus, or evidence semantics

## State presentation

- [x] Play is enabled only while output is STOPPED
- [x] Stop is enabled while output is STARTING or RUNNING
- [x] STARTING/RUNNING/STOPPED status remains driven by the existing runtime state
- [x] Button state refreshes immediately after a command and through the existing presentation timer
- [x] Disabled controls retain explanatory tooltips
- [x] Automation names and help text are available for keyboard/accessibility tooling

## Scope boundary

This phase intentionally does not include:

- [ ] P4.3-C compact Advanced Injection layout
- [ ] P4.3-D Angle context menu
- [ ] sequence, impedance, ramp, sequencer, or transient waveform execution

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

These checks require launching the Windows WPF application and remain intentionally open until performed on the packaged build.

- [ ] Open Advanced Injection Laboratory while source is STOPPED
- [ ] Confirm Play is enabled and Stop is disabled
- [ ] Press Play and confirm STARTING then RUNNING
- [ ] Confirm Play becomes disabled and Stop becomes enabled
- [ ] Press Stop and confirm 0 V / 0 A output while configured values remain armed
- [ ] Confirm STOPPED state restores Play availability
- [ ] Confirm the Main Window START/STOP control remains synchronized
- [ ] Confirm relay reset does not alter either icon state unless the source state itself changes
