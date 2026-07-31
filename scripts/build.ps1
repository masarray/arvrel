[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    dotnet restore .\ARVREL.sln
    dotnet build .\ARVREL.sln -c $Configuration --no-restore
    dotnet test .\ARVREL.sln -c $Configuration --no-build
}
finally {
    Pop-Location
}
