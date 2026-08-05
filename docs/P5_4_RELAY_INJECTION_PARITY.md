# P5.4 — Relay and injection feature parity

## Objective

P5.4 closes the core operator-workflow gap between the established WPF product and the Avalonia migration shell. The phase gives the cross-platform shell a practical virtual test-set workflow and editable 50/51 relay settings without copying protection or injection algorithms into the presentation layer.

Feature parity in this phase means parity of relay/source authority and the essential internal-laboratory workflow. It does not mean pixel parity with the mature WPF faceplate or migration of every research and process-bus dialog.

## Equipment boundary

The virtual source and protection relay are modeled as separate equipment:

```text
Virtual test set
  configured 4I + 4V profile
  frequency and preset
  START / STOP output
          ↓ measurement
Protection relay
  50P / 51P / 50N / 51N settings
  pickup, timing, trip latch
  relay reset
```

The commands deliberately have different authority:

- **START / STOP injection** changes effective virtual output only; configured values remain armed.
- **Clear injection** restores the normal-balanced source profile and does not clear relay timers or a latched trip.
- **Reset relay** clears protection timers and the trip latch without changing the configured injection profile, run state, SMV degradation state, sample counter, or source fingerprints.
- **Reset complete lab** stops the source, restores the normal-balanced profile, restores SMV trust, and resets relay state while retaining the active relay setting group.
- **Apply relay settings** validates and replaces the relay setting group, resets relay timers/latch, and preserves the separately configured source and its run state.

## Injection parity

The Avalonia shell exposes the same portable source catalog used by WPF:

- Normal balanced;
- A-G, B-G, and C-G faults;
- A-B and A-B-G faults;
- three-phase fault;
- 27 undervoltage;
- 59 overvoltage;
- 59N residual voltage;
- 67P forward/reverse;
- 67N forward/reverse.

The source editor exposes all eight virtual channels:

- VA, VB, VC, VN;
- IA, IB, IC, IN;
- enabled state;
- RMS engineering value;
- engineering angle;
- common synchronous frequency from 40 to 70 Hz.

IN and VN provenance remains explicit. Disabled neutral channels use calculated phase sums, while enabled neutral channels become explicit virtual channels.

Edits are staged in the presentation model. Invalid input does not mutate the active source. Applying a complete valid profile is atomic and uses `VirtualInjectionProfile.Normalize()` and the existing portable runtime.

## Relay parity

The Avalonia shell exposes the core overcurrent setting group:

- group name and revision;
- 50P enabled, pickup, definite delay, and dropout ratio;
- 51P enabled, pickup, IEC curve, TMS, definite delay, minimum operate time, dropout ratio, reset mode, and reset delay;
- 50N enabled, pickup, definite delay, and dropout ratio;
- 51N enabled, pickup, IEC curve, TMS, definite delay, minimum operate time, dropout ratio, reset mode, and reset delay.

Fields not exposed by this bounded editor—user-defined curve constants and multifunction feeder settings—are preserved from the active immutable `ProtectionSettings` object. The editor validates through the same `ProtectionSettings.Validate()` authority used by the protection engine.

## Application boundary

P5.4 extends `InternalLabSession` with two explicit operations:

```text
ApplyProfile(profile)
ApplySettingsPreservingSource(settings)
```

The second operation updates the relay engine without resetting or restarting `DeterministicLabScenario`. The previous `ApplySettings()` behavior remains for compatibility with callers that intentionally request a complete scenario reset.

## Avalonia surface

The left workspace contains:

- SOURCE tab for source state, preset, frequency, lifecycle commands, trust control, and process-bus capability reporting;
- 4I + 4V tab for all virtual channels and atomic validation/apply.

The center workspace retains measurements, two-cycle waveform evidence, x-ticks, relay state, trust, and decision reason.

The right workspace contains:

- RELAY tab for the setting group, four core overcurrent elements, apply status, and live element progress;
- EVENTS tab for a bounded operator event history.

## Regression coverage

Display-server-free tests verify:

- all 14 presets and eight channel rows are available;
- a selected preset and frequency populate the editable source;
- invalid input leaves the last valid source active and blocks START;
- START advances the deterministic source;
- STOP forces output to zero while retaining the configured profile and fingerprint;
- A-G fault drives pickup/trip behavior;
- SMV degradation keeps measurement visible while removing trip permission;
- clear injection does not clear a latched trip;
- relay reset clears trip while retaining source profile, fingerprint, and run state;
- relay setting apply changes group/revision while preserving source profile, fingerprint, and run state;
- complete laboratory reset returns to a stopped normal-balanced source while retaining relay settings;
- the application session preserves profile, run state, degradation state, fingerprints, and sample counter when relay settings change.

## Compatibility

P5.4 does not change:

- virtual injection preset definitions or generated samples;
- fixed nominal sampling cadence;
- RMS and phasor estimation;
- 50P, 51P, 50N, or 51N algorithms;
- IEC inverse-curve calculations;
- pickup, operate, dropout, reset-memory, trip-latch, or trust semantics;
- PCAP/PCAPNG parsing;
- live capture transport behavior;
- WPF controls, dialogs, or Windows release packaging.

## Deliberately deferred

- advanced injection context menus and angle rotation helpers;
- per-row keyboard accelerators and bulk table paste;
- setting-group persistence to disk;
- full multifunction feeder settings (27/59/59N/67/67N);
- algorithm laboratory and shadow staging;
- relay annunciation LED and LCD faceplate pixel parity;
- live/replay source controls and external-stream settings recreation in Avalonia;
- SCL import, measurement-context dialogs, evidence export, and file workflows.
