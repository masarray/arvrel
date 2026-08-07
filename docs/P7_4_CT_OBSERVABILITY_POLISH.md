# P7.4 — CT saturation UX and observability finalization

## Objective

P7.4 closes the manual Windows UX findings from the public CT-saturation study path. The CT equivalent-circuit equations remain unchanged. This phase makes the feature discoverable, separates source conditions from CT modelling, and presents relay-relevant evidence without requiring the operator to infer CT state from the normal SMV waveform.

## Source preset and CT model are independent

The INJECT workspace exposes two separate choices:

- **SOURCE PRESET** selects the 4I+4V operating condition or fault;
- **CT MODEL** selects ideal pass-through or a nonlinear protective-CT study model.

Changing an ordinary source preset preserves the selected CT model. This makes combinations such as B-G fault + high burden + positive remanence or three-phase fault + nominal CT directly available without editing JSON or relying on one A-G demonstration preset.

Built-in CT model starting points are:

- Ideal (CT off)
- Protection CT · nominal
- High burden · rem 0%
- High burden · rem +60%
- High burden · rem -60%

The existing `CT saturation - A-G asymmetrical` convenience scenario remains supported. Additional convenience scenarios cover B-G asymmetrical, C-G asymmetrical, and three-phase high-burden studies.

## Persistent CT entry point

A clearly labelled **CT MODEL · IDEAL / ACTIVE / SAT** button is hosted in the shared analysis header and remains visible in INJECT, WAVE, DUAL, and PHASOR views. Clicking it opens **CT Saturation & Observability**.

The label deliberately describes both the feature and its current state; it is not a cryptic status-only abbreviation.

## Relay-relevant measurements

The CT observability surface distinguishes:

- **Total RMS** — time-domain RMS over the displayed evidence window, including distortion and residual DC;
- **Fundamental RMS** — the relay-equivalent final one-cycle DFT measurement;
- **Fundamental magnitude error** — secondary fundamental magnitude relative to the ideal referred current;
- **Phase error** — secondary fundamental phase displacement relative to the ideal referred current;
- **Wave error** — normalized sample-domain waveform difference.

The fundamental values use the same public `FundamentalPhasorEstimator` used by ARVREL protection measurements.

`Vsec peak` is the maximum instantaneous secondary-circuit voltage in the evidence window. The configured `Vk` remains an RMS engineering parameter.

## Magnetic and event state

Per-channel wording distinguishes:

- `IDEAL`
- `BELOW KNEE`
- `BELOW KNEE + HISTORY`
- `SATURATED`
- `CALCULATED SUM`

The window shows source-event elapsed time and the state of any decaying DC component. A residual component below 0.1% of sinusoidal peak is reported as decayed.

`Restart event` returns the virtual source trajectory to t=0 and reapplies configured signed remanence while preserving the process sample counter and wall-clock continuity. `Reset CT state` reapplies remanence without restarting source-event time. `Demagnetize CT` sets runtime flux to zero without altering configured remanence or source-event time.

## Current and flux evidence

The selected physical CT channel is shown with synchronized evidence:

1. ideal referred-secondary current (dashed) versus relay-secondary current (solid);
2. core flux in per-unit with explicit +1 pu and -1 pu knee boundaries.

Calculated 3I0 has no single physical CT core, so no core-flux trace is claimed for that row.

The flux evidence is generated as a non-committing preview from the same committed runtime state and source window. It does not advance magnetic history.

## Desktop layout

The conclusion-first P0 global UX foundation remains intact. P7.4 adds event/DC facts, relay fundamental metrics, and a compact channel-detail table without a horizontal scrollbar at the normal engineering window width. The evidence graph receives the majority of vertical space.

## Validation

Regression coverage includes:

- CT model catalog uniqueness, validation, and name round-trip;
- B-G and C-G asymmetrical CT study scenarios;
- balanced three-phase high-burden CT scenario;
- event restart with process sample-counter continuity;
- CT remanence reinitialization;
- relay-equivalent fundamental magnitude and phase metrics;
- decaying-DC status;
- absolute saturation onset after CT reset/demagnetize.

This remains an engineering-study equivalent-circuit model, not IEC 61869 type-test evidence or a manufacturer-calibrated magnetic digital twin.
