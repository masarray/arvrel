# ARVREL current shipped status

> **Canonical shipped-state document.** This page describes the public Windows package currently released from `masarray/arvrel`. Historical `P*` milestone documents are retained as point-in-time engineering records; when a historical design note conflicts with this page, `VERSION`, `RELEASE-NOTES.md`, the release workflow, or the current source/tests, the shipped-state sources win.

## Public release

- Version: **v0.1.0-beta.6**
- Release channel: public engineering beta
- Platform: Windows 10/11 x64, WPF desktop product
- Official packages: per-user installer, single-file portable EXE, portable ZIP
- Release source of truth: GitHub Releases
- Output authority: **virtual only**
- Pinned IEC 61850 engine: `masarray/ARIEC61850` at `0d0aa4e31c17f9e5a10901ad52fa75e9c4581daf`

The beta.6 release workflow published SHA-256 checksums, dependency evidence, a CycloneDX SBOM, and GitHub build-provenance attestations. Community binaries are not claimed as Authenticode-signed unless a release explicitly says otherwise.

## Product surface

ARVREL currently ships three first-class virtual IED/workspace families:

1. **Protection Relay · OCR** — feeder protection, process-bus analysis, internal secondary injection, and a closed-loop virtual test-set/relay bench.
2. **Transformer Differential · 87T / REF** — two-winding 87T, 87T-HS, REF HV/LV, deterministic self-test, synchronized two-sided internal injection, and paired-SV live/replay engineering.
3. **AVR · OLTC Controller** — simulated transformer plant, 17-position OLTC, virtual authority/interlocks, IEC 61850 MMS browse/read, DataSets, reports, GI/integrity, and modeled controls that terminate inside the simulator.

Live IEC 61850 Sampled Values capture requires Npcap installed separately. PCAP/PCAPNG replay and internal laboratory workflows do not require Npcap.

## Feeder closed-loop test bench — authority model

Beta.6 makes the external virtual-I/O chain authoritative for test-set timing:

```text
TESTSET T0 / configured secondary source
  -> instantaneous waveform
  -> virtual analog wiring
  -> relay terminal sample
  -> relay clipping / ADC quantization / input delay
  -> causal rolling measurement window
  -> protection pickup / timer / trip request
  -> relay BO operate delay / contact behavior
  -> virtual binary wire
  -> independent TESTSET BI sampling / deglitch / debounce
  -> accepted BI edge
  -> measured pickup/trip time
  -> optional source auto-stop
```

**Invariant:** `ProtectionSnapshot.TripLatched` may request relay BO1, but it is never used directly as the TESTSET measured trip result or auto-stop authority. The authoritative trip result is the accepted wired `TESTSET.BI1` edge.

The disconnected-trip-wire regression therefore has a deliberate result: the relay may trip internally while the TESTSET records no trip and does not auto-stop because BO1 is not physically connected to BI1 in the virtual wiring model.

## Clock domains and behavioral profile

The default desktop closed-loop profile currently models:

| Domain | Shipped beta.6 behavior |
|---|---|
| TESTSET metrology clock | monotonic integer microseconds, 1 µs resolution |
| TESTSET BI sampling | 10 kHz / 100 µs sample period |
| BI deglitch | 0.5 ms |
| BI debounce holdoff | 0 ms |
| Relay processing / ADC grid | 4 kHz / 250 µs |
| Relay front-end delay | 1.5 ms behavioral group/input delay |
| Relay ADC | 16-bit equivalent behavioral model |
| Current full scale | 20 A RMS |
| Voltage full scale | 300 V RMS |
| Measurement | one nominal 50 Hz causal rolling DFT |

The relay front end consumes **instantaneous signed terminal samples**. Clipping and quantization occur before the causal measurement stage. Each stopped-source run starts from settled pre-fault history rather than an empty DFT window, avoiding an artificial one-cycle measurement blackout.

These values define a generic numerical-relay/test-set behavioral laboratory profile. They are not a calibration statement and do not claim to clone the internal topology of a named commercial relay or test set.

## Timing semantics visible to the operator

Beta.6 separates timing meanings that were previously easy to confuse:

- **RELAY ANY PU [source]** — first generic protection pickup that drives BO2.
- **TESTSET BI2 ACCEPT** — accepted edge on the wired generic ANY-PICKUP binary input.
- **Operated-element pickup** — pickup timestamp for the protection element that ultimately operates.
- **Operated-element P→T** — that element's own pickup-to-trip interval.
- **Relay trip request** — live relay trip-latch rising edge that requests BO1.
- **TESTSET BI1 ACCEPT** — external accepted trip input and authoritative measured trip time.

