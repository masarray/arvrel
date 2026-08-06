# P6 Avalonia — Native virtual relay faceplate

## Objective

Port the approved futuristic virtual protection relay into the cross-platform Avalonia shell without using the WPF visual tree, PNG image maps, or duplicated protection logic.

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
- trip cause remains latched after the instantaneous pickup falls away;
- relay reset clears the cause latch through the same protection snapshot lifecycle.

No phase or earth cause is inferred from a preset name, button state, or UI threshold.

The current P6.2 adapter reads the immutable `InternalLabTick.Protection` snapshot from the existing Avalonia ViewModel presentation object. This is a temporary migration seam: the external-source projection milestone will expose an explicit presentation snapshot and remove that seam without changing annunciation semantics.

## P6.3 — LCD and hardware-navigation parity

The static faceplate LCD markup has been replaced by a dedicated native `RelayLcdControl`.

The LCD now contains five pages:

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

The navigation changes presentation state only. They do not alter injection, settings, protection, trust, or trip state.

## Architecture boundary

```text
VirtualRelayControl
├── RelayLampControl × 8
└── RelayLcdControl
        ↓ compiled bindings and annunciation projection
MainWindowViewModel
        ↓ immutable InternalLabTick / ProtectionSnapshot
RelayAnnunciationLatch
        ↓
Arvrel.Application / Arvrel.Protection
```

The controls do not instantiate or mutate a protection engine. They read the same immutable presentation state already used by the Avalonia measurement and relay-settings workspaces.

## Visual boundary

The faceplate is built entirely from Avalonia controls, shapes, brushes, text, and buttons. It does not use:

- PNG or raster background assets;
- image maps;
- WPF resources;
- Windows-only UI APIs;
- runtime geometry patches.

## Current parity boundary

The native product shell, complete cause annunciation, five LCD pages, and hardware navigation are now present. The largest remaining migration gaps are external PCAP/live-source operation, evidence/file workflows, the explicit immutable presentation snapshot, and complete multifunction protection settings.

## Next migration milestones

1. port PCAP/PCAPNG replay selection and stream workflow;
2. expose an explicit immutable presentation snapshot and remove the temporary annunciation adapter seam;
3. port evidence export and remaining engineering dialogs;
4. extend full 27/59/59N/67P/67N settings parity;
5. add Linux/macOS live-capture backends when transport and security requirements are defined.
