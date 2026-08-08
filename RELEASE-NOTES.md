# ARVREL v0.1.0-beta.3 — Transformer Differential Public Beta

ARVREL is a vendor-neutral Windows virtual protection relay laboratory for IEC 61850 Sampled Values engineering, education, process-bus troubleshooting, and protection-algorithm research.

This is a **public beta technical preview**, not a certified protection IED.

## Release highlight — two-winding Transformer Differential IED

`v0.1.0-beta.3` adds a dedicated two-winding transformer protection workspace while preserving the existing feeder protection laboratory.

Implemented transformer elements:

- restrained 87T with generic Is1 / K1 / Is2 / K2 dual-slope semantics;
- 87T-HS unrestrained high-set stage;
- 87N-HV and 87N-LV restricted earth fault stages;
- H2 inrush and H5 overexcitation security;
- paired HV/LV Sampled Values engineering and synchronization checks;
- CT ratio, transformer rating, polarity and supported vector-group compensation;
- deterministic CT waveform-distortion evidence;
- context-gated external-fault / CT-saturation security for 87T, optional high-set supervision and winding-specific REF supervision;
- practitioner evidence for external-fault arming, CT-saturation suspicion and active security holds.

The transformer implementation is vendor-neutral. It does not claim proprietary MiCOM CTSat/NoGap, SEL EFD, GE/Multilin, Siemens, or other manufacturer-algorithm equivalence.

## Public tester self-test

A new deterministic **10-scenario Transformer Self-Test** lets users verify the packaged protection core before connecting live Sampled Values or opening a field capture.

The Transformer IED workspace may therefore be opened while the main source remains Internal Demo or no SV streams are present. This does **not** bypass the real process-bus runtime guards: applying Live/Replay transformer protection still requires two distinct HV/LV streams.

The self-test verifies:

1. compensated through-current stability;
2. internal restrained 87T operation;
3. 87T-HS operation;
4. H2 blocking;
5. H5 blocking;
6. external fault with delayed HV CT distortion is secured by P13;
7. distorted internal fault remains trippable and is not overblocked by P13;
8. 87N-HV operation;
9. 87N-LV operation;
10. REF securely blocks when an independent neutral-current input is unavailable.

Expected public-test result:

```text
PASS · 10/10 · transformer-public-beta-v1
```

The result viewer exposes copyable expected/observed evidence suitable for a reproducible issue report.

## Transformer engineering model

The live/replay transformer runtime uses paired `TransformerMeasurementFrame` evidence rather than treating a transformer as a single feeder stream.

Key engineering behavior:

- HV and LV streams are selected independently;
- both streams must have compatible sampling/frequency context and acceptable synchronization evidence;
- vector-group clock displacement is applied through the transformer engineering plan;
- CT ratios and transformer nameplate data establish per-unit scaling;
- zero-sequence removal is limited to the differential compensation path where required by the connection model;
- REF retains local uncompensated residual quantities and requires an independent neutral-current input;
- calculated phase residual is never silently substituted for the REF neutral CT.

Automatic compensation is intentionally rejected for unsupported/ambiguous transformer configurations rather than guessed.

## External-fault / CT-saturation security

P13 deliberately separates waveform evidence from a protection decision.

**Waveform distortion by itself never blocks transformer protection.**

A security hold requires a restraint-leading external/through-fault context before qualified CT distortion can supervise the affected protection path. The public self-test includes both sides of this invariant: an external-fault saturation case must remain secure, and a distorted internal fault must remain dependable.

P13 is disabled by default until explicitly enabled by the practitioner.

## Existing laboratory capabilities retained

- live Npcap IEC 61850 Sampled Values capture;
- PCAP and PCAPNG replay;
- SCL-assisted stream binding, mapping, scaling, APPID, VLAN, svID, datSet, confRev, quality, freshness, and `smpCnt` trust checks;
- 4I+4V one-cycle RMS phasors, symmetrical components, residual quantities, waveform and phasor instruments;
- feeder 50P-1, 51P, 50N, 51N, 67P, 67N, 27, 59, and 59N virtual protection elements;
- practitioner settings and read-only active algorithm source;
- research shadow workspace with deterministic policy validation;
- relay LCD, pickup/trip annunciation, latched trip causes, event trace, settings identity, and evidence export;
- coherent waveform/phasor hold during SMV discontinuity recovery.

## Packages

Official release assets are expected to include:

- `ARVREL-Setup-v0.1.0-beta.3-win-x64.exe` — per-user Windows installer;
- `ARVREL-v0.1.0-beta.3-win-x64-portable.exe` — single-file portable executable;
- `ARVREL-v0.1.0-beta.3-win-x64-portable.zip` — portable package;
- `ARVREL-v0.1.0-beta.3-legal-notices.zip`;
- `SHA256SUMS.txt`;
- NuGet dependency report and CycloneDX SBOM when generated by the release workflow.

The executable packages include the compiled ARIEC61850 dependency. Source-development builds still use the sibling repository layout.

## Requirements

- Windows 10 or Windows 11 x64;
- no additional dependency for the deterministic Transformer Self-Test;
- Npcap only for live capture;
- an authorized, isolated laboratory network for live process-bus testing;
- appropriate SCL context for trusted SCL-bound stream operation.

Npcap is not silently installed or relicensed by ARVREL.

## Recommended public test order

1. verify package SHA-256;
2. run the 10-scenario Transformer Self-Test and retain `PASS · 10/10` evidence;
3. use PCAP replay for repeatable paired-SV evaluation;
4. use Live Npcap only after replay/internal behavior is understood;
5. file reproducible issues without publishing proprietary station data.

See `docs/TRANSFORMER_PUBLIC_TEST.md`.

## Safety boundary

The public beta is virtual-output only. It does not provide physical relay contacts, operational GOOSE trip, MMS control, switching authority, IEC 61850 conformance certification, IEC 60255 type-test, calibration, or protection-grade real-time guarantees.

The deterministic self-test is software regression evidence. It is not relay-test certification or proof of a real substation protection scheme.

Do not use ARVREL as the sole basis for operational protection settings, commissioning acceptance, or switching decisions.

## Licensing

ARVREL source is available under GPL-3.0-or-later. An alternative commercial license may be negotiated for proprietary redistribution, OEM integration, contractual support, or other non-GPL terms. See `COMMERCIAL-LICENSING.md`.

Third-party components retain their own licenses.

## Known beta limitations

- community binaries are not currently claimed as Authenticode-signed and may trigger Windows reputation warnings;
- live performance depends on Windows scheduling, Npcap, adapter drivers, publisher behavior, and host load;
- the beta is not a hard real-time platform;
- transformer automatic engineering is intentionally limited to supported two-winding vector-group cases; unsupported or ambiguous configurations must not be guessed;
- REF requires an independent neutral-current input for authority;
- broad clean-machine, multi-adapter, multi-MU and diverse vendor stream validation continues during the beta period;
- feeder 46, 47, 49, 81U/O, 32, 37, 50BF, 79, 25, 86, 74TCS, and 60 remain unimplemented;
- negative-sequence and memory polarization for feeder 67N remain deferred.

## Reporting

Use GitHub issues for reproducible non-sensitive defects. Include the copied Transformer Self-Test evidence when the issue involves the new transformer workspace. Use the private security-advisory workflow for vulnerabilities. Never publish proprietary substation captures or SCL files.