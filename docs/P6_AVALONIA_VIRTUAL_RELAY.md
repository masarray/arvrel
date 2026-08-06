# P6 Avalonia — Native virtual relay faceplate

## Objective

Port the approved futuristic virtual protection relay into the cross-platform Avalonia shell without using the WPF visual tree, PNG image maps, or duplicated protection logic.

## Delivered in this milestone

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

## Architecture boundary

```text
VirtualRelayControl (Avalonia presentation)
        ↓ compiled bindings
MainWindowViewModel
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

This milestone provides the product shell and high-level annunciation. Phase A/B/C and earth cause lamps are present but are not yet bound to the portable `RelayAnnunciationLatch`. That binding, LCD page parity, and faceplate navigation behavior are the next relay-specific migration steps.

## Next migration milestones

1. bind phase/earth pickup and latched trip causes through `RelayAnnunciationLatch`;
2. port LCD measurement, event, and record pages;
3. port PCAP/PCAPNG replay selection and stream workflow;
4. port evidence export and remaining engineering dialogs;
5. extend full 27/59/59N/67P/67N settings parity.
