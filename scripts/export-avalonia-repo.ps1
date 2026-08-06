[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\avalonia-export'),
    [switch]$SkipValidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$stageRoot = Join-Path $outputRoot 'arvrel-avalonia'
$zipPath = Join-Path $outputRoot 'arvrel-avalonia-p5.9-repo-ready.zip'
$hashPath = "$zipPath.sha256"

Write-Host "Preparing Avalonia split from $repoRoot"

if (Test-Path $outputRoot) {
    Remove-Item $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null

$excludedRootNames = @('.git', 'artifacts')
Get-ChildItem -LiteralPath $repoRoot -Force |
    Where-Object { $_.Name -notin $excludedRootNames } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $stageRoot -Recurse -Force
    }

# Remove the stable Windows/WPF product surface and its release machinery.
# Shared protection, process-bus, capture, application, and Avalonia projects stay.
$removePaths = @(
    'src\Arvrel.App',
    'ARVREL.sln',
    'installer',
    'RELEASE-NOTES.md',
    'VERSION',
    '.github\workflows',
    'Asset\screenshot',
    'scripts\package-release.ps1',
    'scripts\publish-github.ps1',
    'scripts\build.cmd',
    'scripts\run.cmd',
    'scripts\verify-sibling.cmd',
    'scripts\build.ps1',
    'scripts\run.ps1',
    'scripts\validate-public-seo.py',
    'scripts\validate-public-site.py',
    'scripts\export-avalonia-repo.ps1',
    'tests\Arvrel.Protection.Tests\RelayHardwarePresentationSourceTests.cs',
    'tests\Arvrel.Protection.Tests\RelayLedPresentationSourceTests.cs',
    'tests\Arvrel.Protection.Tests\RelayPremiumSurfaceSourceTests.cs',
    'tests\Arvrel.Protection.Tests\RelayVisualConsistencySourceTests.cs'
)

foreach ($relativePath in $removePaths) {
    $target = Join-Path $stageRoot $relativePath
    if (Test-Path $target) {
        Remove-Item $target -Recurse -Force
    }
}

# Remove the original WPF-oriented public website while retaining engineering
# Markdown, especially the P5 migration design records.
$docsRoot = Join-Path $stageRoot 'docs'
if (Test-Path $docsRoot) {
    Get-ChildItem -LiteralPath $docsRoot -File -Recurse -Force |
        Where-Object {
            $_.Extension -eq '.html' -or
            $_.Name -in @('.nojekyll', 'robots.txt', 'sitemap.xml', 'trust-manifest.json')
        } |
        Remove-Item -Force

    foreach ($relativePath in @('research', 'workflows', 'assets')) {
        $target = Join-Path $docsRoot $relativePath
        if (Test-Path $target) {
            Remove-Item $target -Recurse -Force
        }
    }
}

$readme = @'
# ARVREL Avalonia

Cross-platform migration and engineering preview of the ARVREL Virtual Protection Relay Laboratory.

> **Status:** active preview, not the current public production release. The stable Windows release remains in `masarray/arvrel` and uses the WPF P6 real-device interface.

## Purpose

This repository isolates the Avalonia migration so cross-platform architecture, packaging, process-bus acquisition, waveform presentation, and relay-faceplate parity can evolve without reducing the quality of the stable Windows product.

## Current snapshot

- migration milestone: **P5.9**;
- source snapshot: `8ecec49933f7043c77a8f93b30d0cfe9803d5fff`;
- Avalonia application: `src/Arvrel.Desktop`;
- portable application layer: `src/Arvrel.Application`;
- capture abstraction: `src/Arvrel.Capture`;
- process-bus runtime: `src/Arvrel.ProcessBus`;
- protection core: `src/Arvrel.Protection`;
- scoped solution: `desktop/ARVREL.Desktop.sln`.

## Build and test

```powershell
.\scripts\build.ps1
```

Equivalent manual commands:

```powershell
cd desktop
dotnet restore ARVREL.Desktop.sln
dotnet build ARVREL.Desktop.sln -c Release --no-restore
dotnet test ARVREL.Desktop.sln -c Release --no-build
```

Run the application:

```powershell
.\scripts\run.ps1
```

## Migration boundary

The preview retains the shared protection, process-bus, capture, and application projects. The WPF application, Windows installer, WPF-only source-contract tests, and WPF release workflow are intentionally excluded.

The visual target is functional and visual parity with the P6 real-device interface. Cross-platform availability alone is not considered sufficient for replacing the stable product.

## Safety

ARVREL is virtual-output laboratory software. It is not a certified protection IED, calibrated relay test set, switching authority, IEC 61850 conformance result, IEC 60255 type-test platform, or hard-real-time trip system.

Use live capture only on isolated and authorized laboratory networks.

## License

GPL-3.0-or-later. Third-party components retain their own licenses.
'@
Set-Content -LiteralPath (Join-Path $stageRoot 'README.md') -Value $readme -Encoding utf8

$migrationStatus = @'
# Avalonia migration status

## Origin

This repository-ready snapshot was separated from `masarray/arvrel` after the stable product returned to the WPF P6 real-device UX.

- final Avalonia source commit: `8ecec49933f7043c77a8f93b30d0cfe9803d5fff`;
- preserved source branch: `archive/avalonia-p5.9-final-before-split`;
- recommended new repository: `masarray/arvrel-avalonia`;
- intended status: experimental / engineering preview.

## Included

- Avalonia shell and virtual relay faceplate;
- internal injection workflow;
- live capture and PCAP replay abstractions;
- SCL and Sampled Values stream workspace;
- guarded process-bus display handover;
- shared protection and measurement libraries;
- Avalonia and shared-core regression tests;
- Avalonia packaging scripts;
- cross-platform CI definition.

## Excluded

- WPF application project `src/Arvrel.App`;
- WPF-only visual source-contract tests;
- WPF installer and release publication workflow;
- stable-product version, release notes, and public website;
- generated build outputs and Git history.

## Promotion gate

The Avalonia edition should not replace the WPF stable product until it reaches:

1. functional parity;
2. visual parity with the approved P6 device-like UX;
3. stable packaging on Windows, Linux, and macOS;
4. clean-machine validation;
5. no regression in protection, trust, injection, or process-bus evidence behavior.
'@
Set-Content -LiteralPath (Join-Path $stageRoot 'MIGRATION_STATUS.md') -Value $migrationStatus -Encoding utf8

$pushScript = @'
[CmdletBinding()]
param(
    [string]$RepositoryUrl = 'https://github.com/masarray/arvrel-avalonia.git'
)

$ErrorActionPreference = 'Stop'

if (Test-Path '.git') {
    throw 'This folder already contains a .git directory. Use a clean extracted copy.'
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is not installed or not available in PATH.'
}

git init
git add --all
git commit -m 'Initial Avalonia P5.9 migration snapshot'
git branch -M main
git remote add origin $RepositoryUrl
git push -u origin main

Write-Host "Pushed Avalonia migration snapshot to $RepositoryUrl"
'@
Set-Content -LiteralPath (Join-Path $stageRoot 'PUSH-TO-NEW-REPO.ps1') -Value $pushScript -Encoding utf8

$buildScript = @'
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location (Join-Path $root 'desktop')
try {
    dotnet restore ARVREL.Desktop.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

    dotnet build ARVREL.Desktop.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    dotnet test ARVREL.Desktop.sln -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
}
finally {
    Pop-Location
}
'@
Set-Content -LiteralPath (Join-Path $stageRoot 'scripts\build.ps1') -Value $buildScript -Encoding utf8

$runScript = @'
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location (Join-Path $root 'desktop')
try {
    dotnet run --project ..\src\Arvrel.Desktop\Arvrel.Desktop.csproj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Avalonia application run failed.' }
}
finally {
    Pop-Location
}
'@
Set-Content -LiteralPath (Join-Path $stageRoot 'scripts\run.ps1') -Value $runScript -Encoding utf8

$ciDirectory = Join-Path $stageRoot '.github\workflows'
New-Item -ItemType Directory -Path $ciDirectory -Force | Out-Null
$ci = @'
name: Avalonia cross-platform CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build-test:
    strategy:
      fail-fast: false
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    timeout-minutes: 35

    steps:
      - uses: actions/checkout@v4
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Restore
        working-directory: desktop
        run: dotnet restore ARVREL.Desktop.sln

      - name: Build
        working-directory: desktop
        run: dotnet build ARVREL.Desktop.sln -c Release --no-restore

      - name: Test Avalonia desktop solution
        working-directory: desktop
        run: dotnet test ARVREL.Desktop.sln -c Release --no-build

      - name: Test shared core projects
        shell: pwsh
        run: |
          $projects = @(
            'tests/Arvrel.Application.Tests/Arvrel.Application.Tests.csproj',
            'tests/Arvrel.Capture.Tests/Arvrel.Capture.Tests.csproj',
            'tests/Arvrel.ProcessBus.Tests/Arvrel.ProcessBus.Tests.csproj',
            'tests/Arvrel.Protection.Tests/Arvrel.Protection.Tests.csproj'
          )
          foreach ($project in $projects) {
            dotnet test $project -c Release
            if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
          }
'@
Set-Content -LiteralPath (Join-Path $ciDirectory 'ci.yml') -Value $ci -Encoding utf8

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
$snapshot = @"
ARVREL Avalonia repository export
Source repository: https://github.com/masarray/arvrel
Export branch commit: $commit
Final Avalonia migration source commit: 8ecec49933f7043c77a8f93b30d0cfe9803d5fff
Generated UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))
"@
Set-Content -LiteralPath (Join-Path $stageRoot 'SOURCE_SNAPSHOT.txt') -Value $snapshot -Encoding utf8

