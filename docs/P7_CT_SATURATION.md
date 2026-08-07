# P7 — Current Transformer Saturation Study Model

## Objective

P7 adds a deterministic current-transformer saturation stage to ARVREL virtual injection so protection algorithms can be exercised with distorted secondary current rather than ideal sinusoidal current only.

The implementation is an engineering-study model. It is not an IEC 61869 type-test implementation, a calibrated digital twin of a specific CT, or a replacement for an EMT program when detailed hysteresis and magnetic-material validation are required.

## Signal path

```text
configured RMS / angle / decaying DC offset
                  │
                  ▼
        ideal referred secondary current
                  │
                  ▼
     CT equivalent circuit + excitation curve
                  │
                  ├── persistent flux/current/voltage state
                  ├── flux linkage and excitation diagnostics
                  ▼
        distorted relay secondary current
                  │
                  ▼
       DFT / phasor estimator / protection engine
```

Each phase CT is evaluated independently. When IN is an explicit virtual channel, it receives its own CT stage. When IN is calculated, `3I0` is formed from the already-distorted phase secondary currents, matching the relay-side summation path.

## Physical approximation

The model follows the protective-CT equivalent-circuit relationship:

```text
i_ideal = i_secondary + i_excitation
v_secondary = (Rct + Rburden) · i_secondary + Lburden · di_secondary/dt
dλ/dt = v_secondary
```

Core flux linkage `λ` is integrated on the fixed virtual-sampling grid. Excitation current is calculated from a continuous piecewise curve:

- below knee flux: low cubic excitation current;
- above knee flux: configurable post-knee power curve;
- excitation and flux are bounded by explicit numerical study limits.

The resulting excitation current is subtracted from the ideal referred current. This produces ratio error, phase/waveform distortion, flat or collapsed current regions, and dependence on burden, fault asymmetry, and initial remanence.

A relaxed fixed-point iteration solves the secondary-current/excitation-current coupling for each sample without changing the deterministic sampling cadence.

## Configurable parameters

`CtSaturationSettings` exposes:

- rated secondary current;
- RMS knee-point voltage;
- CT secondary winding resistance;
- burden resistance and inductance;
- excitation current at knee flux;
- excitation current at twice knee flux;
- post-knee saturation exponent;
- signed initial remanence;
- maximum flux and excitation-current study limits.

`VirtualInjectionChannel` also supports a decaying DC component:

- DC offset as a percentage of sinusoidal peak;
- DC time constant in milliseconds.

The DC component represents asymmetrical fault-current inception and is included in the injection fingerprint.

## Stateful runtime

P0.2 introduces `CtSaturationChannelState` and `CtSaturationRuntimeState`. For every active current channel the runtime retains:

- absolute flux linkage in volt-seconds;
- previous secondary current;
- previous secondary voltage;
- processed-sample count.

`VirtualInjectionRuntime.Advance` now commits only the samples that elapsed since the previous call. The two-cycle waveform returned to the UI is a non-committing preview beginning at the current source sample index. This prevents UI refresh cadence from changing CT physics.

Source phase and decaying DC time also advance continuously. A 5 ms refresh followed by another 5 ms refresh is therefore equivalent to one continuous 10 ms source trajectory; it no longer restarts the same asymmetric fault at `t = 0` for every frame.

### State-transition rules

- `Start` begins a new deterministic injection event, clears previous runtime history, and seeds the CT from configured remanence.
- Source-only profile changes restart source event time at `t = 0` while preserving magnetic state when CT settings and frequency are unchanged.
- CT parameter or frequency changes reset magnetic state because they represent a different runtime model identity.
- `Stop` remains an absolute 0 V / 0 A interlock and clears runtime magnetic history.
- `Reset` clears source time, sample state, and magnetic state.
- Stateless `VirtualInjectionGenerator.Generate` remains available for isolated reproducible analysis windows.

## Diagnostics

Every generated current channel publishes:

- saturation state and saturated-sample count;
- first saturated sample and time;
- first saturation absolute sample index within the carried CT trajectory;
- initial and final flux per unit;
- whether state was carried into the frame;
- initial and final processed-sample counts;
- maximum absolute flux per unit of knee flux;
- maximum excitation current;
- maximum secondary voltage demand;
- ideal and reproduced RMS current;
- signed RMS magnitude error;
- normalized waveform error;
- minimum instantaneous magnitude ratio above the diagnostic current floor.

`CtSaturationFrameDiagnostics` aggregates phase and explicit-neutral channels and is surfaced through both `VirtualInjectionFrame` and the platform-neutral `ScenarioStep`. `VirtualInjectionRuntimeSnapshot` also exposes the committed CT runtime state and continuous source sample index.

## Built-in study case

The preset `CT saturation - A-G asymmetrical` provides a deterministic severe case:

- 20 A RMS phase-A fault current;
- 100% decaying DC offset with a 60 ms time constant;
- 70 V knee point;
- 1 Ω CT winding resistance;
- 3.5 Ω resistive burden;
- 0.2 mH burden inductance;
- +60% initial remanence.

The preset is intentionally severe enough to make waveform collapse and relay-estimator error visible in a two-cycle 4 kHz laboratory window.

## Runtime and safety contracts

- CT parameters and fault asymmetry participate in the injection fingerprint.
- Applying a changed profile rebuilds one coherent nominal measurement cycle before pickup authority is restored.
- Runtime CT state does not participate in the configuration fingerprint; it is transient evidence exposed in the runtime snapshot.
- STOP remains an absolute 0 V / 0 A virtual-output interlock. The configured CT model remains armed in `ActiveProfile`, while stopped output disables CT response and clears runtime state.
- Existing ideal presets remain exact pass-through cases because CT saturation is disabled by default.

## Automated validation

Tests cover:

- exact pass-through when disabled;
- negligible error below knee flux;
- saturation under high burden, asymmetrical current, and remanence;
- earlier onset and larger ratio error with increased burden;
- integration of distorted secondary current into phasor measurement and calculated residual current;
- fingerprint coverage for CT parameters and DC offset;
- rejection of invalid excitation curves;
- split stateful processing matching one continuous CT solve sample-for-sample;
- magnetic state and decaying-DC time continuity across runtime advances;
- source-event changes preserving state when CT identity is unchanged;
- CT parameter changes resetting state;
- STOP and Reset clearing transient runtime history.

## Known limitations

The current P7 model deliberately does not claim:

- Jiles–Atherton or Preisach hysteresis;
- minor-loop formation;
- frequency-dependent core loss;
- temperature-dependent winding resistance;
- three-dimensional leakage-flux effects;
- manufacturer-specific excitation-curve fitting from arbitrary point sets;
- IEC 61869 transient-class certification;
- protection-grade hard real-time execution.

Initial remanence is represented as signed starting flux rather than a full hysteresis state. P0.2 retains the numerical equivalent-circuit state across related runtime frames, but it does not synthesize magnetic minor loops or long-term material memory beyond that state.

## Reference boundary

The implementation direction is consistent with the application guidance in IEC 61869-2 and IEC TR 61869-100: protective CT response depends on transient current, secondary circuit, and magnetic behavior. PSCAD documents equivalent-circuit CT models and higher-fidelity Jiles–Atherton alternatives, while SEL's *Beyond the Knee Point* material emphasizes burden, asymmetrical current, remanence, excitation characteristics, and relay-event consequences.

Those references define the engineering problem and validation boundary. ARVREL's equations, parameterization, solver, diagnostics, state transition rules, and tests are independently implemented for deterministic educational and protection-algorithm research use.
