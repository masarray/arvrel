# P7.1 — Public WPF waveform time axis

## Objective

P7.1 turns the public Windows waveform evidence panel from a decorative fixed grid into a time-referenced engineering view. The timing and tick contract lives in `Arvrel.Application`, while the WPF control owns only drawing, trigger stabilization, and presentation.

## Delivered

- one framework-neutral `WaveformAxisLayout` used by the public WPF renderer;
- elapsed-time duration derived from sample count and sample rate;
- signal-cycle duration derived from waveform frequency;
- nominal estimator-cycle duration retained separately for off-nominal diagnostics;
- minor ticks at quarter-signal-cycle intervals;
- labelled major ticks at half-signal-cycle intervals;
- emphasized complete signal-cycle boundaries;
- an explicit right-edge timestamp for non-integer-cycle windows;
- invariant-culture millisecond formatting;
- clamped edge labels;
- sample-to-pixel projection based on true sample timestamps;
- frequency-correct positive-going zero-crossing stabilization;
- pickup and trip markers transformed by the same displayed-window origin.

## Timebase contract

The horizontal axis represents elapsed time on the sampling grid.

```text
window duration       = sample count / sample rate
last sample timestamp = (sample count - 1) / sample rate
signal cycle          = 1 / signal frequency
nominal cycle         = nominal samples per cycle / sample rate
trigger cycle samples = sample rate / signal frequency
```

The right edge represents the end boundary of the sample buffer. The final sample is one sampling interval before that boundary. Sample `i` is therefore projected to `i / sampleCount`, rather than stretching the final sample to the window boundary.

`t = 0` means **the sample rendered at the left edge of the displayed window**. The WPF scope may stabilize that point to a positive-going phase-current crossing. The axis, waveform traces, pickup marker, and trip marker all use that same display transform.

## Off-nominal correctness

Signal-cycle ticks and trigger stabilization do not use `samplesPerCycle / sampleRate`. That value describes the nominal estimator grid and can remain 20 ms while a virtual source is configured to 60 Hz.

For a 160-sample window on the fixed 4 kHz laboratory grid:

### 50 Hz signal

```text
window:        40 ms
signal cycle:  20 ms
nominal cycle: 20 ms
major labels:  0 · 10 · 20 · 30 · 40 ms
```

### 60 Hz signal on the same fixed grid

```text
window:        40 ms
signal cycle:  16.67 ms
nominal cycle: 20 ms
trigger cycle: 66.67 samples
major labels:  0 · 8.33 · 16.67 · 25 · 33.33 · 40 ms
```

The final 40 ms label is a window boundary, not a false 60 Hz cycle boundary.

## Architecture

```text
waveform timing metadata
        ↓
Arvrel.Application.Laboratory.WaveformAxisLayout
        ↓
normalized elapsed-time and signal-cycle ticks
        ↓
Arvrel.App WPF WaveformScope
```

The renderer is responsible only for:

- plot allocation;
- pens, brushes, and text metrics;
- trace and marker drawing;
- trigger-stabilized display rotation;
- labels and legend presentation.

The stable `masarray/arvrel` repository remains the public Windows WPF product. Avalonia and cross-platform preview work is maintained in its dedicated repository and is not reintroduced by P7.1.

## Validation

The display-server-free application regression suite checks:

- 50 Hz two-cycle timing, labels, and cycle boundaries;
- 60 Hz signal-cycle ticks on the real fixed 4 kHz / 80-sample nominal grid;
- separation of signal-cycle and nominal-cycle timing;
- explicit non-cycle right-window boundaries;
- last-sample timestamp semantics;
- true sample timestamp projection;
- invariant label formatting;
- stable viewport geometry;
- empty waveform handling;
- invalid timing metadata rejection.

## Compatibility

P7.1 does not change:

- waveform sample values;
- deterministic source timing or sample-counter progression;
- RMS or DFT calculations;
- protection settings or element algorithms;
- pickup, trip, latch, trust, or evidence semantics;
- process-bus capture and replay behavior.

## Deferred

- zoom and pan;
- cursor measurement and delta-time readout;
- absolute capture timestamps;
- per-lane engineering-unit scales;
- waveform export and print layout;
- visual snapshot testing at multiple DPI scales.
