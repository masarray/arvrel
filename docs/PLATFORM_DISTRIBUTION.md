# Platform and distribution policy

> Current Windows release line: **v0.1.0-beta.6**. See [`CURRENT_STATUS.md`](CURRENT_STATUS.md) for the canonical shipped-state summary.

## Product channel

`masarray/arvrel` publishes one desktop product channel:

- **ARVREL Windows WPF** — the stable `net8.0-windows` multi-IED product using the P6 feeder virtual-relay interface plus Transformer Differential and AVR/OLTC workspaces.

Cross-platform Avalonia development is maintained separately in [`masarray/arvrel-avalonia`](https://github.com/masarray/arvrel-avalonia). Avalonia packages, migration status, compatibility claims, and release decisions belong to that repository and must not be presented as releases from `masarray/arvrel`.

## Official Windows packages

Each publishable release contains:

- `ARVREL-Setup-v<version>-win-x64.exe` — current-user Windows installer;
- `ARVREL-v<version>-win-x64-portable.exe` — self-contained single-file executable;
- `ARVREL-v<version>-win-x64-portable.zip` — portable archive;
- `ARVREL-v<version>-legal-notices.zip`;
- `SHA256SUMS.txt`;
- dependency evidence and, when generated, CycloneDX SBOM and GitHub build-provenance attestations.

For beta.6, all of the above evidence categories were published by the release workflow.

The installer remains non-elevated and targets:

```text
%LOCALAPPDATA%\Programs\ARVREL
```

No package from this repository claims Linux or macOS desktop support. Cross-platform protection-core tests are portability checks, not desktop-platform releases.

## Runtime policy

Official Windows packages are self-contained publishes. Users do not need to install the matching .NET runtime separately.

Self-contained packages remain subject to Windows security policy, including SmartScreen, Defender, AppLocker, WDAC, and organization allow-lists.

## Internal laboratory capability

The following beta.6 workflows require no packet-capture driver:

- feeder closed-loop internal secondary injection and TESTSET timing;
- Transformer Differential deterministic self-test;
- synchronized Transformer Differential HV/LV/independent-neutral internal injection;
- AVR/OLTC simulated transformer plant and local virtual controls;
- PCAP/PCAPNG replay.

The closed-loop TESTSET/relay timing profile is deterministic behavioral software. Package availability does not imply calibrated relay-test-equipment performance.

## IEC 61850 live capability

Package availability and live-capture availability are separate claims.

- live Sampled Values capture requires Npcap installed separately under the device owner's policy;
- ARVREL does not silently install or relicense Npcap;
- ARVREL does not install a packet-capture driver or bypass endpoint policy;
- live capture should be used only on isolated, authorized laboratory networks.

The AVR workspace also exposes a laboratory IEC 61850 MMS server/model for browse/read, DataSets, reports, GI/integrity, modeled SBO/SBOw controls, and virtual settings. This MMS functionality does **not** require Npcap merely to operate the internal virtual process; it provides no physical OLTC motor or primary-equipment authority.

## Signing and integrity

The public repository does not assume commercial code-signing secrets.

Windows packages may therefore be unsigned unless a specific release explicitly reports a trusted Authenticode signature. Beta.6 does not claim Authenticode signing. SHA-256 checksums, dependency reports, SBOM files, and GitHub provenance attestations provide source-to-artifact integrity evidence, but they do not replace trusted publisher signing or local IT policy.

Users should obtain packages only from the selected GitHub Release and verify `SHA256SUMS.txt` before execution.

## Managed computers

Portable and per-user packages do not bypass enterprise controls. When execution is blocked, authorized IT staff should verify the published checksum and attestation and use the organization’s approved allow-list or software-distribution process.

Installing Npcap or changing packet-capture permissions must follow the device owner’s policy.

## Deferred Windows distribution channels

The following channels are not currently official:

- Winget;
- Microsoft Store;
- organization-managed MSI distribution;
- signed commercial installer channels.

The selected GitHub Release, `.github/workflows/release.yml`, and [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) remain the source of truth for the Windows package set.
