$version = "4.3.0.1333"

Install-ChocolateyZipPackage "sonarscanner-msbuild-net46" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/${version}/sonar-scanner-msbuild-${version}-net46.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha1' `
    -Checksum "63ccb278629d3c787dae20b4c69016ecb346d39b"
