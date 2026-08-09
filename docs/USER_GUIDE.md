# ARVREL User Guide

ARVREL is a Windows virtual protection and control IED laboratory for deterministic secondary injection, closed-loop virtual TESTSET↔relay testing, IEC 61850 Sampled Values analysis, Transformer Differential evaluation, AVR/OLTC simulation, and reviewable engineering evidence.

This guide describes the public **v0.1.0-beta.6** product. See [`CURRENT_STATUS.md`](CURRENT_STATUS.md) for the canonical shipped-state summary.

## 1. Choose the evaluation path

| Path | Use it when | Additional requirement |
|---|---|---|
| Feeder closed-loop internal laboratory | You want a deterministic virtual secondary-injection/test-set workflow with measured pickup/trip timing | None |
| Transformer deterministic self-test | You want to verify packaged 87T/REF behavior | None |
| Transformer two-sided internal injection | You want synchronized HV/LV/neutral software injection without external SV equipment | None |
| AVR / OLTC internal plant | You want to evaluate voltage regulation, tap authority, reports, and virtual MMS controls | None |
| PCAP replay | You want repeatable offline process-bus analysis | Authorized PCAP/PCAPNG file |
| Live Sampled Values | You are on an isolated, authorized laboratory network | Npcap and a suitable adapter |
| Source development | You need to inspect/build/modify ARVREL | .NET 8 SDK, Git, sibling ARIEC61850 repository |

Start with deterministic internal workflows. Use replay before live capture whenever possible.

## 2. Install and verify

