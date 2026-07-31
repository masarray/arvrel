# ARVREL architecture

## Repository boundary

ARVREL owns the virtual-relay application, protection algorithms, process-bus orchestration, measurement context, trust policy, and evidence presentation. The sibling ARIEC61850 repository remains the single source of truth for reusable IEC 61850 frame, SCL, Sampled Values, and Npcap primitives.

```text
Git/
├── ARIEC61850/
└── arvrel/
```

## P1 runtime pipeline

```text
Npcap live capture or PCAP/PCAPNG replay
        ↓
ARIEC61850 SampledValuesFrameParser
        ↓
stream key and optional SCL profile binding
        ↓
ordered payload decode or fixed value-quality fallback
        ↓
per-stream circular sample rings
        ↓
one-cycle RMS and two-cycle evidence window
        ↓
SMV trust policy
        ↓
Arvrel.Protection MeasurementFrame
        ↓
50P / 51P / 50N / 51N and trip latch
        ↓
immutable UI snapshot and JSON evidence
```

## Projects

- `Arvrel.App`: WPF shell, virtual relay, waveform, Lucide-derived icons, user workflow, and export.
- `Arvrel.ProcessBus`: live/replay source, stream discovery, SCL binding, decoding, sample rings, RMS, trust, and evidence models.
- `Arvrel.Protection`: deterministic protection elements independent from WPF refresh.
- `Arvrel.ProcessBus.Tests`: PCAP/PCAPNG reader and SV-to-protection regression tests.
- `Arvrel.Protection.Tests`: element and algorithm-policy regression tests.

## Threading

- Npcap capture runs outside the UI dispatcher and writes into the sibling transport's bounded channel.
- PCAP replay runs on a worker task.
- each stream runtime protects mutable rings and protection state with a private lock;
- the UI requests immutable snapshots at a bounded refresh cadence;
- protection evaluation is performed when decoded ASDUs arrive, not when WPF renders.

## Trust boundary

A stream may be displayed while trip is blocked. P1 evaluates:

- complete one-cycle measurement window;
- channel mapping;
- live freshness;
- payload decode health;
- `smpCnt` continuity;
- IEC 61850 quality words;
- SCL address, `svID`, dataset, and `confRev` consistency;
- engineering scaling provenance;
- SCL binding.

Unknown mapping or stale data blocks measurement. Recent gaps, non-zero quality, configuration mismatch, unresolved scaling, or absent SCL binding permit inspection and pickup visibility but remove trip permission.

## P1 limitations

- active outputs remain virtual only;
- P1 does not claim calibrated measurement, formal conformance, or deterministic real-time operation;
- generic SCL layouts are decoded through ARIEC61850, while inferred fixed layouts are clearly labelled;
- the Algorithm Editor remains a validated shadow-staging prototype; the deterministic DSL compiler and A/B runtime are P2.
