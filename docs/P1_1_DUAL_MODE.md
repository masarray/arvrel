# P1.1 dual-mode relay workflow

ARVREL P1.1 separates relay application settings from protection algorithm source and keeps the native protection engine as the standard reference.

```text
same measurement frame
  -> native standard algorithm (reference)
  -> bounded custom DSL algorithm (shadow or active)
  -> active native setting group
  -> SMV trust permission
  -> virtual trip only
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

Research mode exposes the exact standard algorithm source generated from the active setting group and a separate custom research workspace.

### Deterministic DSL gate

For P1.1 executable elements `50P-1`, `51P`, `50N`, and `51N`, custom source must pass both policy validation and typed deterministic compilation before it can be staged.

The sandbox exposes only:

- typed RMS current measurements and residual current;
- active protection settings through `setting("...")`;
- SMV trust booleans;
- deterministic arithmetic and boolean expressions;
- bounded timer/integrator state;
- IEC curve evaluation.

The sandbox does **not** expose host objects, files, directories, network access, processes, reflection, unmanaged calls, dynamic code loading, or unbounded loops.

Hard runtime limits are:

- 64 KiB source;
- 64 statements;
- 512 interpreter instructions per frame;
- 64 runtime variable slots;
- timer durations bounded to the relay laboratory range.

Every executable `trip = ...` expression must contain `smv.allowsTrip` as an explicit conjunctive gate. Comment-only, misplaced, negated, false-comparison, and OR-bypass forms are rejected. The host independently re-applies SMV trip permission before publishing any virtual trip request.

P2 feeder elements `67P`, `67N`, `27`, `59`, and `59N` remain exposed for source inspection and shadow research only; P1.1 runtime activation is intentionally restricted to the four 50/51 elements above.

### Standard/custom A/B

A staged custom definition runs in parallel with the native standard algorithm from the exact same `MeasurementFrame`:

- internal deterministic laboratory frames;
- live IEC 61850 Sampled Values frames;
- PCAP/PCAPNG replay frames.

Each protection snapshot can carry an A/B record containing standard and custom pickup/operate/trip state plus both source hashes. While custom is shadow-only, the native standard algorithm remains authoritative.

The editor also provides a deterministic synthetic A/B test bench that compares pickup and trip timing across a bounded scenario before staging or activation.

### Stage, Activate, Rollback

The lifecycle is deliberately explicit:

1. **Compile** — static policy, typed names/units, and instruction budget must pass.
2. **A/B test** — engineer reviews standard/custom behavior.
3. **Stage** — immutable content-addressed definition is stored by SHA-256 and settings fingerprint. Standard remains active.
4. **Activate** — only the exact staged hash can become active for that element.
5. **Rollback** — restores the previous custom version or the native standard algorithm.

Only one custom definition per protection element can be active at a time. Changing the active definition resets dynamic timing state and the virtual trip latch so state cannot leak across algorithms.

Activation identity includes:

- element;
- content-derived version;
- SHA-256 source hash;
- active settings fingerprint;
- author/activation note;
- activation timestamp;
- `virtual-only` output boundary.

A process-session audit trail is included in runtime evidence. The desktop editor additionally appends durable activation/rollback JSONL records under `%LocalAppData%\ARVREL\algorithms\activation-audit.jsonl`.

## Visible safety boundary

When a custom algorithm is active, the relay UI visibly reports `CUSTOM ACTIVE · VIRTUAL ONLY`. The native standard continues in parallel as the shadow reference and remains present in exported evidence.

ARVREL remains virtual-output only. No GOOSE trip, MMS control, relay contact, physical I/O driver, or physical trip path is introduced by either operating mode or by custom algorithm activation.
