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

try {
    Push-Location "${PSScriptRoot}"

    $fixedVersion = $version
    if ($fixedVersion.EndsWith(".0")) {
        $fixedVersion = $version.Substring(0, $version.Length - 2)
    }

    $versionPropsFile = Resolve-Path "Version.props"
    $xml = [xml](Get-Content $versionPropsFile)
    $xml.Project.PropertyGroup.MainVersion = $version
    $xml.Save($versionPropsFile)
    msbuild "ChangeVersion.proj"

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