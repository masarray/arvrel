# Changelog

All notable public changes to ARVREL are documented here. The project uses semantic-style beta version labels while public APIs and evidence formats remain subject to change.

For the exact current shipped-state contract, see [`docs/CURRENT_STATUS.md`](docs/CURRENT_STATUS.md). Historical `P*` documents remain milestone records rather than current release authority.

## [Unreleased]

No public capability is currently declared here. Features become public product claims only after they are included in an official GitHub Release.

## [0.1.0-beta.6] — 2026-08-10

### Added

- metrology-grade closed-loop feeder TESTSET↔relay timing model with monotonic integer-microsecond clock;
- independent 10 kHz TESTSET binary-input sampling with distinct deglitch and debounce behavior;
- causal relay front end driven by instantaneous signed terminal samples, clipping, ADC quantization, configured input delay, and one-cycle rolling DFT;
- settled pre-fault measurement history at T0 to avoid an artificial empty-window blackout;
- explicit virtual relay contact timing/bounce path and arbitrary-time TESTSET BI sampling;
- operator timing rail separating relay ANY PICKUP, TESTSET BI2 acceptance, operated-element pickup/P→T, relay trip request, and TESTSET BI1 acceptance;
- evidence schema 9 with first-any-pickup source, metrology timeline, operated-element timing correlation, BI1-vs-frozen-capture relationship, and closed-loop topology/run identity;
- deterministic one-click relay RESET transaction with READY TO RE-ARM postcondition.

### Changed

- TESTSET measured trip and optional auto-stop are now authoritative only from the accepted wired `TESTSET.BI1` edge;
- `ProtectionSnapshot.TripLatched` can request BO1 but is never read directly as the external TESTSET result;
- BI2 is explicitly generic ANY PICKUP and is no longer semantically conflated with the pickup of the element that ultimately operates;
- definite/inverse timer integration begins at the first observed pickup frame instead of retroactively counting the preceding non-pickup interval;
- desktop closed-loop observation advances in 250 µs relay quanta while WPF remains presentation cadence only;
- post-trip source state is explicitly presented as `OUTPUT OFF · FROZEN CAPTURE`;
- relay RESET preserves completed TESTSET timing and frozen evidence and does not restart or mutate the source.

### Fixed

- closed-loop timing gaps caused by the earlier phasor/timestamp boundary model;
- source-side phasor values being used as if they were relay terminal ADC samples;
- empty rolling-measurement startup latency at T0;
- generic pickup timing being paired incorrectly with the later operated element;
- multi-click RESET behavior caused by stale fault-window samples remaining in the causal acquisition path after auto-stop;
- P6 RESET using a legacy path instead of the unified equipment-authority reset transaction.

### Validation

- final beta.6 feature baseline: **403/403 deterministic tests passed**;
- .NET CI, CodeQL, Windows/macOS/Ubuntu protection-core checks, Windows installer/portable packaging, no-admin/single-file contract, dependency audit, release asset verification, provenance, and SBOM attestation passed before publication.

## [0.1.0-beta.5] — 2026-08-08

### Added

- integrated synchronized two-sided Transformer Differential internal secondary injection;
- HV IA/IB/IC/IN and LV IA/IB/IC/IN source channels with independent neutral/NGR availability;
- CT-ratio and vector-group-aware stable through-load baseline generation;
- editable Balanced through load, Internal fault, REF HV/NGR, and REF LV/NGR presets;
- transformer operator evidence for 87T, 87T-HS, REF HV, and REF LV.

### Retained

- restrained 87T, 87T-HS, REF HV/LV, H2/H5 security, context-gated external-fault/CT-saturation security, paired-HV/LV SV engineering, and deterministic 10-scenario Transformer Self-Test;
- multi-IED OCR / Transformer / AVR shell introduced in beta.4.

## [0.1.0-beta.4] — 2026-08-07

### Added

- AVR / OLTC Controller as a first-class virtual IED workspace;
- simulated transformer plant and 17-position OLTC;
- LOCAL/REMOTE and AUTO/MANUAL virtual authority;
- IEC 61850 MMS browse/read, DataSets, event/integrity reporting, modeled SBO/SBOw controls, and virtual AVR settings;
- multi-IED desktop product shell for feeder OCR, Transformer Differential, and AVR/OLTC workflows.

### Boundary

MMS controls terminate inside the virtual AVR/OLTC process. They provide no physical OLTC motor, switching, or primary-equipment authority.

## [0.1.0-beta.1] — 2026-08-03

### Added

- public Windows x64 installer and portable packaging pipeline;
- automated prerelease publication, SHA-256 checksums, dependency report, and optional CycloneDX SBOM;
- GPL-3.0-or-later and alternative commercial-licensing documentation;
- security, support, contribution, CLA, conduct, citation, third-party, release-checklist, and soak-test documentation;
- structured issue and pull-request templates;
- public static landing page and documentation site;
- live Npcap Sampled Values capture and PCAP/PCAPNG replay;
- SCL-assisted profile binding, mapping, scaling, quality, freshness, and `smpCnt` trust gates;
- IA/IB/IC/IN and VA/VB/VC/VN measurement, RMS phasors, sequence quantities, waveform and phasor instruments;
- feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N;
- practitioner setting groups, IEC curves, CT/VT context, presets, fingerprints, event trace, virtual trip latch, and evidence export.

## Release-history note

Early beta.2/beta.3 development was iterative and is preserved in Git history and the GitHub Releases/tags. This changelog intentionally avoids reconstructing unsupported release claims where the authoritative release notes were superseded. Current public behavior is defined by the selected release and [`docs/CURRENT_STATUS.md`](docs/CURRENT_STATUS.md).
