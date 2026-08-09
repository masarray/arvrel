# ARVREL v0.1.0-beta.6 — Metrology-Grade Closed-Loop Timing Public Beta

ARVREL is a vendor-neutral Windows virtual protection and control IED laboratory for IEC 61850 engineering, education, FAT/SAT preparation, troubleshooting, and research.

This is a **public engineering beta**, not a certified relay, calibrated secondary-injection set, or hard-real-time protection platform.

## Release highlight — metrology-grade virtual test-set timing

`v0.1.0-beta.6` upgrades the feeder closed-loop laboratory so trip timing is measured through an explicit virtual secondary-injection path rather than inferred from relay-internal trip state.

The authoritative timing chain is now:

```text
TESTSET T0
→ instantaneous 4I+4V waveform
→ virtual analog wiring
→ relay terminal samples
→ signed ADC / clipping / quantization
→ causal relay measurement window
→ protection pickup / timer / trip request
→ relay BO contact delay / bounce
→ virtual binary wiring
→ independent TESTSET BI sampling / deglitch
→ accepted BI1 edge
→ measured trip time / optional auto-stop
```

`ProtectionSnapshot.TripLatched` may request relay BO1, but TESTSET timing and auto-stop remain driven only by the externally wired `TESTSET.BI1` path.

## Independent timing domains

The desktop reference model now separates timing authorities:

- monotonic TESTSET metrology clock with **1 µs integer resolution**;
- TESTSET binary-input sampling at **10 kHz / 100 µs**;
- relay/source processing at **4 kHz / 250 µs**;
- WPF presentation refresh remains presentation-only and has no timing authority.

The reference numerical-relay behavioral front end includes signed ADC quantization, clipping, configured input/filter delay and a causal one-cycle rolling measurement window. A timed test begins from settled pre-fault history rather than an artificial empty relay window.

## Protection timer correctness

Definite and inverse timing now starts at the observed pickup edge. The interval before pickup is not retroactively counted.

For settings exactly representable on the 250 µs relay processing grid, a **60 ms definite-time element produces exactly 60.000 ms pickup-to-trip** in the reference engine.

## Generic pickup versus operated-element timing

The relay BO2 pickup output remains an **ANY PICKUP** contact. Therefore the first element that asserts generic pickup can differ from the element that ultimately trips.

Beta.6 makes this distinction explicit:

- `RELAY ANY PU [element]` identifies the first relay pickup source;
- `TESTSET BI2 ACCEPT` reports the independently sampled external pickup indication;
- operated-element pickup is correlated to `LatchedOperation.Element`;
- element `P→T` is calculated only from the pickup and trip timestamps of that same operated element;
- `RELAY TRIP → TESTSET BI1` remains visible as a separate external output-path contribution.

The timing rail is sorted chronologically by timestamp rather than by event type.

## Operator timing and frozen-capture clarity

The feeder Virtual Injection workspace now makes closed-loop timing a first-class operator display:

- full-width chronological timing rail;
- explicit separation between relay pickup and TESTSET BI2 acceptance;
- explicit `OUTPUT OFF · FROZEN CAPTURE` state after auto-stop;
- configured injection columns labeled `RMS SET` / `ANGLE SET` so retained setpoints are not mistaken for live output;
- frozen protection states identified as frozen snapshot evidence;
- native P6 annunciation labels clarified as `PICKUP LIVE` and `TRIP LATCH`;
- capture semantics distinguish the accepted BI1 timer edge from the later relay processing frame used for waveform/phasor freeze;
- evidence schema includes first-any-pickup source plus BI1-versus-capture-frame timing.

## One-click relay RESET / re-arm fix

Beta.6 fixes a desktop issue where RESET could appear to require several clicks after an auto-stopped trip.

The root cause was a frozen causal relay measurement window: after BI1 auto-stop the virtual source was already OFF, but the relay acquisition window could still contain pre-stop fault samples. The former reset path used a fixed settling delay that could end before that acquisition state had fully dropped out.

RESET is now one deterministic equipment transaction:

```text
RESET once
→ allow source-off relay acquisition to clear stale fault samples
→ clear relay latch and timers once
→ model BO1 / BO2 release and contact bounce
→ model TESTSET BI1 / BI2 release
→ verify relay pickup/trip and all BO/BI states are LOW
→ READY TO RE-ARM
```

The transaction uses a bounded simulated timeout and reports diagnostic state if the postcondition cannot be reached. It does not restart or alter the virtual source, and it preserves completed TESTSET timing plus trip/event history.

Alarm ACK/CLEAR remains a separate operator function from protection RESET.

## Regression baseline

The release includes deterministic regression coverage for the realistic desktop closed-loop profile, including:

- `VirtualRelayFrontEndProfile.NumericalRelayDefault`;
- `VirtualRelayContactProfile.RealisticNumericalRelay`;
- `MetrologyTimingProfile.CmcStyle`;
- trip → TESTSET BI1 → auto-stop → **one RESET command only** → BO1/BO2 and BI1/BI2 released → successful re-arm.

