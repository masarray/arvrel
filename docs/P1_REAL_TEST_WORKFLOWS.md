# P1 real test workflows

P1 turns the P0 closed-loop virtual bench into a practical relay test set rather than a static injector.

## Workflow authority

All workflow stop and transition decisions are made from the virtual test-set binary inputs:

```text
relay protection logic
  -> relay BO contact
  -> virtual feedback wire
  -> TESTSET BI1 / BI2
  -> workflow decision
```

P1 never uses relay-internal `TripLatched`, pickup flags, or algorithm state as workflow authority. Relay snapshots remain available only for evidence and presentation.

This means an open feedback wire changes workflow behavior exactly like an open cable in a physical secondary-injection setup.

## Continuous source-state updates

Changing a source value during a real test workflow is not treated as a new configuration lifecycle event.

When injected frequency and CT model identity are unchanged, P1 preserves:

- source sample index / waveform phase continuity;
- active CT numerical state and remanence history;
- coherent measurement authority;
- the 4 kHz / 0.25 ms deterministic simulation grid.

A frequency or CT-model identity change falls back to the normal protected apply path and rebuilds coherence.

## Step ramp

A step ramp applies a bounded sequence of RMS levels with a fixed dwell time. It can stop on:

- no feedback: run all levels;
- BI2 pickup;
- BI1 trip.

The reported operate level is the commanded level at which the selected test-set binary input is observed.

## Pulse ramp

Pulse ramp alternates between a baseline value and increasing/decreasing test pulses.

Each pulse records:

- pulse number;
- commanded RMS;
- relay-side measured RMS after virtual wiring;
- BI2 pickup state;
- BI1 trip state;
- elapsed test time.

The workflow stops when the selected feedback is observed or the pulse range is exhausted.

## Automatic pickup/dropout search

Pickup/dropout search performs two passes through TESTSET.BI2:

1. increase the selected quantity until BI2 asserts;
2. decrease it until BI2 releases.

The reported resolution is the configured RMS step. Dropout ratio is calculated from the externally observed pickup and dropout levels.

Because the search uses BI2, opening the relay BO2 -> TESTSET BI2 wire causes the search to report no feedback even if the relay internally picks up.

## State sequencer

A state sequence contains 1 to 32 deterministic states. Each state contains:

- a complete virtual injection profile;
- maximum state duration;
- optional transition on a new BI2 pickup edge or BI1 trip edge.

Typical sequence:

```text
Pre-fault / normal
  -> fixed duration
Fault
  -> advance on BI1 trip edge or timeout
Post-fault / normal
  -> fixed duration
```

The transition trigger is edge-based so a stale asserted binary input from a previous state cannot silently skip the next state.

## Operator UI

The internal injection workspace exposes a `Tests…` control alongside P0 `Wiring…`.

The P1 console supports:

- workflow selection;
- analog signal selection;
- start/end/step RMS;
- dwell or pulse duration;
- pulse baseline and reset duration;
- stop/advance feedback selection;
- fault preset selection for state sequence;
- result and observation trace.

While the workflow console owns the bench, the normal WPF presentation timer does not advance the internal relay. This prevents double-driving the deterministic simulation clock.

## Deterministic limits

P1 bounds workflows to prevent accidental unbounded runs:

- step ramp: max 10,000 levels;
- pulse ramp: max 2,000 pulses;
- state sequence: 1 to 32 states;
- individual state: max 30 seconds;
- total state sequence: max 60 seconds;
- normal ramp/search dwell: max 5 seconds;
- minimum time step: 0.25 ms.

All workflows stop the virtual source when they finish or are cancelled.

## Acceptance contract

P1 is accepted when the following remain true:

1. step ramp detects pickup from wired TESTSET.BI2;
2. opening BO2 -> BI2 makes pickup ramp/search report no feedback;
3. trip ramp detects TESTSET.BI1 rather than relay internal trip state;
4. pulse ramp identifies the first pulse that produces the selected binary response;
5. pickup/dropout search observes both BI2 assertion and release;
6. state sequence advances on a new wired BI edge and times out instead when that feedback wire is open;
7. continuous RMS changes do not restart waveform phase or force a one-cycle coherence rebuild;
8. workflow completion/cancellation leaves virtual outputs de-energized.

## Safety boundary

P1 remains virtual-only. It adds no physical current or voltage output driver, physical binary I/O, GOOSE trip publishing, MMS control, or real-plant actuation path.
