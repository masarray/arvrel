# P1.1 dual-mode relay workflow

ARVREL P1.1 separates relay application settings from protection algorithm source.

```text
SMV measurement
  -> active standard algorithm
  -> active native setting group
  -> SMV trust permission
  -> virtual trip
```

## Practitioner mode

Practitioner mode presents a numerical-relay-style workflow. The operator configures protection without opening code.

### Native settings

- setting group name, revision and SHA-256 fingerprint;
- enable/disable per implemented protection element;
- 50P-1 phase instantaneous pickup, delay and dropout ratio;
- 51P pickup, characteristic, TMS, definite delay, minimum operate time, dropout and reset behavior;
- 50N earth instantaneous pickup, delay and dropout ratio;
- 51N pickup, characteristic, TMS, definite delay, minimum operate time, dropout and reset behavior;
- secondary-ampere entry with primary-equivalent indication from the active CT context;
- save and load `.arvsettings` presets;
- restore laboratory defaults.

Supported 51P and 51N characteristics:

- IEC Standard / Normal Inverse;
- IEC Very Inverse;
- IEC Extremely Inverse;
- IEC Long-Time Inverse;
- Definite Time;
- user-defined IEC-form `K`, `alpha`, and `C` parameters.

The IEC-form calculation is:

```text
t = TMS x (K / (M^alpha - 1) + C)
```

where `M` is measured current divided by pickup current. A configurable minimum operate time is applied after the characteristic calculation.

Applying a setting group resets element timing memory and the virtual trip latch. Live capture is stopped and the process-bus relay runtime is recreated with the new immutable settings snapshot. Imported SCL and CT context are restored when possible.

## Research mode

Research mode exposes the exact standard algorithm source generated from the active setting group.

- active standard source is read-only and clearly marked as executing;
- custom source is edited in a separate shadow workspace;
- settings remain referenced through typed `setting("...")` calls rather than embedded constants;
- `smv.allowsTrip` remains mandatory;
- policy validation rejects file, network, process, unmanaged and unbounded execution concepts;
- validated source can be staged as an immutable JSON definition tied to the active settings fingerprint;
- staged custom source does not replace the active algorithm in P1.1.

## Current boundary

P1.1 delivers active native settings and exposed algorithm source. Custom algorithm activation and standard-versus-custom runtime A/B execution remain future work. The UI and evidence explicitly describe custom code as shadow-only.

ARVREL remains virtual-output only. No GOOSE trip, MMS control, relay contact or physical trip path is introduced by either operating mode.
