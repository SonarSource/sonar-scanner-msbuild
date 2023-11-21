function Sign-Assemblies {
    param (
        [string]$Pattern
    )
    Get-ChildItem $Pattern |
    Foreach-Object {
        & signtool sign /du https://www.sonarsource.com/ /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 /csp "DigiCert Signing Manager KSP" /kc "$env:SM_KP" /f "$env:SM_CLIENT_CRT_FILE" $_.FullName
    }
}

Write-Host "Signing .NET Framework assemblies"
Sign-Assemblies "build\sonarscanner-net-framework\Sonar*.dll"
Write-Host "[Completed] Signing .NET Framework assemblies"

Write-Host "Signing .NET Core 3 assemblies"
Sign-Assemblies "build\sonarscanner-msbuild-netcoreapp3.0\Sonar*.dll"
Write-Host "[Completed] Signing .NET Core 3 assemblies"