if (-not (Test-Path (Join-Path $stageRoot 'desktop\ARVREL.Desktop.sln'))) {
    throw 'Avalonia scoped solution is missing from the export.'
}
if (-not (Test-Path (Join-Path $stageRoot 'src\Arvrel.Desktop\Arvrel.Desktop.csproj'))) {
    throw 'Avalonia desktop project is missing from the export.'
}
if (Test-Path (Join-Path $stageRoot 'src\Arvrel.App')) {
    throw 'WPF application project leaked into the Avalonia export.'
}

$codeRoots = @('src', 'tests', 'scripts', '.github', 'desktop', 'packaging')
$leaks = foreach ($codeRoot in $codeRoots) {
    $scanRoot = Join-Path $stageRoot $codeRoot
    if (-not (Test-Path $scanRoot)) { continue }

    Get-ChildItem -LiteralPath $scanRoot -File -Recurse -Force |
        Where-Object { $_.Extension -in @('.cs', '.csproj', '.sln', '.ps1', '.cmd', '.yml', '.yaml', '.sh') } |
        Select-String -Pattern 'src[\\/]Arvrel\.App(?:[\\/]|$)' -AllMatches
}
if ($leaks) {
    $locations = $leaks | ForEach-Object { "$($_.Path):$($_.LineNumber)" }
    throw "WPF source references leaked into the Avalonia export: $($locations -join ', ')"
}

