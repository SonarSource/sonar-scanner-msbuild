Install-ChocolateyZipPackage "sonarscanner-msbuild-net46" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$env:ChocolateyPackageVersion/sonar-scanner-msbuild-$env:ChocolateyPackageVersion-net46.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType 'sha256' `
    -Checksum "6AE9ECF7A4B963167AB1DDC5ADB289A1883261827244F4F59102C9EEA5B47A3C"
