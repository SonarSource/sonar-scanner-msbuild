Install-ChocolateyZipPackage "sonarscanner-net-framework" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/__PackageVersion__/sonar-scanner-__PackageVersion__-net-framework.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "not-set"
