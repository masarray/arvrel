# P7.2 — Public CT observability

## Objective

P7.2 makes the nonlinear current-transformer stage visible and operable in the public Windows WPF product. The operator can distinguish ideal current, active nonlinear behavior, and visible saturation without reading exported JSON or inspecting source code.

## Public status contract

The Virtual Injection toolbar exposes one CT status badge:

- `CT IDEAL` — the nonlinear CT stage is disabled;
- `CT NONLINEAR` — CT parameters are armed or active without saturation in the current preview;
- `CT SAT` — at least one current channel reaches knee flux in the current preview.

Opening the badge displays the model configuration, runtime state, per-channel diagnostics, and an ideal-versus-relay-secondary waveform comparison.

## Observability window

The modeless CT window provides:

- read-only CT settings: rated secondary current, knee-point voltage, winding resistance, burden resistance/inductance, and configured remanence;
- runtime source sample index and committed magnetic-history length;
- IA, IB, IC, and explicit IN diagnostics;
- calculated `3I0` provenance when neutral current is derived from phase currents;
- initial, final, and maximum absolute flux in per-unit knee flux;
- ideal and reproduced secondary RMS current;
- signed RMS magnitude error and normalized waveform error;
- maximum excitation current and secondary voltage demand;
- saturation onset time and absolute source sample index;
- a dashed ideal-current reference overlaid with the solid relay-secondary current.

The ideal reference is regenerated from the same absolute source sample index, signal frequency, phase angle, and decaying-DC time used by the stateful runtime. It does not advance or mutate CT state.

## Runtime controls

### Reset CT state

Reapplies the configured signed remanence to every enabled CT channel. Source phase and decaying-DC time remain continuous. Protection pickup and trip authority are restrained for one nominal cycle while a coherent measurement window is rebuilt.

### Demagnetize CT

Sets runtime flux, previous secondary current, and previous secondary voltage to zero while preserving the configured profile and configured remanence. The source event time remains continuous. A later complete Start/Stop lifecycle still uses the configured remanence.

Both controls are disabled when output is stopped or when the nonlinear CT stage is disabled.

## Direct-editor data integrity

The simple public injection table edits only enable state, RMS magnitude, and phase angle. P7.2 preserves hidden decaying-DC parameters and the complete CT settings object whenever that table is edited. Selecting or tuning a CT study preset therefore no longer silently converts it back to an ideal source.

## Evidence and safety boundary

The public window presents the model as an engineering-study equivalent circuit. It does not claim:

- IEC 61869 type-test conformance;
- manufacturer-calibrated magnetic material behavior;
- Jiles–Atherton or Preisach hysteresis;
- minor-loop or long-term material memory.

STOP remains an absolute 0 V / 0 A virtual-output interlock and clears transient CT runtime history.

## Automated validation

Tests cover:

- ideal, nonlinear, and saturated public status projection;
- per-channel diagnostic projection and calculated-residual provenance;
- absolute-time ideal-current reference generation;
- calculated ideal residual current;
- reset-to-remanence without restarting source time;
- demagnetization without changing the configured profile;
- disabling state controls when stopped or when the CT model is ideal;
- preservation of CT and decaying-DC settings through the direct editor.
