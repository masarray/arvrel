# ARVREL User Guide

ARVREL is a Windows virtual protection relay laboratory for observing IEC 61850 Sampled Values or internally generated signals, evaluating protection behavior, and preserving reviewable engineering evidence.

This guide is for first-time users, protection engineers, process-bus engineers, FAT/SAT preparation teams, educators, and researchers.

## 1. Choose the evaluation path

| Path | Use it when | Additional requirement |
|---|---|---|
| Internal laboratory | You want a deterministic first evaluation without external equipment | None |
| Transformer deterministic self-test | You want to verify the packaged 87T/REF/P13 protection core before connecting process-bus data | None |
| PCAP replay | You have an authorized capture and want repeatable offline analysis | PCAP or PCAPNG file |
| Live Sampled Values | You are connected to an isolated, authorized laboratory network | Npcap and a suitable adapter |
| Source development | You need to inspect, build, test, or modify the application | .NET 8 SDK, Git, sibling ARIEC61850 repository |

Start with deterministic internal testing. Move to replay only after the internal workflow is understood. Use live capture last.

## 2. Install and verify

1. Open the [download and verification page](https://masarray.github.io/arvrel/download.html).
2. Download the Windows installer or portable ZIP from GitHub Releases.
3. Download `SHA256SUMS.txt`.
4. Calculate SHA-256 locally and compare the result with the published checksum.
5. Review the [public release status](https://masarray.github.io/arvrel/release-status.html) for version, required assets, pinned engine, signing status, SBOM status, and output authority.

Unsigned community binaries may trigger Windows reputation warnings. Verification confirms file integrity; it does not make the package certified or signed.

## 3. First run: internal laboratory

1. Launch ARVREL.
2. Select **Internal demo**.
3. Review the source and stream-health status.
4. Open **Relay settings** and confirm which protection elements are enabled.
5. Review the CT/VT context and displayed units.
6. Use the available internal injection workflow to establish a known software baseline.

### What to inspect

- source mode and run state;
- active setting group and fingerprint;
- waveform and phasor coherence;
- phase, residual, and sequence quantities;
- `AllowsMeasurement`, `AllowsPickup`, and `AllowsTrip`;
- pickup indication and timer progression;
- operated element and phase or earth cause;
- virtual trip latch;
- event trace and operation evidence.

Do not judge a scenario only from the trip lamp. Review the evidence chain that permitted or blocked the operation.

## 4. Virtual Injection Laboratory

The source workspace models an internal software secondary-injection source.

### Configured versus effective values

Configured 4I+4V values remain armed while the source is stopped. Effective output is zero until **START** is applied.

- **START** energizes the configured profile.
- **STOP** returns effective output to zero.
- Editing while stopped changes armed values only.
- Editing while running applies a valid profile after validation.
- Invalid partial edits leave the last valid profile active.
- A newly accepted profile is visible immediately, while pickup and trip remain restrained until a complete coherent nominal cycle is rebuilt.

The source is not calibrated relay-test equipment.

## 5. PCAP replay

Use replay for repeatable investigation of an authorized capture.

1. Select the replay source.
2. Open a PCAP or PCAPNG file.
3. Select the intended stream.
4. Review APPID, destination MAC, VLAN, `svID`, dataset, `confRev`, mapping, scaling, quality, and continuity.
5. Confirm the measurement window becomes coherent.
6. Review waveform, phasors, sequence quantities, protection state, and trust permissions.
7. Record the replay file identity and ARVREL version with exported evidence.

A capture can contain customer or station-sensitive information. Use only files you are authorized to inspect and share.

## 6. Live Sampled Values capture

Live capture requires Npcap and an authorized, isolated laboratory network.

1. Install Npcap separately.
2. Confirm the selected adapter and Windows permissions.
3. Connect only to an approved laboratory segment.
4. Select the live source and intended adapter.
5. Bind the intended stream and, when available, its SCL context.
6. Confirm identity, mapping, scaling, quality, freshness, and continuity before interpreting protection behavior.
7. Stop capture before changing network topology or adapter configuration.

ARVREL does not provide switching authority. Never use it as the sole basis for operational decisions.

## 7. Understand trust before trip

ARVREL separates three permissions:

```text
AllowsMeasurement  → quantities may enter the measurement and display pipeline
AllowsPickup       → protection pickup and timing may be evaluated
AllowsTrip         → an operated element may assert the virtual trip latch
```

A stream can remain diagnostically visible while trust evidence blocks pickup or trip.

Typical trust inputs include:

- complete coherent measurement windows;
- payload decode health;
- live freshness;
- `smpCnt` continuity;
- quality words;
- mapping and scaling provenance;
- SCL binding;
- address identity;
- `svID`, dataset, and `confRev` consistency.

Duplicate and out-of-order frames remain visible in telemetry but their samples are discarded before measurement and protection ingestion.

## 8. Configure protection responsibly

Public-beta protection includes feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N plus the two-winding Transformer Differential IED workspace for 87T, 87T-HS, 87N-HV and 87N-LV evaluation.

Before enabling an element:

1. verify current and voltage scaling;
2. verify phase order and residual-channel provenance;
3. confirm the intended setting group;
4. record the settings fingerprint;
5. understand the operating quantity and timing mode;
6. confirm trust permissions;
7. define the expected operate and restrain outcomes.

Feeder and transformer protection elements default to disabled until explicitly configured.

## 9. Transformer Differential IED public test

The Transformer Differential IED has a deterministic first-test path that does not require Sampled Values hardware or a PCAP.

### 9.1 Run the packaged-core self-test first

1. Open **Transformer differential IED · 87T / REF** from the main toolbar. It can be opened while the main source remains **Internal Demo**.
2. Find **Public test / deterministic self-test**.
3. Press **RUN 10-SCENARIO SELF-TEST**.
4. The expected result is `PASS · 10/10 · transformer-public-beta-v1`.
5. Press **VIEW RESULT** to review all ten cases.
6. Press **COPY EVIDENCE** and retain the report if you are participating in public testing.

The suite exercises the same `TransformerProtectionEngine` used by the application and covers:

- compensated through-current stability;
- restrained internal 87T operation;
- 87T-HS;
- H2 and H5 harmonic security;
- external-fault security with delayed CT distortion;
- distorted internal-fault dependability so P13 cannot be validated by overblocking alone;
- HV and LV REF;
- the independent neutral-CT requirement for REF.

A failed deterministic self-test is a package/software regression signal. Do not change transformer settings to make the deterministic suite pass.

### 9.2 Continue to paired-SV evaluation

The no-SV path applies only to the deterministic self-test. The actual transformer runtime still requires two distinct HV/LV Sampled Values streams.

For PCAP replay or Live Npcap:

1. acquire or replay two intended transformer-side SV streams;
2. select HV and LV streams explicitly;
3. enter transformer MVA, HV/LV voltage, vector group and phase CT ratios;
4. provide independent neutral CT inputs before expecting REF authority;
5. review engineering compensation evidence;
6. verify `smpCnt`, `smpSynch`, frequency, stream identity and trust;
7. configure generic Is1/K1/Is2/K2 slope settings, harmonic security and optional P13 external-fault security;
8. apply the runtime;
9. interpret operate/block decisions from the authoritative runtime evidence rather than from a plotted characteristic alone;
10. export evidence when a result is being reviewed.

P13 intentionally distinguishes waveform distortion from a protection block. `CT DISTORTION · NO BLOCK` is evidence, not a trip/security decision. A restraint-leading external-fault context must arm before CT distortion can create a security hold.

See [Transformer public test guide](TRANSFORMER_PUBLIC_TEST.md) and [P15 engineering notes](P15_TRANSFORMER_PUBLIC_BETA_HARDENING.md).

## 10. Review and export evidence

A useful evidence package should identify:

- ARVREL version and source commit when applicable;
- source mode and source identity;
- capture or injection profile identity;
- active settings group and fingerprint;
- CT/VT or transformer/CT engineering context;
- trust state and permission decisions;
- measured quantities;
- pickup and trip timestamps;
- operated element and phase or earth cause;
- event trace;
- known limitations and any manual interpretation.

For a transformer public-test issue, include the copied deterministic self-test report before adding any non-sensitive Live/Replay evidence.

Software evidence supports review and reproducibility. It is not calibration, conformance, type-test, or commissioning-acceptance evidence.

## 11. Troubleshooting

### Transformer self-test fails

- copy the complete self-test evidence;
- record the exact ARVREL package/version and Windows build;
- state whether the installer or portable package was used;
- do not tune settings to compensate for a deterministic failure;
- file a reproducible issue using only non-sensitive attachments.

### Transformer runtime says two streams are required

That is expected for Live/Replay evaluation. The deterministic public self-test can run without SV, but the process-bus runtime requires distinct HV and LV streams.

### No live adapters or no packets

- confirm Npcap is installed;
- restart ARVREL after installing Npcap;
- verify adapter permissions;
- confirm the correct physical or virtual adapter;
- check that the laboratory publisher and VLAN path are active;
- use replay to separate capture issues from parser or protection behavior.

### Measurements are visible but pickup or trip is blocked

Review `AllowsMeasurement`, `AllowsPickup`, and `AllowsTrip`. Check continuity, quality, identity, SCL binding, mapping, scaling, freshness, and complete-window status.

### Values look incorrectly scaled

Verify CT/VT context, SCL scaling, channel mapping, units, and phase/residual provenance. For transformer differential, also verify transformer rating, HV/LV voltage, vector group, CT ratios, polarity and engineering compensation. Do not compensate by changing protection settings until the measurement source is understood.

### Internal injection does not operate

Confirm the source is running, the intended profile is active, a complete coherent cycle has rebuilt, the protection element is enabled, thresholds and delays are appropriate, and trust permits pickup and trip.

### Windows blocks the package

Verify the SHA-256 checksum and review the release status. Unsigned community packages may trigger Windows reputation warnings.

### Source build cannot find ARIEC61850

Place the repositories side by side:

```text
C:\Git\
├── ARIEC61850\
└── arvrel\
```

Then run:

```powershell
.\scripts\verify-sibling.cmd
.\scripts\build.cmd
```

## 12. Data handling

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`.

Do not publish:

- customer packet captures;
- proprietary SCL files;
- credentials or IP plans;
- employer-confidential logs;
- evidence containing protected infrastructure information;
- files you are not authorized to redistribute.

Use synthetic or contributor-owned fixtures for public bug reports.

## 13. Safety boundary

ARVREL provides no physical relay contacts, operational GOOSE trip, MMS control, autonomous switching, switching authority, IEC 61850 conformance certification, IEC 60255 type-test evidence, calibrated output, or deterministic hard-real-time guarantee.

The transformer deterministic self-test verifies packaged software behavior only. It does not prove a real CT, merging unit, network, relay output or substation protection scheme.

Use ARVREL for education, controlled laboratory evaluation, source review, research, and FAT/SAT preparation. Do not use it as the sole basis for operational settings, commissioning acceptance, or switching decisions.

## Related documentation

- [Documentation hub](https://masarray.github.io/arvrel/documentation.html)
- [Five-minute quick start](https://masarray.github.io/arvrel/quick-start.html)
- [Capabilities](https://masarray.github.io/arvrel/capabilities.html)
- [Evidence and trust](https://masarray.github.io/arvrel/evidence-and-trust.html)
- [Safety and limitations](https://masarray.github.io/arvrel/safety-and-limitations.html)
- [Transformer public test](TRANSFORMER_PUBLIC_TEST.md)
- [P15 Transformer Public Beta Hardening](P15_TRANSFORMER_PUBLIC_BETA_HARDENING.md)
- [Virtual Injection Laboratory](P4_VIRTUAL_INJECTION.md)
- [Windows setup](WINDOWS_SETUP.md)
- [Engineering FAQ](https://masarray.github.io/arvrel/faq.html)