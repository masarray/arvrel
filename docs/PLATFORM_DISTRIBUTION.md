# Platform and distribution policy

## Current desktop support

ARVREL's desktop application is currently a Windows Presentation Foundation (WPF) application targeting `net8.0-windows`. The process-bus integration project also targets Windows because live capture uses the Windows Npcap transport.

The official desktop release therefore supports **Windows 10/11 x64**. GitHub Actions must not label a Windows build as a Linux or macOS package.

## Windows packages

Each publishable release is expected to contain:

- `ARVREL-Setup-v<version>-win-x64.exe` — current-user installer;
- `ARVREL-v<version>-win-x64-portable.exe` — self-contained single-file executable;
- `ARVREL-v<version>-win-x64-portable.zip` — multi-file portable fallback;
- `ARVREL-v<version>-legal-notices.zip` — license, third-party notices, support, security, and build information;
- `SHA256SUMS.txt`, dependency report, optional CycloneDX SBOM, and GitHub artifact attestations.

### Per-user installer

The installer uses `PrivilegesRequired=lowest` and installs under:

```text
%LOCALAPPDATA%\Programs\ARVREL
```

It does not request elevation and does not write to `Program Files` or machine-wide registry locations.

### Portable single EXE

The portable executable is a self-contained .NET single-file publish. It does not require the .NET runtime to be installed and does not run an installer.

The executable may extract bundled native components to the current user's temporary area at runtime. That is normal .NET single-file behavior and does not require administrator rights.

## Locked or managed Windows computers

“No installer” and “no administrator elevation” do **not** mean “bypass company security.” An unsigned executable can still be blocked by Windows SmartScreen, Microsoft Defender, AppLocker, WDAC, endpoint security, or an organization allow-list.

Users must follow the device owner's policy. When a managed computer blocks ARVREL, the correct path is for authorized IT staff to verify the published SHA-256 checksum and GitHub attestation, then allow the specific release if organizational policy permits it.

Live IEC 61850 Sampled Values capture requires an authorized Npcap installation. Installing or updating that driver normally requires administrator approval. Internal virtual injection, source review, and PCAP replay do not install a capture driver.

## Code signing

The public workflow intentionally performs no commercial Authenticode signing because no signing certificate is configured. Release metadata states that the binaries are unsigned. Checksums, SBOM data, pinned dependencies, and GitHub build-provenance attestations provide integrity evidence, but they are not a replacement for a trusted operating-system code-signing identity.

## Linux and macOS

The current WPF desktop UI cannot run on Linux or macOS. A GitHub Actions matrix cannot convert it into a native cross-platform application.

The protection engine itself targets cross-platform `net8.0`; the release workflow builds and tests that core on Windows, Ubuntu, and macOS to prevent unnecessary platform coupling. This is engineering groundwork, not a Linux or macOS desktop release.

A genuine Linux/macOS product requires a separate port with at least:

1. a cross-platform UI layer such as Avalonia;
2. separation of Windows-specific WPF code from view models and application services;
3. a capture abstraction with supported Linux/macOS transports;
4. platform-specific packaging, icons, permissions, and file locations;
5. macOS signing/notarization strategy or explicit unsigned distribution instructions;
6. native smoke tests on each supported operating system.

Until that port exists and passes native validation, releases must remain accurately labeled Windows-only.
