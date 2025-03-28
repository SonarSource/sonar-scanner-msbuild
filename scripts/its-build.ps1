Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Build-Scanner() {
    Write-Host "Building SonarScanner for MSBuild"
    Invoke-MSBuild "SonarScanner.MSBuild.sln" "/t:Restore"
    Invoke-MSBuild "SonarScanner.MSBuild.sln" "/t:Rebuild" "/p:Configuration=Release"
    Write-Host "Build for SonarScanner has completed."
}

function CleanAndRecreate-BuildDirectories([string]$suffix) {
    if (Test-Path("$fullBuildOutputDir\sonarscanner-$suffix")) {
        Remove-Item "$fullBuildOutputDir\sonarscanner-$suffix\*" -Recurse -Force
    }
}

try {
    Write-Host $PSScriptRoot

    . (Join-Path $PSScriptRoot "build-utils.ps1")
    . (Join-Path $PSScriptRoot "package-artifacts.ps1")
    . (Join-Path $PSScriptRoot "variables.ps1")

    CleanAndRecreate-BuildDirectories "net-framework"
    CleanAndRecreate-BuildDirectories "net"
    Download-ScannerCli

    Build-Scanner

    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        Package-NetFrameworkScanner
    }
    Package-NetScanner

    Write-Host -ForegroundColor Green "SUCCESS: CI job was successful!"
    exit 0
}
catch {
    Write-Host -ForegroundColor Red $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}
