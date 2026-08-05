# ARVREL Avalonia desktop

This folder scopes the cross-platform presentation toolchain independently from the established WPF/core solution.

## Prerequisites

- .NET 10 SDK for source builds;
- Windows, Linux with a supported desktop session, or macOS;
- optional sibling `ARIEC61850` checkout beside the `arvrel` repository for SV decoder and replay capability.

## Build and test

```bash
dotnet build ARVREL.Desktop.sln -c Release
dotnet test ../tests/Arvrel.Desktop.Tests/Arvrel.Desktop.Tests.csproj -c Release --no-build
```

## Run from source

```bash
dotnet run --project ../src/Arvrel.Desktop/Arvrel.Desktop.csproj
```

The deterministic internal laboratory works without the sibling decoder. Missing live/replay capability is reported in the shell rather than treated as an application-start failure.

## Native packages

P5.5 publishes self-contained package candidates on native GitHub runners:

- Windows x64: portable ZIP and per-user Inno Setup installer;
- Linux x64: portable tar archive and Debian package;
- macOS Apple Silicon: zipped `.app` bundle and DMG.

Package names, install paths, signing limitations, checksums, and release behavior are documented in:

```text
docs/P5_5_CROSS_PLATFORM_PACKAGING.md
docs/PLATFORM_DISTRIBUTION.md
```

Local packaging entry points are:

```text
scripts/package-avalonia-windows.ps1
scripts/package-avalonia-linux.sh
scripts/package-avalonia-macos.sh
```

These scripts expect an existing self-contained `dotnet publish` directory. The GitHub workflow supplies the pinned ARIEC61850 decoder, native runtime identifier, version, and output paths.

The current Windows WPF product remains in the repository-root `ARVREL.sln` and continues to use the root .NET 8 SDK selection and its established release workflow.
