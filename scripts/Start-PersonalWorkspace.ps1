[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'VInternships/VInternships.csproj'
$contentRoot = Join-Path $root 'VInternships'
$previousDataDirectory = $env:VINTERNSHIPS_DEMO_DATA_DIR
$env:VINTERNSHIPS_DEMO_DATA_DIR = Join-Path $contentRoot 'App_Data/PersonalDemo'
Push-Location $root
try {
    Write-Host 'Starting VInternships personal demo: http://127.0.0.1:5099' -ForegroundColor Cyan
    Write-Host 'Use fictional students, companies and placements. No SQL server or passwords are required.'
    Write-Host 'Wait for the listening message. Press Ctrl+C to stop.'
    & dotnet run --project $project --configuration Release --no-build --no-launch-profile -- '--environment' 'Development' '--contentRoot' $contentRoot '--urls' 'http://127.0.0.1:5099'
    if ($LASTEXITCODE -ne 0) { throw 'The website stopped with an error. Paste the output here.' }
}
finally { $env:VINTERNSHIPS_DEMO_DATA_DIR = $previousDataDirectory; Pop-Location }
