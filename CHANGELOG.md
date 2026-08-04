# Changelog

All notable public changes to ARVREL are documented here. The project uses semantic-style version labels while the public API and evidence formats remain subject to change during beta.

## [Unreleased]

### Added

- Internal demo **Virtual Injection Laboratory** with editable 4I+4V RMS magnitude, angle, common frequency, enable state, and neutral-channel provenance;
- validated debounced auto apply with last-valid retention and an explicit coherent-window rebuild state;
- fixed 4 kHz nominal sampling grid feeding the existing mean-removed 50 Hz single-bin DFT, so off-nominal injection exposes the estimator response instead of changing the measurement grid;
- normal, phase/ground-fault, voltage, and directional protection presets that populate the same editable table;
- IN/VN explicit virtual channels with calculated `IA+IB+IC` and `VA+VB+VC` residual fallback;
- interlocked **START** and **STOP** controls aligned with the ARSVIN publisher operating pattern;
- configured-versus-effective output identity, state timestamps, and internal evidence schema version 4;
- deterministic virtual-injection tests covering phasor reconstruction, off-nominal behavior, residual provenance, stopped-zero output, protection operation/restraint, exact pickup threshold, configured delay, and trip-latch retention;
- modeless P4.2 **Advanced Injection Laboratory** foundation with single-window editor authority, an active Direct view, and clearly reserved future Symmetrical, Impedance, Ramp, Sequencer, and advanced Waveform stages.

### Changed

- Internal demo is no longer limited to a fixed A-G scenario;
- the Internal analysis workspace defaults to **DUAL** while Injection, Waveform, and Phasor-only views remain available;
- configured injection values remain armed while stopped and energize only after START;
- STOP forces all effective virtual voltage and current outputs to zero without erasing the configured table;
- the main Run control mirrors the same Start injection / Stop injection interlock;
- changing or clearing injection does not erase a latched operation; relay reset and complete laboratory reset remain separate actions;
- relay pickup and trip remain governed by measured quantities, active settings, configured delay, and trust permission;
- opening Advanced Injection transfers the existing Direct editor out of Main Window, hides the Main `INJECT` tab, and leaves Main Window in DUAL monitoring mode;
- closing Advanced Injection restores the same editor and `INJECT` tab without replacing the configured source, output state, or trip latch;
- leaving Internal demo closes the Advanced Injection Window safely so Live Npcap and PCAP replay retain exclusive source context.

## [0.1.0-beta.1] — 2026-08-03

### Added

- public Windows x64 installer and portable packaging pipeline;
- automated prerelease publication, SHA-256 checksums, dependency report, and optional CycloneDX SBOM;
- GPL-3.0-or-later and alternative commercial-licensing documentation;
- security, support, contribution, CLA, conduct, citation, third-party, release-checklist, and soak-test documentation;
- structured issue and pull-request templates;
- professional README and SEO-ready static landing page;
- explicit engine-owned protection operation evidence;
- culture-invariant settings identities and malformed-enum validation;
- rejected-SMV continuity telemetry without sample admission.

### Protection and process-bus baseline

- live Npcap Sampled Values capture and PCAP/PCAPNG replay;
- SCL-assisted profile binding, mapping, scaling, quality, freshness, and `smpCnt` trust gates;
- IA/IB/IC/IN and VA/VB/VC/VN measurement, RMS phasors, sequence quantities, waveform and phasor instruments;
- 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N;
- practitioner setting groups, IEC curves, familiar TMS notation, CT/VT context, presets, and fingerprints;
- research mode with read-only active source and deterministic shadow staging;
- numerical-relay LCD, event trace, annunciation, virtual trip latch, and evidence export.

### Changed

- trip attribution prioritizes operated elements across legacy and feeder functions;
- pickup/trip timing is tracked per element;
- relay annunciation consumes causes captured at the protection evaluation that latched the trip;
- 67P produces deterministic phase-cause evidence;
- 67N uses explicit decoded IN/3I0 and VN/3V0 channels when present, with calculated fallback;
- research shadow artifacts are immutable by settings fingerprint and source hash;
- relay LED animation state is guarded against synchronous WPF reentrancy;
- SCL file and Npcap adapter preferences are restored locally on startup;
- coherent waveform/phasor presentation is held during SMV trust recovery.

### Fixed

- potential relay-lamp UI stack overflow;
- cross-element pickup/trip evidence mixing;
- short-fault evidence loss between UI polling intervals;
- feeder trip records attributed to an earlier legacy pickup;
- completed trip evidence overwritten by a subsequent pickup;
- duplicate/out-of-order frames missing from exported continuity telemetry;
- culture-dependent SHA-256 settings fingerprints;
- malformed enum values terminating settings workflows;
- explicit residual phasors being discarded in directional earth-fault logic;
- waveform and phasor instability during local publisher focus changes;
- startup crash caused by premature XAML selection handling;
- relay phase operated lenses showing a blue core beneath a red glow;
- avoidable text clipping in common 1600-pixel layouts.

### Known limitations

See [`RELEASE-NOTES.md`](RELEASE-NOTES.md) and the README safety boundary. The release remains a public beta and virtual-output-only laboratory build.
