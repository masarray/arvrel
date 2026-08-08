# ARVREL v0.1.0-beta.4 — Multi-IED AVR / OLTC Public Beta

ARVREL is a vendor-neutral Windows virtual protection and control IED laboratory for IEC 61850 engineering, education, FAT/SAT preparation, troubleshooting, and research.

This is a **public engineering beta**, not a certified protection or control IED.

## Release highlight — Automatic Voltage Regulator / OLTC virtual IED

`v0.1.0-beta.4` adds a dedicated AVR / OLTC Controller workspace designed to behave like a real transformer voltage-control device while remaining fully virtual and laboratory-safe.

Key AVR capabilities:

- 17-position OLTC model with neutral/start position 09;
- default simulated transformer + OLTC feedback plant;
- REMOTE + AUTO default operating state;
- independent LOCAL / REMOTE authority and AUTO / MANUAL operating mode;
- RAISE / LOWER / STOP tap-change behavior with command pulse, motor travel, end-position limits and operation count;
- T1 / T2 timing, voltage deadband, under/over-voltage blocking, optional current blocking and line-drop compensation;
- current and voltage injection controls for practical what-if experimentation;
- real-device-style LCD navigation, events, measured values, control and network pages;
- illuminated physical-mode buttons and industrial brushed-aluminium virtual chassis;
- standardized tap-end indications exposed as IEC 61850 YLTC `EndPosR` and `EndPosL`.

## IEC 61850 MMS server for AVR

The AVR workspace includes a live TCP IEC 61850 MMS endpoint for laboratory interoperability testing with engineering clients such as IEDScout and SAS software.

Implemented laboratory functions include:

- browse and read of the online IEC 61850 model;
- DataSets;
- buffered and unbuffered reports;
- General Interrogation and integrity reporting;
- event-driven `dchg` reporting without client polling;
- per-association SBO / SBOw / Oper / Cancel handling for modeled AVR controls;
- virtual AUTO / MANUAL control;
- virtual LTC blocking;
- virtual tap RAISE / LOWER / STOP control;
- writable AVR setpoint, bandwidth and T1 delay;
- interlock and authority validation before virtual process commands are accepted.

The AVR model uses vendor-neutral IEC 61850 logical nodes including `ATCC`, `YLTC`, `MMXU`, `LLN0`, `LPHD` and `GGIO` where appropriate. Tap end-position indications use the standard YLTC objects `EndPosR` and `EndPosL` rather than proprietary addresses.

The MMS implementation is intended for controlled laboratory interoperability work. It is **not an IEC 61850 conformance-certification claim**.

## Event-driven reporting

Beta.4 updates the sibling ARIEC61850 simulation engine used by ARVREL so report-enabled clients can receive unsolicited data-change InformationReports.

For enabled RCBs, live DataSet value/quality changes are detected independently of client polling. `BufTm` is respected so rapid changes can be coalesced instead of flooding the client. GI and integrity behavior remain available.

## Multi-IED laboratory

ARVREL now supports multiple virtual IED workspaces in one desktop application. Current public-beta workspaces include:

- feeder / overcurrent protection relay laboratory;
- two-winding Transformer Differential IED;
- AVR / OLTC Controller.

The application keeps device-specific operator surfaces while sharing common engineering, release, trust and evidence boundaries.

## Transformer Differential workspace retained

The two-winding transformer protection workspace introduced during the beta series remains available, including:

- restrained 87T dual-slope behavior;
- 87T-HS unrestrained high-set;
- 87N-HV and 87N-LV REF stages;
- H2 inrush and H5 overexcitation security;
- paired HV/LV Sampled Values engineering;
- CT ratio, polarity and supported vector-group compensation;
- deterministic CT distortion evidence;
- external-fault / CT-saturation security logic;
- deterministic 10-scenario Transformer Self-Test.

Expected transformer packaged-core baseline remains:

```text
PASS · 10/10 · transformer-public-beta-v1
```

