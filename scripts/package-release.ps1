[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipInstaller,
    [switch]$SkipSbom
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid release version '$Version'."
}

$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$release = Join-Path $artifacts 'release'
Remove-Item $publish, $release -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish, $release -ItemType Directory -Force | Out-Null

Write-Host "Publishing ARVREL $Version for Windows x64..."
& dotnet publish (Join-Path $root 'src\Arvrel.App\Arvrel.App.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$distributionFiles = @(
    'LICENSE',
    'COMMERCIAL-LICENSING.md',
    'THIRD-PARTY-NOTICES.md',
    'SECURITY.md',
    'SUPPORT.md',
    'RELEASE-NOTES.md'
)
foreach ($relativePath in $distributionFiles) {
    $source = Join-Path $root $relativePath
    if (-not (Test-Path $source)) {
        throw "Required distribution notice is missing: $relativePath"
    }
    Copy-Item $source $publish -Force
}

$commit = (& git -C $root rev-parse HEAD).Trim()
$engineCommit = if (Test-Path (Join-Path $root '..\ARIEC61850\.git')) {
    (& git -C (Join-Path $root '..\ARIEC61850') rev-parse HEAD).Trim()
} else {
    'compiled-dependency-not-available-as-sibling-source'
}
@"
ARVREL version: $Version
ARVREL commit: $commit
ARIEC61850 commit: $engineCommit
Runtime: win-x64 self-contained
Build UTC: $([DateTimeOffset]::UtcNow.ToString('O'))
Safety boundary: virtual output only; no GOOSE/MMS/physical trip output
"@ | Set-Content (Join-Path $publish 'BUILD-INFO.txt') -Encoding UTF8

$dependencyReport = Join-Path $release "ARVREL-v$Version-nuget-dependencies.txt"
& dotnet list (Join-Path $root 'ARVREL.sln') package --include-transitive | Out-File $dependencyReport -Encoding UTF8
if ($LASTEXITCODE -ne 0) {
    throw "Dependency report generation failed with exit code $LASTEXITCODE."
}

if (-not $SkipSbom) {
    $cycloneDx = Get-Command 'dotnet-CycloneDX' -ErrorAction SilentlyContinue
    if ($null -ne $cycloneDx) {
        & $cycloneDx.Source (Join-Path $root 'ARVREL.sln') `
            -o $release `
            -fn "ARVREL-v$Version-sbom.cdx.json"
        if ($LASTEXITCODE -ne 0) {
            throw "CycloneDX SBOM generation failed with exit code $LASTEXITCODE."
        }
    } else {
        Write-Warning 'dotnet-CycloneDX is not installed; SBOM generation was skipped.'
    }
}

$portableName = "ARVREL-v$Version-win-x64-portable.zip"
$portablePath = Join-Path $release $portableName
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portablePath -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $isccCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($iscc)) {
        throw 'Inno Setup 6 compiler was not found. Install it or use -SkipInstaller.'
    }

    & $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\ARVREL.iss')
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
}

$checksumsPath = Join-Path $release 'SHA256SUMS.txt'
Get-ChildItem $release -File |
    Where-Object Name -ne 'SHA256SUMS.txt' |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    } | Set-Content $checksumsPath -Encoding ASCII

Write-Host "Release artifacts created in $release"
Get-ChildItem $release -File | Sort-Object Name | Format-Table Name, Length
