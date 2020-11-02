Write-Host "Signing .NET 5.0 assemblies"
Get-ChildItem "build\sonarscanner-msbuild-net5.0\Sonar*.dll" | 
Foreach-Object {
    & $env:SIGNTOOL_PATH sign /fd SHA256 /f $env:PFX_PATH /p $env:PFX_PASSWORD /tr http://timestamp.digicert.com?alg=sha256 $_.FullName
}
Write-Host "[Completed] Signing .NET 5.0 assemblies"

Write-Host "Signing .NET 4.6 assemblies"
Get-ChildItem "build\sonarscanner-msbuild-net46\Sonar*.dll" | 
Foreach-Object {
    & $env:SIGNTOOL_PATH sign /fd SHA256 /f $env:PFX_PATH /p $env:PFX_PASSWORD /tr http://timestamp.digicert.com?alg=sha256 $_.FullName
}
Get-ChildItem "build\sonarscanner-msbuild-net46\Sonar*.exe" | 
Foreach-Object {
    & $env:SIGNTOOL_PATH sign /fd SHA256 /f $env:PFX_PATH /p $env:PFX_PASSWORD /tr http://timestamp.digicert.com?alg=sha256 $_.FullName
}
Write-Host "[Completed]Signing .NET 4.6 assemblies"

Write-Host "Signing .NET Core 2 assemblies"
Get-ChildItem "build\sonarscanner-msbuild-netcoreapp2.0\Sonar*.dll" | 
Foreach-Object {
    & $env:SIGNTOOL_PATH sign /fd SHA256 /f $env:PFX_PATH /p $env:PFX_PASSWORD /tr http://timestamp.digicert.com?alg=sha256 $_.FullName
}
Write-Host "[Completed] Signing .NET Core 2 assemblies"

Write-Host "Signing .NET Core 3 assemblies"
Get-ChildItem "build\sonarscanner-msbuild-netcoreapp3.0\Sonar*.dll" | 
Foreach-Object {
    & $env:SIGNTOOL_PATH sign /fd SHA256 /f $env:PFX_PATH /p $env:PFX_PASSWORD /tr http://timestamp.digicert.com?alg=sha256 $_.FullName
}
Write-Host "[Completed] Signing .NET Core 3 assemblies"