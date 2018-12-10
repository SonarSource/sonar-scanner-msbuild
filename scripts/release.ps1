<#

.SYNOPSIS
Release script.

#>

[CmdletBinding(PositionalBinding = $false)]
param (
    [Parameter(Mandatory = $True, Position = 1)]
    [ValidatePattern("^\d{1,3}\.\d{1,3}\.\d{1,3}.\d{4}$")]
    [string]$version,

    [string]$downloadFolder = "$PSScriptRoot\\..\\DeploymentArtifacts"
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

if ($PSBoundParameters['Verbose'] -Or $PSBoundParameters['Debug']) {
    $global:DebugPreference = "Continue"
}

function Get-ScannerArtifact($artifactKind, $localName) {
    $url = "https://repox.sonarsource.com/sonarsource-public-releases/org/sonarsource/scanner/msbuild/sonar-scanner-msbuild/$version/sonar-scanner-msbuild-$version-$artifactKind"

    Write-Host "Downloading artifact from '${url}' to '${downloadFolder}'"

    (New-Object System.Net.WebClient).DownloadFile($url, (Join-Path $downloadFolder $localName))
}

Get-ScannerArtifact "net46.zip" "sonar-scanner-msbuild-$version-net46.zip"
Get-ScannerArtifact "netcoreapp2.0.zip" "sonar-scanner-msbuild-$version-netcoreapp2.0.zip"

Get-ScannerArtifact "net46.nupkg" "sonarscanner-msbuild-net46.$version.nupkg"
Get-ScannerArtifact "netcoreapp2.0.nupkg" "sonarscanner-msbuild-netcoreapp2.0.$version.nupkg"

$lastDot = $version.LastIndexOf('.')
$mainVersion = $version.Substring(0, $lastDot)
Get-ScannerArtifact "netcoreapp2.1.nupkg" "dotnet-sonarscanner.$mainVersion.nupkg"