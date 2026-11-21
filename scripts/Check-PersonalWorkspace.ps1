[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 10 SDK before running this checker.' }
$project = Join-Path $root 'VInternships.Checks/VInternships.Checks.csproj'
Push-Location $root
try {
    Write-Host 'Building the personal demo and checks...' -ForegroundColor Cyan
    & dotnet build $project --configuration Release --nologo -warnaserror -p:NuGetAuditMode=all
    if ($LASTEXITCODE -ne 0) { throw 'VInternships build failed. Paste the build errors here.' }
    Write-Host 'Running checks with independent temporary sample workspaces...' -ForegroundColor Cyan
    & dotnet run --project $project --configuration Release --no-build --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'VInternships regression checks failed. Paste the full output here.' }
    Write-Host 'Build and regression checks passed. Browser and Docker acceptance remain separate.' -ForegroundColor Green
}
finally { Pop-Location }
