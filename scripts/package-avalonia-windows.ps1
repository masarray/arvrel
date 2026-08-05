[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^[0-9a-fA-F]{7,64}$|^unknown$')]
    [string]$CommitSha = 'unknown'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishPath = (Resolve-Path $PublishDirectory).Path
$outputPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDirectory))
$iconPath = (Resolve-Path (Join-Path $repoRoot 'Asset\icon\favicon.ico')).Path
$installerScript = (Resolve-Path (Join-Path $repoRoot 'packaging\avalonia\windows\ARVREL-Avalonia.iss')).Path

if (-not (Test-Path (Join-Path $publishPath 'Arvrel.Desktop.exe') -PathType Leaf)) {
    throw "Published Windows host is missing: $publishPath\Arvrel.Desktop.exe"
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$stagePath = Join-Path ([System.IO.Path]::GetTempPath()) ("arvrel-win-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagePath -Force | Out-Null

try {
    Copy-Item -Path (Join-Path $publishPath '*') -Destination $stagePath -Recurse -Force

    foreach ($legalFile in @(
        'LICENSE',
        'README.md',
        'SECURITY.md',
        'SUPPORT.md',
        'THIRD-PARTY-NOTICES.md'
    )) {
        $source = Join-Path $repoRoot $legalFile
        if (-not (Test-Path $source -PathType Leaf)) {
            throw "Required package document is missing: $source"
        }
        Copy-Item $source -Destination (Join-Path $stagePath $legalFile) -Force
    }

    $portableName = "ARVREL-Avalonia-v$Version-win-x64-portable.zip"
    $portablePath = Join-Path $outputPath $portableName
    if (Test-Path $portablePath) {
        Remove-Item $portablePath -Force
    }
    Compress-Archive -Path (Join-Path $stagePath '*') -DestinationPath $portablePath -CompressionLevel Optimal

    $isccCandidates = @(
        @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            'C:\ProgramData\chocolatey\bin\ISCC.exe'
        ) | Where-Object { $_ -and (Test-Path $_ -PathType Leaf) }
    )

    if ($isccCandidates.Count -eq 0) {
        throw 'Inno Setup Compiler (ISCC.exe) was not found.'
    }

    $iscc = $isccCandidates[0]
    & $iscc `
        "/DAppVersion=$Version" `
        "/DPublishDir=$stagePath" `
        "/DOutputDir=$outputPath" `
        "/DIconFile=$iconPath" `
        $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $installerName = "ARVREL-Avalonia-v$Version-win-x64-setup.exe"
    $installerPath = Join-Path $outputPath $installerName
    if (-not (Test-Path $installerPath -PathType Leaf)) {
        throw "Expected installer was not produced: $installerPath"
    }

    foreach ($pePath in @(
        (Join-Path $stagePath 'Arvrel.Desktop.exe'),
        $installerPath
    )) {
        $stream = [System.IO.File]::OpenRead($pePath)
        try {
            if ($stream.ReadByte() -ne 0x4D -or $stream.ReadByte() -ne 0x5A) {
                throw "File does not have a valid PE header: $pePath"
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($portablePath)
    try {
        if (-not ($archive.Entries | Where-Object FullName -eq 'Arvrel.Desktop.exe')) {
            throw "$portableName does not contain Arvrel.Desktop.exe at the archive root."
        }
    }
    finally {
        $archive.Dispose()
    }

    $portableSignature = Get-AuthenticodeSignature (Join-Path $stagePath 'Arvrel.Desktop.exe')
    $installerSignature = Get-AuthenticodeSignature $installerPath

    $manifestName = "ARVREL-Avalonia-v$Version-win-x64-manifest.json"
    $manifestPath = Join-Path $outputPath $manifestName
    [ordered]@{
        schemaVersion = 1
        product = 'ARVREL Avalonia'
        version = $Version
        runtimeIdentifier = 'win-x64'
        commit = $CommitSha
        selfContained = $true
        packages = @($portableName, $installerName)
        installScope = 'current-user'
        installDirectory = '%LOCALAPPDATA%\Programs\ARVREL-Avalonia'
        executable = 'Arvrel.Desktop.exe'
        signing = [ordered]@{
            portable = $portableSignature.Status.ToString()
            installer = $installerSignature.Status.ToString()
            trustedPublisherConfigured = $false
        }
        capabilities = [ordered]@{
            internalLaboratory = $true
            pcapReplay = $true
            liveCapture = 'Npcap backend when an authorized Npcap installation is present'
        }
    } | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding utf8NoBOM

    $checksumPath = Join-Path $outputPath 'SHA256SUMS-win-x64.txt'
    @($portablePath, $installerPath, $manifestPath) |
        ForEach-Object {
            $hash = Get-FileHash $_ -Algorithm SHA256
            '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [System.IO.Path]::GetFileName($_)
        } |
        Set-Content -Path $checksumPath -Encoding ascii

    foreach ($required in @($portablePath, $installerPath, $manifestPath, $checksumPath)) {
        if (-not (Test-Path $required -PathType Leaf) -or (Get-Item $required).Length -eq 0) {
            throw "Windows package output is missing or empty: $required"
        }
    }

    Write-Host "Created Windows Avalonia package set in $outputPath"
}
finally {
    if (Test-Path $stagePath) {
        Remove-Item $stagePath -Recurse -Force
    }
}
