# ARVREL Avalonia desktop

This folder scopes the cross-platform presentation toolchain independently from the established WPF/core solution.

## Prerequisites

- .NET 10 SDK
- Windows, Linux with a supported desktop session, or macOS
- optional sibling `ARIEC61850` checkout beside the `arvrel` repository for SV decoder and live/replay capability

## Build and test

```bash
dotnet build ARVREL.Desktop.sln -c Release
dotnet test ../tests/Arvrel.Desktop.Tests/Arvrel.Desktop.Tests.csproj -c Release --no-build
```

## Run

```bash
dotnet run --project ../src/Arvrel.Desktop/Arvrel.Desktop.csproj
```

The deterministic internal laboratory works without the sibling decoder. Missing live/replay capability is reported in the shell rather than treated as an application-start failure.

The current Windows WPF product remains in the repository-root `ARVREL.sln` and continues to use the root .NET 8 SDK selection.
