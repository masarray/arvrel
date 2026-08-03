# ARVREL SMV soak-test protocol

This protocol measures laboratory stability; it is not IEC 60255 performance, calibration, or type-test evidence.

## Test environment

Record:

- ARVREL version and commit;
- ARIEC61850 commit;
- Windows edition/build;
- CPU, RAM, display scale, and power mode;
- Npcap version and adapter/driver;
- publisher name, version, APPID, VLAN, svID, sample rate, nominal frequency, and payload layout;
- SCL filename/hash and CT/VT context;
- active protection setting fingerprint.

Use an isolated, authorized laboratory network and synthetic signals.

## Baseline duration

Run continuously for at least 60 minutes. A longer 8-hour engineering soak is recommended before promoting a beta to stable.

## Workload

1. Publish a stable 4I+4V SV stream at the target rate.
2. Keep waveform and phasor in DUAL view.
3. Switch focus between publisher and ARVREL at least once per minute.
4. Change WAVE/DUAL/PHASOR and current/voltage/sequence views periodically.
5. Inject at least 20 pickup-trip-reset cycles.
6. Exercise stale, gap, duplicate, out-of-order, quality, and recovery scenarios using controlled synthetic input.
7. Export evidence before and after a fault.
8. Change secondary/primary display while a trust hold is active.

## Record every five minutes

- process CPU;
- working set and private memory;
- UI responsiveness and longest visible freeze;
- displayed frame rate;
- accepted frame and ASDU counts;
- gap, duplicate, out-of-order, and invalid-quality counts;
- SMV trust state;
- crash-log presence;
- waveform/phasor coherence;
- relay trip and cause state.

## Pass criteria for beta

- no crash or unhandled exception;
- no persistent UI hang;
- no false physical/network output path;
- no trip through an active SMV trust block;
- no duplicate/out-of-order payload admitted to measurement buffers;
- no waveform or phasor presentation of partial/corrupt windows;
- no LED flicker or incorrect operated colour;
- no loss or replacement of completed trip evidence before reset;
- no sustained unbounded memory growth after warm-up;
- every injected fault has the expected element, timestamp order, quantity/unit, and phase/earth cause.

## Report

Store results as a versioned Markdown or CSV report using synthetic identifiers. Do not publish customer captures, station SCL files, IP plans, credentials, or proprietary network information.
