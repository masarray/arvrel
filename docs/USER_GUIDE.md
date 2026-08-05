# ARVREL User Guide

ARVREL is a Windows virtual protection relay laboratory for observing IEC 61850 Sampled Values or internally generated signals, evaluating protection behavior, and preserving reviewable engineering evidence.

This guide is for first-time users, protection engineers, process-bus engineers, FAT/SAT preparation teams, educators, and researchers.

## 1. Choose the evaluation path

| Path | Use it when | Additional requirement |
|---|---|---|
| Internal laboratory | You want a deterministic first evaluation without external equipment | None |
| PCAP replay | You have an authorized capture and want repeatable offline analysis | PCAP or PCAPNG file |
| Live Sampled Values | You are connected to an isolated, authorized laboratory network | Npcap and a suitable adapter |
| Source development | You need to inspect, build, test, or modify the application | .NET 8 SDK, Git, sibling ARIEC61850 repository |

Start with the internal laboratory. Move to replay only after the internal workflow is understood. Use live capture last.

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
6. Apply the available A-G internal scenario in `v0.1.0-beta.1`.

The current `main` development line adds the P4 editable 4I+4V Virtual Injection Laboratory. It becomes an official packaged capability only after a release tag includes it.

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

## 4. Virtual Injection Laboratory on the development line

The P4 source workspace models an internal software secondary-injection source.

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

Supported public elements are 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N.

Before enabling an element:

1. verify current and voltage scaling;
2. verify phase order and residual-channel provenance;
3. confirm the intended setting group;
4. record the settings fingerprint;
5. understand the operating quantity and timing mode;
6. confirm trust permissions;
7. define the expected operate and restrain outcomes.

Feeder elements default to disabled until explicitly configured.

## 9. Review and export evidence

A useful evidence package should identify:

- ARVREL version and source commit when applicable;
- source mode and source identity;
- capture or injection profile identity;
- active settings group and fingerprint;
- CT/VT context;
- trust state and permission decisions;
- measured quantities;
- pickup and trip timestamps;
- operated element and phase or earth cause;
- event trace;
- known limitations and any manual interpretation.

Software evidence supports review and reproducibility. It is not calibration, conformance, type-test, or commissioning-acceptance evidence.

## 10. Troubleshooting

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

Verify CT/VT context, SCL scaling, channel mapping, units, and phase/residual provenance. Do not compensate by changing protection settings until the measurement source is understood.

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

## 11. Data handling

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`.

Do not publish:

- customer packet captures;
- proprietary SCL files;
- credentials or IP plans;
- employer-confidential logs;
- evidence containing protected infrastructure information;
- files you are not authorized to redistribute.

Use synthetic or contributor-owned fixtures for public bug reports.

## 12. Safety boundary

ARVREL provides no physical relay contacts, operational GOOSE trip, MMS control, autonomous switching, switching authority, IEC 61850 conformance certification, IEC 60255 type-test evidence, calibrated output, or deterministic hard-real-time guarantee.

Use ARVREL for education, controlled laboratory evaluation, source review, research, and FAT/SAT preparation. Do not use it as the sole basis for operational settings, commissioning acceptance, or switching decisions.

## Related documentation

- [Documentation hub](https://masarray.github.io/arvrel/documentation.html)
- [Five-minute quick start](https://masarray.github.io/arvrel/quick-start.html)
- [Capabilities](https://masarray.github.io/arvrel/capabilities.html)
- [Evidence and trust](https://masarray.github.io/arvrel/evidence-and-trust.html)
- [Safety and limitations](https://masarray.github.io/arvrel/safety-and-limitations.html)
- [Virtual Injection Laboratory](P4_VIRTUAL_INJECTION.md)
- [Windows setup](WINDOWS_SETUP.md)
- [Engineering FAQ](https://masarray.github.io/arvrel/faq.html)
