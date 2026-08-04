# P4 — Virtual Injection Laboratory

## Objective

Make ARVREL independently useful when an engineer, student, or researcher does not have an IEC 61850 Sampled Values publisher.

Internal demo mode becomes a validated virtual secondary-injection source for the same measurement, phasor, protection, trust, annunciation, and evidence pipeline used by the application.

## Operator workflow

1. Select **Internal demo**.
2. Open the **INJECT** analysis view.
3. Select a visible preset or edit the 4I+4V table.
4. Changes are validated and debounced.
5. A complete profile is applied atomically; invalid partial edits never replace the last valid source.
6. One coherent measurement cycle is rebuilt before pickup and trip permission return.
7. Review the phasor, waveform, sequence quantities, relay LCD, protection timing, trip latch, event trace, and exported evidence.

## Table semantics

| Row | Unchecked | Checked |
|---|---|---|
| VA/VB/VC | channel is zero | explicit virtual phase voltage |
| IA/IB/IC | channel is zero | explicit virtual phase current |
| VN | `3V0 = VA + VB + VC` | independent virtual VN/3V0 channel |
| IN | `3I0 = IA + IB + IC` | independent virtual IN/3I0 channel |

All channels share one synchronous frequency in the public demo. Frequency validation is 40–70 Hz.

## Processing contract

```text
VirtualInjectionProfile
        ↓
synthetic 4I + 4V complete sample windows
        ↓
mean removal + nominal-frequency single-bin DFT
        ↓
complex RMS phase / residual / sequence phasors
        ↓
existing ProtectionEngine
        ↓
pickup · timing · trust restraint · virtual trip · evidence
```

The editor does not construct a protection decision directly and does not bypass the signal-estimation layer.

## State behavior

- **EDITING** — input changed and debounce is active.
- **INVALID · LAST VALID ACTIVE** — at least one field is invalid; the previous coherent profile remains authoritative.
- **APPLIED · REBUILDING** — the new immutable profile was accepted and one coherent cycle is rebuilding.
- **READY** — measurement, pickup, and virtual-trip evaluation may proceed.

Changing or clearing the injection does not clear an existing trip latch. **Reset relay** clears timers and trip evidence while retaining the injection. **Reset all** returns the source, trust, relay state, and markers to balanced nominal defaults.

## Built-in presets

- Normal balanced
- A-G, B-G, and C-G faults
- A-B and A-B-G faults
- Three-phase fault
- 27 undervoltage
- 59 overvoltage
- 59N residual voltage
- 67P forward and reverse
- 67N forward and reverse

Presets only populate the same editable table. They do not bypass validation or the normal processing chain.

## Evidence export

Internal evidence schema version 3 includes:

- complete virtual injection profile;
- injection SHA-256 fingerprint;
- apply timestamp and coherent-window state;
- common frequency and sample rate;
- explicit/calculated IN and VN provenance;
- trust-degraded state;
- measured phasors and protection snapshot;
- protection settings and settings fingerprint;
- event trace.

## Progress checklist

- [x] Immutable profile and per-channel validation
- [x] Culture-invariant SHA-256 profile fingerprint
- [x] Synthetic 4I+4V sample generator
- [x] Single-bin DFT measurement path
- [x] Explicit-neutral and calculated-residual provenance
- [x] Atomic apply with coherent-cycle pickup/trip restraint
- [x] Injection table and common-frequency editor
- [x] Auto apply with debounce and last-valid retention
- [x] Preset library
- [x] Phasor and waveform synchronization
- [x] Trip latch preserved when injection clears
- [x] Reset relay and reset-all separation
- [x] Evidence schema v3
- [x] Deterministic core tests
- [ ] Windows CI confirmation
- [ ] Visual smoke test on packaged application

## Safety boundary

Virtual injection is a software laboratory source. It is not a calibrated relay test set, IEC 60255 type-test source, real-time process-bus publisher, commissioning acceptance instrument, physical trip source, or operational switching authority.
