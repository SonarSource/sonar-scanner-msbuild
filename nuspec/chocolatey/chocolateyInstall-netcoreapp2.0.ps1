Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp2.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:ChocolateyPackageVersion/sonar-scanner-msbuild-$env:ChocolateyPackageVersion-netcoreapp2.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "35571D22D3A1A24E63A3227933CFADAB22929E4C5EA717B027C98E1AA1A7ED30"
