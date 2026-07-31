# Changelog

All notable public changes to ARVREL are documented here.

## [Unreleased]

### P1 process-bus integration

- added live Npcap Sampled Values capture through sibling ARIEC61850;
- added classic PCAP and PCAPNG Ethernet replay;
- added dynamic SV stream discovery and selection;
- added SCL-assisted profile binding and ordered payload decoding;
- added fixed value-quality fallback, circular sample rings, one-cycle RMS, and two-cycle waveform snapshots;
- added CT ratio and nominal-frequency measurement context;
- added freshness, `smpCnt`, quality, SCL, scaling, and mapping trust gates;
- connected live/replay measurements to 50P, 51P, 50N, and 51N;
- added JSON evidence export and process-bus regression tests;
- replaced text/symbol button treatment with compact Lucide-derived icon buttons and filled primary actions.

### Added

- sibling-ready standalone repository extracted from the ARIEC61850 Virtual Relay Lab P0 work;
- premium one-screen WPF protection workspace;
- deterministic 50P, 51P, 50N and 51N engine;
- stationary two-cycle waveform and virtual relay faceplate;
- SMV measurement/pickup/trip trust boundary;
- typed Algorithm Editor policy validation and shadow staging;
- deterministic tests, Windows CI and GitHub publication scripts.
