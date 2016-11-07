[CmdletBinding()]
param([switch]$Legacy)

Write-Verbose "Importing module: TestHelpersModule"
Import-Module $PSScriptRoot/TestHelpersModule -Verbose:$false