1. Open the [download page](https://masarray.github.io/arvrel/download.html).
2. Download the beta.6 Windows installer or portable package from the official GitHub Release.
3. Download `SHA256SUMS.txt` from the same release.
4. Calculate SHA-256 locally and compare the full value.
5. Review the [release status](https://masarray.github.io/arvrel/release-status.html) and [current shipped status](CURRENT_STATUS.md).

Unsigned community binaries may trigger Windows reputation warnings. Integrity verification does not turn an unsigned package into a signed or certified product.

## 3. Feeder closed-loop secondary injection

The feeder internal laboratory models separate virtual equipment authorities:

```text
TESTSET source → virtual analog wiring → relay front end → protection
    ↑                                                   ↓
    └── timing / optional auto-stop ← TESTSET BI ← wire ← relay BO
```

### 3.1 Configured setpoints versus effective output

Configured source values remain available while the output is stopped. Effective output is zero until START.

- **START** energizes the configured source and establishes TESTSET T0.
- **STOP** drives effective output to zero without erasing configured setpoints.
- After a BI1 auto-stop, setpoints remain visible for repeatability while the authoritative state is **OUTPUT OFF · FROZEN CAPTURE**.
- WPF refresh does not define protection or TESTSET timing.

### 3.2 What owns measured timing

The TESTSET owns the external result through its wired binary inputs:

- `RELAY ANY PU [source]` — first generic protection pickup requesting BO2;
- `TESTSET BI2 ACCEPT` — accepted generic ANY-PICKUP edge;
- operated-element pickup — pickup of the element that ultimately trips;
- operated-element P→T — that element's own pickup-to-trip interval;
- relay trip request — live trip-latch edge requesting BO1;
- `TESTSET BI1 ACCEPT` — authoritative external trip time and optional auto-stop trigger.

`ProtectionSnapshot.TripLatched` is not a TESTSET measurement shortcut. It can drive BO1, but the test set records trip only after the external virtual wiring and BI acceptance path completes.

BI2 is **ANY PICKUP**. It may legitimately occur before the pickup of the element that eventually operates. Do not interpret T0→BI2 as the operated element's pickup time unless the evidence says they are the same event.

### 3.3 Default beta.6 timing model

- TESTSET clock resolution: 1 µs;
- TESTSET BI sampling: 10 kHz / 100 µs;
- BI deglitch: 0.5 ms;
- BI debounce holdoff: 0 ms;
- relay acquisition/processing: 4 kHz / 250 µs;
- behavioral relay front-end delay: 1.5 ms;
- one nominal 50 Hz causal rolling measurement cycle.

These are behavioral laboratory parameters, not calibrated CMC/relay specifications.

### 3.4 First closed-loop test

1. Launch ARVREL and select the feeder **Internal demo** path.
2. Review the enabled feeder settings and CT/VT context.
3. Apply a known internal fault preset or editable 4I+4V source.
4. Start output.
5. Observe source T0, relay ANY PICKUP, TESTSET BI2 acceptance, operated-element pickup/trip, relay trip request, and TESTSET BI1 acceptance.
6. Confirm auto-stop occurs only after accepted BI1 when that option is enabled.
7. Review the frozen waveform/phasor/evidence after output turns off.
8. Press relay RESET once and wait for **READY TO RE-ARM**.
9. Start the next run only after the re-arm postcondition is complete.

### 3.5 One-click RESET behavior

After auto-stop, fault-window samples may remain in the causal relay measurement history even though source output is already zero. Beta.6 RESET handles this deterministically:

1. advance the modeled relay acquisition in 250 µs steps until stale pickup releases;
2. clear latch/timers once;
3. continue the feedback path until no pickup remains, BO1/BO2 are LOW, and TESTSET BI1/BI2 are LOW;
4. show **READY TO RE-ARM** only after the postcondition is satisfied.

Completed timing, trip capture, and event evidence are preserved. RESET does not restart or alter the source. If the source remains energized, a valid fault may reassert protection after reset.

### 3.6 Wiring-invariant check

For a strong closed-loop sanity test, disconnect virtual `RELAY.BO1.TRIP → TESTSET.BI1.TRIP` and apply a fault expected to trip internally.

Expected behavior:

- relay internal trip/latch may assert;
- no accepted TESTSET BI1 trip edge exists;
- no external measured trip time exists;
- optional TESTSET trip auto-stop does not occur.

If the TESTSET still reports trip with the wire open, that is a regression.

## 4. Process-bus replay

Use replay for repeatable analysis of authorized PCAP/PCAPNG captures:

1. select replay;
2. open the capture;
3. select the intended stream(s);
4. review APPID, destination MAC, VLAN, `svID`, dataset, `confRev`, mapping, scaling, quality, and continuity;
5. confirm coherent measurement windows;
6. review trust permissions, waveform/phasors, protection state, and evidence;
7. retain capture identity and ARVREL version with exported evidence.

## 5. Live Sampled Values

Live capture requires Npcap installed separately and an isolated, authorized laboratory network.

1. select the approved adapter;
2. bind the intended stream and SCL context where available;
3. confirm identity, mapping, scaling, quality, freshness, and continuity;
4. verify trust permissions before interpreting pickup/trip;
5. stop capture before changing laboratory network topology.

## 6. Trust before trip

ARVREL separates permissions:

```text
AllowsMeasurement  → quantities may enter measurement/display
AllowsPickup       → protection pickup/timing may be evaluated
AllowsTrip         → an operated element may assert the virtual relay trip latch
TESTSET BI1        → owns the external closed-loop trip result
```

A stream can remain diagnostically visible while trust blocks new protection authority. Duplicate/out-of-order frames may remain visible in telemetry but are rejected before measurement/protection ingestion.

## 7. Feeder protection scope

Public feeder protection includes 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N.

Before evaluating an element:

1. verify current/voltage scaling and units;
2. verify phase order and residual provenance;
3. confirm the active setting group and fingerprint;
4. understand pickup/dropout and timing mode;
5. confirm trust permissions;
6. define the expected operate and restrain outcomes;
7. in closed-loop testing, distinguish internal relay timing from the external TESTSET BI path.

## 8. Transformer Differential IED

The Transformer workspace has two no-hardware entry points: deterministic self-test and synchronized two-sided internal injection.

### 8.1 Packaged-core self-test

1. select **Transformer Differential · 87T / REF**;
2. run the 10-scenario self-test;
3. expect `PASS · 10/10 · transformer-public-beta-v1`;
4. review/copy the case evidence before changing settings.

The suite covers through-current stability, internal 87T, 87T-HS, H2/H5 security, external-fault/CT-distortion context, internal-fault dependability, REF HV/LV, and independent neutral-current requirements.

### 8.2 Two-sided internal injection

The internal transformer source can generate synchronized:

- HV IA/IB/IC/IN;
- LV IA/IB/IC/IN;
- explicit independent neutral/NGR current per side.

Use Balanced through load, Internal fault, REF HV/NGR, and REF LV/NGR baselines before moving to external process-bus data. Stable through-load generation uses the active transformer engineering configuration so compensated currents cancel appropriately.

### 8.3 Paired-SV live/replay evaluation

When using external PCAP or live Sampled Values, the transformer runtime requires two distinct intended HV/LV streams. Configure transformer MVA, HV/LV voltage, vector group, CT ratios/polarity, and neutral inputs, then verify stream identity, `smpCnt`, `smpSynch`, frequency, mapping, scaling, and trust.

Calculated phase residual is not silently promoted to independent neutral-CT evidence for REF.

## 9. AVR / OLTC Controller and MMS

The AVR workspace models a transformer plant, 17-position OLTC, voltage-regulation logic, authority/interlocks, reports, and virtual IEC 61850 controls.

Laboratory MMS behavior includes browse/read, DataSets, GI/integrity/event reporting, modeled SBO/SBOw controls, and virtual AVR settings.

**Important:** MMS control exists, but it terminates inside the simulator. It does not provide a physical OLTC motor command, switching authority, or permission to operate primary equipment.

Recommended first evaluation:

1. select **AVR · OLTC Controller**;
2. inspect transformer/tap state and LOCAL/REMOTE + AUTO/MANUAL authority;
3. observe voltage-regulation behavior using the internal plant;
4. enable the MMS server only when testing an authorized laboratory client;
5. browse/read first, then reports, then modeled controls;
6. verify every accepted command changes only virtual state and is preserved in evidence.

## 10. Review and export evidence

A useful evidence package identifies:

- ARVREL version and source commit when applicable;
- source mode and source/run identity;
- injection profile or capture identity;
- virtual wiring/topology when closed-loop timing is relevant;
- active settings and fingerprint;
- CT/VT or transformer engineering context;
- trust state and permissions;
- measured quantities;
- first-any pickup, operated-element pickup/trip, TESTSET BI timing, and timing resolution where applicable;
- event trace and operation cause;
- known limitations and manual interpretation.

Closed-loop evidence schema 9 is designed to keep generic pickup, operated-element timing, relay trip request, TESTSET BI acceptance, and frozen capture causally distinct.

## 11. Troubleshooting

### Relay trips but TESTSET shows no trip

Check the virtual BO1→BI1 wire, relay BO1 contact state, TESTSET BI1 raw/accepted state, deglitch configuration, and timeline. This result is correct when the trip wire is intentionally disconnected.

### BI2 is earlier than the operated element pickup

This can be correct. BI2 is generic ANY PICKUP; another enabled element may assert BO2 before the element that eventually trips.

### RESET appears not ready

Allow the one-click reset transaction to reach **READY TO RE-ARM**. If it reports a bounded settle failure, capture the diagnostic state; do not repeatedly click RESET to hide the condition.

### Internal injection does not operate

Confirm output is running, the intended profile is active, the element is enabled, thresholds/delays are appropriate, and the causal relay measurement has crossed pickup.

### Transformer self-test fails

Copy the full deterministic evidence, record exact package/version and Windows build, and file a reproducible issue. Do not tune settings simply to force a pass.

### Transformer live/replay says two streams are required

That is expected for the external paired-SV path. Internal two-sided injection is a separate no-external-stream path.

### No live adapters or packets

Confirm Npcap, adapter permissions, publisher/VLAN path, and approved laboratory topology. Use replay to separate capture issues from parser/protection behavior.

### Measurements are visible but pickup/trip is blocked

Review `AllowsMeasurement`, `AllowsPickup`, `AllowsTrip`, continuity, quality, identity, SCL binding, mapping, scaling, freshness, and complete-window status.

### Windows blocks the package

Verify SHA-256 and release provenance, then follow local IT policy. Community packages are not claimed as Authenticode-signed.

## 12. Data handling

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`.

Do not publish customer captures, proprietary SCL files, credentials, IP plans, protected infrastructure information, employer-confidential evidence, or files you are not authorized to redistribute.

## 13. Safety boundary

ARVREL provides no physical relay contacts, operational GOOSE trip, physical OLTC motor authority, autonomous switching, IEC 61850 conformance certification, IEC 60255 type-test/calibration evidence, commissioning acceptance, calibrated secondary injection, or deterministic protection-grade hard-real-time guarantee.

Modeled MMS control is limited to the virtual AVR/OLTC process. Closed-loop TESTSET timing is a behavioral software model, not calibrated test-equipment timing.

## Related documentation

- [Current shipped status](CURRENT_STATUS.md)
- [Documentation hub](https://masarray.github.io/arvrel/documentation.html)
- [Five-minute quick start](https://masarray.github.io/arvrel/quick-start.html)
- [Architecture](ARCHITECTURE.md)
- [P0 metrology-grade timing engine](P0_METROLOGY_GRADE_TIMING_ENGINE.md)
- [Transformer public test](TRANSFORMER_PUBLIC_TEST.md)
- [AVR IEC 61850 SAS test](AVR-IEC61850-SAS-TEST.md)
- [Safety and limitations](https://masarray.github.io/arvrel/safety-and-limitations.html)
- [Engineering FAQ](https://masarray.github.io/arvrel/faq.html)
