[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$expectedRepo = 'st10174227/vinternships-portfolio-demo'
$expectedRemote = "https://github.com/$expectedRepo.git"
$marker = Join-Path $root '.publication-ready.json'
if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
    throw 'Run Prepare-VInternships-Publication.ps1 first. No repository was published.'
}
foreach ($command in @('git', 'gh')) {
    if (-not (Get-Command $command -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "$command is missing. Install Git and GitHub CLI, then retry from this prepared folder."
    }
}
$ready = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
if ($ready.repository -ne $expectedRepo -or $ready.productionContainer -ne 'passed' -or @($ready.files).Count -lt 100) { throw 'Invalid publication record.' }
foreach ($file in $ready.files) {
    $path = Join-Path $root $file.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Prepared source is missing: $($file.path)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "Prepared source changed after checks: $($file.path). Prepare a fresh copy before publishing."
    }
}
# In Windows PowerShell 5.1, redirected native stderr can become a terminating
# error under Stop. Capture the exit code explicitly for expected API failures.
$savedPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $profileText = & gh api user 2>$null
    $profileCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = $savedPreference }
if ($profileCode -ne 0) { throw 'Run gh auth login, sign in as st10174227, then retry. Do not share tokens.' }
$profile = ($profileText -join "`n") | ConvertFrom-Json
if ($profile.login -ne 'st10174227') { throw 'GitHub CLI must be signed in as st10174227. No repository was changed.' }

Push-Location -LiteralPath $root
try {
    # This directory was created by the preparation script; a parent Git tree is never reused.
    if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) {
        & git init -b main
        if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the separate publication repository.' }
    }
    $top = & git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($root)) {
        throw 'Git root does not match the separate publication folder.'
    }
    $branch = & git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') { throw 'Expected the main branch in the publication folder.' }
    $remotes = @(& git remote)
    if ($LASTEXITCODE -ne 0 -or @($remotes | Where-Object { $_ -ne 'origin' }).Count -gt 0) { throw 'Unexpected Git remote.' }
    $origin = $null
    if ($remotes -contains 'origin') {
        $origin = & git remote get-url origin
        if ($LASTEXITCODE -ne 0 -or $origin -ne $expectedRemote) { throw 'Origin does not match the intended personal repository.' }
    }

    # Stage the verified list only; generated data and unlisted files cannot enter the commit.
    foreach ($file in $ready.files) {
        & git add -- $file.path
        if ($LASTEXITCODE -ne 0) { throw "Could not stage $($file.path)." }
    }
    $tracked = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect staged files.' }
    $allowed = @($ready.files | ForEach-Object { $_.path })
    foreach ($path in $tracked) {
        if ($allowed -notcontains $path) { throw "Unexpected tracked file: $path. Publication stopped." }
    }
    & git -c user.name='Mohammed Ba Yazed' -c "user.email=$($profile.id)+st10174227@users.noreply.github.com" diff --cached --quiet
    $diffCode = $LASTEXITCODE
    if ($diffCode -gt 1) { throw 'Could not inspect staged changes.' }
    if ($diffCode -eq 1) {
        & git -c user.name='Mohammed Ba Yazed' -c "user.email=$($profile.id)+st10174227@users.noreply.github.com" commit -m 'Publish personal VInternships portfolio demo'
        if ($LASTEXITCODE -ne 0) { throw 'Commit failed. Nothing was pushed.' }
    }

    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $repoResponse = @(& gh api --include "repos/$expectedRepo" 2>$null)
        $repoCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($repoCode -ne 0) {
        if (($repoResponse -join "`n") -notmatch '(?m)^HTTP/\S+\s+404\b') {
            throw 'Could not verify repository availability. Check GitHub connectivity and retry.'
        }
        & gh repo create $expectedRepo --public --description 'Interactive ASP.NET Core internship workspace with isolated fictional student and company records'
        if ($LASTEXITCODE -ne 0) { throw 'Repository creation failed. The prepared local commit is retained.' }
    }
    elseif (-not $origin) {
        throw 'That repository already exists. No existing repository was changed; share its URL before continuing.'
    }
    if (-not $origin) {
        & git remote add origin $expectedRemote
        if ($LASTEXITCODE -ne 0) { throw 'Could not add the personal origin.' }
    }
    # Use the active GitHub CLI account without changing global Git credential settings.
    & git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push --set-upstream origin main
    if ($LASTEXITCODE -ne 0) { throw 'Push failed. Retry this script from the prepared folder. No force-push was used.' }
    Write-Host "Published source: https://github.com/$expectedRepo" -ForegroundColor Green
    Write-Host "Check automated builds: https://github.com/$expectedRepo/actions"
    Write-Host 'Public hosting is the next step. LinkedIn and the CV were not changed.'
}
finally { Pop-Location }
