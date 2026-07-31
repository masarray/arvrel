[CmdletBinding()]
param(
    [string]$AriecRoot = (Join-Path $PSScriptRoot '..\..\ARIEC61850')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$engine = Join-Path $AriecRoot 'src\AR.Iec61850\AR.Iec61850.csproj'
$npcap = Join-Path $AriecRoot 'src\AR.Iec61850.Transports.Npcap\AR.Iec61850.Transports.Npcap.csproj'

Write-Host "ARVREL : $root"
Write-Host "ARIEC  : $AriecRoot"

if (-not (Test-Path $engine)) { throw "Missing sibling engine project: $engine" }
if (-not (Test-Path $npcap)) { throw "Missing sibling Npcap project: $npcap" }

Write-Host 'Sibling layout is valid.' -ForegroundColor Green
