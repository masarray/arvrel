# ARVREL architecture

## Repository boundary

`masarray/arvrel` owns the stable Windows WPF product: the P6 virtual-relay interface, Windows operator workflows, protection algorithms, process-bus orchestration, measurement context, trust policy, evidence presentation, and official Windows packaging.

The sibling `masarray/ARIEC61850` repository remains the single source of truth for reusable IEC 61850 frame, SCL, Sampled Values, and native transport primitives.

```text
Git/
├── ARIEC61850/
└── arvrel/
```

The cross-platform Avalonia engineering preview is maintained separately in `masarray/arvrel-avalonia`. Its application source, tests, packaging, migration milestones, and CI are outside this repository.

## Dependency direction

The Windows shell depends inward on shared, UI-independent engineering layers:

```text
Arvrel.App (WPF P6 presentation)
        │
        ├── Arvrel.Application
        ├── Arvrel.ProcessBus ── Arvrel.Capture
        └── Arvrel.Protection
```

- `Arvrel.App` owns WPF XAML, controls, dialogs, operator interaction, display formatting, and Windows lifecycle.
- `Arvrel.Application` owns deterministic laboratory orchestration and immutable workspace state.
- `Arvrel.Capture` owns packet-source contracts and PCAP/PCAPNG replay.
- `Arvrel.ProcessBus` owns stream discovery, SCL binding, decode orchestration, continuity, measurement windows, trust, and evidence projection.
- `Arvrel.Protection` owns virtual injection, protection elements, timing, annunciation state, operation records, and trip-latch semantics.

Shared projects must not make protection timing depend on WPF rendering or dispatcher cadence.

## Capture and process-bus pipeline

```text
Live Npcap backend or PCAP/PCAPNG replay
        ↓
Timestamped Ethernet frame
        ↓
ARIEC61850 Sampled Values decoder
        ↓
Stream identity and optional SCL profile binding
        ↓
Channel mapping, scaling, quality, and continuity
        ↓
Per-stream sample rings
        ↓
One-cycle measurement and two-cycle evidence window
        ↓
SMV trust policy
        ↓
Protection MeasurementFrame
        ↓
Protection elements, timers, latch, and evidence
        ↓
Immutable WPF presentation snapshot
```

`Arvrel.ProcessBus` multi-targeting is an implementation detail of the shared runtime. The released desktop product in this repository remains Windows WPF.

## Virtual source and relay authority

The virtual source and relay are separate equipment authorities even when they run inside one laboratory session:

```text
VirtualInjectionProfile / VirtualInjectionRuntime
        ↓ measurement and waveform
InternalLabSession
        ↓
ProtectionEngine / ProtectionSettings
```

Authority rules:

- source START/STOP changes effective output without discarding configured source values;
- applying or clearing a source profile does not erase relay timers or a latched trip;
- relay reset does not change source profile, run state, sample counter, or fingerprints;
- applying protection settings preserves the source while resetting the appropriate relay state;
- complete laboratory reset is the only command that intentionally restores source and relay state together;
- invalid editor drafts never partially mutate active equipment state.

## P6 WPF presentation boundary

P6 is the only runtime authority for the physical geometry and materials of the public virtual-relay faceplate.

```text
VirtualRelayControl
├── enclosure and perimeter trim
├── identity header
├── shared RelayLampControl annunciators
├── recessed LCD and retained presenters
├── F1–F5 and reset column
├── navigation deck
└── footer and trust identity
```

The faceplate consumes existing protection, injection, process-bus, trust, event, measurement, and reset authorities. It does not instantiate a second protection engine or infer operation from visual state.

## Projects

- `Arvrel.App` — Windows WPF product shell, P6 relay controls, settings dialogs, waveform/phasor instruments, local preferences, and evidence export.
- `Arvrel.Application` — platform-neutral workspace state and deterministic laboratory orchestration required by the WPF application.
- `Arvrel.Capture` — capture contracts, captured-frame models, and PCAP/PCAPNG replay.
- `Arvrel.ProcessBus` — stream discovery, SCL binding, decoding, sample rings, measurements, continuity, trust, and evidence models.
- `Arvrel.Protection` — deterministic protection elements, virtual-injection models, annunciation, timing, and operation records.
- `Arvrel.Application.Tests`, `Arvrel.Capture.Tests`, `Arvrel.ProcessBus.Tests`, and `Arvrel.Protection.Tests` — regression coverage for the shared engineering core used by the WPF product.

The authoritative solution is `ARVREL.sln` at repository root.

## Threading and state

- live capture runs outside the WPF dispatcher and yields timestamped frames through an asynchronous backend contract;
- PCAP replay streams frames from disk without materializing the entire capture;
- each stream runtime protects mutable rings and protection state with a private lock;
- protection evaluation occurs when decoded ASDUs arrive, not when WPF renders;
- the UI requests immutable snapshots at a bounded refresh cadence;
- steady presentation updates are suppressed when operator-visible state has not changed;
- source and relay mutations enter shared layers only as complete validated models.

## Trust boundary

A stream may remain diagnostically visible while new trip authority is blocked. Trust evaluation includes:

- complete measurement windows;
- payload decode health;
- channel mapping and scaling provenance;
- live freshness and `smpCnt` continuity;
- IEC 61850 quality words;
- SCL address, `svID`, dataset, and `confRev` consistency;
- selected stream identity and source context.

Unknown mapping or stale data blocks measurement. Recent gaps, invalid quality, unresolved scaling, configuration mismatch, or absent SCL binding may permit inspection while removing trip permission. Rejected frames remain available as telemetry but do not enter measurement or protection buffers.

## Safety boundary

- outputs are virtual only;
- no calibrated measurement, IEC 61850 conformance, IEC 60255 type-test, or deterministic real-time claim is made;
- no operational GOOSE trip, MMS control, physical contact, or switching authority is implemented;
- Windows, Npcap, adapter drivers, publisher behavior, and host load influence live performance;
- the Algorithm Editor remains validated shadow staging rather than unrestricted runtime code execution.