Final pre-release validation on the accepted implementation completed with:

```text
Capture        5 / 5
Protection   276 / 276
Application   71 / 71
ProcessBus    51 / 51
---------------------
TOTAL        403 / 403
```

Release build completed with zero warnings / zero errors, NuGet vulnerability audit clean, compatibility compile against the older pinned ARIEC61850 interface passed, CodeQL passed, and the protection core passed on Windows, macOS and Ubuntu. Windows installer, single-file portable executable, portable archive and no-admin package contracts also passed.

## Multi-IED laboratory retained

Beta.6 retains the public multi-IED capabilities from earlier betas:

- **Protection Relay · OCR** with 50P, 51P, 50N, 51N, 67P, 67N, 27, 59 and 59N;
- **Transformer Differential · 87T / REF** with synchronized HV/LV internal secondary injection, restrained 87T, 87T-HS, REF HV/LV, harmonic and CT-saturation security, and deterministic transformer self-test;
- **AVR · OLTC Controller** with the virtual transformer plant, 17-position OLTC, operator authority model, MMS browse/read, reports and virtual controls;
- Internal Demo, PCAP/PCAPNG replay and authorized live Npcap Sampled Values capture;
- evidence export, settings fingerprints, trust/authority diagnostics and native P6 relay operator experience.

## Public package set

Official release assets are expected to include:

- `ARVREL-Setup-v0.1.0-beta.6-win-x64.exe` — per-user Windows installer;
- `ARVREL-v0.1.0-beta.6-win-x64-portable.exe` — single-file portable executable;
- `ARVREL-v0.1.0-beta.6-win-x64-portable.zip` — portable archive;
- `ARVREL-v0.1.0-beta.6-legal-notices.zip`;
- `SHA256SUMS.txt`;
- NuGet dependency evidence;
- CycloneDX SBOM when generated by the release workflow;
- GitHub build-provenance attestations.

The installer remains per-user and non-elevated.

## Recommended feeder timing evaluation

1. verify the downloaded package with `SHA256SUMS.txt`;
2. select **Protection Relay · OCR** and keep **SOURCE = Internal demo**;
3. choose a virtual injection preset and confirm the configured protection settings;
4. start injection and observe `RELAY ANY PU`, `TESTSET BI2 ACCEPT`, operated-element pickup/trip and `TESTSET BI1 ACCEPT` in chronological order;
5. confirm auto-stop reports `OUTPUT OFF · FROZEN CAPTURE`;
6. press **RESET once** and confirm the relay reaches `READY TO RE-ARM` without a second or third click;
7. start the next injection and verify a new test run is created.

## Requirements

- Windows 10 or Windows 11 x64;
- no additional dependency for Internal Demo injection, deterministic Transformer self-test, or the virtual AVR plant;
- Npcap only for live IEC 61850 Sampled Values capture;
- an authorized, isolated laboratory network for live Ethernet protocol testing.

Npcap is not silently installed or relicensed by ARVREL.

## Safety boundary

ARVREL remains **virtual-output only**.

Synthetic secondary-injection currents, modeled MMS commands, virtual trip states and all simulated process actions terminate inside the software. ARVREL does not provide physical relay contacts, physical OLTC motor authority, operational GOOSE trip authority, autonomous switching, or permission to operate primary equipment.

The software is not a calibrated relay test set, protection-grade hard-real-time platform, IEC 61850 certified IED, IEC 60255 type-tested relay, or substitute for approved commissioning procedures.

Do not use ARVREL as the sole basis for operational protection settings, AVR settings, switching decisions, commissioning acceptance, or primary-equipment control.

## Known beta limitations

- community binaries are not currently claimed as Authenticode-signed and may trigger Windows reputation warnings;
- metrology behavior is a deterministic software reference model, not calibration evidence for physical test hardware;
- relay front-end/contact parameters are generic behavioral profiles and are not claims about a named commercial relay;
- the Transformer internal injector is a deterministic software source, not a calibrated secondary-injection instrument;
- the AVR MMS/control model is vendor-neutral laboratory behavior and not a formal conformance profile;
- live performance depends on Windows scheduling, adapter drivers, publisher behavior and host load;
- broad clean-machine, multi-adapter and diverse-vendor interoperability validation continues during the beta period;
- physical output authority is intentionally absent.

## Licensing and reporting

ARVREL source is available under GPL-3.0-or-later. Third-party components retain their own licenses.

Use GitHub issues for reproducible non-sensitive defects. Include the exact ARVREL version, selected virtual IED, source mode, injection vector or engineering-client behavior, and minimal non-proprietary evidence. Use the private security-advisory workflow for vulnerabilities. Never publish proprietary substation captures, credentials or confidential SCL files.
