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

$versionCore = ($Version -split '-', 2)[0]
$numericParts = @($versionCore -split '\.')
while ($numericParts.Count -lt 4) {
    $numericParts += '0'
}
$numericVersion = ($numericParts[0..3] -join '.')

$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts 'publish'
$portableSingle = Join-Path $artifacts 'portable-single'
$notices = Join-Path $artifacts 'legal-notices'
$release = Join-Path $artifacts 'release'
Remove-Item $publish, $portableSingle, $notices, $release -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish, $portableSingle, $notices, $release -ItemType Directory -Force | Out-Null

$appProject = Join-Path $root 'src\Arvrel.App\Arvrel.App.csproj'

Write-Host "Publishing ARVREL $Version Windows x64 installer payload..."
& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publish `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "Installer-payload dotnet publish failed with exit code $LASTEXITCODE."
}

$installedExe = Join-Path $publish 'ARVREL.exe'
if (-not (Test-Path $installedExe -PathType Leaf)) {
    throw "Expected application executable was not produced: $installedExe"
}

Write-Host "Publishing ARVREL $Version as a self-contained single-file portable executable..."
& dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $portableSingle `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:UseAppHost=true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "Single-file dotnet publish failed with exit code $LASTEXITCODE."
}

$singleExecutables = @(Get-ChildItem $portableSingle -Filter '*.exe' -File)
if ($singleExecutables.Count -ne 1) {
    throw "Expected exactly one portable executable, found $($singleExecutables.Count)."
}
if (@(Get-ChildItem $portableSingle -Filter '*.dll' -File).Count -ne 0) {
    throw 'Single-file publish unexpectedly left one or more DLL files beside the executable.'
}

$portableExeName = "ARVREL-v$Version-win-x64-portable.exe"
$portableExePath = Join-Path $release $portableExeName
Copy-Item $singleExecutables[0].FullName $portableExePath -Force

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
    if (-not (Test-Path $source -PathType Leaf)) {
        throw "Required distribution notice is missing: $relativePath"
    }
    Copy-Item $source $publish -Force
    Copy-Item $source $notices -Force
}

$commit = (& git -C $root rev-parse HEAD).Trim()
$engineRoot = Join-Path $root '..\ARIEC61850'
$engineCommit = if (Test-Path (Join-Path $engineRoot '.git')) {
    (& git -C $engineRoot rev-parse HEAD).Trim()
} else {
    'compiled-dependency-not-available-as-sibling-source'
}

$buildInfoName = "ARVREL-v$Version-build-info.txt"
$buildInfoPath = Join-Path $release $buildInfoName
@"
ARVREL version: $Version
ARVREL commit: $commit
ARIEC61850 commit: $engineCommit
Runtime: win-x64 self-contained
Installer scope: current user only; no elevation requested
Portable form: single-file EXE plus multi-file ZIP fallback
Code signing: intentionally not performed; no certificate secrets are required
Build UTC: $([DateTimeOffset]::UtcNow.ToString('O'))
Safety boundary: virtual output only; no GOOSE/MMS/physical trip output
"@ | Set-Content $buildInfoPath -Encoding UTF8
Copy-Item $buildInfoPath (Join-Path $publish 'BUILD-INFO.txt') -Force
Copy-Item $buildInfoPath (Join-Path $notices 'BUILD-INFO.txt') -Force

@"
ARVREL unsigned distribution notice

These public beta binaries are built by GitHub Actions without commercial Authenticode signing.
The per-user installer and portable executable do not request administrator elevation.
Windows SmartScreen, AppLocker, WDAC, antivirus, or organization policy may still block an unsigned executable.
Do not bypass an employer or device-owner security policy. Ask the authorized IT administrator to allow the verified SHA-256 hash when required.
Live Npcap capture still requires an authorized Npcap installation; PCAP replay and internal virtual-injection workflows do not install a driver.
"@ | Set-Content (Join-Path $notices 'UNSIGNED-DISTRIBUTION.txt') -Encoding UTF8

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

$portableZipName = "ARVREL-v$Version-win-x64-portable.zip"
$portableZipPath = Join-Path $release $portableZipName
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $portableZipPath -CompressionLevel Optimal

$noticesZipName = "ARVREL-v$Version-legal-notices.zip"
$noticesZipPath = Join-Path $release $noticesZipName
Compress-Archive -Path (Join-Path $notices '*') -DestinationPath $noticesZipPath -CompressionLevel Optimal

if (-not $SkipInstaller) {
    $issPath = Join-Path $root 'installer\ARVREL.iss'
    $issText = Get-Content $issPath -Raw
    if ($issText -notmatch '(?m)^PrivilegesRequired=lowest\s*$') {
        throw 'The installer must remain a current-user installation with PrivilegesRequired=lowest.'
    }
    if ($issText -match '(?m)^PrivilegesRequiredOverridesAllowed=') {
        throw 'Privilege override options are not allowed in the no-admin installer.'
    }

    $isccCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($iscc)) {
        throw 'Inno Setup 6 compiler was not found. Install it or use -SkipInstaller.'
    }

    & $iscc "/DAppVersion=$Version" "/DAppVersionNumeric=$numericVersion" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $installerPath = Join-Path $release "ARVREL-Setup-v$Version-win-x64.exe"
    if (-not (Test-Path $installerPath -PathType Leaf)) {
        throw "Inno Setup completed without producing the expected installer: $installerPath"
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
