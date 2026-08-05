# Platform and distribution policy

## Product channels

ARVREL currently maintains two desktop channels during the cross-platform migration:

1. **ARVREL WPF** — the established Windows `net8.0-windows` product and release path.
2. **ARVREL Avalonia** — the cross-platform `net10.0` migration shell packaged for Windows, Linux, and macOS after P5.5.

The package family is always included in the filename. A WPF artifact must not be presented as a Linux or macOS application, and an Avalonia migration package must not silently replace the established WPF product.

## Established Windows WPF packages

Each publishable WPF release contains:

- `ARVREL-Setup-v<version>-win-x64.exe` — current-user installer;
- `ARVREL-v<version>-win-x64-portable.exe` — self-contained single-file executable;
- `ARVREL-v<version>-win-x64-portable.zip` — multi-file portable fallback;
- `ARVREL-v<version>-legal-notices.zip`;
- checksums, dependency evidence, optional SBOM, and GitHub attestations.

The installer remains non-elevated and targets:

```text
%LOCALAPPDATA%\Programs\ARVREL
```

## Avalonia cross-platform packages

P5.5 produces a second, explicitly named package family:

### Windows x64

- `ARVREL-Avalonia-v<version>-win-x64-portable.zip`;
- `ARVREL-Avalonia-v<version>-win-x64-setup.exe`.

The installer is per-user, non-elevated, and targets:

```text
%LOCALAPPDATA%\Programs\ARVREL-Avalonia
```

### Linux x64

- `ARVREL-Avalonia-v<version>-linux-x64.tar.gz`;
- `ARVREL-Avalonia-v<version>-linux-x64.deb`.

The Debian package installs under `/opt/arvrel`, creates `/usr/bin/arvrel`, and supplies a desktop entry and application icon. The `.deb` is intended for current Debian/Ubuntu-family x64 systems; the tar archive is the portable fallback for other compatible distributions.

### macOS Apple Silicon

- `ARVREL-Avalonia-v<version>-osx-arm64.app.zip`;
- `ARVREL-Avalonia-v<version>-osx-arm64.dmg`.

The native bundle identifier is `io.github.masarray.arvrel`. The initial package targets Apple Silicon and macOS 12 or later.

## Runtime policy

Avalonia release candidates are self-contained publishes. Users do not need to install the matching .NET runtime separately.

Self-contained does not mean that operating-system desktop libraries disappear:

- Linux still requires the declared X11, font, FreeType, OpenGL, ICE, and SM packages;
- macOS still enforces Gatekeeper and platform security policy;
- Windows still enforces SmartScreen, Defender, AppLocker, WDAC, and organization allow-lists.

## Capture capability

Package availability and live-capture availability are separate claims.

- Windows Avalonia packages include the existing Npcap adapter when the build has the pinned decoder, but Npcap itself must already be installed by an authorized administrator.
- Linux and macOS packages support the internal laboratory and PCAP/PCAPNG replay.
- P5.5 does not add a Linux libpcap backend or macOS BPF backend.

No package installs a packet-capture driver or bypasses local security policy.

## Signing and notarization

The public repository has no commercial signing secrets configured.

- WPF and Avalonia Windows packages are unsigned unless a future release explicitly reports a trusted Authenticode signature.
- Linux archives and Debian packages are checksum-verified but are not signed by an APT repository key.
- macOS packages receive an ad-hoc CI signature for bundle-integrity validation; they are not Developer ID signed or notarized.

Users may receive platform warnings. SHA-256 checksums and GitHub provenance attestations provide source-to-artifact integrity evidence but do not replace trusted publisher signatures or Apple notarization.

## Integrity evidence

The Avalonia package workflow generates:

- one platform manifest per runtime;
- one checksum file per runtime;
- an aggregate package index;
- an aggregate checksum file;
- GitHub build-provenance attestations on matching version tags.

The aggregate verification job downloads artifacts produced by independent native runners and verifies the checksum files before any tag publication.

## Managed computers

Portable and per-user packages do not bypass enterprise controls. When execution is blocked, authorized IT staff should verify the published checksum and attestation and then apply the organization’s approved allow-list or software-distribution process.

Installing Npcap, changing packet-capture permissions, overriding Gatekeeper, or modifying endpoint controls must follow the device owner’s policy.

## Deferred distribution channels

The following channels are not yet official:

- Winget or Microsoft Store;
- AppImage, Flatpak, Snap, or an APT repository;
- Homebrew or the Mac App Store;
- Intel macOS and ARM64 Linux packages;
- signed/notarized commercial distribution.

See `P5_5_CROSS_PLATFORM_PACKAGING.md` for the package implementation and acceptance boundary.
