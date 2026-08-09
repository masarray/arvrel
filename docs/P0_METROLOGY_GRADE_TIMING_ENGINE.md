# P0 metrology-grade timing engine

> **Milestone status:** this P0 timing engine is shipped in **ARVREL v0.1.0-beta.6**. The subsequent operator/evidence/reset tranche in PR #131 extended the same architecture with evidence schema 9, first-any-pickup attribution, frozen-capture semantics, and deterministic one-click relay RESET/re-arm. For the current product contract, use [`CURRENT_STATUS.md`](CURRENT_STATUS.md); this file remains the detailed metrology-engine design record.

This tranche moved ARVREL closed-loop timing away from a UI-oriented phasor/timestamp model and toward a secondary-injection metrology model with explicit clock domains and causal relay acquisition.

## Authority model

The virtual test set owns the measured trip result.

```text
TESTSET metrology clock T0
  -> instantaneous secondary waveform
  -> virtual analog wiring
  -> relay terminal samples
  -> relay ADC / clipping / quantization
  -> causal rolling measurement window
  -> protection pickup / timer / trip request
  -> relay BO operate delay / contact bounce
  -> virtual binary wire
  -> independent TESTSET BI sampler / deglitch / debounce
  -> accepted BI1 edge
  -> measured trip time / optional auto-stop
```

`ProtectionSnapshot.TripLatched` may request relay BO1, but it is never read directly as the TESTSET trip result or auto-stop authority.

## Clock domains

### TESTSET metrology clock

The test-set clock is a monotonic integer microsecond counter. It is independent of WPF refresh and independent of wall-clock `DateTimeOffset` formatting.

Desktop profile:

- metrology clock resolution: 1 µs;
- TESTSET binary-input sample rate: 10 kHz;
- BI sample period: 100 µs;
- BI deglitch: 0.5 ms;
- BI debounce holdoff: 0 ms.

A measured START-to-BI duration does not have to be an exact multiple of 100 µs because output T0 and the free-running BI sampler can have an asynchronous phase offset. The accepted BI edge is nevertheless resolved on the independent BI sampling grid.

### Relay processing grid

The current desktop relay/source processing authority remains 4 kHz / 250 µs. WPF refresh is presentation cadence only and has no authority over protection or TESTSET timing.

## Causal relay front end

The metrology desktop path no longer passes the source-side RMS/phasor estimate directly into the relay front end.

Instead, the relay front end consumes instantaneous terminal samples and performs:

1. virtual analog wiring;
2. signed input clipping against the configured peak range;
3. signed ADC quantization;
4. configured input/filter group delay;
5. a causal one-cycle rolling DFT built only from samples that have already arrived.

A powered numerical relay does not begin a timed injection with an empty DFT/filter window. Before T0 it has already been sampling the pre-fault condition. The behavioral front end therefore starts each stopped-source test from one settled cycle of zero/pre-fault history. New fault samples replace that history causally after T0. This avoids an artificial one-cycle measurement blackout.

The default numerical-relay behavioral profile is:

- 16-bit equivalent ADC;
- 20 A RMS current full scale;
- 300 V RMS voltage full scale;
- 4 kHz ADC rate;
- 1.5 ms front-end/filter delay;
- one nominal 50 Hz rolling measurement cycle.

These are generic behavioral model parameters, not a claim about a named commercial relay.

## Protection timer edge semantics

Definite and inverse timer integration starts at the first observed pickup frame. The interval between the preceding non-pickup frame and the pickup frame is not retroactively counted.

Therefore, when a 60 ms definite-time setting is exactly representable on the 250 µs relay processing grid, pickup-to-trip is exactly **60.000 ms** in the reference engine.

## Generic pickup versus operated-element pickup

The relay BO2 pickup contact is intentionally an **ANY PICKUP** output. With the normal desktop setting group, multiple feeder elements may be enabled at once, so an earth/time element can assert BO2 before the element that ultimately trips.

The shipped distinction is explicit:

- `RELAY ANY PU [source]` identifies the first generic pickup request that drives BO2;
- `TESTSET.BI2` measures the wired generic ANY-PICKUP contact;
- relay operated-element `P->T` is correlated to `LatchedOperation.Element` and its own pickup timestamp;
- the live `TripLatched` rising edge is the relay trip request that drives BO1;
- BO1-to-BI1 timing is measured from that live trip request to the independent TESTSET.BI1 edge.

Consequently BI2 may legitimately occur before the pickup timestamp of the element that eventually trips. Those timestamps must never be subtracted and presented as the operated element's `P->T`.

## Binary contact and input path

Desktop relay contact behavior:

- pickup BO operate delay: 1 ms;
- trip BO operate delay: 3 ms;
- release delay: 1 ms;
- deterministic contact bounce: 1 ms;
- bounce period: 250 µs.