BI2 is intentionally **ANY PICKUP**. It may occur before the pickup of the element that eventually trips. Do not subtract generic BI2 time from an operated-element trip and label that interval as the element's P→T.

For a 50P definite-time setting of 60 ms that is exactly representable on the 250 µs relay grid, the reference protection engine records an element P→T of exactly **60.000 ms**.

## Frozen capture and one-click reset / re-arm

After a TESTSET BI1 auto-stop, the source output is OFF while the completed run evidence remains frozen for review. Beta.6 makes this state explicit as **OUTPUT OFF · FROZEN CAPTURE**.

Relay RESET is a deterministic equipment transaction, not a source restart:

1. if output is already OFF, advance the causal relay acquisition in 250 µs quanta until stale fault-window pickup drops out;
2. clear relay latch/timers once;
3. continue the modeled feedback path until relay trip latch is clear, no protection pickup remains, BO1/BO2 are LOW, and TESTSET BI1/BI2 are LOW;
4. report **READY TO RE-ARM** only after that postcondition is true.

The transaction has a bounded 100 ms simulated settle timeout with diagnostic state on failure. It preserves completed TESTSET timing and frozen trip/event evidence and does not mutate or restart the source. If RESET is pressed while the source remains energized, protection may legitimately reassert.

## Evidence status

Closed-loop exported evidence is schema **9** and includes the metrology profile, relative microsecond timing, first-any-pickup source, operated-element correlation, TESTSET timeline, causal front-end state, topology/run identity, and frozen capture relationship.

The WPF refresh cadence is presentation only. It does not own protection timers or test-set timing.

## Transformer Differential status

The shipped Transformer workspace includes:

- restrained 87T with generic Is1/K1/Is2/K2 slope semantics;
- 87T-HS;
- REF HV and REF LV with independent neutral/NGR current inputs;
- H2 inrush and H5 overexcitation security;
- external-fault / CT-saturation security with contextual arming;
- CT ratio, polarity, transformer rating, and supported vector-group compensation;
- deterministic 10-scenario packaged-core self-test;
- synchronized internal HV IA/IB/IC/IN and LV IA/IB/IC/IN secondary injection;
- paired HV/LV SV live/replay path with identity, synchronization, `smpCnt`, `smpSynch`, frequency, and trust evidence.

Expected deterministic packaged-core result:

```text
PASS · 10/10 · transformer-public-beta-v1
```

Calculated phase residual is not silently promoted to independent neutral-CT evidence for REF.

## AVR / OLTC and MMS status

The AVR workspace includes a simulated transformer plant and 17-position OLTC with modeled LOCAL/REMOTE and AUTO/MANUAL authority. Laboratory IEC 61850 MMS behavior includes browse/read, DataSets, reports, GI/integrity, modeled SBO/SBOw controls, and virtual AVR settings.

MMS control exists **only as virtual simulator authority**. It provides no physical OLTC motor command, primary switching authority, or permission to operate field equipment.

## Validation baseline

The beta.6 release line was published only after the release branch state passed:

- .NET CI and release build/test/audit gates;
- **403/403** deterministic tests in the final beta.6 feature baseline before release metadata updates;
- CodeQL analysis;
- cross-platform protection-core tests on Windows, macOS, and Ubuntu;
- Windows installer and portable package build;
- non-admin installer and portable single-file contract checks;
- release asset verification;
- build provenance and SBOM attestation.

Use the GitHub Actions history and selected GitHub Release as the authoritative run/asset record.

## Safety and fidelity boundary

ARVREL is not a calibrated relay test set, protection-grade hard-real-time platform, certified IEC 61850 IED, IEC 60255 type-tested relay, or commissioning-acceptance instrument. It provides no physical relay contact, operational GOOSE trip, physical OLTC motor authority, autonomous switching, or primary-equipment control.

Future device-specific fidelity may add measured anti-alias transfer functions, exact ADC topology, channel skew, input burden, real binary-input electrical thresholds, measured/statistical contact behavior, manufacturer-specific processing fast paths, and hardware-calibrated uncertainty. Those are not claimed by beta.6.

## Documentation authority order

For current public behavior, use this order:

1. selected GitHub Release and release assets;
2. `VERSION` and `RELEASE-NOTES.md`;
3. this `CURRENT_STATUS.md`;
4. README, User Guide, public Pages, architecture and capability documentation;
5. current source and regression tests for implementation detail;
6. historical `P*` milestone documents for design history only.
