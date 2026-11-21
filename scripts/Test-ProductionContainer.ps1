[CmdletBinding()]
param([string]$Image = 'vinternships-demo:publication')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$name = 'vinternships-production-check-' + [guid]::NewGuid().ToString('N').Substring(0,10)
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.UseCookies = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(5)
$started = $false
function Send-DemoRequest([string]$Path, [bool]$Forwarded, [string]$Cookie = '', [hashtable]$Fields = $null) {
    $method = [System.Net.Http.HttpMethod]::Get
    if ($null -ne $Fields) { $method = [System.Net.Http.HttpMethod]::Post }
    $request = [System.Net.Http.HttpRequestMessage]::new($method, $Path)
    try {
        if ($Forwarded) { $request.Headers.Add('X-Forwarded-Proto', 'https') }
        if ($Cookie) { $request.Headers.Add('Cookie', $Cookie) }
        if ($null -ne $Fields) {
            $values = [System.Collections.Generic.Dictionary[string,string]]::new()
            foreach ($key in $Fields.Keys) { $values.Add([string]$key, [string]$Fields[$key]) }
            $request.Content = [System.Net.Http.FormUrlEncodedContent]::new($values)
        }
        return $client.SendAsync($request).GetAwaiter().GetResult()
    }
    finally { $request.Dispose() }
}
try {
    # This trust setting is scoped to a loopback-only simulation of Render's managed ingress.
    & docker run --detach --rm --name $name -p '127.0.0.1::8080' `
        --env 'ASPNETCORE_ENVIRONMENT=Production' `
        --env 'AllowedHosts=127.0.0.1;localhost' `
        --env 'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true' $Image
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the production test container.' }
    $started = $true
    $binding = & docker port $name '8080/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Expected a loopback-only test port.' }
    $client.BaseAddress = [uri]('http://127.0.0.1:' + $Matches[1])
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = Send-DemoRequest '/health' $false
            try { $ready = [int]$response.StatusCode -eq 307 } finally { $response.Dispose() }
        } catch { $ready = $false }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'The production container did not become ready with a safe HTTP redirect.' }
    foreach ($path in @('/', '/Demo', '/health', '/Internship/Create')) {
        $response = Send-DemoRequest $path $false
        try {
            if ([int]$response.StatusCode -ne 307 -or $response.Headers.Location.Scheme -ne 'https' -or
                $response.Headers.Location.Port -ne 443 -or $response.Headers.Location.AbsolutePath -ne $path) {
                throw "Plain HTTP $path must redirect to the same path on standard HTTPS."
            }
            if ($response.Headers.Contains('Set-Cookie')) { throw 'An insecure probe received application cookies.' }
        } finally { $response.Dispose() }
    }
    Write-Host 'PASS Production HTTP probes redirect before rendering forms or issuing cookies'
    $response = Send-DemoRequest '/health' $true
    try {
        $health = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        if ([int]$response.StatusCode -ne 200 -or $health.application -ne 'vinternships-demo' -or $health.status -ne 'ok') { throw 'Forwarded HTTPS health failed.' }
        if ($response.Headers.Contains('Set-Cookie')) { throw 'Health probes must not create visitor cookies.' }
    } finally { $response.Dispose() }
    $response = Send-DemoRequest '/Student/Create' $true
    try {
        if ([int]$response.StatusCode -ne 200) { throw 'The production student form did not load.' }
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $tokenTag = @([regex]::Matches($body, '<input\b[^>]*>') | ForEach-Object { $_.Value } | Where-Object { $_ -match 'name="__RequestVerificationToken"' })
        if ($tokenTag.Count -ne 1 -or $tokenTag[0] -notmatch 'value="([^"]+)"') { throw 'Production antiforgery form token is missing.' }
        $token = [System.Net.WebUtility]::HtmlDecode($Matches[1])
        $cookies = @($response.Headers.GetValues('Set-Cookie'))
        foreach ($cookieName in @('vinternships-demo.session', 'vinternships-demo.antiforgery')) {
            $found = @($cookies | Where-Object { $_.StartsWith($cookieName + '=') })
            if ($found.Count -ne 1 -or $found[0] -notmatch '(?i);\s*secure(?:;|$)' -or $found[0] -notmatch '(?i);\s*httponly(?:;|$)') {
                throw "Production $cookieName must use Secure and HttpOnly."
            }
        }
        # Replay Secure cookies manually only because this private test simulates TLS termination over loopback HTTP.
        $cookieHeader = ($cookies | ForEach-Object { $_.Split(';')[0] }) -join '; '
    } finally { $response.Dispose() }
    Write-Host 'PASS Forwarded HTTPS serves the form with secure session and antiforgery cookies'
    $response = Send-DemoRequest '/Student/Create' $true $cookieHeader @{ Name='Missing Token'; Email='missing@example.invalid'; DegreeProgram='Software Development'; YearOfStudy='3' }
    try { if ([int]$response.StatusCode -ne 400) { throw 'Production accepted a tokenless form submission.' } } finally { $response.Dispose() }
    $response = Send-DemoRequest '/Student/Create' $true $cookieHeader @{ Name='Container Visitor'; Email='container@example.invalid'; DegreeProgram='Software Development'; YearOfStudy='3'; __RequestVerificationToken=$token }
    try { if ([int]$response.StatusCode -ne 302) { throw 'The valid production form submission failed.' } } finally { $response.Dispose() }
    $response = Send-DemoRequest '/Student' $true $cookieHeader
    try { if ([int]$response.StatusCode -ne 200 -or $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() -notmatch 'Container Visitor') { throw 'The saved production sample was not retained in its session.' } } finally { $response.Dispose() }
    $response = Send-DemoRequest '/Student' $true
    try { if ([int]$response.StatusCode -ne 200 -or $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() -match 'Container Visitor') { throw 'The sample leaked into another production session.' } } finally { $response.Dispose() }
    Write-Host 'PASS Production forms enforce antiforgery and retain independent visitor data'
    foreach ($path in @('/', '/Student', '/Student/Create', '/Company', '/Company/Create', '/Internship', '/Internship/Create', '/Demo', '/Home/Privacy')) {
        $response = Send-DemoRequest $path $true $cookieHeader
        try {
            if ([int]$response.StatusCode -ne 200) { throw "Production page failed: $path" }
        } finally { $response.Dispose() }
    }
    Write-Host 'PASS All primary pages load in the production container' 
    $savedPreference = $ErrorActionPreference
    try { $ErrorActionPreference='Continue'; $logs=(& docker logs $name 2>&1 | Out-String); $logsCode=$LASTEXITCODE }
    finally { $ErrorActionPreference=$savedPreference }
    if ($logsCode -ne 0) { throw 'Could not inspect production container logs.' }
    if ($logs -match 'The antiforgery system has the configuration value|Failed to determine the https port') { throw 'The container reproduced a production HTTPS configuration error.' }
    Write-Host 'VInternships production container checks passed.' -ForegroundColor Green
}
finally {
    $client.Dispose(); $handler.Dispose()
    if ($started) {
        & docker stop $name | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not stop test container $name." }
    }
}