TESTSET BI behavior is modeled separately:

- sample rate: 10 kHz;
- deglitch: 0.5 ms;
- debounce holdoff: 0 ms.

Deglitch and debounce are distinct concepts. Deglitch requires a candidate state to remain stable before it is accepted. Debounce holdoff suppresses further accepted transitions for a configured interval after an accepted edge.

## Event correlation

A timed run stores one test-run ID and one T0. The metrology timeline correlates:

- output application;
- generic relay pickup assertion that drives BO2;
- TESTSET BI2 accepted ANY-PICKUP;
- operated-element pickup and trip correlation;
- live relay trip request that drives BO1;
- TESTSET BI1 accepted trip;
- output stop command.

The operated-element timing correlation additionally records the element that operated, its pickup from T0, operation-record trip from T0, exact element pickup-to-trip interval, live trip request, live trip-request-to-BI1 interval, and operation-record-to-live-trip correlation error.

Relay operation-record timestamps remain diagnostic evidence; the TESTSET result still comes only from the wired BI path.

## Evidence schema 9

The original P0 tranche introduced the metrology evidence foundation as schema 8. The **shipped beta.6 state is schema 9** after the operator/evidence tranche.

Schema 9 includes:

- test-set metrology profile and timing resolution;
- relative microsecond pickup/trip timestamps;
- first generic ANY-PICKUP source and live relay trip-request timestamps;
- operated-element timing correlation;
- metrology event timeline;
- causal relay front-end snapshot;
- front-end/contact profiles and fingerprints;
- topology and test-run identity;
- exact trip/frozen capture and BI1-versus-capture-frame relationship;
- protection and algorithm evidence.

`DateTimeOffset` remains available for human-readable relay/event timestamps, but TESTSET duration calculations use the monotonic integer clock.

## Frozen capture and one-click reset/re-arm

After accepted TESTSET BI1 causes auto-stop, source output is OFF while the completed run remains available as **OUTPUT OFF · FROZEN CAPTURE**. The frozen relay/waveform frame may be a later causal 250 µs processing frame than the accepted BI1 edge; schema 9 preserves that relationship rather than labeling them as one instant.

Relay RESET uses one deterministic `ClosedLoopRelayResetTransaction`:

1. when source output is already OFF, advance causal relay acquisition in 250 µs quanta until stale fault-window pickup drops out;
2. clear relay latch/timers once;
3. continue the modeled feedback path until relay trip latch is clear, no protection pickup remains, BO1/BO2 are LOW, and TESTSET BI1/BI2 are LOW;
4. expose **READY TO RE-ARM** only after that postcondition is true.

The transaction has a bounded 100 ms simulated settle timeout with diagnostics on failure. It preserves completed TESTSET timing and frozen trip/event evidence and does not restart or mutate source setpoints/output. If the source remains energized, protection may legitimately reassert.

## Desktop acceptance scenario

The regression reproduces a normal GUI-style test rather than a simplified 50P-only laboratory shortcut:

- source preset: A-B-G fault;
- normal default protection settings with multiple elements enabled;
- expected first operation: 50P-1;
- 50P pickup: 4 A;
- 50P definite delay: 60 ms;
- settled causal numerical-relay front end enabled;
- realistic relay BO model enabled;
- metrology TESTSET BI path enabled.

Acceptance requires:

1. relay front-end measurement is valid at T0 from settled pre-fault history;
2. the relay does not jump immediately to the final fault phasor, but a strong fault crosses pickup causally without the old source/phasor latency or an empty-window blackout;
3. generic ANY-PICKUP timing stays separate from the eventual operated-element pickup;
4. correlated 50P pickup-to-trip equals the exact 60 ms setting when representable on the processing grid;
5. live relay trip request correlates to the operation record within one relay processing quantum;
6. live trip-request-to-TESTSET.BI1 stays inside the modeled BO/bounce/deglitch/10 kHz path budget;
7. the full desktop closed-loop result rejects the old unexplained ~124.75 ms behavior;
8. TESTSET trip timing comes from BI1, not relay internals;
9. opening BO1 -> BI1 permits internal relay trip but prevents TESTSET trip measurement and auto-stop;
10. the event timeline remains monotonic and internally self-consistent;
11. one relay RESET transaction after auto-stop reaches the full READY TO RE-ARM postcondition while preserving completed evidence.

## Remaining fidelity boundary

This is still a behavioral numerical-relay/test-set model, not a certified clone of a named relay or a calibrated physical test set. Future device profiles may add measured anti-alias transfer functions, exact ADC topology, channel skew, input burden, real binary-input voltage/current thresholds, measured/statistical contact behavior, manufacturer-specific protection processing/fast paths, and hardware-calibrated timing uncertainty.
