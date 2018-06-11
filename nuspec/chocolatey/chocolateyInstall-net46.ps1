Install-ChocolateyZipPackage "sonarscanner-msbuild-net46" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:ChocolateyPackageVersion/sonar-scanner-msbuild-$env:ChocolateyPackageVersion-net46.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "81541CB41357EDAF0D375017B3AD3E67C228B365C9EA85B29A2879A2B5C84890"
