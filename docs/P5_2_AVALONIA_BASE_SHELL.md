# P5.2 — Avalonia base shell

## Objective

P5.2 introduces a real cross-platform desktop executable without replacing the mature WPF product in one large migration. The new shell consumes the portable application, process-bus, capture, and protection layers created in P5.0 and P5.1.

The acceptance target is a buildable and testable shell on Windows, Linux, and macOS with a working deterministic internal laboratory. Feature parity with the WPF application is deliberately outside this phase.

## Dependency direction

```text
Arvrel.Desktop (Avalonia presentation and UI scheduling)
        |
        +----> Arvrel.Application (workspace and deterministic lab lifecycle)
        +----> Arvrel.ProcessBus (portable decoder/replay capability boundary)
        +----> Arvrel.Protection (immutable protection snapshots)
```

The shell does not duplicate protection evaluation, virtual-injection source state, capture parsing, or Npcap ownership.

## Toolchain boundary

Avalonia 12.1 uses the current desktop framework generation and the shell targets `net10.0`. The reusable core, process-bus, protection, and current WPF product remain on their existing `net8.0` targets.

Two solutions make that boundary explicit:

- `ARVREL.sln`: current WPF product, portable core, process bus, capture, and established regression suites;
- `desktop/ARVREL.Desktop.sln`: Avalonia migration shell plus its portable dependencies and shell tests.

The root `global.json` remains pinned to .NET 8. `desktop/global.json` independently selects the .NET 10 SDK. This prevents the shell toolchain from silently changing the established WPF release build while still allowing the shell to reference the `net8.0` libraries.

## Developer entry point

From the repository root:

```text
cd desktop
dotnet build ARVREL.Desktop.sln -c Release
dotnet test ../tests/Arvrel.Desktop.Tests/Arvrel.Desktop.Tests.csproj -c Release --no-build
dotnet run --project ../src/Arvrel.Desktop/Arvrel.Desktop.csproj
```

The sibling ARIEC61850 checkout remains optional for the deterministic internal laboratory. When it is absent, the shell reports live/replay capability as unavailable instead of failing to start.

## Delivered shell

`Arvrel.Desktop` targets `net10.0` and uses Avalonia Desktop 12.1.0. It provides:

- classic desktop lifetime startup on supported desktop operating systems;
- a compact dark laboratory layout independent from WPF resources;
- deterministic internal source RUN/PAUSE lifecycle;
- A-G fault injection and clear action;
- SMV trust degradation and restore action;
- two-cycle phase and residual waveform rendering;
- phase and residual RMS measurements;
- 50P, 51P, 50N, and 51N state/progress presentation;
- trip, pickup, trust, sample-counter, and event presentation;
- live-capture and replay capability reporting from the P5.1 process-bus boundary;
- bounded 40 ms presentation refresh scheduling.

## Ownership boundary

The Avalonia layer owns:

- windows, XAML, styles, controls, and presentation-specific view models;
- dispatcher timer lifecycle;
- adaptation of immutable application/protection data into display strings;
- shell event presentation.

The Avalonia layer does not own:

- protection settings validation or element algorithms;
- virtual-injection state and deterministic sample generation;
- packet capture or PCAP parsing;
- SV decoding, continuity, trust, or evidence policy;
- active physical outputs.

## Tests

`Arvrel.Desktop.Tests` exercises the shell ViewModel without opening a window or requiring a display server. Coverage includes:

- initial source and capability state;
- RUN and scheduled deterministic advancement;
- A-G fault propagation into pickup/trip presentation;
- SMV degradation propagation into trip blocking;
- reset returning to a stopped normal-balanced profile.

## Portability gate

The `Avalonia shell portability` workflow uses the .NET 10 SDK and runs on Windows, Ubuntu, and macOS. Each runner:

1. builds `desktop/ARVREL.Desktop.sln` in decoder-less capability mode;
2. runs shell lifecycle tests without a display server;
3. publishes a framework-dependent host for the runner RID with the pinned ARIEC61850 decoder;
4. verifies that the managed assembly and native app host are present.

## Compatibility

P5.2 intentionally does not change:

- `ARVREL.sln` membership or the WPF executable;
- the root .NET 8 SDK selection;
- the established Windows release packaging workflow;
- core library target frameworks;
- protection algorithms, settings, timing, trust, or evidence semantics;
- Npcap filter, buffering, or timeout behavior;
- PCAP/PCAPNG parsing semantics;
- release versioning.

## Deliberately deferred

- WPF feature parity;
- SCL import and file-dialog workflows in Avalonia;
- live adapter selection and capture controls;
- replay file selection and stream browsing;
- protection settings editors and algorithm laboratory;
- evidence export;
- application icon and native package installers for Linux/macOS;
- replacement or removal of `Arvrel.App`.

These should be migrated as bounded vertical slices after the base shell proves stable on all three desktop operating systems.
