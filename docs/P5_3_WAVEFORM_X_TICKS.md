# P5.3 — Waveform time axis and x-ticks

## Objective

P5.3 turns the waveform evidence panel from a decorative grid into a time-referenced engineering view. P0.1 hardening moves the timebase contract into `Arvrel.Application` so the public WPF edition and the Avalonia shell render the same frequency-correct axis.

## Delivered

- one framework-neutral `WaveformAxisLayout` shared by WPF and Avalonia;
- a dedicated x-axis band below the waveform plot;
- elapsed-time duration derived from sample count and sample rate;
- signal-cycle duration derived from the waveform frequency;
- nominal estimator-cycle duration retained separately for off-nominal diagnostics;
- minor ticks at quarter-signal-cycle intervals;
- labelled major ticks at half-signal-cycle intervals;
- emphasized complete signal-cycle boundaries;
- an explicit right-edge timestamp for non-integer-cycle windows;
- invariant-culture millisecond formatting;
- clamped edge labels in both renderers;
- sample-to-pixel projection based on true sampling timestamps;
- four independent current lanes and stable zero-reference lines.

## Timebase contract

The horizontal axis always represents elapsed time on the sampling grid.

```text
window duration       = sample count / sample rate
last sample timestamp = (sample count - 1) / sample rate
signal cycle          = 1 / signal frequency
nominal cycle         = nominal samples per cycle / sample rate
```

The right edge represents the end boundary of the sample buffer. The final sample is one sampling interval before that boundary. Renderers therefore project sample `i` to `i / sampleCount`, rather than stretching the final sample to the end boundary.

`t = 0` means **the sample rendered at the left edge of the displayed window**. The WPF scope may stabilize that displayed start to its locked positive-going current crossing; the axis remains relative to the displayed trace and pickup/trip markers are rotated by the same display transform.

## Off-nominal correctness

Signal-cycle ticks no longer use `samplesPerCycle / sampleRate`. That value describes the nominal estimator grid and can remain 20 ms while a virtual source is configured to 60 Hz.

For a 160-sample window on the fixed 4 kHz laboratory grid:

### 50 Hz signal

```text
window:       40 ms
signal cycle: 20 ms
major labels: 0 · 10 · 20 · 30 · 40 ms
```

### 60 Hz signal on the same fixed grid

```text
window:       40 ms
signal cycle: 16.67 ms
nominal cycle: 20 ms
major labels: 0 · 8.33 · 16.67 · 25 · 33.33 · 40 ms
```

The final 40 ms label is the window boundary; it is not falsely marked as a 60 Hz cycle boundary.

## Architecture

`WaveformAxisLayout` is a pure geometry and formatting component in `Arvrel.Application.Laboratory`. It accepts waveform timing metadata and returns normalized tick positions plus semantic tick types.

```text
waveform timing metadata
        ↓
Arvrel.Application.WaveformAxisLayout
        ↓
normalized elapsed-time and signal-cycle ticks
        ├── WPF WaveformScope
        └── Avalonia WaveformScope
```

Renderer responsibilities are limited to:

- viewport allocation;
- pens, brushes, and text metrics;
- grid and tick drawing;
- trace projection;
- marker and legend presentation.

## Validation

The display-server-free regression suite checks:

- 50 Hz two-cycle duration, labels, and cycle boundaries;
- 60 Hz signal-cycle ticks on the real fixed 4 kHz / 80-sample nominal grid;
- separation between signal-cycle and nominal-cycle timing;
- explicit non-cycle right-window boundaries;
- last-sample timestamp semantics;
- true sample timestamp projection;
- invariant label formatting;
- stable viewport insets and axis-band geometry;
- empty waveform handling;
- invalid timing metadata rejection.

The desktop shell preset-count regression was also updated for the new CT saturation study preset so the Avalonia shell portability workflow does not fail on a stale fixed count.

## Compatibility

P0.1 does not change:

- waveform sample values;
- deterministic source timing or sample counter progression;
- RMS or DFT calculation;
- protection settings or element algorithms;
- pickup, trip, latch, trust, or evidence semantics;
- process-bus capture and replay behavior;
- WPF trigger stabilization behavior.

## Deferred

- zoom and pan;
- cursor measurement and delta-time readout;
- absolute capture timestamps;
- per-lane engineering-unit scales;
- waveform export and print layout;
- visual snapshot testing at multiple DPI scales.
