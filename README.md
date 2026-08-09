<div align="center">

# ARVREL

### IEC 61850 Virtual Protection & Control IED Laboratory

**Observe the signal. Exercise the virtual I/O chain. Explain every protection or control decision.**

[![Windows CI](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/masarray/arvrel/actions/workflows/dotnet.yml)
[![Public site](https://github.com/masarray/arvrel/actions/workflows/pages.yml/badge.svg)](https://masarray.github.io/arvrel/)
[![Release](https://img.shields.io/github/v/release/masarray/arvrel?include_prereleases&label=public%20beta)](https://github.com/masarray/arvrel/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0--or--later-0b7285)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-2563eb)](#public-beta-status)
[![UI](https://img.shields.io/badge/desktop-WPF%20P6-334155)](#repository-scope)
[![Output](https://img.shields.io/badge/output-virtual%20only-b45309)](#engineering-and-safety-boundary)

[Product site](https://masarray.github.io/arvrel/) ·
[Documentation](https://masarray.github.io/arvrel/documentation.html) ·
[Current shipped status](docs/CURRENT_STATUS.md) ·
[Quick start](https://masarray.github.io/arvrel/quick-start.html) ·
[Download](https://github.com/masarray/arvrel/releases/tag/v0.1.0-beta.6)

</div>

![ARVREL Windows engineering workspace](docs/assets/arvrel-main.webp)

## Repository scope

This repository is the **stable Windows WPF edition** of ARVREL. The public desktop product combines:

- **Protection Relay · OCR** — feeder protection, process-bus analysis, internal secondary injection, and a closed-loop virtual TESTSET↔relay bench;
- **Transformer Differential · 87T / REF** — 87T, 87T-HS, REF HV/LV, deterministic self-test, synchronized two-sided internal injection, and paired-SV live/replay engineering;
- **AVR · OLTC Controller** — simulated transformer plant, 17-position OLTC, virtual authority/interlocks, and laboratory IEC 61850 MMS browse/read/report/control behavior.

Cross-platform Avalonia development is intentionally isolated in **[masarray/arvrel-avalonia](https://github.com/masarray/arvrel-avalonia)**. Its application source, migration status, packaging, and release decisions are separate from this Windows repository.

## Public-beta status

| Item | Current position |
|---|---|
| Public release | **`v0.1.0-beta.6`** |
| Release highlight | Metrology-grade closed-loop feeder timing + explicit operator timing semantics + one-click RESET/re-arm |
| Desktop product | Windows WPF multi-IED lab: feeder OCR + Transformer Differential 87T/REF + AVR/OLTC |
| Supported platform | Windows 10/11 x64 |
| Official packages | Per-user installer, portable EXE, portable ZIP |
| TESTSET timing authority | Accepted external virtual BI edges; BI1 owns measured trip and optional auto-stop |
| Transformer first test | Deterministic 10-scenario self-test + synchronized HV/LV/neutral internal injection |
| AVR first test | Built-in simulated transformer plant and 17-position OLTC |
| Live capture | Npcap required separately |
| Output authority | Virtual only |
| Intended use | Education, source review, controlled laboratory evaluation, FAT/SAT preparation, interoperability study, and research |
| Not claimed | Calibrated relay test set, certified IED, IEC 61850 conformance result, IEC 60255 type-test evidence, commissioning acceptance, or hard-real-time platform |

The selected GitHub Release is the package source of truth. See [`docs/CURRENT_STATUS.md`](docs/CURRENT_STATUS.md) for the canonical shipped-state description and documentation authority order.

## Beta.6 closed-loop secondary-injection model

The feeder laboratory now keeps the virtual source, relay, contacts, wiring, and test-set binary inputs as separate equipment authorities:

```text
TESTSET metrology T0
  → instantaneous secondary waveform
  → virtual analog wiring
  → relay terminal samples
  → clipping / ADC quantization / input delay
  → causal rolling relay measurement
  → protection pickup / timer / trip request
  → relay BO delay / contact behavior
  → virtual binary wire
  → independent TESTSET BI sampler / deglitch / debounce
  → accepted BI edge
  → measured timing / optional auto-stop
```

**Critical invariant:** the TESTSET never treats the relay's internal `TripLatched` state as its measured trip. Internal trip may request BO1, but measured trip and auto-stop occur only after the wired `TESTSET.BI1` edge is accepted.

This makes a disconnected BO1→BI1 test meaningful: the relay can trip internally while the TESTSET correctly records **no external trip** and leaves the source running.

### Shipped timing profile

- monotonic TESTSET metrology clock: **1 µs resolution**;
- TESTSET BI sampling: **10 kHz / 100 µs**;
- BI deglitch: **0.5 ms**;
- BI debounce holdoff: **0 ms**;
- relay acquisition/processing grid: **4 kHz / 250 µs**;
- behavioral relay front-end delay: **1.5 ms**;
- 16-bit-equivalent behavioral ADC, 20 A RMS current full scale, 300 V RMS voltage full scale;
- one nominal 50 Hz **causal rolling DFT**, primed with settled pre-fault history.

These are generic behavioral model parameters, not a calibration claim or a clone of a named commercial relay/test set.

## Timing semantics that stay separate

The operator timing rail and evidence schema 9 distinguish:

- `RELAY ANY PU [source]` — first generic pickup that drives BO2;
- `TESTSET BI2 ACCEPT` — accepted generic ANY-PICKUP input;
- operated-element pickup — pickup of the element that ultimately operates;
- operated-element P→T — that element's own pickup-to-trip interval;
- relay trip request — live relay trip-latch edge requesting BO1;
- `TESTSET BI1 ACCEPT` — authoritative external trip time.

BI2 is deliberately **ANY PICKUP**, so it may precede the pickup of the element that later trips. It must not be used as a substitute for operated-element pickup timing.

## One-click RESET and frozen evidence

After BI1 auto-stop, the source is explicitly **OUTPUT OFF · FROZEN CAPTURE**. One relay RESET transaction advances the modeled relay/feedback path until stale fault pickup releases, clears the relay latch/timers once, and waits until the relay, BO1/BO2, and TESTSET BI1/BI2 all satisfy the re-arm postcondition. Only then is **READY TO RE-ARM** shown.

RESET preserves completed TESTSET timing and frozen trip/event evidence and does not restart or mutate the source. If the source remains energized, protection can legitimately reassert.

## Engineering capabilities

### Signal sources, wiring, and process bus

- deterministic feeder 4I+4V internal secondary injection;
- explicit virtual analog and binary wiring for the closed-loop test bench;
- synchronized Transformer Differential HV IA/IB/IC/IN and LV IA/IB/IC/IN internal injection;
- independent neutral/NGR inputs for REF HV and REF LV;
- live IEC 61850 Sampled Values capture through Npcap;
- PCAP/PCAPNG replay;
- SCL-assisted stream identity, dataset, mapping, scaling, and `confRev` review;
- APPID, destination MAC, VLAN, `svID`, continuity, freshness, quality, and trust evidence.

### Measurement and relay front end

- feeder live/replay complete-window fundamental phasor estimation and sequence quantities;
- beta.6 closed-loop relay path based on instantaneous signed terminal samples, clipping, quantization, configured input delay, and a causal rolling DFT;
- 4I+4V RMS phasors and positive-, negative-, and zero-sequence quantities;
- explicit residual channels with documented calculated fallback where appropriate;
- coherent waveform evidence and phasor view;
- P6 native WPF relay faceplate, annunciation, LCD, operation records, and timing strip.

### Protection, transformer, AVR, and evidence

- feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N;
- two-winding transformer 87T, 87T-HS, REF HV, and REF LV;
- H2/H5 transformer security plus context-gated external-fault/CT-saturation security;
- transformer rating, CT ratio, polarity, and supported vector-group compensation;
- AVR/OLTC simulated plant with 17 tap positions and modeled LOCAL/REMOTE + AUTO/MANUAL authority;
- laboratory IEC 61850 MMS browse/read, DataSets, reports, GI/integrity, modeled SBO/SBOw controls, and virtual AVR settings;
- setting groups, revisions, presets, SHA-256 fingerprints, operation attribution, trip/control cause, event trace, trust state, source provenance, and exportable evidence.

## First feeder closed-loop evaluation

1. Download **v0.1.0-beta.6** and verify `SHA256SUMS.txt`.
2. Select the feeder Protection Relay / Internal demo path.
3. Review enabled settings and source setpoints.
4. Start the virtual source and observe the timing rail.
5. Read `RELAY ANY PU`, `TESTSET BI2 ACCEPT`, operated-element pickup/P→T, relay trip request, and `TESTSET BI1 ACCEPT` as distinct events.
6. Confirm BI1 auto-stop produces **OUTPUT OFF · FROZEN CAPTURE** while retaining configured source values and completed evidence.
7. Press relay RESET once and wait for **READY TO RE-ARM**.
8. For wiring validation, disconnect BO1→BI1 and verify that an internal relay trip is **not** reported as a TESTSET trip.

## Transformer public test

A first Transformer Differential check needs no external merging unit, PCAP, or Npcap:

1. select **Transformer Differential · 87T / REF**;
2. run the deterministic 10-scenario self-test; expected result: `PASS · 10/10 · transformer-public-beta-v1`;
3. use synchronized internal two-sided injection for Balanced through load, Internal fault, REF HV/NGR, and REF LV/NGR cases;
4. move to paired-SV PCAP/live evaluation only when external process-bus behavior is part of the test objective.

Calculated phase residual is never silently promoted to independent neutral-CT evidence for REF.

## Trust before trip or virtual control

```text
AllowsMeasurement  → quantities may enter measurement/display
AllowsPickup       → protection pickup/timing may be evaluated
AllowsTrip         → an operated element may assert the virtual relay trip latch
TESTSET BI1        → external measured trip / optional source auto-stop
Virtual MMS control → may affect only the modeled AVR/OLTC process when interlocks permit
```

Duplicate/out-of-order process-bus frames remain diagnostically visible but are rejected before measurement/protection admission. Virtual MMS controls terminate inside the simulated process.

## Architecture at a glance

```text
Feeder TESTSET source ──virtual analog wiring──> causal relay front end ──> ProtectionEngine
       ▲                                                        │
       │                                                        ▼
       └──── timing/auto-stop <── TESTSET BI <── virtual wire <── relay BO

Live Npcap / PCAP replay ──> decode · identity · mapping · trust ──> feeder/transformer runtime

Transformer internal HV/LV source ──> TransformerProtectionRuntime

Virtual transformer plant <──> AVR / OLTC logic <──> laboratory MMS model

All paths ──> immutable state · operation/event evidence · export
```

WPF is presentation cadence only; it is not a protection or metrology clock.

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

Or use the root solution directly:

```powershell
dotnet restore .\ARVREL.sln
dotnet build .\ARVREL.sln -c Release --no-restore
dotnet test .\ARVREL.sln -c Release --no-build
```

## Documentation map

- [`docs/CURRENT_STATUS.md`](docs/CURRENT_STATUS.md) — canonical shipped-state authority;
- [Public documentation hub](https://masarray.github.io/arvrel/documentation.html);
- [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md) — operating and evaluation workflow;
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — runtime authority and data flow;
- [`docs/P0_METROLOGY_GRADE_TIMING_ENGINE.md`](docs/P0_METROLOGY_GRADE_TIMING_ENGINE.md) — metrology engine implementation detail;
- [`docs/TRANSFORMER_PUBLIC_TEST.md`](docs/TRANSFORMER_PUBLIC_TEST.md) — transformer test workflow;
- [`docs/AVR-IEC61850-SAS-TEST.md`](docs/AVR-IEC61850-SAS-TEST.md) — AVR/MMS laboratory workflow.

Historical `P*` documents are preserved as engineering milestone records and may describe an earlier state. They are not the current product-status authority.

## Engineering and safety boundary

ARVREL is virtual-output laboratory software. It does not provide physical relay contacts, operational GOOSE trip, physical OLTC motor authority, autonomous switching, IEC 61850 conformance certification, IEC 60255 type-test/calibration evidence, or deterministic protection-grade hard-real-time guarantees.

ARVREL **does** model IEC 61850 MMS controls for the virtual AVR/OLTC process. Those commands terminate inside the software simulation and provide no primary-equipment authority.

Use live capture or protocol testing only on isolated, authorized laboratory networks. Do not use ARVREL as the sole basis for operational settings, commissioning acceptance, or switching decisions.

## Privacy, integrity, and licensing

ARVREL stores local preferences and diagnostics under `%LOCALAPPDATA%\ARVREL`. Do not publish customer captures, proprietary SCL files, credentials, IP plans, or employer-confidential information.

Official beta.6 release assets include SHA-256 checksums, dependency evidence, CycloneDX SBOM, and GitHub build-provenance attestations.

ARVREL is licensed under **GPL-3.0-or-later**. See [Commercial licensing](COMMERCIAL-LICENSING.md) and [Third-party notices](THIRD-PARTY-NOTICES.md).

---

<div align="center">

**See the stream. Exercise the virtual I/O. Preserve the evidence.**

</div>
