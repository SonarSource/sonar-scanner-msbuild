param (
  [string] $sourcesDirectory = $env:BUILD_SOURCESDIRECTORY,
  [string] $buildId = $env:BUILD_BUILDID
)

[xml] $versionProps = Get-Content "$sourcesDirectory\scripts\version\Version.props"
$version = $versionProps.Project.PropertyGroup.MainVersion + $versionProps.Project.PropertyGroup.PrereleaseSuffix
$artifactsFolder = "$sourcesDirectory\build"
$netFrameworkScannerZipPath = Get-Item "$artifactsFolder\sonarscanner-net-framework.zip"
$netScannerZipPath = Get-Item "$artifactsFolder\sonarscanner-net.zip"
$netScannerGlobalToolPath = Get-Item "$artifactsFolder\dotnet-sonarscanner.$version.nupkg"
$sbomJsonPath = Get-Item "$sourcesDirectory\build\bom.json"

Write-Host "Generating the chocolatey packages"
$netFrameworkZipPath = (Get-FileHash $netFrameworkScannerZipPath -Algorithm SHA256).hash
$netFrameworkPs1 = "nuspec\chocolatey\chocolateyInstall-net-framework.ps1"
(Get-Content $netFrameworkPs1) `
  -Replace '-Checksum "not-set"', "-Checksum $netFrameworkZipPath" `
  -Replace "__PackageVersion__", "$version" `
| Set-Content $netFrameworkPs1

$netZipHash = (Get-FileHash $netScannerZipPath -Algorithm SHA256).hash
$netPs1 = "nuspec\chocolatey\chocolateyInstall-net.ps1"
(Get-Content $netPs1) `
  -Replace '-Checksum "not-set"', "-Checksum $netZipHash" `
  -Replace "__PackageVersion__", "$version" `
| Set-Content $netPs1

choco pack nuspec\chocolatey\sonarscanner-net-framework.nuspec `
  --outputdirectory $artifactsFolder `
  --version $version

choco pack nuspec\chocolatey\sonarscanner-net.nuspec `
  --outputdirectory $artifactsFolder `
  --version $version

Write-Host "Update artifacts locations in pom.xml"
$pomFile = ".\pom.xml"
(Get-Content $pomFile) `
  -Replace 'netFrameworkScannerZipPath', "$netFrameworkScannerZipPath" `
  -Replace 'netScannerZipPath', "$netScannerZipPath" `
  -Replace 'netScannerGlobalToolPath', "$netScannerGlobalToolPath" `
  -Replace 'netFrameworkScannerChocoPath', "$artifactsFolder\\sonarscanner-net-framework.$version.nupkg" `
  -Replace 'netScannerChocoPath', "$artifactsFolder\\sonarscanner-net.$version.nupkg" `
  -Replace 'sbomPath', "$sbomJsonPath" `
| Set-Content $pomFile