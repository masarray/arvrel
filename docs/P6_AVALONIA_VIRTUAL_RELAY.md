# P6 Avalonia — Native virtual relay and process-bus workflow

## Objective

Migrate the approved futuristic virtual protection relay and its essential source workflows into the cross-platform Avalonia shell without using the WPF visual tree, PNG image maps, or duplicated protection logic.

## P6.1 — Native product shell

- native `VirtualRelayControl` authored on a fixed 560 × 700 hardware canvas and scaled uniformly;
- reusable `RelayLampControl` with a common bezel, cavity, lens, shade, and highlight model;
- live binding to the existing Avalonia `MainWindowViewModel` for:
  - SMV trust / Healthy indication;
  - Pickup indication;
  - Trip latch indication;
  - SMV Block indication;
  - IA, IB, IC, and residual current;
  - trip state, trust state, setting group, and relay reset command;
- a new `FACEPLATE` tab inserted into the existing right relay workspace while preserving `RELAY` settings and `EVENTS` tabs;
- source-contract tests that run without a display server.

## P6.2 — Annunciation cause parity

Phase A, Phase B, Phase C, and Earth lamps use the portable `RelayAnnunciationLatch` from `Arvrel.Protection`.

The state contract is:

- `Off` → lamp inactive;
- `Pickup` → amber lens;
- `Trip` → red lens;
- trip cause remains latched after instantaneous pickup falls away;
- relay reset clears the cause latch through the same protection snapshot lifecycle.

No phase or earth cause is inferred from a preset name, button state, or UI threshold.

## P6.3 — LCD and hardware-navigation parity

The static faceplate LCD markup was replaced by a dedicated native `RelayLcdControl`.

The LCD contains five pages:

1. **Measure** — IA, IB, IC, residual current, frequency, and trust state;
2. **Events** — bounded operator event history from the existing ViewModel;
3. **Records** — trip state, decision reason, active profile, setting group, and source/settings fingerprints;
4. **Setup** — source mode, profile, setting group, neutral provenance, evidence window, and settings status;
5. **Diagnostics** — platform, live-capture capability, replay capability, and sample counter.

Navigation authority is explicit and local to the faceplate:

- F1 → Measure;
- F2 → Events;
- F3 → Records;
- F4 → Setup;
- F5 → Diagnostics;
- Home and Back → Measure;
- Menu → Setup;
- Star → Records;
- Up/Left → previous page;
- Down/Right/OK → next page;
- LCD footer tabs remain directly clickable.

Navigation changes presentation state only. It does not alter injection, settings, protection, trust, or trip state.

## P6.4 — PCAP replay and unified presentation projection

A native `PROCESS BUS` workspace now provides the first complete external-source workflow in Avalonia.

### Capture selection and replay

- cross-platform Avalonia `StorageProvider` file picker;
- explicit `.pcap` and `.pcapng` filters;
- finite replay through the existing `SmvProcessBusController.ReplayAsync()` authority;
- no second PCAP parser, SV decoder, measurement runtime, or protection engine;
- replay lifecycle and frame count shown to the operator;
- a command to return cleanly to the internal virtual test set.

### Stream workflow

After replay, the workspace exposes:

- decoded Sampled Values stream list;
- selected stream identity and APPID;
- source summary;
- trust summary;
- mapping/SCL summary;
- bounded runtime diagnostics.

Changing the selected stream projects that stream's immutable runtime snapshot into the existing waveform, measurement, protection, LCD, annunciation, and relay-state surfaces.

### Unified projection

`DesktopPresentationSnapshot` is the single immutable presentation contract for both internal and external sources. It contains:

- source mode and identity;
- `MeasurementFrame`;
- `ProtectionSnapshot`;
- two-cycle `ScenarioWaveform`;
- sample counter;
- source, trust, mapping, and diagnostic evidence.

The faceplate no longer reads a private ViewModel field through reflection. `VirtualRelayControl` consumes the public `CurrentPresentationSnapshot` and `CurrentProtectionSnapshot` properties and resets its cause latch whenever the selected source identity changes.

### Source authority

```text
Internal deterministic laboratory ─┐
                                    ├─ DesktopPresentationSnapshot
PCAP / PCAPNG replay runtime ───────┘            │
                                                 ▼
Waveform · measurements · trust · relay · LCD · annunciation
```

Internal injection commands explicitly switch back to the internal source. External replay is read-only for setting edits in P6.4; relay reset remains available for the selected external stream.

## Architecture boundary

```text
VirtualRelayControl
├── RelayLampControl × 8
└── RelayLcdControl

ProcessBusSourceControl
        ↓
MainWindowViewModel
        ↓ DesktopPresentationSnapshot
InternalLabSession / SmvProcessBusController
        ↓ immutable snapshots
Arvrel.Application / Arvrel.ProcessBus / Arvrel.Protection
```

The controls do not instantiate or mutate a protection engine. The replay workflow uses the existing capture abstraction, ARIEC61850 decoder, continuity/trust gate, measurement runtime, and protection snapshot.

## Visual and platform boundary

The migrated surfaces are built entirely from Avalonia controls, shapes, brushes, text, storage APIs, and buttons. They do not use:

- PNG or raster background assets;
- image maps;
- WPF resources;
- Windows-only UI APIs;
- runtime geometry patches.

Windows can report the existing authorized Npcap live backend. Linux and macOS remain replay/internal-laboratory platforms until native live-capture transport and security boundaries are implemented.

## Current parity boundary

Completed in this branch:

- native futuristic relay shell;
- shared physical lamp model;
- complete cause annunciation;
- five LCD pages and hardware navigation;
- PCAP/PCAPNG file selection and replay;
- stream selection, trust/mapping/diagnostics;
- unified internal/replay presentation projection.

Largest remaining migration gaps:

1. evidence export and file/dialog workflows;
2. full 27/59/59N/67P/67N settings parity;
3. SCL import and measurement-context workflow;
4. live adapter selection/start-stop on Windows;
5. Linux/macOS live-capture backends;
6. final removal of the WPF product channel after parity and field validation.
