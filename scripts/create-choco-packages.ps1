# This script generates the chocolatey packages for the .NET Scanner and the .NET Framework Scanner.

function Create-Choco-Package([string] $runtime) {
    $Packaging = "$PSScriptRoot\..\Packaging"
    $Zip = "$Packaging\Binaries\sonar-scanner-$env:FULL_VERSION-$runtime.zip"
    Write-Host "Generating the '$runtime' chocolatey package for $Zip"
    $Hash = (Get-FileHash $Zip -Algorithm SHA256).Hash
    $Content = "Install-ChocolateyZipPackage ""sonarscanner-$runtime"" ``
        -Url ""https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:FULL_VERSION/sonar-scanner-$env:FULL_VERSION-$runtime.zip"" ``
        -UnzipLocation ""`$(Split-Path -parent `$MyInvocation.MyCommand.Definition)"" ``
        -ChecksumType ""sha256"" ``
        -Checksum ""$Hash""
    "
    New-Item -ItemType Directory -Path "$Packaging\Binaries\Chocolatey\" -Force | Out-Null
    Set-Content -Path "$Packaging\Binaries\Chocolatey\chocolateyInstall-$runtime.ps1" -Value $Content
    choco pack "$Packaging\Chocolatey\sonarscanner-$runtime.nuspec" --outputdirectory "$Packaging\Binaries\Chocolatey" --version $env:PATCH_VERSION
}

Create-Choco-Package "net-framework"
Create-Choco-Package "net"
