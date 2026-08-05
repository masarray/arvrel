# P5.1 — Packet-capture abstraction

## Objective

P5.1 removes native packet-capture and PCAP parser ownership from the Sampled Values process-bus controller. The controller consumes platform-neutral frame sources while the current Windows product continues using the existing Npcap implementation.

This is a transport-boundary change. It does not alter IEC 61850 decoding, stream identity, continuity handling, measurement windows, trust policy, protection timing, or evidence semantics.

## Dependency direction

```text
Arvrel.App (current WPF shell)
        |
        v
Arvrel.ProcessBus (SV decode, continuity, trust, protection feed)
        |
        +----> Arvrel.Capture (portable contracts and PCAP replay)
        |
        +----> Npcap adapter (net8.0-windows implementation only)
```

`Arvrel.Capture` targets plain `net8.0` and contains no WPF, Windows desktop, Npcap, ARIEC61850, or native-library dependency.

`Arvrel.ProcessBus` multi-targets `net8.0` and `net8.0-windows`. Both targets use the same SV decoder, replay, continuity, trust, and protection-feed code. Only the Windows target references the native Npcap transport and defines the Npcap backend.

## Portable contracts

### `ILiveCaptureBackend`

A live backend exposes:

- backend identity and display name;
- availability and a diagnostic message;
- opaque adapter identities;
- adapter display/address metadata;
- an asynchronous stream of timestamped data-link frames.

The process-bus controller does not know whether an implementation uses Npcap, libpcap, BPF, or another capture mechanism.

### `ICaptureReplaySource`

A replay source exposes:

- source identity;
- file-format capability detection;
- an asynchronous stream of timestamped captured frames.

The built-in `PcapFileReplaySource` supports classic PCAP and PCAPNG Ethernet captures without native dependencies.

## Current implementations

- `NpcapLiveCaptureBackend`: Windows live-capture adapter isolated inside `Arvrel.ProcessBus/Capture` and compiled only for the `net8.0-windows` target when the pinned ARIEC61850 sibling is available.
- `PcapFileReplaySource`: portable streaming classic-PCAP/PCAPNG reader in `Arvrel.Capture`.
- `UnavailableLiveCaptureBackend`: explicit capability object for targets without a supported live backend. Decoder and replay availability remain independent from live-capture availability.

## Compatibility seam

`Arvrel.ProcessBus.PcapPacketReader` remains as a compatibility facade for existing callers and tests. Parsing is delegated to `Arvrel.Capture.PcapFileReader`.

`SmvProcessBusController` keeps its existing default constructor. A second constructor accepts `ILiveCaptureBackend` and `ICaptureReplaySource` for future desktop shells, alternate backends, and deterministic tests.

The source model uses `LiveCapture`, not a backend-specific `LiveNpcap` value. The current WPF shell may still display Npcap because that is the active Windows implementation.

## Acceptance checks

P5.1 is acceptable when:

1. `Arvrel.Capture` and its tests build on plain `net8.0`.
2. Capture tests pass on Windows, Ubuntu, and macOS.
3. `Arvrel.ProcessBus` builds its portable `net8.0` target with the pinned ARIEC61850 decoder on Windows, Ubuntu, and macOS.
4. `SmvProcessBusController` contains no direct `NpcapAdapterCatalog` or `NpcapProcessBusFrameSource` usage.
5. The Windows application still discovers adapters and captures SV through the default Npcap adapter.
6. Classic PCAP and PCAPNG replay remain behavior-compatible.
7. Injected fake and unavailable live backends are covered by controller regression tests.
8. Existing process-bus, protection, application, packaging, and security checks remain green.

## Deliberately deferred

P5.1 does not:

- ship a libpcap backend;
- make native live capture available on Linux or macOS;
- migrate file dialogs or WPF source controls;
- create the Avalonia executable shell;
- change packet filtering or capture buffer policy;
- change protection or trust behavior.

A later phase may add a libpcap implementation and select a backend through runtime capability discovery without changing the controller contract.
