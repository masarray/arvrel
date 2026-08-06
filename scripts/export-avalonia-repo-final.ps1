[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\avalonia-export')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$stageRoot = Join-Path $outputRoot 'arvrel-avalonia'
$zipPath = Join-Path $outputRoot 'arvrel-avalonia-p5.9-repo-ready.zip'
$hashPath = "$zipPath.sha256"
$engineRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot '..\ARIEC61850'))

if (-not (Test-Path (Join-Path $engineRoot 'src\AR.Iec61850\AR.Iec61850.csproj'))) {
    throw "Pinned ARIEC61850 sibling was not found at $engineRoot"
}
$engineProperty = "-p:ARIEC61850Root=$engineRoot"

# First build the sanitized repository tree using the audited export logic.
& (Join-Path $PSScriptRoot 'export-avalonia-repo.ps1') `
    -OutputDirectory $outputRoot `
    -SkipValidation
if ($LASTEXITCODE -ne 0) {
    throw 'Base Avalonia export failed.'
}

# The standalone repository keeps full process-bus validation by checking out
# the pinned decoder as a sibling, matching the source-development contract.
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
    timeout-minutes: 40

    steps:
      - name: Checkout ARVREL Avalonia
        uses: actions/checkout@v4
        with:
          path: arvrel-avalonia
          persist-credentials: false

      - name: Checkout pinned ARIEC61850 engine
        uses: actions/checkout@v4
        with:
          repository: masarray/ARIEC61850
          ref: c1afc68c9931e857eb787b0f249db2c2b0b757c4
          path: ARIEC61850
          persist-credentials: false

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            10.0.x

      - name: Restore
        working-directory: arvrel-avalonia/desktop
        run: dotnet restore ARVREL.Desktop.sln

      - name: Build
        working-directory: arvrel-avalonia/desktop
        run: dotnet build ARVREL.Desktop.sln -c Release --no-restore

      - name: Test Avalonia desktop solution
        working-directory: arvrel-avalonia/desktop
        run: dotnet test ARVREL.Desktop.sln -c Release --no-build

      - name: Test shared core projects
        shell: pwsh
        run: |
          $projects = @(
            'arvrel-avalonia/tests/Arvrel.Application.Tests/Arvrel.Application.Tests.csproj',
            'arvrel-avalonia/tests/Arvrel.Capture.Tests/Arvrel.Capture.Tests.csproj',
            'arvrel-avalonia/tests/Arvrel.ProcessBus.Tests/Arvrel.ProcessBus.Tests.csproj',
            'arvrel-avalonia/tests/Arvrel.Protection.Tests/Arvrel.Protection.Tests.csproj'
          )
          foreach ($project in $projects) {
            dotnet test $project -c Release
            if ($LASTEXITCODE -ne 0) { throw "Tests failed: $project" }
          }
'@
Set-Content -LiteralPath (Join-Path $ciDirectory 'ci.yml') -Value $ci -Encoding utf8

$readmePath = Join-Path $stageRoot 'README.md'
$readme = Get-Content -LiteralPath $readmePath -Raw
$readme = $readme.Replace(
    'The desktop toolchain is pinned separately from the legacy Windows product toolchain.',
    'The desktop toolchain is pinned separately from the legacy Windows product toolchain. Full live-decoder and process-bus validation expects the public `masarray/ARIEC61850` repository beside this repository; CI checks out the pinned engine automatically.')
Set-Content -LiteralPath $readmePath -Value $readme -Encoding utf8

# Validate exactly the tree that will be delivered, explicitly binding the
# nested staging tree to the checked-out pinned decoder.
Push-Location (Join-Path $stageRoot 'desktop')
try {
    dotnet restore ARVREL.Desktop.sln $engineProperty
    if ($LASTEXITCODE -ne 0) { throw 'Avalonia restore failed.' }

    dotnet build ARVREL.Desktop.sln -c Release --no-restore $engineProperty
    if ($LASTEXITCODE -ne 0) { throw 'Avalonia build failed.' }

    dotnet test ARVREL.Desktop.sln -c Release --no-build $engineProperty
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
    dotnet test $project -c Release $engineProperty
    if ($LASTEXITCODE -ne 0) {
        throw "Shared test project failed: $relativeProject"
    }
}

# Ensure the export stays repository-clean after validation.
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

Write-Host "Final Avalonia export created: $zipPath"
Write-Host "SHA256: $($hash.Hash.ToLowerInvariant())"
