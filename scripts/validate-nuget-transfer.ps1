param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('edge', 'saas')]
    [string] $Mode,
    [Parameter(Mandatory = $true)]
    [string] $ReaderRole,
    [string] $PrivatePackageId = 'SonarAnalyzer.CFG',
    [string] $PrivatePackageVersion = '10.35.0.4138'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression

$hostName = if ($Mode -eq 'edge') { 'repox.sonar.build' } else { 'repox.jfrog.io' }
foreach ($variable in @('ARTIFACTORY_URL', 'ARTIFACTORY_USERNAME', 'ARTIFACTORY_ACCESS_TOKEN', 'ARTIFACTORY_USER', 'ARTIFACTORY_PASSWORD', 'ARTIFACTORY_READER_ROLE')) {
    if ([Environment]::GetEnvironmentVariable($variable) -ne "unchanged-$variable") {
        throw "Unrelated Artifactory environment was not preserved: $variable"
    }
}
$root = Join-Path $env:RUNNER_TEMP "build-13211-$Mode-$([Guid]::NewGuid().ToString('N'))"
$evidenceDirectory = Join-Path $env:GITHUB_WORKSPACE "edge-validation-evidence/$Mode"
New-Item -ItemType Directory -Force -Path $root, $evidenceDirectory | Out-Null
$env:NUGET_PACKAGES = Join-Path $root 'packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $root 'http-cache'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $root 'plugins-cache'
$env:DOTNET_CLI_HOME = Join-Path $root 'dotnet-home'
New-Item -ItemType Directory -Force -Path $env:NUGET_PACKAGES, $env:NUGET_HTTP_CACHE_PATH, $env:NUGET_PLUGINS_CACHE_PATH, $env:DOTNET_CLI_HOME | Out-Null

$trackedLocks = @(git -c core.quotepath=false ls-files '*packages.lock.json')
$beforeLocks = @{}
foreach ($path in $trackedLocks) { $beforeLocks[$path] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash }
[xml] $config = Get-Content NuGet.Config -Raw
$configuredSource = @($config.configuration.packageSources.add | Where-Object { $_.key -eq 'Repox' })
if ($configuredSource.Count -ne 1 -or ([Uri] $configuredSource[0].value).Host -ne $hostName) {
    throw "Effective Repox configuration does not use expected host $hostName"
}
if ($config.configuration.config.add | Where-Object { $_.key -eq 'signatureValidationMode' -and $_.value -eq 'require' }) {
    Write-Host 'signatureValidationMode=require'
} else {
    throw 'NuGet signature validation is not required'
}

[xml] $baselineConfig = (git show HEAD:NuGet.Config) -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot load committed NuGet configuration for invariant checks' }
foreach ($section in @('config', 'trustedSigners', 'packageSourceMapping', 'packageSourceCredentials')) {
    if ($config.configuration.$section.OuterXml -ne $baselineConfig.configuration.$section.OuterXml) {
        throw "NuGet configuration section changed: $section"
    }
}

$summary = [ordered]@{
    mode = $Mode
    started_at = [DateTime]::UtcNow.ToString('o')
    runner_os = $env:RUNNER_OS
    reader_role = $ReaderRole
    effective_host = $hostName
    solution_source = $configuredSource[0].value
    run_url = "https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    run_attempt = $env:GITHUB_RUN_ATTEMPT
    job_key = $env:GITHUB_JOB
    commit = $env:GITHUB_SHA
    generic_artifactory_environment_preserved = $true
    shared_action_ref = 'SonarSource/ci-github-actions/config-nuget@b4eede988476ebb13a242fbc3ae4e8e374d78ad0'
    initial_package_files = @(Get-ChildItem $env:NUGET_PACKAGES -File -Recurse).Count
    initial_http_cache_files = @(Get-ChildItem $env:NUGET_HTTP_CACHE_PATH -File -Recurse).Count
    transfers = @()
}
Write-Host ($summary | ConvertTo-Json -Compress)

dotnet restore SonarScanner.MSBuild.sln --locked-mode --force --no-http-cache --configfile NuGet.Config --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "Locked solution restore failed: $LASTEXITCODE" }
dotnet tool restore --tool-manifest .config/dotnet-tools.json --configfile NuGet.Config --no-cache --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "Tool restore failed: $LASTEXITCODE" }
foreach ($path in $trackedLocks) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash -ne $beforeLocks[$path]) { throw "Lockfile changed: $path" }
}
$summary.lockfiles_preserved = $trackedLocks.Count
$summary.restored_nupkg_files = @(Get-ChildItem $env:NUGET_PACKAGES -Filter '*.nupkg' -File -Recurse).Count

$credentialFields = @{}
foreach ($field in $env:NuGetPackageSourceCredentials_Repox.Split(';')) {
    $pair = $field.Split('=', 2)
    if ($pair.Count -eq 2) { $credentialFields[$pair[0]] = $pair[1] }
}
if (-not $credentialFields.Username -or -not $credentialFields.Password) {
    throw 'Matching native-reader NuGet credentials are unavailable'
}
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(90)
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($credentialFields.Username):$($credentialFields.Password)"))
$client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Basic', $basic)
$client.DefaultRequestHeaders.CacheControl = New-Object System.Net.Http.Headers.CacheControlHeaderValue
$client.DefaultRequestHeaders.CacheControl.NoCache = $true
$client.DefaultRequestHeaders.CacheControl.NoStore = $true
$client.DefaultRequestHeaders.UserAgent.ParseAdd("BUILD-13211-validation/$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT-$env:RUNNER_OS-$Mode")
$basic = $null
$credentialFields.Clear()

