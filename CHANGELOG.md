# Changelog

All notable public changes to ARVREL are documented here.

## [Unreleased]

### P1.1 native relay settings and research mode

- added a dedicated native protection setting workspace separate from CT/timebase context;
- added setting-group name, revision, SHA-256 fingerprint, preset save/load and restore defaults;
- added active enable, pickup, delay, dropout, TMS, minimum operate time and reset settings for 50P-1, 51P, 50N and 51N;
- added IEC Standard/Normal, Very, Extremely, Long-Time, Definite Time and user-defined IEC-form characteristics;
- added secondary-current setting entry with primary-equivalent CT readout;
- added runtime setting replacement with timer and virtual trip-latch reset;
- added explicit Practitioner and Research modes;
- exposed exact active standard algorithm source and separate deterministic custom shadow staging;
- added active setting identity to the main workspace, event trace and evidence;
- added deterministic curve-family, disabled-element and setting-update regression tests.

### P1 process-bus integration

- added live Npcap Sampled Values capture through sibling ARIEC61850;
- added classic PCAP and PCAPNG Ethernet replay;
- added dynamic SV stream discovery and selection;
- added SCL-assisted profile binding and ordered payload decoding;
- added fixed value-quality fallback, circular sample rings, one-cycle RMS, and two-cycle waveform snapshots;
- added CT ratio and nominal-frequency measurement context;
- added freshness, `smpCnt`, quality, SCL, scaling, mapping trust gates;
- connected live/replay measurements to 50P, 51P, 50N and 51N;
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
