# ARVREL

**Premium IEC 61850 Sampled Values virtual protection relay laboratory for Windows.**

ARVREL presents the complete cause-and-effect chain in one engineering workspace:

```text
SMV waveform → measurement → 50/51 pickup → timing → virtual trip → evidence
```

The P0 repository contains a deterministic, testable 50/51 phase and earth-fault protection engine, a stationary two-cycle oscilloscope, an original vendor-neutral relay faceplate, an SMV trip-permission guard, and a typed Algorithm Editor prototype.

> ARVREL is an engineering and educational laboratory. It is not a certified protection IED, deterministic real-time platform, calibrated relay test set, or authorization to operate primary equipment. P0 produces virtual indications only: no GOOSE trip, MMS control, relay contact, or physical output.

## Repository relationship

The intended local layout is:

```text
Git/
├── ARIEC61850/   # existing protocol engine repository
└── arvrel/       # this application repository
```

`Directory.Build.props` automatically detects:

```text
..\ARIEC61850\src\AR.Iec61850\AR.Iec61850.csproj
..\ARIEC61850\src\AR.Iec61850.Transports.Npcap\AR.Iec61850.Transports.Npcap.csproj
```

When found, both projects are referenced as sibling dependencies and the app is compiled with `ARIEC61850_SIBLING`. The P0 deterministic laboratory can still build without the sibling; live Npcap, PCAP replay, SCL binding and real SMV measurement remain the P1 integration boundary.

## Included

- premium clean lean one-screen WPF workspace;
- stationary two-cycle IA/IB/IC/3I0 waveform;
- fault, pickup and trip causality markers;
- 50P definite-time phase overcurrent;
- 51P IEC standard-inverse phase overcurrent;
- 50N definite-time earth fault;
- 51N IEC standard-inverse earth fault;
- trip latch and reset;
- SMV trust policy that can expose pickup while blocking unsafe trip;
- original virtual-relay LCD, LEDs and keypad;
- typed Algorithm Editor validation and immutable shadow staging;
- deterministic regression tests;
- Windows GitHub Actions workflow;
- GPL-3.0-or-later public repository baseline.

## Build

Requirements:

- Windows 10/11 x64;
- .NET 8 SDK;
- Visual Studio 2022, Rider, or VS Code with C# tooling;
- optional sibling `ARIEC61850` checkout for the process-bus engine foundation.

```powershell
cd Git\arvrel
.\scripts\verify-sibling.ps1
.\scripts\build.ps1
```

Run:

```powershell
.\scripts\run.ps1
```

Or:

```powershell
dotnet run --project .\src\Arvrel.App\Arvrel.App.csproj -c Release
```

## Publish the new sibling repository

Authenticate GitHub CLI once:

```powershell
gh auth login
```

Then:

```powershell
cd Git\arvrel
.\scripts\publish-github.ps1 -Owner masarray -Repository arvrel -Visibility public
```

The script initializes Git when needed, creates the first commit, creates `masarray/arvrel` when it does not exist, and pushes `main`.

## Protection boundary

The protection engine runs independently from UI refresh. In P0, deterministic measurement frames feed the same protection contract that the P1 ArSubsv/ARIEC61850 adapter will implement.

A degraded SMV condition can remain measurable and visible while `AllowsTrip=false`. Once an element reaches operation, ARVREL records `TRIP BLOCKED` rather than silently continuing with uncertain process-bus data.

## Roadmap

P1 connects the actual sibling engine:

- Npcap live capture;
- PCAP/PCAPNG replay;
- SCL binding and dataset-order mapping;
- CT/VT measurement context and primary/secondary scaling;
- real circular sample buffers and measurement frames;
- stream freshness, continuity, quality and configuration evidence;
- versioned event and disturbance packages.

P2 completes the typed algorithm compiler, deterministic sandbox, A/B reference-versus-custom evaluation, live variables and algorithm evidence.

See [`docs/PRD.md`](docs/PRD.md).

## License

Copyright (C) 2026 Ari Sulistiono.

ARVREL is licensed under GNU General Public License v3.0 or later. Third-party and sibling repositories retain their own notices and licensing boundaries.
