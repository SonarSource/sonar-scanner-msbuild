Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Build-TFSProcessor() {
    Write-Host "Building TFSProcessor"
    Invoke-MSBuild "SonarScanner.MSBuild.TFS.sln" "/t:Rebuild" "/p:Configuration=Release"
    Write-Host "TFSProcessor build has completed."
}

function Build-Scanner() {
    Write-Host "Building SonarScanner for MSBuild"
    Invoke-MSBuild "SonarScanner.MSBuild.sln" "/t:Rebuild" "/p:Configuration=Release"
    Write-Host "Build for SonarScanner has completed."
}

function CleanAndRecreate-BuildDirectories() {
    if (Test-Path("$fullBuildOutputDir\sonarscanner-msbuild-net46")) {
        Remove-Item "$fullBuildOutputDir\sonarscanner-msbuild-net46\*" -Recurse -Force
    }
    if (Test-Path("$fullBuildOutputDir\sonarscanner-msbuild-netcoreapp2.0")) {
        Remove-Item "$fullBuildOutputDir\sonarscanner-msbuild-netcoreapp2.0\*" -Recurse -Force
    }
    if (Test-Path("$fullBuildOutputDir\sonarscanner-msbuild-netcoreapp3.0")) {
        Remove-Item "$fullBuildOutputDir\sonarscanner-msbuild-netcoreapp3.0\*" -Recurse -Force
    }
}

try {
    Write-Host $PSScriptRoot
    
    . (Join-Path $PSScriptRoot "build-utils.ps1")
    . (Join-Path $PSScriptRoot "package-artifacts.ps1")
    . (Join-Path $PSScriptRoot "variables.ps1")

    CleanAndRecreate-BuildDirectories
    Download-ScannerCli

    Build-TFSProcessor
    Build-Scanner

    Package-Net46Scanner
    Package-NetCoreApp2Scanner
    Package-NetCoreApp3Scanner
    
    Write-Host -ForegroundColor Green "SUCCESS: CI job was successful!"
    exit 0
}
catch {
    Write-Host -ForegroundColor Red $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}