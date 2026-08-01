# ARVREL

**IEC 61850 Sampled Values virtual multifunction feeder relay and protection-algorithm laboratory for Windows.**

ARVREL presents the protection cause-and-effect chain in one engineering workspace:

```text
4I+4V SMV → stream trust → RMS/phasor measurement → active algorithm → native settings → virtual trip → evidence
```

> ARVREL is an engineering and educational laboratory. It is not a certified protection IED, calibrated relay test set, deterministic real-time platform, or authorization to operate primary equipment. Outputs are virtual only: no GOOSE trip, MMS control, relay contact, or physical trip path.

## Two operating modes

### Practitioner mode

Configure and operate ARVREL through native numerical-relay-style settings without opening code:

- setting group name, revision and fingerprint;
- 50P-1 and 50N enable, pickup, definite delay and dropout;
- 51P and 51N enable, pickup, characteristic, TMS, definite delay, minimum operate time, dropout and reset behavior;
- IEC Standard/Normal, Very, Extremely and Long-Time Inverse;
- Definite Time and user-defined IEC-form curves;
- 67P positive-sequence directional phase overcurrent;
- 67N residual directional earth fault;
- 27 undervoltage with phase selection logic;
- 59 overvoltage with phase selection logic;
- 59N residual overvoltage;
- CT and VT primary/secondary context;
- save/load `.arvsettings` presets and restore defaults.

### Research mode

Inspect the exact standard algorithm generated from the active setting group and edit a separate custom shadow definition:

- read-only active standard source;
- editable typed laboratory DSL;
- deterministic safety-policy validation;
- immutable shadow staging tied to the active settings fingerprint;
- visible virtual-output-only boundary.

Custom source remains shadow-only and does not replace the running standard algorithm. Runtime A/B comparison and explicit custom activation remain future research-engine work.

## Multifunction feeder protection

The active P2 feeder package adds a phasor-domain layer beside the existing overcurrent engine:

| ANSI | Function | Current implementation |
|---|---|---|
| 67P | Directional phase overcurrent | Positive-sequence `V1/I1`, MTA, forward/reverse selection, minimum polarizing voltage and definite delay |
| 67N | Directional earth fault | Residual `3V0/3I0`, MTA, forward/reverse selection, minimum polarizing voltage and definite delay |
| 27 | Undervoltage | Phase-neutral, phase-phase or positive-sequence voltage with 1/2/3-of-3 logic |
| 59 | Overvoltage | Phase-neutral, phase-phase or positive-sequence voltage with 1/2/3-of-3 logic |
| 59N | Residual overvoltage | `3V0` pickup, dropout and definite delay |

The feeder functions default to **disabled**. This preserves the existing 50/51 laboratory behavior until an operator explicitly enables and configures them.

All feeder elements use the same SMV trust gate and virtual trip latch as 50/51. Directional elements remain restrained when phasors or minimum polarizing voltage are unavailable.

Functions such as 46, 47, 49, 81U/O, 32, 50BF, 79, 25 and 86 are not part of this P2 baseline yet.

## Process-bus capabilities

- live IEC 61850 Sampled Values capture through Npcap;
- classic PCAP and PCAPNG replay;
- dynamic stream discovery by source, destination, VLAN, APPID and `svID`;
- SCL/SCD/CID/ICD/IID import through the sibling ARIEC61850 parser;
- SCL-assisted stream binding and ordered payload decoding;
- fixed value-quality fallback for common current and 4I+4V payloads;
- explicit CT and VT primary/secondary contexts with 50/60 Hz selection;
- one-cycle RMS for 50P, 51P, 50N and 51N;
- one-cycle fundamental DFT phasors for feeder functions;
- positive-, negative- and zero-sequence current and voltage quantities;
- stationary two-cycle IA/IB/IC/3I0 waveform and retained voltage sample windows;
- `smpCnt`, freshness, quality, scaling, mapping and configuration trust gates;
- JSON evidence export;
- compact WPF interface with locally rendered Lucide-derived icon geometry.

The internal deterministic source provides 4I+4V phasors and a repeatable A-G fault with voltage depression for laboratory checks.

## Relay faceplate

The virtual relay LCD and keypad provide native operation pages for:

- current and voltage phasor measurements;
- 50P/51P/50N/51N status;
- 67P/67N/27/59/59N status;
- native settings;
- events and process-bus diagnostics;
- last-trip record with pickup time, trip time, operate time and the correct current or voltage operating quantity;
- directional angle and polarizing voltage for 67P/67N trip records.

## Repository relationship

Use sibling checkouts:

```text
C:\Git\
├── ARIEC61850\
└── arvrel\
```

`Directory.Build.props` detects the sibling engine automatically. The application remains buildable in deterministic simulation mode without it; live Npcap, replay decoding and SCL workflows require the sibling.

## Build and run

Requirements:

- Windows 10/11 x64;
- .NET 8 SDK;
- Npcap for live capture;
- sibling `ARIEC61850` checkout for process-bus workflows.

```powershell
cd C:\Git\arvrel
git pull origin main
.\scripts\verify-sibling.cmd
.\scripts\build.cmd
.\scripts\run.cmd
```

Direct command:

```powershell
dotnet run --project .\src\Arvrel.App\Arvrel.App.csproj -c Release
```

## Laboratory workflow

1. Open **Relay Settings** and configure the active setting group.
2. Open **CT/VT context** and enter CT, VT and nominal-frequency values.
3. Enable the required feeder functions on the **Feeder protection** tab.
4. Select the internal source, **Live Npcap**, or **PCAP replay**.
5. Import the matching SCL when available.
6. Start capture or open a capture file, then select a discovered SV stream.
7. Review mapping, scaling, continuity, phasors, directional decision, protection operation and evidence.
8. Switch to **Research mode** to inspect or stage algorithm source.

A decoded but unbound or unscaled stream remains visible while trip permission is explicitly blocked. This prevents uncertain measurement provenance from silently producing a virtual trip.

## Architecture

```text
Arvrel.App
  ├─ native current and feeder settings
  ├─ interactive multifunction relay faceplate
  ├─ research algorithm workspace
  └─ immutable UI snapshots
       ↓
Arvrel.ProcessBus
  ├─ Npcap live source
  ├─ PCAP / PCAPNG reader
  ├─ ARIEC61850 SV parser and SCL profiles
  ├─ 4I+4V stream runtime and locked sample rings
  ├─ CT/VT measurement context
  └─ trust and evidence
       ↓ MeasurementFrame + PhasorMeasurementSet
Arvrel.Protection
  ├─ IEC curve calculator
  ├─ fundamental phasor and symmetrical-component engine
  ├─ guarded 50P / 51P / 50N / 51N
  └─ guarded 67P / 67N / 27 / 59 / 59N
```

See [`docs/P1_1_DUAL_MODE.md`](docs/P1_1_DUAL_MODE.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/PRD.md`](docs/PRD.md).

## License

Copyright (C) 2026 Ari Sulistiono.

ARVREL is licensed under GNU GPL v3.0 or later. Sibling and third-party components retain their own notices and licensing boundaries.
