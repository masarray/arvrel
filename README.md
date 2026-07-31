# ARVREL

**IEC 61850 Sampled Values virtual protection relay laboratory for Windows.**

ARVREL presents the protection cause-and-effect chain in one engineering workspace:

```text
SMV waveform → stream trust → RMS measurement → 50/51 pickup → timing → virtual trip → evidence
```

> ARVREL is an engineering and educational laboratory. It is not a certified protection IED, calibrated relay test set, deterministic real-time platform, or authorization to operate primary equipment. Outputs are virtual only: no GOOSE trip, MMS control, relay contact, or physical trip path.

## P1 capabilities

- live IEC 61850 Sampled Values capture through Npcap;
- classic PCAP and PCAPNG replay;
- dynamic stream discovery by source, destination, VLAN, APPID, and `svID`;
- SCL/SCD/CID/ICD/IID import through the sibling ARIEC61850 parser;
- SCL-assisted stream binding and ordered payload decoding;
- fixed value-quality fallback for common current and 4I+4V payloads;
- explicit CT primary/secondary context and 50/60 Hz selection;
- one-cycle RMS feeding real 50P, 51P, 50N, and 51N elements;
- stationary two-cycle IA/IB/IC/3I0 waveform;
- `smpCnt`, freshness, quality, scaling, mapping, and configuration trust gates;
- JSON evidence export;
- compact premium WPF interface with Lucide-derived icon geometry and filled icon-button treatment.

The internal deterministic source remains available for repeatable demonstrations and regression checks.

## Repository relationship

Use sibling checkouts:

```text
C:\Git\
├── ARIEC61850\
└── arvrel\
```

`Directory.Build.props` detects the sibling engine automatically. The application remains buildable in deterministic simulation mode without it; live Npcap, replay decoding, and SCL workflows require the sibling.

## Build and run

Requirements:

- Windows 10/11 x64;
- .NET 8 SDK;
- Npcap for live capture;
- sibling `ARIEC61850` checkout for P1 process-bus workflows.

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

## P1 workflow

1. Select **Live Npcap** or **PCAP replay**.
2. Import the matching SCL when available.
3. Open **CT context** and set CT primary, secondary, and nominal frequency.
4. Start capture or open a capture file.
5. Select a discovered SV stream.
6. Review mapping, scaling, continuity, quality, RMS, protection operation, and evidence.

A decoded but unbound or unscaled stream remains visible while trip permission is explicitly blocked. This prevents uncertain measurement provenance from silently producing a virtual trip.

## Architecture

```text
Arvrel.App
  ↓ immutable UI snapshots
Arvrel.ProcessBus
  ├─ Npcap live source
  ├─ PCAP / PCAPNG reader
  ├─ ARIEC61850 SV parser and SCL profiles
  ├─ stream runtime and sample rings
  ├─ RMS and measurement context
  └─ trust and evidence
  ↓ MeasurementFrame
Arvrel.Protection
  └─ guarded 50P / 51P / 50N / 51N
```

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/PRD.md`](docs/PRD.md).

## License

Copyright (C) 2026 Ari Sulistiono.

ARVREL is licensed under GNU GPL v3.0 or later. Sibling and third-party components retain their own notices and licensing boundaries.