function Get-ProofBytes([string] $Url, [string] $Kind) {
    $uri = [Uri] $Url
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne $hostName -or $uri.UserInfo) {
        throw "Rejected credential-bearing $Kind request outside expected HTTPS host"
    }
    $startedAt = [DateTime]::UtcNow.ToString('o')
    $response = $client.GetAsync($uri).GetAwaiter().GetResult()
    try {
        if ([int] $response.StatusCode -ne 200) { throw "$Kind fetch returned HTTP $([int] $response.StatusCode)" }
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        if ($bytes.Length -eq 0) { throw "$Kind fetch returned an empty body" }
        if ($response.Content.Headers.ContentLength -and $response.Content.Headers.ContentLength -ne $bytes.Length) {
            throw "$Kind did not receive the full advertised response body"
        }
        $sha256 = [Security.Cryptography.SHA256]::Create()
        $sha512 = [Security.Cryptography.SHA512]::Create()
        try {
            $proof = [ordered]@{
                kind = $Kind
                started_at = $startedAt
                completed_at = [DateTime]::UtcNow.ToString('o')
                effective_url = $response.RequestMessage.RequestUri.AbsoluteUri
                method = 'GET'
                status = [int] $response.StatusCode
                bytes = $bytes.Length
                sha256 = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
                sha512_base64 = [Convert]::ToBase64String($sha512.ComputeHash($bytes))
                user_agent = $client.DefaultRequestHeaders.UserAgent.ToString()
            }
        } finally { $sha256.Dispose(); $sha512.Dispose() }
        foreach ($headerName in @('X-Artifactory-Node-Id', 'X-Artifactory-Id', 'X-JFrog-Version', 'X-JFrog-Trace-Id')) {
            if ($response.Headers.Contains($headerName)) { $proof[$headerName] = @($response.Headers.GetValues($headerName)) }
        }
        $summary.transfers += $proof
        Write-Host ($proof | ConvertTo-Json -Compress)
        return ,$bytes
    } finally { $response.Dispose() }
}

try {
    $serviceIndexUrl = "https://$hostName/artifactory/api/nuget/v3/sonarsource-nuget-releases/index.json"
    $serviceIndex = [Text.Encoding]::UTF8.GetString((Get-ProofBytes $serviceIndexUrl 'private-service-index')) | ConvertFrom-Json
    $registrationResource = @($serviceIndex.resources | Where-Object { $_.'@type' -like 'RegistrationsBaseUrl*' } | Sort-Object { $_.'@type' } -Descending)[0]
    if (-not $registrationResource) { throw 'Private service index has no registration resource' }
    $id = $PrivatePackageId.ToLowerInvariant()
    $registrationUrl = "$($registrationResource.'@id'.TrimEnd('/'))/$id/index.json"
    $registration = [Text.Encoding]::UTF8.GetString((Get-ProofBytes $registrationUrl 'private-registration')) | ConvertFrom-Json
    $leaf = $null
    foreach ($page in $registration.items) {
        $items = @($page.items)
        if (-not $page.items) {
            $pageDocument = [Text.Encoding]::UTF8.GetString((Get-ProofBytes $page.'@id' 'private-registration-page')) | ConvertFrom-Json
            $items = @($pageDocument.items)
        }
        $leaf = @($items | Where-Object { $_.catalogEntry.version -eq $PrivatePackageVersion }) | Select-Object -First 1
        if ($leaf) { break }
    }
    if (-not $leaf -or -not $leaf.packageContent) { throw 'Requested private package version is absent from registration metadata' }
    $packageBytes = Get-ProofBytes $leaf.packageContent 'private-nupkg-full-transfer'
    $packagePath = Join-Path $root "$id.$PrivatePackageVersion.nupkg"
    [IO.File]::WriteAllBytes($packagePath, $packageBytes)
    $stream = [IO.MemoryStream]::new($packageBytes)
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Read)
    try {
        $nuspec = @($archive.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        if ($nuspec.Count -ne 1) { throw 'Private package has an unexpected nuspec count' }
        $reader = New-Object IO.StreamReader($nuspec[0].Open())
        try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($manifest.package.metadata.id -ne $PrivatePackageId -or $manifest.package.metadata.version -ne $PrivatePackageVersion) {
            throw 'Private package nuspec identity differs from requested version'
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
    dotnet nuget verify $packagePath --all
    if ($LASTEXITCODE -ne 0) { throw "Private package signature verification failed: $LASTEXITCODE" }
    $summary.private_package = "$PrivatePackageId/$PrivatePackageVersion"
    $summary.private_package_signature_verified = $true
    $summary.completed_at = [DateTime]::UtcNow.ToString('o')
    $summary | ConvertTo-Json -Depth 20 | Set-Content -Encoding utf8 (Join-Path $evidenceDirectory 'summary.json')
} finally {
    $client.Dispose()
    $handler.Dispose()
    Remove-Item $root -Force -Recurse
}
