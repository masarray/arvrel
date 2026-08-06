# P6 — Virtual Relay Total Refactor

## Objective

P6 replaces the accumulated WPF virtual-relay visual mutation stack with one modular, deterministic faceplate implementation inspired by the approved futuristic protection-relay concept.

The concept image is a visual reference only. P6 does not use the generated render as a background, image map, or clickable PNG. Every visible and interactive region is native WPF geometry, text, controls, gradients, and effects.

## Design read

The visual doctrine follows the product-design taste audit:

- industrial protection HMI;
- trust-first and low glare;
- high legibility at normal Windows scaling;
- premium but technically credible materials;
- restrained blue accent;
- material depth instead of glassmorphism;
- no hard diagonal gloss polygon;
- no decorative neon;
- no default desktop focus noise.

Calibration:

- design variance: 4/10;
- motion intensity: 2/10;
- visual density: 6/10.

## Component architecture

```text
VirtualRelayControl
├── outer enclosure and perimeter trim
├── identity header
├── status annunciator
│   └── 8 × RelayLampControl
├── LCD module
│   └── existing faceplate/menu/measurement presenters
├── F1–F5 and reset column
├── navigation/control deck
└── footer and trust identity
```

### `VirtualRelayTokens.xaml`

Single source for:

- enclosure and fascia materials;
- module and LCD materials;
- status and typography colours;
- lamp bezel/cavity/shade materials;
- shell/module shadows;
- function and navigation key templates.

### `RelayLampControl`

Every faceplate indication uses the same physical construction:

1. emitted halo;
2. 19 px metal bezel;
3. 14.5 px dark cavity;
4. 10.5 px logical state lens;
5. optical shade;
6. independent specular highlight.

The halo stays behind the bezel. Green, amber, red, blue, and off states therefore cannot alter the common physical ring or lamp diameter.

### `VirtualRelayControl`

The faceplate is authored on a fixed 520 × 680 design canvas and scaled uniformly through a WPF `Viewbox`. This avoids runtime coordinate patches and preserves the approved proportions across the existing right workspace column.

## Visual authority boundary

P6 is the only authority for:

- enclosure geometry;
- fascia material and perimeter trim;
- LCD/module recess geometry;
- function/navigation key geometry;
- button press depth;
- lamp bezel, cavity, lens, highlight, and glow geometry.

The following historical visual mutation sources remain in repository history but are removed from the WPF compile:

- `MainWindow.RelayHardwarePresentation.cs`;
- `MainWindow.RelayFullFaceGloss.cs`;
- `MainWindow.RelayLedPresentation.cs`;
- `MainWindow.RelayPremiumButtonTuning.cs`;
- `Controls/RelayIndicatorLampBehavior.cs`;
- `Controls/RelayTactileButtonBehavior.cs`.

This removes dispatcher-order dependency and prevents multiple presentation owners from rewriting the same element.

## Existing state authorities retained

P6 deliberately preserves:

- `MainWindow.RelayFaceplate.P2.cs` for LCD pages, menus, event browsing, and operation records;
- `MainWindow.RelayMeasurementHome.cs` for the measurement matrix and stabilized phasor display;
- `RelayAnnunciationLatch` and the annunciation timer for pickup/trip cause latching;
- existing reset handling;
- existing virtual injection, process-bus, capture/replay, trust, and evidence behavior.

`MainWindow.P6VirtualRelay.cs` replaces the old visual subtree, redirects the existing named presentation anchors to P6, then reinitializes the retained LCD presenters. It does not create or update a protection engine and does not modify protection settings.

## Interaction contract

- navigation tooltips remain `Up`, `Down`, `Enter`, `Next`, and `Cancel`, preserving the existing faceplate handlers;
- reset delegates to the existing `Reset_Click` path;
- button focus chrome is disabled;
- press travel is 0.6 px;
- the lower lip remains visible while pressed;
- no blue click/focus ring is introduced.

## PNG asset decision

No PNG is required for the production faceplate.

The approved concept render remains a design reference. Native components provide:

- sharper output at Windows scaling levels;
- true dynamic LED states;
- proper hit testing;
- maintainable layout and typography;
- deterministic state ownership;
- future portability to Avalonia without image slicing.

A future marketing screenshot or orthographic product render may be stored separately, but it must not become the operational UI surface.

## Safety and compatibility

P6 does not change:

- protection algorithms;
- pickup, timing, dropout, reset, or trip-latch semantics;
- settings or fingerprints;
- virtual injection generation;
- IEC 61850 SV parsing;
- live capture or PCAP replay;
- trust gating;
- event/evidence content;
- release versioning;
- Avalonia packaging.

## Validation

Automated source contracts verify:

- all P6 XAML dictionaries and controls are well-formed XML;
- the faceplate uses one uniformly scaled native canvas;
- no PNG, image wallpaper, drawing brush, or geometry-parsed gloss patch is used;
- eight annunciation rows use one lamp component;
- lamp optical layers remain ordered and physically separate;
- keys use shallow travel and no keyboard-focus border;
- retired mutation sources are excluded from compilation;
- existing LCD and annunciation state authorities are rebound rather than duplicated;
- the P6 adapter does not reference protection-engine mutation APIs.

Manual Windows QA must still verify:

- visual proportions against the approved concept;
- readable LCD content at 100%, 125%, and 150% display scaling;
- consistent Healthy/Pickup/Trip/SMV Block hardware geometry;
- shallow key travel and absence of blue focus noise;
- menu navigation, event page, measurement/phasor page, and reset behavior;
- no clipping in the minimum supported main-window size.
