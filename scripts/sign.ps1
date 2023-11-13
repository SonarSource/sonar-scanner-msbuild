function Sign-Assemblies {
    param (
        [string]$Pattern
    )
    Get-ChildItem $Pattern |
    Foreach-Object {
        & signtool sign /du https://www.sonarsource.com/ /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 /csp "DigiCert Signing Manager KSP" /kc "$env:SM_KP" /f "$env:SM_CLIENT_CRT_FILE" $_.FullName
    }
}

Write-Host "Signing .NET 5.0 assemblies"
Sign-Assemblies "build\sonarscanner-msbuild-net5.0\Sonar*.dll"
Write-Host "[Completed] Signing .NET 5.0 assemblies"

Write-Host "Signing .NET 4.6 assemblies"
Sign-Assemblies "build\sonarscanner-msbuild-net46\Sonar*.dll"
Write-Host "[Completed]Signing .NET 4.6 assemblies"

Write-Host "Signing .NET Core 2 assemblies"
Sign-Assemblies "build\sonarscanner-msbuild-netcoreapp2.0\Sonar*.dll"
Write-Host "[Completed] Signing .NET Core 2 assemblies"

Write-Host "Signing .NET Core 3 assemblies"
Sign-Assemblies "build\sonarscanner-msbuild-netcoreapp3.0\Sonar*.dll"
Write-Host "[Completed] Signing .NET Core 3 assemblies"