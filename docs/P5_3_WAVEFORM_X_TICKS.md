# P5.3 — Waveform time axis and x-ticks

## Objective

P5.3 turns the Avalonia waveform evidence panel from a decorative grid into a time-referenced engineering view. The plotted samples remain owned by the portable application and protection layers; the desktop shell now derives a stable horizontal time axis from waveform metadata.

## Delivered

- reserves a dedicated x-axis band below the waveform plot;
- derives the displayed duration from sample count and sample rate;
- derives cycle duration from samples-per-cycle and sample rate;
- renders minor ticks at quarter-cycle intervals;
- renders labelled major ticks at half-cycle intervals;
- emphasizes complete-cycle boundaries;
- formats labels in milliseconds using invariant culture;
- clamps edge labels so they remain visible at the left and right viewport limits;
- preserves four independent current lanes and zero-reference lines;
- keeps the axis geometry independent from DPI and operating-system text metrics.

For the current deterministic 50 Hz, 80-samples-per-cycle, two-cycle source, the major labels are:

```text
0 ms · 10 ms · 20 ms · 30 ms · 40 ms
```

For a 60 Hz, 96-samples-per-cycle, two-cycle source, the same layout automatically becomes:

```text
0 ms · 8.33 ms · 16.67 ms · 25 ms · 33.33 ms
```

## Geometry boundary

`WaveformAxisLayout` is a pure geometry and formatting component. It accepts `ScenarioWaveform` metadata and returns normalized tick positions plus semantic tick types. It does not depend on a display server, renderer, dispatcher, or protection engine.

```text
ScenarioWaveform metadata
        ↓
WaveformAxisLayout
        ↓
normalized quarter-cycle ticks
        ↓
WaveformScope Avalonia rendering
```

`WaveformScope` owns only rendering concerns:

- plot and axis viewport allocation;
- grid, cycle-boundary, baseline, and tick pens;
- formatted tick labels;
- sample-to-pixel projection.

## Validation

The display-server-free regression suite checks:

- 50 Hz two-cycle duration and tick count;
- 50 Hz major labels and cycle-boundary positions;
- 60 Hz frequency-aware decimal labels;
- invariant decimal formatting;
- stable viewport insets and axis-band geometry;
- empty waveform handling without invented timing data.

The existing Avalonia portability workflow continues to compile XAML, run shell tests, publish native app hosts, and validate outputs on Windows, Ubuntu, and macOS.

## Compatibility

P5.3 does not change:

- `ScenarioWaveform` data shape;
- deterministic source timing;
- sample values or sample counter progression;
- RMS calculation;
- protection settings or element algorithms;
- pickup, trip, latch, trust, or evidence semantics;
- process-bus capture or replay behavior;
- WPF application rendering or packaging.

## Deferred

- zoom and pan;
- cursor measurement and delta-time readout;
- trigger markers and trip-event overlay;
- absolute capture timestamps;
- per-lane engineering-unit scales;
- waveform export and print layout.
