# Platform and distribution policy

## Product channel

`masarray/arvrel` publishes one desktop product channel:

- **ARVREL Windows WPF** — the stable `net8.0-windows` product using the P6 real-device virtual-relay interface.

Cross-platform Avalonia development is maintained separately in [`masarray/arvrel-avalonia`](https://github.com/masarray/arvrel-avalonia). Avalonia packages, migration status, compatibility claims, and release decisions are owned by that repository and must not be presented as releases from `masarray/arvrel`.

## Official Windows packages

Each publishable release contains:

- `ARVREL-Setup-v<version>-win-x64.exe` — current-user Windows installer;
- `ARVREL-v<version>-win-x64-portable.exe` — self-contained single-file executable;
- `ARVREL-v<version>-win-x64-portable.zip` — portable package;
- `ARVREL-v<version>-legal-notices.zip`;
- `SHA256SUMS.txt`;
- dependency evidence and, when generated, CycloneDX SBOM and GitHub attestations.

The installer remains non-elevated and targets:

```text
%LOCALAPPDATA%\Programs\ARVREL
```

No package from this repository claims Linux or macOS support.

## Runtime policy

Official Windows packages are self-contained publishes. Users do not need to install the matching .NET runtime separately.

Self-contained packages still remain subject to Windows security policy, including SmartScreen, Defender, AppLocker, WDAC, and organization allow-lists.

## Capture capability

Package availability and live-capture availability are separate claims.

- the internal laboratory and PCAP/PCAPNG replay do not require Npcap;
- live Sampled Values capture requires Npcap to be installed separately by an authorized administrator;
- ARVREL does not install a packet-capture driver or bypass endpoint policy;
- live capture should be used only on isolated and authorized laboratory networks.

## Signing and integrity

The public repository does not assume commercial code-signing secrets.

Windows packages may therefore be unsigned unless a specific release explicitly reports a trusted Authenticode signature. SHA-256 checksums, dependency reports, SBOM files, and GitHub provenance attestations provide source-to-artifact integrity evidence, but they do not replace trusted publisher signing.

Users should obtain packages only from the GitHub Releases page and verify `SHA256SUMS.txt` before execution.

## Managed computers

Portable and per-user packages do not bypass enterprise controls. When execution is blocked, authorized IT staff should verify the published checksum and attestation and use the organization’s approved allow-list or software-distribution process.

Installing Npcap or changing packet-capture permissions must follow the device owner’s policy.

## Deferred Windows distribution channels

The following channels are not currently official:

- Winget;
- Microsoft Store;
- organization-managed MSI distribution;
- signed commercial installer channels.

The release workflow and [`RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md) remain the source of truth for the Windows package set.
