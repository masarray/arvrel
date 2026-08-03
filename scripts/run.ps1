[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'ScriptSupport.ps1')

Push-Location $root
try {
    Stop-ArvrelDevelopmentInstances -RepositoryRoot $root
    Invoke-DotNetChecked -Arguments @('run', '--project', '.\src\Arvrel.App\Arvrel.App.csproj', '-c', $Configuration) -FailureMessage 'ARVREL run failed'
}
finally {
    Pop-Location
}
