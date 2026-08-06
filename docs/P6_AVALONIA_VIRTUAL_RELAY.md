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

Phase A, Phase B, Phase C, and Earth lamps now use the portable `RelayAnnunciationLatch` from `Arvrel.Protection`.

The state contract is:

- `Off` → lamp inactive;
- `Pickup` → amber lens;
- `Trip` → red lens;
- trip cause remains latched after the instantaneous pickup falls away;
- relay reset clears the cause latch through the same protection snapshot lifecycle.

No phase or earth cause is inferred from a preset name, button state, or UI threshold.

The current P6.2 adapter reads the immutable `InternalLabTick.Protection` snapshot from the existing Avalonia ViewModel presentation object. This is a temporary migration seam: the next external-source milestone will expose an explicit presentation snapshot and remove that adapter seam without changing annunciation semantics.

## Architecture boundary

```text
VirtualRelayControl / RelayLampControl
        ↓ compiled bindings and annunciation projection
MainWindowViewModel
        ↓ immutable InternalLabTick / ProtectionSnapshot
RelayAnnunciationLatch
        ↓
Arvrel.Application / Arvrel.Protection
```

The control does not instantiate or mutate a protection engine. It reads the same immutable presentation state already used by the Avalonia measurement and relay-settings workspaces.

## Visual boundary

The faceplate is built entirely from Avalonia controls, shapes, brushes, text, and buttons. It does not use:

- PNG or raster background assets;
- image maps;
- WPF resources;
- Windows-only UI APIs;
- runtime geometry patches.

## Current parity boundary

The native product shell and complete high-level/cause annunciation are now present. LCD measurement/event/record navigation and external process-bus source workflows remain the next major parity gaps.

## Next migration milestones

1. port LCD measurement, event, and record pages with faceplate navigation;
2. port PCAP/PCAPNG replay selection and stream workflow;
3. expose an explicit immutable presentation snapshot and remove the temporary annunciation adapter seam;
4. port evidence export and remaining engineering dialogs;
5. extend full 27/59/59N/67P/67N settings parity.
