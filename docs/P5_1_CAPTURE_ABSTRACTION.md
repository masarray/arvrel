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
        +----> Npcap adapter (Windows implementation only)
```

`Arvrel.Capture` targets plain `net8.0` and contains no WPF, Windows desktop, Npcap, ARIEC61850, or native-library dependency.

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

- `NpcapLiveCaptureBackend`: Windows live capture adapter isolated inside `Arvrel.ProcessBus/Capture` and compiled only when the pinned ARIEC61850 sibling is available.
- `PcapFileReplaySource`: portable streaming classic-PCAP/PCAPNG reader in `Arvrel.Capture`.
- `UnavailableLiveCaptureBackend`: explicit capability object for builds without a supported live backend.

## Compatibility seam

`Arvrel.ProcessBus.PcapPacketReader` remains as a compatibility facade for existing callers and tests. Parsing is delegated to `Arvrel.Capture.PcapFileReader`.

`SmvProcessBusController` keeps its existing default constructor. A second constructor accepts `ILiveCaptureBackend` and `ICaptureReplaySource` for future desktop shells, alternate backends, and deterministic tests.

## Acceptance checks

P5.1 is acceptable when:

1. `Arvrel.Capture` and its tests build on plain `net8.0`.
2. Capture tests pass on Windows, Ubuntu, and macOS.
3. `SmvProcessBusController` contains no direct `NpcapAdapterCatalog` or `NpcapProcessBusFrameSource` usage.
4. The Windows application still discovers adapters and captures SV through the default Npcap adapter.
5. Classic PCAP and PCAPNG replay remain behavior-compatible.
6. Existing process-bus, protection, application, packaging, and security checks remain green.

## Deliberately deferred

P5.1 does not:

- ship a libpcap backend;
- make the complete process-bus decoder project cross-platform;
- migrate file dialogs or WPF source controls;
- create the Avalonia executable shell;
- change packet filtering or capture buffer policy;
- change protection or trust behavior.

A later phase may add a libpcap implementation and select a backend through runtime capability discovery without changing the controller contract.
