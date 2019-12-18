Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp3.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/__PackageVersion__/sonar-scanner-msbuild-__PackageVersion__-netcoreapp3.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "not-set"
