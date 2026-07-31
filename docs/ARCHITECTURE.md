# Architecture

```text
Arvrel.App
  WPF shell, stationary waveform, relay faceplate, scenarios and editor UI
        │
        ├── Arvrel.Protection
        │     deterministic settings, timers, inverse curves, trip latch,
        │     SMV trust contract and algorithm policy validation
        │
        └── sibling ARIEC61850 (when present)
              SV/SCL/PCAP codecs, observation and Npcap transport foundation
```

The protection worker owns timing and state. UI refresh reads snapshots and never advances protection timers. P0 uses deterministic measurement frames; P1 will add a bounded adapter from ARIEC61850 stream observations to the same `MeasurementFrame` contract.

## Trust contract

`SmvTrustState` separates three permissions:

- `AllowsMeasurement`: samples may update engineering quantities;
- `AllowsPickup`: protection starts may be evaluated and shown;
- `AllowsTrip`: an operated element may assert the virtual trip latch.

This separation lets the laboratory demonstrate a vital process-bus behavior: an operating quantity can remain visible while an uncertain stream blocks a new trip request with a specific reason.
