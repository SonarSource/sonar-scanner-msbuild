Install-ChocolateyZipPackage "sonarscanner-msbuild-net46" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:ChocolateyPackageVersion/sonar-scanner-msbuild-$env:ChocolateyPackageVersion-net46.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "8C1293F9AB180356F142B915A0B51FE95F3C58F9870AF409AC3262578AB113FD"
