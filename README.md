<div align="center">

# ARVREL

### IEC 61850 Sampled Values Virtual Protection Relay Laboratory

**Observe process-bus evidence. Evaluate protection behavior. Preserve the reason behind every virtual operation.**

[![Windows CI](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml)
[![Public site](https://github.com/masarray/arvrel/actions/workflows/pages.yml/badge.svg)](https://masarray.github.io/arvrel/)
[![Release](https://img.shields.io/github/v/release/masarray/arvrel?include_prereleases&label=public%20beta)](https://github.com/masarray/arvrel/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-0b7285)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2563eb)](#public-beta-status)
[![UI](https://img.shields.io/badge/desktop-WPF%20P6-334155)](#repository-scope)
[![Output](https://img.shields.io/badge/output-virtual%20only-b45309)](#engineering-and-safety-boundary)

[Product site](https://masarray.github.io/arvrel/) ·
[Documentation](https://masarray.github.io/arvrel/documentation.html) ·
[Quick start](https://masarray.github.io/arvrel/quick-start.html) ·
[Download](https://github.com/masarray/arvrel/releases)

</div>

![ARVREL Windows engineering workspace](docs/assets/arvrel-main.webp)

## Repository scope

This repository is the **stable Windows WPF edition** of ARVREL. Its public desktop product combines the P6 feeder virtual-relay interface with a dedicated two-winding Transformer Differential IED practitioner workspace, packaged for Windows 10/11 x64.

Cross-platform development is intentionally isolated in **[masarray/arvrel-avalonia](https://github.com/masarray/arvrel-avalonia)**. That repository is an engineering preview and has its own source tree, CI, migration status, and release decisions. Avalonia application source, tests, packaging, and migration workflows do not live in this repository.

This separation keeps the Windows download, documentation, issue scope, build instructions, and release history unambiguous for existing ARVREL users.

## Why ARVREL exists

Protection engineers need more than a pickup or trip result. They need to show which Sampled Values stream was accepted, whether identity and continuity were trustworthy, how quantities were derived, which settings were active, why an element operated or restrained, and what evidence remains available afterward.

ARVREL brings that cause-and-effect chain into one vendor-neutral Windows workspace.

| Observe | Evaluate | Prove |
|---|---|---|
| Inspect live, replayed, or internally generated signals; stream identity; continuity; quality; mapping; scaling; waveform; phasors; and sequence quantities. | Apply native settings and review pickup, timers, directional decisions, transformer differential restraint, harmonic/CT security, blocking, operation, and the virtual trip latch. | Preserve settings identity, trust state, timestamps, operating quantity, trip cause, event trace, fingerprints, and exportable evidence. |

## Public-beta status

| Item | Current position |
|---|---|
| Public release line | `v0.1.0-beta.3` |
| Desktop product | Windows WPF P6 feeder UX + two-winding Transformer Differential IED |
| Supported platform | Windows 10/11 x64 |
| Official packages | Per-user installer, portable EXE, and portable ZIP |
| Transformer first test | Built-in deterministic 10-scenario self-test; no SV/Npcap/PCAP required |
| Live capture | Npcap required separately |
| Output authority | Virtual output only |
| Intended use | Education, source review, controlled laboratory evaluation, FAT/SAT preparation, and research |
| Not claimed | Certified IED, calibrated relay test set, IEC 61850 conformance result, IEC 60255 type-test evidence, or hard-real-time trip platform |

The release page is the package source of truth. Features on `main` become public package features only after a release tag and integrity assets are published.

## Engineering capabilities

### Signal sources and process bus

- deterministic internal laboratory scenarios and editable 4I+4V virtual injection;
- live IEC 61850 Sampled Values capture through Npcap;
- PCAP and PCAPNG replay;
- SCL-assisted identity, dataset, mapping, scaling, and `confRev` review;
- APPID, destination MAC, VLAN, `svID`, continuity, freshness, and quality evidence;
- paired HV/LV SV selection for transformer protection with synchronization, `smpCnt`, `smpSynch`, frequency and trust checks.

### Measurement and visualization

- complete one-cycle fundamental phasor estimation;
- 4I+4V RMS phasors and positive-, negative-, and zero-sequence quantities;
- explicit residual channels with calculated phase-sum fallback for feeder measurement;
- coherent waveform evidence and phasor view;
- P6 native WPF relay faceplate with common physical lamp geometry, recessed LCD, hardware navigation, event pages, and operation records;
- transformer Idiff/Ibias characteristic display and authoritative per-phase runtime evidence.

### Protection and evidence

- setting groups, revisions, presets, and SHA-256 fingerprints;
- feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N elements;
- two-winding transformer 87T, 87T-HS, 87N-HV and 87N-LV elements;
- generic Is1/K1/Is2/K2 transformer differential slope semantics;
- H2/H5 transformer security plus context-gated external-fault / CT-saturation security;
- transformer rating, CT ratio, polarity and supported vector-group engineering;
- independent neutral-current requirement for REF; calculated phase residual is never silently promoted to neutral CT;
- pickup, definite/inverse timing, directional torque, trust blocking, operated-element attribution, and latched virtual trip;
- evidence export with settings identity, measurements, timestamps, causes, trust state, and source provenance;
- deterministic scenarios tied to automated regression tests.

## Transformer public test — start here

A tester does not need a merging unit, Npcap, or a field PCAP to perform the first Transformer Differential IED check.

1. Start ARVREL and open **Transformer differential IED · 87T / REF**.
2. The main source may remain **Internal Demo**.
3. Press **RUN 10-SCENARIO SELF-TEST**.
4. Expected result: `PASS · 10/10 · transformer-public-beta-v1`.
5. Use **VIEW RESULT** to inspect each scenario.
6. Use **COPY EVIDENCE** before filing a transformer defect.
7. Continue to paired-SV PCAP replay or Live Npcap only after this deterministic baseline passes.

The suite covers through-current stability, internal 87T, 87T-HS, H2/H5 security, external-fault CT-saturation security, distorted internal-fault dependability, HV/LV REF, and secure REF blocking without an independent neutral input.

See the [Transformer public test guide](docs/TRANSFORMER_PUBLIC_TEST.md).

## Trust before trip

ARVREL deliberately separates diagnostic visibility from protection authority:

```text
AllowsMeasurement  → quantities may enter measurement and display
AllowsPickup       → protection pickup and timing may be evaluated
AllowsTrip         → an operated element may assert the virtual trip latch
```

The trust pipeline evaluates complete windows, payload decode, freshness, `smpCnt` continuity, quality words, mapping, scaling provenance, SCL binding, address identity, `svID`, dataset, and `confRev` consistency. Duplicate and out-of-order frames remain visible in telemetry but are rejected before admission to measurement, waveform, phasor, or protection buffers.

For transformer differential, diagnostic waveform distortion also remains separate from protection authority: **distortion alone never creates a P13 security block**. A restraint-leading external-fault context must arm before qualified CT distortion can assert a security hold.

## Evaluate the Windows release

1. Download the installer or portable package from [GitHub Releases](https://github.com/masarray/arvrel/releases).
2. Verify it with the published `SHA256SUMS.txt`.
3. Start with **Internal demo** for feeder evaluation.
4. Open the Transformer Differential IED and run the deterministic 10-scenario self-test.
5. Review relay settings, CT/VT or transformer/CT engineering context, and copied evidence.
6. Use PCAP replay for repeatable process-bus evaluation before moving to live capture.
7. Install Npcap only for authorized capture on an isolated laboratory network.

See the [five-minute quick start](https://masarray.github.io/arvrel/quick-start.html), [user guide](docs/USER_GUIDE.md), and [Transformer public test guide](docs/TRANSFORMER_PUBLIC_TEST.md).

## Build the WPF edition from source

Source development expects ARVREL beside its pinned ARIEC61850 engine repository:

```text
C:\Git\
├── ARIEC61850\
└── arvrel\
```

```powershell
cd C:\Git\arvrel
.\scripts\verify-sibling.cmd
.\scripts\build.cmd
.\scripts\run.cmd
```

The root solution is authoritative:

```powershell
dotnet restore .\ARVREL.sln
dotnet build .\ARVREL.sln -c Release --no-restore
dotnet test .\ARVREL.sln -c Release --no-build
```

See [Windows setup](docs/WINDOWS_SETUP.md) for prerequisites and troubleshooting.

## Architecture at a glance

```text
Internal injection / Live Npcap / PCAP replay
                    │
                    ▼
Synthetic source / ARIEC61850 parse + SCL model
                    │
                    ▼
Continuity · quality · identity · mapping · scaling · trust
                    │
          ┌─────────┴────────────────┐
          ▼                          ▼
Feeder measurement             Paired HV/LV transformer path
RMS · phasors · sequence       sync · CT/vector engineering
          │                          │
          ▼                          ▼
Feeder protection             H1/H2/H5 · CT evidence · 87T/REF
          └──────────────┬───────────┘
                         ▼
            Virtual trip latch · evidence export
```

Protection evaluates when coherent measurements arrive, not when WPF renders. Runtime state is isolated and exposed through immutable snapshots. The P15 deterministic self-test invokes the same transformer protection core without pretending to validate the packet-capture path.

## Project layout

- `src/Arvrel.App` — Windows WPF product shell, P6 feeder interface, and transformer practitioner workspace;
- `src/Arvrel.Application` — deterministic laboratory orchestration used by the WPF product;
- `src/Arvrel.Capture` — capture contracts and PCAP/PCAPNG replay;
- `src/Arvrel.ProcessBus` — Sampled Values stream, trust, paired transformer measurement, and evidence runtime;
- `src/Arvrel.Protection` — UI-independent feeder/transformer protection and deterministic self-test logic;
- `tests/` — regression coverage for the Windows product and shared engineering core;
- `installer/` and `scripts/package-release.ps1` — official Windows packaging path.

## Engineering and safety boundary

ARVREL is virtual-output laboratory software. It does not provide physical relay contacts, operational GOOSE trip, MMS control, autonomous switching, switching authority, IEC 61850 conformance certification, IEC 60255 type-test or calibration evidence, or deterministic hard-real-time guarantees.

The Transformer Self-Test is deterministic software regression evidence only. It does not prove a real CT, merging unit, Ethernet network, relay binary output, or substation protection scheme.

Use live capture only on isolated, authorized laboratory networks. Do not use ARVREL as the sole basis for operational protection settings, commissioning acceptance, or switching decisions.

## Privacy, integrity, and licensing

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`. Do not publish customer captures, proprietary SCL files, credentials, IP plans, or employer-confidential information.

Official releases include Windows packages, SHA-256 checksums, dependency evidence, and—when generated—SBOM and build-provenance attestations.

ARVREL is licensed under **GPL-3.0-or-later**. Separate commercial terms may be negotiated for proprietary redistribution, closed-source integration, OEM deployment, or contractual support. See [Commercial licensing](COMMERCIAL-LICENSING.md) and [Third-party notices](THIRD-PARTY-NOTICES.md).

---

<div align="center">

**See the stream. Evaluate the protection. Preserve the evidence.**

</div>