param (
    [switch]
    $Its = $false
)

# Check if running in WSL
if (-Not $env:WSL_DISTRO_NAME) {
    # Check if wsl is installed
    if (-Not (Get-Command wsl -ErrorAction SilentlyContinue)) {
        Write-Host "ERROR: WSL is not installed. Please install WSL to run this script."
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace((wsl command -v pwsh))) {
        Write-Host "ERROR: PowerShell is not installed in WSL. Please install PowerShell in WSL to run this script."
        exit 1
    }

    $sonarHome = Join-Path -Path $Env:USERPROFILE -ChildPath ".sonar"
    $orchestratorHome = Join-Path -Path $sonarHome -ChildPath "orchestrator"

    $sonarHome = $sonarHome -replace '\\', '/'
    $orchestratorHome = $orchestratorHome -replace '\\', '/'

    $sonarHome = wsl wslpath -a "$sonarHome"
    $orchestratorHome = wsl wslpath -a "$orchestratorHome"

    $m2Home = (Join-Path -Path $Env:USERPROFILE -ChildPath ".m2") -replace '\\', '/'
    $m2Home = wsl wslpath -a "$m2Home"

    Write-Host "Not running in WSL. Re-invoking the script in WSL..."
    $escapedPath = $MyInvocation.MyCommand.Path -replace '\\', '/'
    $wslPath = wsl wslpath -a "$escapedPath"
    wsl env ORCHESTRATOR_HOME=$orchestratorHome `
        SONAR_HOME=$sonarHome `
        M2_HOME=$m2Home `
        ARTIFACTORY_USER=$env:ARTIFACTORY_USER `
        ARTIFACTORY_PASSWORD=$env:ARTIFACTORY_PASSWORD `
        pwsh -File "$wslPath"
    exit
}

$MissingDeps = @()
if (-Not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $MissingDeps += "dotnet"
}
if (-Not (Get-Command mvn -ErrorAction SilentlyContinue)) {
    $MissingDeps += "mvn"
}
if (-Not (Get-Command unzip -ErrorAction SilentlyContinue)) {
    $MissingDeps += "unzip"
}
if (-Not (Get-Command java -ErrorAction SilentlyContinue)) {
    $MissingDeps += "java"
}

if ($MissingDeps.Count -gt 0) {
    Write-Host "ERROR: The following dependencies are missing: $($MissingDeps -join ', '). Please install them to run this script."
    exit 1
}

pwsh scripts/run-test-linux.ps1 -TestToRun ($Its ? "IT" : "UT")