if (-not $SkipValidation) {
    Push-Location (Join-Path $stageRoot 'desktop')
    try {
        dotnet restore ARVREL.Desktop.sln
        if ($LASTEXITCODE -ne 0) { throw 'Avalonia restore failed.' }

        dotnet build ARVREL.Desktop.sln -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Avalonia build failed.' }

        dotnet test ARVREL.Desktop.sln -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Avalonia desktop tests failed.' }
    }
    finally {
        Pop-Location
    }

    $sharedTestProjects = @(
        'tests\Arvrel.Application.Tests\Arvrel.Application.Tests.csproj',
        'tests\Arvrel.Capture.Tests\Arvrel.Capture.Tests.csproj',
        'tests\Arvrel.ProcessBus.Tests\Arvrel.ProcessBus.Tests.csproj',
        'tests\Arvrel.Protection.Tests\Arvrel.Protection.Tests.csproj'
    )
    foreach ($relativeProject in $sharedTestProjects) {
        $project = Join-Path $stageRoot $relativeProject
        dotnet test $project -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Shared test project failed: $relativeProject"
        }
    }
}

# Do not ship generated build output.
Get-ChildItem -LiteralPath $stageRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj', 'TestResults') } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $stageRoot,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true)

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($zipPath))" |
    Set-Content -LiteralPath $hashPath -Encoding ascii

Write-Host "Avalonia export created: $zipPath"
Write-Host "SHA256: $($hash.Hash.ToLowerInvariant())"
