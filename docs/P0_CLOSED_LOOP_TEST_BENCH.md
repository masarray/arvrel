# P0 closed-loop virtual test bench

P0 turns the internal ARVREL laboratory into two black-box virtual devices connected through an explicit laboratory backplane:

```text
VIRTUAL TEST SET                         VIRTUAL PROTECTION RELAY
VA  ------------------------------------------------->  VA
VB  ------------------------------------------------->  VB
VC  ------------------------------------------------->  VC
VN  ------------------------------------------------->  VN
IA  ------------------------------------------------->  IA
IB  ------------------------------------------------->  IB
IC  ------------------------------------------------->  IC
IN  ------------------------------------------------->  IN
BI1 TRIP  <-------------------------------------------  BO1 TRIP
BI2 PICKUP <------------------------------------------  BO2 PICKUP
```

The virtual test set owns analog output generation and independent external timing. The protection relay owns measurement, protection algorithms, pickup/trip logic, latching, relay-local evidence and the delayed output contacts. Neither side is allowed to bypass the virtual wires.

## Authority boundary

The test set must never read `ProtectionSnapshot.TripLatched` to stop injection or measure trip time.

The allowed path is:

```text
relay protection logic
  -> relay BO1 trip request
  -> virtual output-contact operate delay
  -> BO1 terminal state
  -> BO1-to-BI1 virtual wire
  -> TESTSET.BI1 rising edge
  -> external trip timestamp
  -> optional auto-stop of analog outputs
```

A disconnected BO1-to-BI1 wire therefore produces the same black-box behavior as an open cable in a physical test setup: the relay can indicate and latch trip while the test set sees no trip, records no trip time and does not stop automatically.

## Deterministic clock

The WPF UI remains a presentation surface and can refresh at tens of milliseconds. The closed-loop simulation authority advances at the existing internal source sample grid:

- nominal sample rate: 4 kHz;
- simulation quantum: 0.25 ms;
- source/relay/wiring/contact/timer order is deterministic within each quantum.

This separates simulation accuracy from UI refresh rate.

## Virtual relay contacts

P0 models binary output contact latency independently from protection logic. The default numerical-relay laboratory profile is:

- pickup output operate delay: 1 ms;
- trip output operate delay: 3 ms;
- contact release delay: 1 ms.

Relay-local operate time and test-set measured time are intentionally different pieces of evidence. External timing includes the modeled output-contact delay.

## Typed topology

The default topology contains eight analog wires and two binary feedback wires. Each wire has a stable ID, source terminal, destination terminal, signal type and connected state. The topology has a SHA-256 fingerprint so exported evidence can identify the exact virtual wiring used for a test.

Disconnecting an analog wire changes the measurement delivered to the relay; it does not modify the injector configuration. Disconnecting a binary feedback wire changes only what the test set can observe.

## Evidence

Closed-loop evidence schema 5 records:

- configured and effective injector state;
- 0.25 ms simulation quantum;
- virtual topology and topology fingerprint;
- relay output-contact profile;
- independent test-set pickup/trip timestamps and elapsed times;
- relay-side measurement and protection snapshot;
- active settings fingerprint;
- actual standard/custom algorithm runtime identity;
- event trace.

## P0 acceptance cases

1. Connected trip feedback: relay operates, BO1 closes after contact latency, TESTSET.BI1 detects the edge, external trip time is recorded and injector output stops.
2. Open trip feedback: relay still trips, TESTSET.BI1 never detects trip and injector continues running.
3. Open IA analog wire: injector can remain configured above pickup while the relay sees zero IA through the open wire and must not behave as if IA were still connected.
4. Topology identity changes whenever a wire connected state changes.
5. Simulation authority is fixed to the 4 kHz / 0.25 ms source grid rather than the WPF dispatcher interval.

## Safety boundary

P0 remains virtual-only. The backplane is an in-process deterministic laboratory model. It does not add a physical current/voltage output driver, physical binary I/O, GOOSE trip publisher, MMS control path or any other path capable of operating real primary plant.
