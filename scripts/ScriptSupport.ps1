Set-StrictMode -Version Latest

function Invoke-DotNetChecked {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [string]$FailureMessage = 'dotnet command failed'
    )

    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "$FailureMessage (exit code $exitCode)."
    }
}

function Stop-ArvrelDevelopmentInstances {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,

        [ValidateRange(1, 30)]
        [int]$TimeoutSeconds = 8
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\') + '\'
    $targets = @()

    foreach ($process in @(Get-Process -Name 'ARVREL' -ErrorAction SilentlyContinue)) {
        $path = $null
        try {
            $path = $process.Path
        }
        catch {
            # A process owned by another account may not expose its executable path.
        }

        if ([string]::IsNullOrWhiteSpace($path)) {
            continue
        }

        $fullPath = [System.IO.Path]::GetFullPath($path)
        if ($fullPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $targets += $process
        }
    }

    if ($targets.Count -eq 0) {
        return
    }

    $processList = ($targets | ForEach-Object { "ARVREL ($($_.Id))" }) -join ', '
    Write-Host "Stopping running ARVREL development instance(s): $processList"

    foreach ($process in $targets) {
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $remaining = @(
            $targets | Where-Object {
                Get-Process -Id $_.Id -ErrorAction SilentlyContinue
            }
        )

        if ($remaining.Count -eq 0) {
            Write-Host 'ARVREL output files released.'
            return
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    $remainingList = ($remaining | ForEach-Object { $_.Id }) -join ', '
    throw "ARVREL process(es) did not exit within $TimeoutSeconds second(s): $remainingList"
}
