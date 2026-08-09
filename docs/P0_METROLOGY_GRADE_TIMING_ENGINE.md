# P0 metrology-grade timing engine

This tranche moves ARVREL closed-loop timing away from a UI-oriented phasor/timestamp model and toward a secondary-injection metrology model with explicit clock domains and causal relay acquisition.

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
  -> independent TESTSET BI sampler / deglitch
  -> accepted BI1 edge
  -> measured trip time / optional auto-stop
```

`ProtectionSnapshot.TripLatched` may request relay BO1, but it is never read directly as the test-set trip result or auto-stop authority.

## Clock domains

### Test-set metrology clock

The test-set clock is a monotonic integer microsecond counter. It is independent of WPF refresh and independent of wall-clock `DateTimeOffset` formatting.

Desktop profile:

- metrology clock resolution: 1 us;
- TESTSET binary-input sample rate: 10 kHz;
- BI sample period: 100 us.

A measured START-to-BI duration does not have to be an exact multiple of 100 us because output T0 and the free-running BI sampler can have an asynchronous phase offset. The accepted BI edge is nevertheless resolved on the independent BI sampling grid.

### Relay processing grid

The current desktop relay/source processing authority remains 4 kHz / 250 us. WPF still refreshes at a much slower presentation cadence and has no authority over trip timing.

## Causal relay front end

The metrology desktop path no longer passes the source-side RMS/phasor estimate directly into the relay front end.

Instead, the relay front end consumes instantaneous terminal samples and performs:

1. virtual analog wiring;
2. signed input clipping against the configured peak range;
3. signed ADC quantization;
4. configured input/filter group delay;
5. a causal one-cycle rolling DFT built only from samples that have already arrived.

A real powered relay does not begin a timed injection with an empty DFT/filter window. Before T0 it has already been sampling the pre-fault condition. The behavioral front end therefore starts each stopped-source test from one settled cycle of zero/pre-fault history. New fault samples replace that history causally after T0. This produces a physically meaningful step response instead of an artificial one-cycle blackout before pickup is even possible.

The default numerical-relay behavioral profile is currently:

- 16-bit equivalent ADC;
- 20 A RMS current full scale;
- 300 V RMS voltage full scale;
- 4 kHz ADC rate;
- 1.5 ms front-end/filter delay;
- one nominal 50 Hz rolling measurement cycle.

These are generic behavioral model parameters, not a claim about a named commercial relay.

## Protection timer edge semantics

Definite and inverse timer integration starts at the first observed pickup frame. The interval between the preceding non-pickup frame and the pickup frame is not retroactively counted.

Therefore, when a 60 ms definite-time setting is exactly representable on the 250 us relay processing grid, pickup-to-trip is exactly 60.000 ms in the reference engine.

## Generic pickup versus operated-element pickup

The relay BO2 pickup contact is intentionally an **ANY PICKUP** output. With the normal desktop setting group, 50P, 51P, 50N and 51N can all be enabled at the same time. An earth or time-overcurrent element may therefore assert BO2 before the element that ultimately produces trip.

This distinction is now explicit:

- `TESTSET.BI2` measures the wired generic ANY-PICKUP contact;
- the relay event timeline records the first generic pickup request that drives BO2;
- relay `P->T` is correlated to `LatchedOperation.Element` and its own element-specific pickup timestamp;
- the live `TripLatched` rising edge remains the timestamp that requests BO1;
- BO1-to-BI1 timing is measured from that live trip request to the independent TESTSET.BI1 edge.

Consequently BI2 may legitimately occur before the pickup timestamp of the element that eventually trips. Those two timestamps must never be subtracted from each other and presented as the operated element's `P->T`.

## Binary contact and input path

Desktop relay contact behavior:

- pickup BO operate delay: 1 ms;
- trip BO operate delay: 3 ms;
- release delay: 1 ms;
- deterministic contact bounce: 1 ms;
- bounce period: 250 us.

TESTSET BI behavior is modeled separately:

- sample rate: 10 kHz;
- deglitch: 0.5 ms;
- debounce holdoff: 0 ms.

Deglitch and debounce are distinct concepts. Deglitch requires a candidate state to remain stable before it is accepted. Debounce holdoff suppresses further accepted transitions for a configured interval after an accepted edge.

## Event correlation

A timed run stores one test-run ID and one T0. The metrology timeline can correlate:

- output application;
- generic relay pickup assertion that drives BO2;
- live relay trip request that drives BO1;
- TESTSET BI2 accepted ANY-PICKUP;
- TESTSET BI1 accepted trip;
- output stop command.

The operated-element timing correlation additionally records:

- the element that actually operated;
- that element's pickup from T0;
- operation-record trip from T0;
- exact element pickup-to-trip interval;
- live trip request from T0;
- live trip request to TESTSET.BI1;
- operation-record-to-live-trip correlation error.

Relay operation-record timestamps remain diagnostic evidence; the TESTSET result still comes only from the wired BI path. The live `TripLatched` edge is used to explain the BO1 physical path, while the operation record is used to pair trip with the correct protection element's pickup rather than with generic BO2 pickup.

## Evidence

Closed-loop evidence schema 8 includes:

- test-set metrology profile;
- timing resolution;
- relative microsecond pickup/trip timestamps;
- generic live relay pickup and live trip-request timestamps;
- operated-element timing correlation;
- metrology event timeline;
- causal relay front-end snapshot;
- front-end/contact profiles and fingerprints;
- topology and test-run identity;
- exact trip capture;
- protection and algorithm evidence.

`DateTimeOffset` remains available for human-readable relay/event timestamps, but TESTSET duration calculations in metrology mode use the monotonic integer clock.

## Desktop acceptance scenario

The regression reproduces a normal GUI-style test rather than a simplified 50P-only laboratory shortcut:

- source preset: A-B-G fault;
- default protection settings, including 50P, 51P, 50N and 51N enabled;
- expected first operation: 50P-1;
- 50P pickup: 4 A;
- 50P definite delay: 60 ms;
- settled causal numerical-relay front end enabled;
- realistic relay BO model enabled;
- metrology TESTSET BI path enabled.

Acceptance requires:

1. the relay front end is already measurement-valid at T0 from settled pre-fault history;
2. the relay does not jump immediately to the final fault phasor, but a strong 8.4 A fault crosses the 4 A pickup threshold during the causal window transition without the old ~40 ms source/phasor latency or an empty-window 20 ms blackout;
3. generic ANY-PICKUP timing is kept separate from the eventual 50P operated-element pickup;
4. the correlated 50P pickup-to-trip interval equals the exact 60 ms setting when representable on the processing grid;
5. live relay trip request correlates to the operation record within one relay processing quantum;
6. live trip request to TESTSET.BI1 remains within the modeled BO/bounce/deglitch and 10 kHz sampling budget;
7. the full desktop A-B-G closed-loop result stays well below the previously observed unexplained ~124.75 ms result;
8. TESTSET trip timing comes from the BI1 edge, not relay internals;
9. opening BO1 -> BI1 allows the relay to trip internally but prevents TESTSET trip measurement and auto-stop;
10. the event timeline remains monotonic and internally self-consistent.

## Remaining fidelity boundary

This is still a behavioral numerical-relay model, not a certified clone of a named relay or a calibrated physical test set. Future device profiles may add measured anti-alias transfer functions, exact ADC topology, channel skew, input burden, contact-specific bounce distributions, binary-input threshold voltage/current models, and manufacturer-specific protection processing pipelines.
