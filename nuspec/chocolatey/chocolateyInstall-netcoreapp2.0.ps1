Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp2.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:ChocolateyPackageVersion/sonar-scanner-msbuild-$env:ChocolateyPackageVersion-netcoreapp2.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "246E289E151FAF6D96972994F5D1A9F66B0CA650EFAF8FE9789657C888DA4884"
