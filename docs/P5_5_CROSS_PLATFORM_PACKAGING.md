# P5.5 — Windows, Linux, and macOS packaging

## Objective

P5.5 converts the P5.4 Avalonia shell from a CI-published folder into named, verifiable desktop packages for the three operating systems already exercised by the portability matrix.

The packaging boundary is intentionally separate from the established WPF Windows release. The WPF product remains available while the Avalonia package set matures.

## Package matrix

For `VERSION=<version>`, the workflow produces:

| Platform | Runtime | Portable package | Installed package |
|---|---|---|---|
| Windows 10/11 x64 | `win-x64` | `ARVREL-Avalonia-v<version>-win-x64-portable.zip` | `ARVREL-Avalonia-v<version>-win-x64-setup.exe` |
| Linux x64 | `linux-x64` | `ARVREL-Avalonia-v<version>-linux-x64.tar.gz` | `ARVREL-Avalonia-v<version>-linux-x64.deb` |
| macOS Apple Silicon | `osx-arm64` | `ARVREL-Avalonia-v<version>-osx-arm64.app.zip` | `ARVREL-Avalonia-v<version>-osx-arm64.dmg` |

Every runtime publish is self-contained. A separate .NET runtime installation is therefore not required.

## Windows contract

The Windows package is built on a Windows runner.

The portable ZIP contains the complete self-contained publish directory and starts through:

```text
Arvrel.Desktop.exe
```

The Inno Setup installer:

- uses `PrivilegesRequired=lowest`;
- installs for the current user under `%LOCALAPPDATA%\Programs\ARVREL-Avalonia`;
- creates Start Menu and optional desktop shortcuts;
- does not offer an administrator-elevation override;
- does not write to `Program Files`.

Npcap is not installed or modified by this package. Live capture is available only when an authorized Npcap installation already exists. Internal injection and PCAP replay remain available without installing a capture driver.

## Linux contract

The portable archive expands to:

```text
ARVREL/
└── Arvrel.Desktop
```

The Debian package installs the application under:

```text
/opt/arvrel
```

and creates:

```text
/usr/bin/arvrel
/usr/share/applications/arvrel.desktop
/usr/share/icons/hicolor/512x512/apps/arvrel.png
```

The `.deb` declares desktop runtime dependencies for X11, font configuration, FreeType, OpenGL, ICE, and SM.

P5.5 does not claim universal Linux distribution support. The `.deb` targets current x64 Debian/Ubuntu-family systems. The tarball is the fallback for other compatible x64 distributions.

No Linux live-capture backend is included yet. PCAP/PCAPNG replay uses the portable capture abstraction.

## macOS contract

The macOS runner creates a native bundle:

```text
ARVREL.app/
└── Contents/
    ├── Info.plist
    ├── MacOS/Arvrel.Desktop
    └── Resources/ARVREL.icns
```

The bundle identifier is:

```text
io.github.masarray.arvrel
```

The package requires macOS 12 or later and targets Apple Silicon.

The workflow applies an **ad-hoc** code signature so bundle integrity can be verified during CI. It does not use an Apple Developer ID certificate and does not notarize the package. Gatekeeper may therefore require an explicit user approval. Checksums and GitHub provenance attestations do not replace Apple notarization.

No BPF or libpcap live-capture backend is included in P5.5.

## Integrity evidence

Each platform artifact contains a manifest describing:

- product and version;
- runtime identifier;
- source commit;
- self-contained status;
- package names;
- install location;
- executable;
- signing status;
- available capture capabilities.

Platform-specific checksum files are generated and revalidated by an independent aggregate job:

```text
SHA256SUMS-win-x64.txt
SHA256SUMS-linux-x64.txt
SHA256SUMS-osx-arm64.txt
```

The aggregate job also produces:

```text
ARVREL-Avalonia-v<version>-package-index.json
SHA256SUMS-Avalonia.txt
```

Tag builds attach GitHub build-provenance attestations and extend the corresponding prerelease with the verified package set.

## CI acceptance

The `Avalonia desktop packages` workflow:

1. validates `VERSION` and tag consistency;
2. checks out the pinned ARIEC61850 decoder;
3. publishes self-contained hosts on their native runners;
4. creates the portable and installed package formats;
5. validates executable architecture and package structure;
6. validates installer privilege policy, bundle metadata, and disk image integrity;
7. verifies all platform checksums in an independent job;
8. uploads a complete verified workflow artifact;
9. publishes and attests the package set only for a matching `v<version>` tag.

Source-level regression tests protect package names, install paths, privilege policy, bundle identity, self-contained publication, and explicit signing limitations.

## Compatibility

P5.5 does not change:

- protection, injection, phasor, or trust algorithms;
- relay and source lifecycle semantics;
- PCAP/PCAPNG parsing;
- the Windows Npcap backend;
- the established WPF Windows package names or installer;
- release version derivation from `VERSION`.

## Deliberately deferred

- Windows Authenticode signing;
- Apple Developer ID signing and notarization;
- Intel macOS packages;
- Linux ARM64 packages;
- AppImage, Flatpak, Snap, Homebrew, Winget, and package-repository publication;
- automatic Npcap, libpcap, or BPF installation;
- Linux/macOS live capture;
- replacement or removal of the WPF release.
