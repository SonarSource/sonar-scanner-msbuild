Install-ChocolateyZipPackage "sonarscanner-net" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/__PackageVersion__/sonar-scanner-__PackageVersion__-net.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "not-set"
