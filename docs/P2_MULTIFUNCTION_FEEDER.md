# P2 Multifunction Feeder Protection

## Implemented scope

P2 extends ARVREL from a current-only 50/51 laboratory into a 4I+4V virtual multifunction feeder relay.

Implemented standard-active elements:

- 50P, 51P, 50N and 51N from the existing current engine;
- 67P directional phase overcurrent;
- 67N directional earth fault;
- 27 undervoltage;
- 59 overvoltage;
- 59N residual overvoltage.

All outputs remain virtual. No physical contact, GOOSE trip or MMS control path is introduced.

## Measurement pipeline

```text
IEC 61850 SV 4I+4V
→ SCL-assisted or fixed-layout decode
→ CT/VT secondary engineering values
→ newest one-cycle RMS and fundamental DFT
→ symmetrical components I1/I2/I0 and V1/V2/V0
→ protection elements
→ SMV trust permission
→ virtual trip latch and evidence
```

The display scope uses complete locked two-cycle windows. RMS and phasor protection quantities use the newest one-cycle samples and are not delayed by the scope lock.

## 67P

The baseline phase-directional element uses:

- maximum phase RMS current as pickup magnitude;
- positive-sequence current `I1` as directional operating phasor;
- positive-sequence voltage `V1` as polarizing phasor;
- configurable maximum-torque/characteristic angle;
- forward or reverse direction;
- minimum polarizing-voltage supervision;
- definite-time operation and dropout.

Conceptual decision:

```text
pickup current
AND V1 available
AND selected directional torque region
AND definite timer complete
AND smv.allowsTrip
```

## 67N

The baseline earth-directional element uses residual `3I0` and `3V0` phasors with:

- residual-current pickup;
- configurable characteristic angle;
- forward or reverse direction;
- minimum residual polarizing voltage;
- definite-time operation and dropout.

Negative-sequence polarization and voltage-memory polarization are not implemented in this baseline.

## 27 and 59

Selectable voltage domains:

- phase-to-neutral;
- phase-to-phase;
- positive-sequence voltage.

Selectable phase logic:

- one of three;
- two of three;
- three of three.

27 uses a reset ratio above pickup. 59 uses a dropout ratio below pickup.

## 59N

59N operates from residual `3V0` magnitude with configurable pickup, dropout and definite delay.

## Native operation

Practitioner Mode exposes a **Feeder protection** settings tab. Settings participate in:

- validation;
- setting-group fingerprinting;
- save/load `.arvsettings` presets;
- factory-default restoration;
- timer and trip-latch reset on Apply;
- JSON evidence.

The relay LCD provides current/voltage phasors, feeder-element states and trip records. Directional trip records include operating angle and polarizing-voltage magnitude.

## Research operation

Algorithm Laboratory exposes read-only active standard sources for:

```text
50P-1  51P  50N  51N  67P  67N  27  59  59N
```

Custom source remains validation-and-shadow staging only. It does not replace the executing standard algorithm.

## Security and engineering boundaries

- Feeder elements default disabled.
- Missing voltage channels or incomplete phasor windows restrain feeder operation.
- Directional operation is restrained below minimum polarizing voltage.
- Existing SMV freshness, continuity, quality, mapping, scaling and SCL trust gates remain authoritative.
- This implementation is deterministic software regression evidence, not IEC 60255 conformance, type testing or calibrated relay performance evidence.

## Deferred feeder functions

The following remain roadmap items:

- 46 negative-sequence overcurrent;
- 47 phase sequence and voltage unbalance;
- 49 thermal overload;
- 81U/81O frequency;
- 32 forward/reverse power;
- 37 undercurrent;
- 50BF breaker failure;
- 79 autoreclose;
- 25 synch-check;
- 86 lockout;
- 74TCS trip-circuit supervision;
- 60 VT fuse-failure supervision.
