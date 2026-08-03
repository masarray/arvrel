<div align="center">

# ARVREL

### Virtual Protection Relay Laboratory for IEC 61850 Sampled Values

Vendor-neutral Windows software for process-bus engineering, protection education, FAT/SAT preparation, algorithm research, waveform/phasor analysis, and virtual trip evidence.

[![Windows CI](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml)
[![Release](https://img.shields.io/github/v/release/masarray/arvrel?include_prereleases&label=beta)](https://github.com/masarray/arvrel/releases)
[![License: GPL-3.0-or-later](https://img.shields.io/badge/license-GPL--3.0--or--later-0b7285)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2563eb)](#system-requirements)
[![Safety boundary](https://img.shields.io/badge/output-virtual%20only-b45309)](#safety-boundary)

[Download beta](https://github.com/masarray/arvrel/releases) · [Windows setup](docs/WINDOWS_SETUP.md) · [Architecture](docs/ARCHITECTURE.md) · [Commercial licensing](COMMERCIAL-LICENSING.md) · [Security](SECURITY.md)

</div>

![ARVREL virtual protection relay laboratory](docs/assets/arvrel-main.png)

## What ARVREL is

ARVREL is a desktop virtual protection relay laboratory built around IEC 61850 Sampled Values. It combines a live/replay process-bus subscriber, coherent waveform and phasor instruments, configurable protection functions, a numerical-relay-style faceplate, deterministic research tooling, and exportable engineering evidence.

It is designed for:

- protection and substation-automation engineers;
- IEC 61850 training and university laboratories;
- process-bus integration and troubleshooting;
- FAT/SAT preparation and controlled demonstration;
- protection-algorithm research and reproducible software testing;
- analysis of SCL-bound and synthetic Sampled Values streams.

## Public beta status

The current target is **v0.1.0-beta.1**. The beta is suitable for source review, education, controlled laboratory testing, and engineering evaluation. It is not presented as a certified protection IED or a hard real-time trip platform.

Official release assets provide a self-contained Windows x64 installer and portable archive. Source-development builds use the sibling repositories:

```text
Git/
  ARIEC61850/
  arvrel/
```

## Core capabilities

### IEC 61850 process bus

- live Npcap Sampled Values capture;
- PCAP and PCAPNG replay;
- SCL import and SampledValueControl profile matching;
- APPID, destination MAC, VLAN, svID, datSet, confRev, quality, freshness, scaling, mapping, and `smpCnt` evidence;
- transactional duplicate/out-of-order rejection before measurement ingestion;
- coherent waveform/phasor hold during communication recovery;
- remembered SCL file and Npcap adapter;
- secondary and primary CT/VT display context.

### Measurement and phasors

- IA, IB, IC, IN/3I0 and VA, VB, VC, VN/3V0;
- newest one-cycle fundamental RMS phasors for protection;
- complete two-cycle waveform windows for visual evidence;
- positive-, negative-, and zero-sequence quantities;
- explicit residual-channel preference with calculated phase-sum fallback;
- current, voltage, and sequence phasor views;
- common VA reference convention where voltage evidence is available.

### Protection functions

| ANSI | Function | Implementation |
|---|---|---|
| 50P-1 | Instantaneous phase overcurrent | Pickup, dropout, definite delay |
| 51P | Time phase overcurrent | IEC inverse, definite time, TMS, reset modes |
| 50N | Instantaneous earth fault | Residual/neutral operating quantity |
| 51N | Time earth fault | IEC inverse, definite time, TMS, reset modes |
| 67P | Directional phase overcurrent | Positive-sequence V1/I1 polarization |
| 67N | Directional earth fault | Residual 3V0/3I0 polarization |
| 27 | Undervoltage | Phase-neutral, phase-phase, or V1; 1/2/3-of-3 logic |
| 59 | Overvoltage | Phase-neutral, phase-phase, or V1; 1/2/3-of-3 logic |
| 59N | Residual overvoltage | 3V0 magnitude |

Protection evidence captures the operating element, pickup and trip timestamps, operating quantity and unit, settings identity, SMV trust state, and latched phase/earth cause.

### Practitioner and research workflows

**Practitioner mode** provides familiar relay notation such as `I>`, `I0>`, `TMS`, `tI>`, `tMin`, and `tReset`, plus setting groups, revision, SHA-256 fingerprint, preset save/load, CT/VT context, and live curve-time previews.

**Research mode** exposes the exact active standard algorithm source as read-only evidence. Editable custom source is validated and staged as an immutable shadow artifact; it does not replace the running protection algorithm.

## Five-minute start

### Install a beta build

1. Download the Windows x64 installer or portable ZIP from [Releases](https://github.com/masarray/arvrel/releases).
2. Install Npcap separately when live capture is required.
3. Launch ARVREL.
4. Use **Internal demo** for the first test, or import an SCL file and select **Live Npcap**.
5. Open **Relay settings** and **CT/VT context**.
6. Use **Inject A-G fault** in the internal laboratory, or publish an authorized synthetic SV stream.
7. Review waveform, phasor, relay LCD, event trace, trip cause, and exported evidence.

### Build from source

Requirements: Windows 10/11 x64, .NET 8 SDK, Git, and Npcap for live capture.

```powershell
cd C:\Git\ARIEC61850
git pull origin main

cd C:\Git\arvrel
git pull origin main
.\scripts\build.cmd
.\scripts\run.cmd
```

Run deterministic tests:

```powershell
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
dotnet test .\tests\Arvrel.ProcessBus.Tests\Arvrel.ProcessBus.Tests.csproj -c Release
```

## SMV trust policy

ARVREL separates visibility from trip permission. A stream may remain measurable while virtual trip permission is blocked by recent communication or configuration evidence.

Typical trust states include:

- `HEALTHY`;
- `WINDOW_NOT_READY`;
- `STREAM_STALE`;
- `SMPCNT_DISCONTINUITY`;
- `QUALITY_INVALID`;
- `SCL_MISMATCH`;
- `SCALING_UNRESOLVED`;
- `SCL_UNBOUND`;
- `PAYLOAD_INVALID`.

Rejected duplicate and out-of-order frames are counted in evidence but their payload samples do not enter RMS, waveform, phasor, or protection buffers.

## Architecture

```text
Internal deterministic source / Live Npcap / PCAP replay
                         │
                         ▼
              ARIEC61850 parse + SCL model
                         │
                         ▼
     continuity · mapping · scaling · quality · trust gates
                         │
              ┌──────────┴──────────┐
              ▼                     ▼
      one-cycle measurement    two-cycle evidence
      RMS + phasors            waveform + phasor hold
              │                     │
              ▼                     ▼
      protection engine        WPF operator workspace
              │
              ▼
  virtual trip latch · operation record · evidence export
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/P2_MULTIFUNCTION_FEEDER.md`](docs/P2_MULTIFUNCTION_FEEDER.md).

## Safety boundary

ARVREL's standard public build is **virtual-output only**. It does not provide:

- physical relay contacts;
- operational GOOSE trip;
- MMS control;
- autonomous switching;
- switching authority or interlocking approval;
- IEC 61850 conformance certification;
- IEC 60255 type-test or calibration evidence;
- deterministic hard real-time guarantees.

Use live capture only on isolated, authorized laboratory networks. Do not use ARVREL as the sole basis for operational protection settings, commissioning acceptance, or switching decisions.

## Known beta limitations

- unsigned community binaries may trigger Windows reputation warnings;
- live performance depends on host scheduling, Npcap, drivers, publisher behaviour, adapter quality, and system load;
- 46, 47, 49, 81U/O, 32, 37, 50BF, 79, 25, 86, 74TCS, and 60 are not implemented;
- negative-sequence and memory polarization for 67N are deferred;
- broad clean-machine, multi-adapter, and long-duration field validation continues during beta.

## Evidence and privacy

ARVREL stores local preferences and logs under `%LOCALAPPDATA%\ARVREL`. Do not publish customer captures, proprietary station SCL files, credentials, IP plans, or employer-confidential information. Use synthetic or contributor-owned fixtures.

Crash log:

```text
%LOCALAPPDATA%\ARVREL\logs\arvrel-crash.log
```

## Release integrity

Official releases are produced by GitHub Actions and include, when available:

- self-contained Windows x64 installer;
- self-contained portable ZIP;
- SHA-256 checksums;
- NuGet transitive dependency report;
- CycloneDX SBOM;
- build and engine commit metadata;
- GPL, commercial-licensing, security, support, and third-party notices.

## Licensing

ARVREL source is licensed under **GPL-3.0-or-later**. GPL permits commercial use when its obligations are followed.

A separate commercial license may be negotiated for proprietary redistribution, closed-source integration, OEM deployment, contractual support, or other agreed terms. See [`COMMERCIAL-LICENSING.md`](COMMERCIAL-LICENSING.md).

Third-party components retain their own licenses. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Contributing, support, and security

- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`CLA.md`](CLA.md)
- [`SUPPORT.md`](SUPPORT.md)
- [`SECURITY.md`](SECURITY.md)
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)

## Citation

Research and teaching publications may cite the versioned software metadata in [`CITATION.cff`](CITATION.cff).

---

<div align="center">

**ARVREL — inspect the process bus, evaluate the protection logic, preserve the evidence.**

</div>
