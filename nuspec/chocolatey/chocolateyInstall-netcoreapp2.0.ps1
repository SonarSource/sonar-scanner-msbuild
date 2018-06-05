$version = "4.3.0.1333"

Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp2.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/${version}/sonar-scanner-msbuild-${version}-netcoreapp2.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha1' `
    -Checksum "4d54469c716b56fba9c8a0ccc561293e4cdb54ee"
