# ARVREL public release checklist

A release is published only after the applicable gates below are complete. Software CI evidence does not replace laboratory or certification evidence.

## Source and provenance

- [ ] public-source provenance review completed;
- [ ] no proprietary capture, SCL, customer, station, credential, or employer-confidential data;
- [ ] GPL-3.0-or-later license present;
- [ ] commercial-licensing notice present;
- [ ] third-party notices and dependency manifest generated;
- [ ] contributor rights are compatible with dual licensing.

## Correctness

- [ ] Release build: zero warnings, zero errors;
- [ ] all protection tests pass;
- [ ] all process-bus tests pass;
- [ ] operated-element attribution tested across legacy and feeder functions;
- [ ] short-fault pickup/trip evidence survives UI polling;
- [ ] explicit residual channels and phase-sum fallback tested;
- [ ] duplicate/out-of-order frames rejected before measurement and counted in telemetry;
- [ ] settings identity invariant across cultures;
- [ ] malformed settings fail safely;
- [ ] trip latch and cause clear only on explicit reset.

## Operator workflow

- [ ] internal demo starts without a sibling source checkout;
- [ ] SCL and adapter preferences restore safely;
- [ ] Npcap absence produces a useful error;
- [ ] relay settings and CT/VT context load/save correctly;
- [ ] waveform, phasor, LCD, LED, event trace, and evidence export remain coherent;
- [ ] startup/runtime crash log is generated when required;
- [ ] 100%, 125%, and 150% display scaling reviewed;
- [ ] minimum and common 1600×900 layouts reviewed.

## Release assets

- [ ] self-contained Windows x64 portable ZIP;
- [ ] per-user Windows installer;
- [ ] SHA-256 checksums;
- [ ] NuGet transitive dependency report;
- [ ] CycloneDX SBOM or documented reason it was not generated;
- [ ] build-info file with ARVREL and ARIEC61850 commits;
- [ ] GPL, commercial, security, support, release, and third-party notices included;
- [ ] clean-machine install, launch, uninstall, and portable smoke test;
- [ ] unsigned-binary limitation disclosed or code signature verified.

## Live laboratory evidence

- [ ] authorized isolated network confirmed;
- [ ] 50 Hz and applicable 60 Hz profiles exercised;
- [ ] ARSVIN/ARVREL focus switching exercised;
- [ ] duplicate, out-of-order, gap, stale, quality, SCL mismatch, and recovery scenarios reviewed;
- [ ] representative 4I+4V live stream tested;
- [ ] soak-test report attached or beta limitation explicitly stated.

## Claim boundary

- [ ] release is marked prerelease while beta limitations remain;
- [ ] no IEC 61850 conformance claim without accredited evidence;
- [ ] no IEC 60255 type-test or calibration claim;
- [ ] no hard real-time, zero-loss, or protection-grade claim without benchmark and qualification evidence;
- [ ] virtual-output-only boundary is visible in README, release notes, and application.
