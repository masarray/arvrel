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

    Invoke-DotNetChecked -Arguments @('restore', '.\ARVREL.sln') -FailureMessage 'ARVREL restore failed'
    Invoke-DotNetChecked -Arguments @('build', '.\ARVREL.sln', '-c', $Configuration, '--no-restore') -FailureMessage 'ARVREL build failed'
    Invoke-DotNetChecked -Arguments @('test', '.\ARVREL.sln', '-c', $Configuration, '--no-build') -FailureMessage 'ARVREL tests failed'
}
finally {
    Pop-Location
}
