<#

.SYNOPSIS
This script allows to set the specified version in all required files.

#>

[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [ValidatePattern("^\d{1,3}\.\d{1,3}\.\d{1,3}$")]
    [string]$version
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Set-VersionForDotNet() {
    Write-Header "Updating version in .Net files"

    Invoke-InLocation ".\scripts\version" {
        $versionPropsFile = Resolve-Path "Version.props"
        $xml = [xml](Get-Content $versionPropsFile)
        $xml.Project.PropertyGroup.MainVersion = $version
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