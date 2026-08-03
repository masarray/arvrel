[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'ScriptSupport.ps1')

function Write-LatestArvrelCrashLog {
    $logPath = Join-Path $env:LOCALAPPDATA 'ARVREL\logs\arvrel-crash.log'
    if (-not (Test-Path -LiteralPath $logPath)) {
        return
    }

    Write-Host ''
    Write-Host "Latest ARVREL crash diagnostics: $logPath" -ForegroundColor Yellow
    Write-Host '------------------------------------------------------------' -ForegroundColor DarkGray
    Get-Content -LiteralPath $logPath -Tail 80 | ForEach-Object { Write-Host $_ }
    Write-Host '------------------------------------------------------------' -ForegroundColor DarkGray
}

Push-Location $root
try {
    Stop-ArvrelDevelopmentInstances -RepositoryRoot $root
    try {
        Invoke-DotNetChecked -Arguments @('run', '--project', '.\src\Arvrel.App\Arvrel.App.csproj', '-c', $Configuration) -FailureMessage 'ARVREL run failed'
    }
    catch {
        Write-LatestArvrelCrashLog
        throw
    }
}
finally {
    Pop-Location
}
