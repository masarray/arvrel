<div align="center">

# ARVREL

### IEC 61850 Virtual Protection & Control IED Laboratory

**Observe process-bus evidence. Evaluate protection and control behavior. Preserve the reason behind every virtual operation.**

[![Windows CI](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml)
[![Public site](https://github.com/masarray/arvrel/actions/workflows/pages.yml/badge.svg)](https://masarray.github.io/arvrel/)
[![Release](https://img.shields.io/github/v/release/masarray/arvrel?include_prereleases&label=public%20beta)](https://github.com/masarray/arvrel/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-0b7285)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2563eb)](#public-beta-status)
[![UI](https://img.shields.io/badge/desktop-WPF%20P6-334155)](#repository-scope)
[![Output](https://img.shields.io/badge/output-virtual%20only-b45309)](#engineering-and-safety-boundary)

[Product site](https://masarray.github.io/arvrel/) ·
[Documentation](https://masarray.github.io/arvrel/documentation.html) ·
[Engineering FAQ](https://masarray.github.io/arvrel/faq.html) ·
[Quick start](https://masarray.github.io/arvrel/quick-start.html) ·
[Download](https://github.com/masarray/arvrel/releases)

</div>

![ARVREL Windows engineering workspace](docs/assets/arvrel-main.webp)

## Repository scope

This repository is the **stable Windows WPF edition** of ARVREL. Its public desktop product is a multi-IED laboratory combining the P6 feeder virtual relay, a two-winding Transformer Differential IED, and an AVR / OLTC Controller workspace for Windows 10/11 x64.

Cross-platform development is intentionally isolated in **[masarray/arvrel-avalonia](https://github.com/masarray/arvrel-avalonia)**. That repository is an engineering preview and has its own source tree, CI, migration status, and release decisions. Avalonia application source, tests, packaging, and migration workflows do not live in this repository.

This separation keeps the Windows download, documentation, issue scope, build instructions, and release history unambiguous for existing ARVREL users.

## Why ARVREL exists

Protection and substation-automation engineers need more than a pickup, trip, tap position, or accepted control result. They need to show which signal source was accepted, whether identity and continuity were trustworthy, how quantities were derived, which settings and authority were active, why an element operated or restrained, and what evidence remains available afterward.

ARVREL brings that cause-and-effect chain into one vendor-neutral Windows workspace.

| Observe | Evaluate | Prove |
|---|---|---|
| Inspect live, replayed, or internally generated signals; stream identity; continuity; quality; mapping; scaling; waveform; phasors; sequence quantities; AVR measurements; and OLTC state. | Apply native settings and review pickup, timers, directional decisions, transformer differential restraint, harmonic/CT security, AVR deadband, blocking, tap travel, reports, and virtual controls. | Preserve settings identity, trust state, timestamps, operating quantity, trip/control cause, event trace, fingerprints, and exportable evidence. |

## Public-beta status

| Item | Current position |
|---|---|
| Public release line | `v0.1.0-beta.5` |
| Desktop product | Windows WPF multi-IED lab: OCR feeder relay + Transformer Differential 87T/REF + AVR/OLTC Controller |
| Supported platform | Windows 10/11 x64 |
| Official packages | Per-user installer, portable EXE, and portable ZIP |
| Transformer first test | Deterministic 10-scenario self-test plus synchronized HV/LV + independent NGR internal secondary injection |
| AVR first test | Built-in simulated transformer plant and 17-position OLTC |
| Live capture | Npcap required separately |
| Output authority | Virtual output only |
| Intended use | Education, source review, controlled laboratory evaluation, FAT/SAT preparation, interoperability study, and research |
| Not claimed | Certified IED, calibrated relay test set, IEC 61850 conformance result, IEC 60255 type-test evidence, or hard-real-time trip/control platform |

The release page is the package source of truth. Features on `main` become public package features only after a release tag and integrity assets are published.

## Engineering capabilities

### Signal sources and process bus

- deterministic internal feeder laboratory scenarios and editable 4I+4V virtual injection;
- synchronized Transformer Differential internal injection with HV IA/IB/IC/IN and LV IA/IB/IC/IN;
- independent neutral / NGR availability for REF HV and REF LV;
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
- transformer single-line LCD with HV/LV secondary currents, independent neutral indication and authoritative per-phase Idiff;
- transformer Idiff/Ibias characteristic display and authoritative per-phase runtime evidence.

### Protection, control, and evidence

- setting groups, revisions, presets, and SHA-256 fingerprints;
- feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N elements;
- two-winding transformer 87T, 87T-HS, REF HV and REF LV elements;
- generic Is1/K1/Is2/K2 transformer differential slope semantics;
- H2/H5 transformer security plus context-gated external-fault / CT-saturation security;
- transformer rating, CT ratio, polarity and supported vector-group engineering;
- independent neutral-current requirement for REF; calculated phase residual is never silently promoted to neutral CT;
- AVR / OLTC virtual controller with simulated transformer plant, 17 tap positions, REMOTE/LOCAL and AUTO/MANUAL authority;
- laboratory IEC 61850 MMS browse/read, DataSets, reports, GI/integrity, modeled SBO/SBOw controls and virtual AVR settings;
- pickup, definite/inverse timing, directional torque, trust blocking, operated-element attribution, latched virtual trip, and modeled virtual-control evidence;
- evidence export with settings identity, measurements, timestamps, causes, trust state, and source provenance;
- deterministic scenarios tied to automated regression tests.

## Transformer public test — start here

A tester does not need a merging unit, Npcap, or a field PCAP to perform the first Transformer Differential IED checks.

1. Start ARVREL and select **Transformer Differential · 87T / REF**.
2. Keep **SOURCE = Internal demo**.
3. Run the deterministic 10-scenario self-test; expected result is `PASS · 10/10 · transformer-public-beta-v1`.
4. Open **INJECTION** and select **Balanced through load**; verify low Idiff and no 87T operation.
5. Apply **Internal A fault** and verify restrained 87T pickup / operation / virtual trip latch.
6. Reset and test **REF HV / NGR**; verify only REF HV operates.
7. Reset and test **REF LV / NGR**; verify only REF LV operates.
8. Continue to paired-SV PCAP replay or Live Npcap only after these deterministic baselines pass.

The internal Transformer source creates synchronized HV/LV snapshots and passes them through the same `TransformerProtectionRuntime` used by the protection path. It does not implement a second 87T/REF algorithm and does not transmit synthetic Ethernet SV.

See [P18 Transformer two-sided injection](docs/P18_TRANSFORMER_TWO_SIDED_INJECTION.md) and the [Transformer public test guide](docs/TRANSFORMER_PUBLIC_TEST.md). Public-site maintenance and release-documentation contracts are described in [docs/PUBLIC_SITE.md](docs/PUBLIC_SITE.md).

## Trust before trip or virtual control

ARVREL deliberately separates diagnostic visibility from protection/control authority:

```text
AllowsMeasurement  → quantities may enter measurement and display
AllowsPickup       → protection pickup and timing may be evaluated
AllowsTrip         → an operated element may assert the virtual trip latch
Virtual control    → modeled commands may change only the virtual AVR/OLTC process when interlocks permit
```

The trust pipeline evaluates complete windows, payload decode, freshness, `smpCnt` continuity, quality words, mapping, scaling provenance, SCL binding, address identity, `svID`, dataset, and `confRev` consistency. Duplicate and out-of-order frames remain visible in telemetry but are rejected before admission to measurement, waveform, phasor, or protection buffers.

For transformer differential, diagnostic waveform distortion also remains separate from protection authority: **distortion alone never creates a CT-saturation security block**. A restraint-leading external-fault context must arm before qualified CT distortion can assert a security hold.

## Evaluate the Windows release

1. Download the installer or portable package from [GitHub Releases](https://github.com/masarray/arvrel/releases).
2. Verify it with the published `SHA256SUMS.txt`.
3. Start with **Internal demo** for feeder evaluation.
4. Select the Transformer Differential IED and run both deterministic self-test and two-sided injection checks.
5. Select **AVR · OLTC Controller** and evaluate the built-in transformer/OLTC plant before enabling laboratory MMS.
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
Internal injection / Live Npcap / PCAP replay / virtual AVR plant
                    │
                    ▼
Synthetic source / ARIEC61850 parse + SCL model
                    │
                    ▼
Continuity · quality · identity · mapping · scaling · trust / authority
                    │
          ┌─────────┼─────────────────────┐
          ▼         ▼                     ▼
Feeder path    Paired HV/LV path      AVR / OLTC process
RMS/phasors    sync · CT/vector       measurements · interlocks
          │         │                     │
          ▼         ▼                     ▼
50/51/67...    H1/H2/H5 · 87T/REF     AVR logic · reports · virtual controls
          └─────────┴──────────┬──────────┘
                               ▼
                  Virtual state · evidence export
```

Protection evaluates when coherent measurements arrive, not when WPF renders. Runtime state is isolated and exposed through immutable snapshots. The deterministic Transformer self-test and internal two-sided injector use the existing protection runtime without pretending to validate external test hardware or packet-capture transport.

## Project layout

- `src/Arvrel.App` — Windows WPF multi-IED product shell, feeder/Transformer relay interfaces, AVR/OLTC workspace, and engineering UI;
- `src/Arvrel.Application` — deterministic laboratory orchestration used by the WPF product;
- `src/Arvrel.Capture` — capture contracts and PCAP/PCAPNG replay;
- `src/Arvrel.ProcessBus` — Sampled Values stream, trust, paired transformer measurement, virtual transformer injection, and evidence runtime;
- `src/Arvrel.Protection` — UI-independent feeder/transformer protection and deterministic self-test logic;
- `tests/` — regression coverage for the Windows product and shared engineering core;
- `installer/` and `scripts/package-release.ps1` — official Windows packaging path.

## Engineering and safety boundary

ARVREL is virtual-output laboratory software. It does not provide physical relay contacts, operational GOOSE trip, physical OLTC motor authority, autonomous switching, switching authority, IEC 61850 conformance certification, IEC 60255 type-test or calibration evidence, or deterministic hard-real-time guarantees.

ARVREL can model IEC 61850 MMS controls for the **virtual AVR/OLTC process**. Those commands terminate inside the software simulation and do not provide primary-equipment authority.

The Transformer Self-Test and internal two-sided injection are deterministic software evidence only. They do not prove a real CT, secondary-injection set, merging unit, Ethernet network, relay binary output, or substation protection scheme.

Use live capture or protocol testing only on isolated, authorized laboratory networks. Do not use ARVREL as the sole basis for operational protection settings, AVR settings, commissioning acceptance, or switching decisions.

## Privacy, integrity, and licensing

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`. Do not publish customer captures, proprietary SCL files, credentials, IP plans, or employer-confidential information.

Official releases include Windows packages, SHA-256 checksums, dependency evidence, and—when generated—SBOM and build-provenance attestations.

ARVREL is licensed under **GPL-3.0-or-later**. Separate commercial terms may be negotiated for proprietary redistribution, closed-source integration, OEM deployment, or contractual support. See [Commercial licensing](COMMERCIAL-LICENSING.md) and [Third-party notices](THIRD-PARTY-NOTICES.md).

---

<div align="center">

**See the stream. Evaluate the protection and control. Preserve the evidence.**

</div>