## AVR user-interface improvements

The AVR faceplate received a dedicated industrial-device visual pass:

- fixed hardware proportions with uniform fit-to-window scaling;
- compact engineering configuration panel;
- Inter-first typography;
- compact Lucide hardware navigation icons;
- bright hover feedback with readable dark legends;
- black-glass status strip for PWR / INPUT / BLOCK / TAP LIMIT;
- amber/red blocked-state annunciation;
- brushed-aluminium outer chassis and brighter inner front plate;
- low-FPS smoothed trend presentation;
- compact T1/T2 and OLTC motor progress indicators.

## Public package set

Official release assets are expected to include:

- `ARVREL-Setup-v0.1.0-beta.4-win-x64.exe` — per-user Windows installer;
- `ARVREL-v0.1.0-beta.4-win-x64-portable.exe` — single-file portable executable;
- `ARVREL-v0.1.0-beta.4-win-x64-portable.zip` — portable archive;
- `ARVREL-v0.1.0-beta.4-legal-notices.zip`;
- `SHA256SUMS.txt`;
- NuGet dependency evidence;
- CycloneDX SBOM when generated by the release workflow;
- GitHub build-provenance attestations.

The installer remains per-user and non-elevated.

## Requirements

- Windows 10 or Windows 11 x64;
- no additional dependency for the internal AVR plant or deterministic transformer self-test;
- Npcap only for live IEC 61850 Sampled Values capture;
- an authorized, isolated laboratory network for live Ethernet protocol testing.

Npcap is not silently installed or relicensed by ARVREL.

## Recommended AVR / SAS evaluation

1. verify the downloaded package with `SHA256SUMS.txt`;
2. select **AVR · OLTC Controller**;
3. keep the default simulated transformer plant and verify neutral tap 09/17;
4. review REMOTE + AUTO default state;
5. start the IEC 61850 server from the Network configuration;
6. connect the engineering client to the selected PC IPv4 address and MMS port;
7. enable an RCB and confirm values update by report without manual polling;
8. test modeled SBO/SBOw controls only in the virtual laboratory;
9. verify end-position and blocking behavior before using custom settings.

## Safety boundary

ARVREL remains **virtual-output only**.

Beta.4 can accept modeled IEC 61850 MMS control commands for the virtual AVR process, but every accepted command terminates inside the software simulation. It does not provide physical relay contacts, physical OLTC motor authority, operational GOOSE trip authority, autonomous switching, or permission to operate primary equipment.

The software is not a calibrated relay test set, protection-grade hard-real-time platform, IEC 61850 certified IED, IEC 60255 type-tested relay, or substitute for approved commissioning procedures.

Do not use ARVREL as the sole basis for operational protection settings, AVR settings, switching decisions, commissioning acceptance, or primary-equipment control.

## Licensing

ARVREL source is available under GPL-3.0-or-later. An alternative commercial license may be negotiated for proprietary redistribution, OEM integration, contractual support, or other non-GPL terms. See `COMMERCIAL-LICENSING.md`.

Third-party components retain their own licenses.

## Known beta limitations

- community binaries are not currently claimed as Authenticode-signed and may trigger Windows reputation warnings;
- the AVR MMS/control model is vendor-neutral laboratory behavior and not a formal conformance profile;
- engineering clients may differ in supported control-structure and SCL expectations;
- live performance depends on Windows scheduling, adapter drivers, publisher behavior and host load;
- the application is not a hard-real-time control platform;
- broad clean-machine, multi-adapter and diverse-vendor interoperability validation continues during the beta period;
- physical output authority is intentionally absent.

## Reporting

Use GitHub issues for reproducible non-sensitive defects. Include exact ARVREL version, relevant virtual IED, selected mode, engineering-client behavior and minimal non-proprietary evidence. Use the private security-advisory workflow for vulnerabilities. Never publish proprietary substation captures, credentials or confidential SCL files.
