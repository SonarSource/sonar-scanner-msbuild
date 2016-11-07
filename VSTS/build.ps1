# Download and extract the specified version of the SonarSource Scanner for MSBuild
$sonar_scanner_version = "2.2"
$sonar_scanner_full_version = "$sonar_scanner_version.0.24"
$sonar_scanner_url = "https://github.com/SonarSource-VisualStudio/sonar-scanner-msbuild/releases/download/$sonar_scanner_version/sonar-scanner-msbuild-$sonar_scanner_full_version.zip"
$sonar_scanner_msbuild_file_name = "$PSScriptRoot\\sonar-scanner-msbuild.zip"

(New-Object System.Net.WebClient).DownloadFile($sonar_scanner_url, $sonar_scanner_msbuild_file_name)

Add-Type -AssemblyName "System.IO.Compression.FileSystem"
[IO.Compression.ZipFile]::ExtractToDirectory($sonar_scanner_msbuild_file_name, "$PSScriptRoot\\Tasks\\SonarQubeScannerMsBuildBegin\\SonarQube.Bootstrapper")




