<div align="center">

# ARVREL

### IEC 61850 Sampled Values Virtual Protection Relay Laboratory

**Observe process-bus evidence. Evaluate protection behavior. Preserve the reason behind every virtual operation.**

[![Windows CI](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml)
[![Public site](https://github.com/masarray/arvrel/actions/workflows/pages.yml/badge.svg)](https://masarray.github.io/arvrel/)
[![Release](https://img.shields.io/github/v/release/masarray/arvrel?include_prereleases&label=public%20beta)](https://github.com/masarray/arvrel/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-0b7285)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2563eb)](#public-beta-status)
[![Output](https://img.shields.io/badge/output-virtual%20only-b45309)](#engineering-and-safety-boundary)

[Product site](https://masarray.github.io/arvrel/) ·
[Documentation hub](https://masarray.github.io/arvrel/documentation.html) ·
[Quick start](https://masarray.github.io/arvrel/quick-start.html) ·
[Research and validation](https://masarray.github.io/arvrel/research/) ·
[FAQ](https://masarray.github.io/arvrel/faq.html) ·
[Download and verify](https://masarray.github.io/arvrel/download.html)

</div>

![ARVREL engineering workspace for IEC 61850 Sampled Values and virtual protection](docs/assets/arvrel-main.webp)

## Why ARVREL exists

Protection engineers often need to explain more than a pickup or trip result. They need to show which Sampled Values stream was accepted, whether identity and continuity were trustworthy, how quantities were derived, which settings were active, why an element operated or restrained, and what evidence can be reviewed later.

ARVREL brings that cause-and-effect chain into one vendor-neutral Windows workspace.

| Observe | Evaluate | Prove |
|---|---|---|
| Inspect live, replayed, or internally generated signals; stream identity; continuity; quality; mapping; scaling; waveform; phasors; and sequence quantities. | Apply native settings and review pickup, timers, directional decisions, blocking, operation, and the virtual trip latch. | Preserve settings identity, trust state, timestamps, operating quantity, trip cause, event trace, fingerprints, and exportable evidence. |

## Who it is for

- protection and control engineers investigating feeder protection behavior;
- substation-automation and process-bus engineers validating IEC 61850 Sampled Values integration;
- FAT/SAT teams preparing repeatable laboratory evidence before site execution;
- universities teaching process bus, phasors, protection logic, and engineering traceability;
- researchers who need deterministic scenarios tied to exact source and automated tests;
- developers reviewing an open, vendor-neutral protection-laboratory architecture.

## Public-beta status

| Item | Current position |
|---|---|
| Public release | `v0.1.0-beta.1` |
| Development line | P4 Virtual Injection Laboratory and modeless advanced injection workspace |
| Supported platform | Windows 10/11 x64 |
| Official packages | Self-contained installer and portable ZIP |
| Live capture | Npcap required separately |
| Output authority | Virtual output only |
| Intended use | Education, source review, controlled laboratory evaluation, FAT/SAT preparation, and research |
| Not claimed | Certified IED, calibrated relay test set, IEC 61850 conformance result, IEC 60255 type-test evidence, or hard-real-time trip platform |

The release page is the package source of truth. Features present on `main` become packaged features only after a release tag and integrity assets are published.

## Engineering capabilities

### Signal sources and process bus

- internal deterministic laboratory scenarios;
- current-source P4 editable 4I+4V virtual injection with RMS, angle, enable state, frequency, and neutral provenance;
- live IEC 61850 Sampled Values capture through Npcap;
- PCAP and PCAPNG replay;
- SCL-assisted identity, dataset, mapping, scaling, and `confRev` review;
- APPID, destination MAC, VLAN, `svID`, continuity, freshness, and quality evidence.

### Measurement and visualization

- complete one-cycle fundamental phasor estimation;
- 4I+4V RMS phasors;
- positive-, negative-, and zero-sequence quantities;
- explicit residual channels with calculated phase-sum fallback;
- coherent waveform evidence, phasor view, relay faceplate, event trace, and operation record.

### Protection and evidence

- native settings, setting groups, revisions, presets, and SHA-256 fingerprints;
- virtual 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N elements;
- pickup, definite/inverse timing, directional torque, trust blocking, operated-element attribution, and latched virtual trip;
- evidence export with settings identity, measurements, timestamps, causes, trust state, and source provenance;
- deterministic public scenarios mapped to exact source files and automated test methods.

## Trust before trip

ARVREL deliberately separates diagnostic visibility from protection authority:

```text
AllowsMeasurement  → quantities may enter the measurement and display pipeline
AllowsPickup       → protection pickup and timing may be evaluated
AllowsTrip         → an operated element may assert the virtual trip latch
```

The trust pipeline evaluates complete measurement windows, payload decode, freshness, `smpCnt` continuity, quality words, mapping, scaling provenance, SCL binding, address identity, `svID`, dataset, and `confRev` consistency.

Duplicate and out-of-order frames remain visible in telemetry, but their samples are rejected before entering measurement, waveform, phasor, or protection buffers.

## Protection coverage

| ANSI | Function | Public implementation | Explicit boundary |
|---|---|---|---|
| 50P-1 | Instantaneous phase overcurrent | Pickup, dropout, definite delay | Virtual operation only |
| 51P | Time phase overcurrent | IEC inverse, definite time, TMS, reset modes | No IEC 60255 type-test claim |
| 50N | Instantaneous earth fault | Explicit IN/3I0 preferred; calculated residual fallback | Virtual operation only |
| 51N | Time earth fault | IEC inverse, definite time, TMS, reset modes | No IEC 60255 type-test claim |
| 67P | Directional phase overcurrent | Positive-sequence V1/I1 polarization | No memory polarization |
| 67N | Directional earth fault | Residual 3V0/3I0 polarization | Negative-sequence and memory polarization deferred |
| 27 | Undervoltage | Phase-neutral, phase-phase, or V1; 1/2/3-of-3 logic | Trust-gated measurement provenance |
| 59 | Overvoltage | Phase-neutral, phase-phase, or V1; 1/2/3-of-3 logic | Trust-gated measurement provenance |
| 59N | Residual overvoltage | 3V0 magnitude and definite delay | Virtual operation only |

Feeder elements default to disabled until explicitly configured.

## Evaluate ARVREL

### Official Windows package

Use this path for first evaluation.

1. Download the installer or portable ZIP from [GitHub Releases](https://github.com/masarray/arvrel/releases).
2. Verify the package using the published `SHA256SUMS.txt`.
3. Start with **Internal demo**.
4. Review relay settings and CT/VT context.
5. Apply the available internal A-G scenario or the editable **INJECT** workspace in a later package that includes P4.
6. Review trust, waveform, phasors, relay state, pickup/trip timing, event trace, and operation evidence.
7. Install Npcap only for authorized live capture on an isolated laboratory network.

Follow the [five-minute quick start](https://masarray.github.io/arvrel/quick-start.html) or the complete [user guide](docs/USER_GUIDE.md).

### Build from source

Source development expects ARVREL beside its pinned ARIEC61850 engine repository.

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

Run deterministic tests:

```powershell
dotnet test .\tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj -c Release
dotnet test .\tests\Arvrel.ProcessBus.Tests\Arvrel.ProcessBus.Tests.csproj -c Release
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
          ┌─────────┴─────────┐
          ▼                   ▼
One-cycle measurement    Two-cycle evidence
RMS · phasors · sequence waveform · coherent hold
          │                   │
          ▼                   ▼
Protection engine        WPF engineering workspace
          │
          ▼
Virtual trip latch · operation record · evidence export
```

Protection evaluates when coherent measurements arrive, not when WPF renders. Runtime state is isolated per stream and exposed to the UI through immutable snapshots.

## Documentation

| Start and operate | Engineering detail | Trust, research, and project |
|---|---|---|
| [Documentation hub](https://masarray.github.io/arvrel/documentation.html) | [Architecture](docs/ARCHITECTURE.md) | [Evidence and trust](https://masarray.github.io/arvrel/evidence-and-trust.html) |
| [Five-minute quick start](https://masarray.github.io/arvrel/quick-start.html) | [Multifunction feeder protection](docs/P2_MULTIFUNCTION_FEEDER.md) | [Research guide](RESEARCH.md) |
| [User guide](docs/USER_GUIDE.md) | [Virtual injection laboratory](docs/P4_VIRTUAL_INJECTION.md) | [Validation matrix](https://masarray.github.io/arvrel/research/validation.html) |
| [FAQ](https://masarray.github.io/arvrel/faq.html) | [Windows setup](docs/WINDOWS_SETUP.md) | [Public-site and SEO maintenance](docs/PUBLIC_SITE.md) |
| [Download and verify](https://masarray.github.io/arvrel/download.html) | [Capabilities](https://masarray.github.io/arvrel/capabilities.html) | [Roadmap](https://masarray.github.io/arvrel/roadmap.html) |
| [Workflow router](https://masarray.github.io/arvrel/workflows/) | [Release checklist](docs/RELEASE_CHECKLIST.md) | [Security](SECURITY.md) |

## Research and reproducibility

The current fundamental estimator is a complete one-cycle, arithmetic-mean-removed, nominal-frequency **single-bin DFT** scaled to a complex RMS phasor. ARVREL does not claim a full harmonic-spectrum FFT, adaptive frequency tracking, calibrated phasor accuracy, or IEC 60255 type-test performance.

Public research material includes application notes, deterministic scenario IDs, exact test-method references, laboratory exercises, related-work positioning, and an evidence-gated roadmap:

- [Research and validation hub](https://masarray.github.io/arvrel/research/)
- [Signal-processing application note](https://masarray.github.io/arvrel/research/signal-processing.html)
- [SMV continuity and trust](https://masarray.github.io/arvrel/research/smv-continuity.html)
- [Directional 67P and 67N](https://masarray.github.io/arvrel/research/directional-protection.html)
- [Deterministic validation matrix](https://masarray.github.io/arvrel/research/validation.html)
- [Laboratory exercises](https://masarray.github.io/arvrel/laboratory-exercises.html)
- [Machine-readable scenarios](docs/data/research-scenarios.json)

## Engineering and safety boundary

ARVREL is virtual-output laboratory software. It does not provide physical relay contacts, operational GOOSE trip, MMS control, autonomous switching, switching authority, IEC 61850 conformance certification, IEC 60255 type-test or calibration evidence, or deterministic hard-real-time guarantees.

Use live capture only on isolated, authorized laboratory networks. Do not use ARVREL as the sole basis for operational protection settings, commissioning acceptance, or switching decisions.

## Privacy, integrity, and licensing

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`. Do not publish customer captures, proprietary SCL files, credentials, IP plans, or employer-confidential information.

Official release assets include, when available, installer and portable packages, SHA-256 checksums, dependency reports, a CycloneDX SBOM, and build/engine commit metadata.

ARVREL is licensed under **GPL-3.0-or-later**. Separate commercial terms may be negotiated for proprietary redistribution, closed-source integration, OEM deployment, or contractual support. See [Commercial licensing](COMMERCIAL-LICENSING.md) and [Third-party notices](THIRD-PARTY-NOTICES.md).

## Citation

Research and teaching publications may cite the versioned metadata in [CITATION.cff](CITATION.cff) and should preserve the scenario identifiers and limitations described in [RESEARCH.md](RESEARCH.md).

---

<div align="center">

**See the stream. Evaluate the protection. Preserve the evidence.**

</div>
