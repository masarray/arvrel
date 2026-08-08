# P18 — Transformer two-sided secondary injection

## Problem

P17 deliberately reused the complete OCR operator workspace, but the existing internal injection editor still drove only the single-feeder OCR scenario. The Transformer Differential relay therefore showed moving waveform/phasor evidence without receiving a paired HV/LV measurement.

## Runtime correction

P18 keeps one protection authority. The internal test source creates two synchronized synthetic `SmvRuntimeSnapshot` objects and passes them to `TransformerProtectionRuntime.EvaluateSnapshots()`:

- `VIRTUAL-87T-HV`
- `VIRTUAL-87T-LV`
- same timestamp
- same `smpCnt`
- `smpSynch = 2`
- 80 samples/cycle
- two coherent waveform cycles

The normal P9 pairing, P10 harmonic estimation, P13 CT waveform evidence, engineering compensation and P8 87T/REF engine remain in the path.

The internal source does **not** publish Ethernet Sampled Values. It is an in-process secondary-injection model only.

## Eight current channels

The Transformer injection drawer exposes independently editable secondary-current phasors:

| Side | Phase channels | Neutral / NGR channel |
|---|---|---|
| HV | IA, IB, IC | IN / NGR |
| LV | IA, IB, IC | IN / NGR |

Every channel has RMS amperes and angle. Neutral channels also have an explicit availability switch.

`IN / NGR` is never synthesized from `IA + IB + IC`. `PhasorMeasurementSet.NeutralCurrentAvailable` is true only when the corresponding explicit neutral channel is enabled, preserving the independent-neutral-CT requirement of REF.

## Stable through-load baseline

The default vector is generated from the active transformer nameplate, phase CT ratios and vector-group compensation. P18 first chooses balanced normalized HV currents, then solves the raw CT-secondary HV/LV phasors so that:

```text
I_HV(normalized) + I_LV(normalized) ≈ 0
```

This provides a genuine stable-through-load starting point instead of assuming equal secondary amperes or hard-coding one vector group.

## Included editable presets

- Balanced through load
- Internal A fault
- REF HV / NGR
- REF LV / NGR

Presets are only starting vectors; the operator can edit all eight currents afterwards.

## Shared OCR instruments

The P17 workspace remains the master UI. In Transformer + Internal demo mode:

- the INJECTION drawer switches from the feeder 4I+4V editor to the Transformer 8-current editor;
- DISPLAY selects `HV / Primary` or `LV / Secondary` for the shared waveform and phasor instruments;
- Start/Stop injection controls the paired Transformer virtual source;
- the lower operation panel consumes the authoritative 87T / 87T-HS / REF HV / REF LV snapshot;
- Live Capture and PCAP Replay are never overwritten by the synthetic source.

## Relay LCD

On the Transformer HOME page the shared physical relay LCD receives a compact presentation-only single-line:

```text
HV --CT--[ 87T ]--CT-- LV
A   IHV-A          ILV-A
B   IHV-B          ILV-B
C   IHV-C          ILV-C
N   IN-HV          IN-LV
Id  Idiff-A Idiff-B Idiff-C pu
```

HV/LV/neutral currents come from the paired measurement snapshot. `Idiff` comes directly from `TransformerDifferentialPhaseSnapshot.OperatingCurrentPu`; the UI does not recalculate differential current.

## Verification contracts

Automated tests cover:

1. synchronized distinct HV/LV virtual streams;
2. stable through-load with near-zero `Idiff`;
3. internal A-fault drives the authoritative restrained 87T to trip;
4. neutral/NGR availability remains independent of phase residual;
5. HV NGR operates only the HV REF path;
6. LV NGR operates only the LV REF path;
7. stopped source presents zero waveform and resets protection timing/latch;
8. UI source contracts prevent a second `TransformerProtectionEngine` and prevent the internal source from overwriting live/replay authority.

## Safety boundary

P18 remains virtual protection evidence only:

- no physical trip;
- no breaker output;
- no GOOSE trip;
- no synthetic Ethernet SV transmission;
- no calibration or IEC type-test claim.
