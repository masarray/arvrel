# P7.4 — CT observability engineering polish

## Objective

P7.4 hardens the public WPF CT study surface after manual Windows review of P7.2/P7.3. The numerical CT model remains unchanged; this phase improves terminology, relay-relevant measurement evidence, event lifecycle visibility, and desktop readability.

## Relay-relevant measurements

The CT table distinguishes two different error views:

- **Total RMS / RMS mag err**: time-domain RMS over the displayed evidence window, including waveform distortion and any residual DC content.
- **Fund. RMS / Fund. mag err / Phase err**: one-cycle fundamental measurement using the same `FundamentalPhasorEstimator` used by ARVREL relay measurements. The estimator removes the window mean and evaluates the final nominal cycle.

`Wave err` remains the normalized RMS waveform difference between ideal referred-secondary current and relay-secondary current.

`Vsec peak` is the maximum instantaneous secondary-circuit voltage observed in the evidence window. It is intentionally not labelled as an RMS value. The configured CT knee voltage `Vk` remains an RMS engineering parameter.

## Channel state wording

The public state column uses:

- `IDEAL` — nonlinear CT stage disabled;
- `BELOW KNEE` — nonlinear CT enabled and current evidence window remains below |flux| = 1 pu;
- `BELOW KNEE + HISTORY` — below knee in the current window while committed magnetic history exists;
- `SATURATED` — at least one evidence sample reaches |flux| >= 1 pu;
- `CALCULATED SUM` — 3I0 is derived from IA + IB + IC and therefore has no single physical CT-core state.

## Event time and decaying DC

The CT window now shows virtual source event elapsed time using the absolute source sample index and sample rate. Decaying-DC status is reported as a percentage of sinusoidal peak. Values below 0.1% are presented as decayed.

This distinction matters for the `CT saturation - A-G asymmetrical` preset: after many time constants the asymmetrical DC component is expected to disappear even when steady-state burden and remanence still produce CT saturation.

## Restart event

`Restart event` restarts the configured virtual source trajectory at t = 0 and reinitializes CT state from configured signed remanence. It deliberately keeps the process-bus sample counter and wall-clock timestamp continuous. One nominal cycle is marked rebuilding before normal protection authority resumes.

This differs from:

- **Reset CT state** — reapply configured remanence while keeping source phase and DC event time continuous;
- **Demagnetize CT** — force runtime CT flux to zero while keeping source event time and configured remanence unchanged.

Protection trip latch is not cleared by any of these CT-study controls.

## Persistent CT status

The CT IDEAL / CT NONLINEAR / CT SAT control is hosted in the shared analysis header. It therefore remains visible in INJECT, WAVE, DUAL, and PHASOR views and opens the same modeless CT observability window.

## Current and flux evidence

The comparison scope shows two synchronized plots for the selected channel:

1. ideal referred-secondary current (dashed) versus relay-secondary current (solid);
2. CT core flux in per-unit with explicit +1 pu and -1 pu knee boundaries.

Calculated 3I0 intentionally reports no single-core flux plot.

The flux preview is non-committing: it is calculated from the same committed CT state and source sample origin used by the displayed waveform but does not advance runtime history.

## Desktop layout

The CT summary table is bounded to the four public current rows and does not use a horizontal scrollbar at the normal engineering window width. The waveform/flux evidence area receives the remaining vertical space. Window onset text explicitly reports window-local milliseconds together with the absolute source sample number.

## Validation

Regression coverage includes:

- event restart returns source sample time to zero;
- process sample counter remains continuous across event restart;
- CT state is reinitialized from configured signed remanence;
- event restart enters one-cycle rebuilding state;
- fundamental magnitude and phase metrics are finite when evidence is available;
- long-running asymmetrical preset reports DC transient decay;
- below-knee carried channels are labelled with magnetic history.

P7.4 does not change the equivalent-circuit equations, excitation curve, saturation threshold, persistence schema, or independent CPython golden-vector contract.
