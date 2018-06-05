$version = "not-set"
$sha1 = "not-set"

Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp2.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/${version}/sonar-scanner-msbuild-${version}-netcoreapp2.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha1' `
    -Checksum $sha1
