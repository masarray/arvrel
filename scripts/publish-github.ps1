[CmdletBinding()]
param(
    [string]$Owner = 'masarray',
    [string]$Repository = 'arvrel',
    [ValidateSet('public','private')]
    [string]$Visibility = 'public'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is not installed.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) is not installed.' }

    if (-not (Test-Path .git)) {
        git init -b main
        if ($LASTEXITCODE -ne 0) { throw 'git init failed.' }
    }

    git add --all
    if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }

    git rev-parse --verify HEAD 2>$null | Out-Null
    $hasCommit = $LASTEXITCODE -eq 0

    git diff --cached --quiet
    $hasStagedChanges = $LASTEXITCODE -ne 0

    if (-not $hasCommit) {
        git commit -m 'feat: publish ARVREL virtual protection relay laboratory'
        if ($LASTEXITCODE -ne 0) { throw 'Initial git commit failed. Configure git user.name and user.email when needed.' }
    }
    elseif ($hasStagedChanges) {
        git commit -m 'chore: update ARVREL project'
        if ($LASTEXITCODE -ne 0) { throw 'Git commit failed.' }
    }

    $fullName = "$Owner/$Repository"
    gh repo view $fullName 2>$null | Out-Null
    $repoExists = $LASTEXITCODE -eq 0

    if (-not $repoExists) {
        $visibilityFlag = if ($Visibility -eq 'public') { '--public' } else { '--private' }
        gh repo create $fullName $visibilityFlag --source . --remote origin --push
        if ($LASTEXITCODE -ne 0) { throw 'GitHub repository creation or initial push failed.' }
    }
    else {
        git remote get-url origin 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            git remote add origin "https://github.com/$fullName.git"
        }
        git push -u origin main
        if ($LASTEXITCODE -ne 0) { throw 'Git push failed.' }
    }

    Write-Host "Published: https://github.com/$fullName" -ForegroundColor Green
}
finally {
    Pop-Location
}
