# ARVREL architecture

## Repository boundary

ARVREL owns the virtual-relay application, protection algorithms, process-bus orchestration, measurement context, trust policy, and evidence presentation. The sibling ARIEC61850 repository remains the single source of truth for reusable IEC 61850 frame, SCL, Sampled Values, and native transport primitives.

```text
Git/
├── ARIEC61850/
└── arvrel/
```

## Application dependency direction

P5.0 introduces a platform-neutral application layer so present and future desktop shells depend inward on shared orchestration rather than owning simulation behavior themselves.

```text
Arvrel.App (WPF presentation)       Arvrel.Desktop (Avalonia presentation)
              \                         /
               \                       /
                Arvrel.Application
                        ↓
                Arvrel.Protection
```

Presentation projects may adapt application snapshots into framework-specific controls. `Arvrel.Application` must not reference WPF, Avalonia, Windows desktop APIs, dialogs, controls, or dispatchers.

## Capture dependency direction

P5.1 separates packet-source ownership from Sampled Values processing.

```text
ILiveCaptureBackend / ICaptureReplaySource
        ↓
Arvrel.ProcessBus controller
        ↓
ARIEC61850 SV decoder
        ↓
continuity, trust, measurement, and protection feed
```

`Arvrel.Capture` targets plain `net8.0` and owns portable capture contracts plus classic-PCAP/PCAPNG replay. `Arvrel.ProcessBus` targets both `net8.0` and `net8.0-windows`; only the Windows target references the Npcap transport. A future libpcap or BPF implementation can satisfy the same live-backend contract without changing the controller.

## Avalonia presentation boundary

P5.2 adds a second executable shell rather than converting the WPF application in place.

```text
Arvrel.Desktop (net10.0)
        ↓
MainWindowViewModel and Avalonia controls
        ↓
ArvrelWorkspace / InternalLabSession (net8.0)
        ↓
immutable scenario and protection snapshots
```

The Avalonia shell owns XAML, styles, dispatcher timing, commands, display formatting, and custom waveform rendering. It consumes the same deterministic internal laboratory and process-bus capability boundary as any future presentation. It does not duplicate source generation, protection evaluation, capture parsing, SV decoding, trust policy, or active-output behavior.

`Arvrel.App` remains the current Windows product shell. `Arvrel.Desktop` is the cross-platform migration target. Both remain buildable until bounded feature slices reach parity and release policy explicitly changes.

The repository deliberately maintains two solutions during migration:

- `ARVREL.sln` retains the established .NET 8 WPF/core build and release path;
- `ARVREL.Desktop.sln` owns the .NET 10 Avalonia shell, its portable dependencies, and shell tests.

The split prevents the newer presentation toolchain from silently changing Windows release packaging while still preserving one-way references from the shell into the portable core.

## P1 runtime pipeline

```text
live capture backend or PCAP/PCAPNG replay source
        ↓
platform-neutral timestamped Ethernet frame
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

- `Arvrel.App`: current WPF presentation shell, framework-specific controls, dialogs, user workflow, and Windows release packaging.
- `Arvrel.Desktop`: .NET 10 Avalonia cross-platform shell, presentation ViewModels, dispatcher lifecycle, and portable waveform control.
- `Arvrel.Application`: platform-neutral workspace state and deterministic laboratory orchestration shared by current and future presentation layers.
- `Arvrel.Capture`: platform-neutral live-capture contracts, captured-frame models, and classic-PCAP/PCAPNG replay.
- `Arvrel.ProcessBus`: multi-target SV stream discovery, SCL binding, decoding, sample rings, RMS, trust, evidence models, and backend orchestration.
- `Arvrel.Protection`: deterministic protection elements and virtual-injection models independent from presentation refresh.
- `Arvrel.Application.Tests`: application-boundary and source-lifecycle regression tests.
- `Arvrel.Capture.Tests`: portable capture-contract and replay regression tests.
- `Arvrel.Desktop.Tests`: display-server-free Avalonia shell lifecycle and application-core integration tests.
- `Arvrel.ProcessBus.Tests`: capture injection, compatibility replay, and SV-to-protection regression tests.
- `Arvrel.Protection.Tests`: element and algorithm-policy regression tests.

## Threading

- live capture runs outside the UI dispatcher and yields timestamped frames through an asynchronous backend contract;
- PCAP replay streams frames from disk without materializing the complete capture file;
- each stream runtime protects mutable rings and protection state with a private lock;
- the UI requests immutable snapshots at a bounded refresh cadence;
- protection evaluation is performed when decoded ASDUs arrive, not when a presentation framework renders;
- the Avalonia shell uses a 40 ms dispatcher timer and advances the deterministic laboratory through fixed 5 ms substeps;
- closing the Avalonia window stops source activity and asynchronously disposes process-bus resources.

P5.0 keeps the existing WPF refresh cadence unchanged. P5.1 keeps capture filter, buffer, timeout, decoder, and trust behavior unchanged while making source selection injectable. P5.2 adds presentation scheduling only and does not alter deterministic source or protection timing.

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
