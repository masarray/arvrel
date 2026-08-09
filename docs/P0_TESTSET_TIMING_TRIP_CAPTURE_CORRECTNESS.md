# P0 test-set timing and trip-capture correctness

This tranche makes closed-loop timing behave like a physical secondary-injection test set rather than a UI timer layered on relay internals.

## Authority boundary

The test-set timer is authoritative only from its wired binary inputs:

```text
relay protection
  -> relay BO contact delay
  -> contact bounce
  -> virtual wire
  -> TESTSET BI debounce
  -> accepted BI edge
  -> measured time / optional auto-stop
```

`ProtectionSnapshot.TripLatched` remains relay-internal state. It is never used directly as TESTSET trip authority.

## Timer state machine

The virtual test set exposes four timer states:

- `Idle` — no timed test is active;
- `Armed` — injection is running and the timer is waiting for wired BI edges;
- `Completed` — the selected trip edge has been accepted and the measured result is latched;
- `Blocked` — a new timed test was refused because monitored feedback was already active before start.

## Correct arming

Before a timed test starts, output remains OFF while the modeled relay front end and binary-feedback path are allowed to settle.

If TESTSET.BI1 or TESTSET.BI2 is already active after settlement, the new run is not armed. The bench does not reset BO or BI state to manufacture a new rising edge.

A blocked start:

- does not start virtual output;
- does not increment the test-run ID;
- does not replace the previous measured edge;
- reports the active BI that prevented arming.

A relay reset remains separate equipment authority. With output OFF, the BO release, contact bounce and TESTSET BI debounce path is advanced naturally before the next run is armed.

## Exact trip edge

The deterministic authority remains 4 kHz / 0.25 ms.

When TESTSET.BI1 accepts a rising edge and auto-stop is enabled, the closed-loop bench stops the source and returns immediately from the same 0.25 ms quantum. It does not continue through the unused remainder of a WPF 40 ms presentation tick.

The returned step therefore represents the exact energized frame that existed when the test set accepted trip.

## Trip capture

`ClosedLoopTripCapture` latches:

- test-run ID;
- accepted BI1 timestamp;
- source waveform and phasor measurement at the edge;
- relay-side front-end measurement at the edge;
- protection snapshot at the edge;
- test-set timing snapshot at the edge.

The physical virtual output is still stopped immediately. Later zero-output simulation frames do not overwrite the capture.

The desktop waveform and phasor views reassert the captured frame while the source is stopped. The capture is released when a new timed injection begins.

## Timing domains

The UI and evidence distinguish these quantities explicitly:

```text
Injection START -> relay pickup
Injection START -> relay internal trip
relay pickup    -> relay internal trip (P->T)
Injection START -> TESTSET.BI2 pickup
Injection START -> TESTSET.BI1 trip
```

These values are related but are not interchangeable.

For a valid current run, TESTSET.BI1 trip should normally be later than relay internal trip because the external measurement includes the relay output-contact path, wiring and BI acceptance delay.

Relay operation timestamps are correlated only when their pickup and trip timestamps belong to the current timed run. A stale latched operation from an older run is not presented as the current run's relay timing.

## Operator BI strip

The injection footer exposes a compact test-set panel with:

- circular BI2 PICKUP state indicator;
- BI2 rising-edge time;
- circular BI1 TRIP state indicator;
- BI1 rising-edge time;
- live ARMED timer;
- latched `MEASURED TRIP` result;
- explicit `NOT ARMED` state when feedback is pre-active.

Raw relay BO state remains available in the Wiring view and test-set tooltip so contact behavior can be distinguished from accepted BI state.

## Evidence

Closed-loop evidence schema 7 includes:

- timer state;
- arm-block reason;
- test-run ID;
- current-run relay pickup/trip timestamps;
- TESTSET BI pickup/trip timestamps;
- exact trip capture;
- front-end/contact profiles and fingerprints;
- topology and algorithm evidence already present in earlier schemas.

## Acceptance contract

P0 timing/capture correctness is accepted when:

1. a completed trip with BI1 still active cannot be re-armed as a fresh timed run;
2. a blocked arm attempt cannot create a synthetic new BI edge;
3. relay reset allows the modeled feedback path to release before a subsequent run arms;
4. auto-stop returns on the exact accepted BI1 quantum;
5. the captured source frame remains energized/faulted even though the physical virtual output is already stopped;
6. later zero-output frames do not overwrite the captured waveform/phasor;
7. relay internal timing and TESTSET external timing are correlated to the same run and labeled with distinct time bases;
8. disconnecting BO1 -> BI1 still prevents TESTSET trip detection and auto-stop.

## Manual acceptance

1. Reset relay and confirm BI1/BI2 are OFF.
2. Start a fault injection.
3. Confirm timer shows ARMED.
4. Observe BI2 rising-edge time when pickup contact is accepted.
5. Observe BI1 rising-edge time and `MEASURED TRIP` when trip contact is accepted.
6. Confirm output stops at BI1 and waveform/phasor remain frozen at the trip condition.
7. Press Start again without resetting relay. The test must show `NOT ARMED` and must not create a second trip time.
8. Reset relay. BI contacts must release through the modeled path.
9. Start again. A new run ID and new timing sequence must begin.
10. Disconnect BO1 -> BI1 in Wiring and repeat. Relay may trip internally, but TESTSET must not measure trip or auto-stop from relay internals.
