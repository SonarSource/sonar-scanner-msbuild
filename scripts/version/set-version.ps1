<#

.SYNOPSIS
This script allows to set the specified version in all required files.

#>

[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [ValidatePattern("^\d{1,3}\.\d{1,3}\.\d{1,3}$")]
    [string]$version,

    [Parameter(Mandatory = $False, Position = 1)]
    [ValidatePattern("^alpha|beta|rc$")] # see https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort#pre-release-versions
    [string]$prereleaseSuffix = ""
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Set-VersionForDotNet() {
    Write-Header "Updating version in .Net files"

    Invoke-InLocation ".\scripts\version" {
        $versionPropsFile = Resolve-Path "Version.props"
        $xml = [xml](Get-Content $versionPropsFile)
        $xml.Project.PropertyGroup.MainVersion = $version
        if (-not [string]::IsNullOrWhiteSpace($prereleaseSuffix)) {
            $xml.Project.PropertyGroup.PrereleaseSuffix = "-" + $prereleaseSuffix
        }
        else {
            $xml.Project.PropertyGroup.PrereleaseSuffix = ""
        }

        $xml.Save($versionPropsFile)
        msbuild "ChangeVersion.proj"
        Test-ExitCode "ERROR: Change version FAILED."
    }
}

try {
    . (Join-Path $PSScriptRoot "..\utils.ps1")

    Push-Location "${PSScriptRoot}\..\.."

    $fixedVersion = $version
    if ($fixedVersion.EndsWith(".0")) {
        $fixedVersion = $version.Substring(0, $version.Length - 2)
    }

    Set-VersionForDotNet

    exit 0
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}