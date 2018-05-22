$version = "4.2.0.1214"

Install-ChocolateyZipPackage "sonarscanner-msbuild-netcoreapp2.0" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/${version}/sonar-scanner-msbuild-${version}-netcoreapp2.0.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha1' `
    -Checksum "f0cdd999f298100485c3e25fe9380cf2f244cfb4"
