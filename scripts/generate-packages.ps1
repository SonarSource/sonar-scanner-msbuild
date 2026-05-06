# This script generates the chocolatey packages for the .NET Scanner and the .NET Framework Scanner and updates the pom.xml file with the new artifacts locations.

# The chocolatey packages are generated from the nuspec files located in the nuspec\chocolatey folder and they point to GitHub artifacts that have the following format:
# Release candidates:
# - https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/9.0.0-rc.99116/sonar-scanner-9.0.0-rc.99116-net.zip
# - https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/9.0.0-rc.99116/sonar-scanner-9.0.0-rc.99116-net-framework.zip
# Normal releases:
# - https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/8.0.3.99785/sonar-scanner-8.0.3.99785-net.zip
# - https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/8.0.3.99785/sonar-scanner-8.0.3.99785-net-framework.zip

# In the case of pre-releases, the full version is following the Sem 2 versioning format: 9.0.0-rc.99116.
# Unfortunatelly Choco only supports Sem 1 versioning format, which does not allow anything except for [0-9A-Za-z-] after the dash in `-rc`.
# Due to this, when calling `choco pack` the version should not contain the build number (9.0.0-rc).
# At the same time the the url inside the ps1 file that downloads the scanner should be correct and contain the build number.

[CmdletBinding()]
param (
  [Parameter()]
  [AllowNull()]
  [string]
  $sourcesDirectory = $env:BUILD_SOURCESDIRECTORY
)

function Update-Choco-Package([string] $scannerZipFileName, [string] $runtimeSuffix) {
  Write-Host "Generating the chocolatey package from $scannerZipFileName"

  $hash = (Get-FileHash $scannerZipFileName -Algorithm SHA256).hash
  $powershellScriptPath = "nuspec/chocolatey/chocolateyInstall-$runtimeSuffix.ps1"
  Write-Host (Get-Item $powershellScriptPath).FullName
  (Get-Content $powershellScriptPath) `
    -Replace '-Checksum "not-set"', "-Checksum $hash" `
    -Replace '__PackageVersion__', "$env:FULL_VERSION" `
  | Set-Content $powershellScriptPath

  choco pack "nuspec/chocolatey/sonarscanner-$runtimeSuffix.nuspec" --outputdirectory $artifactsFolder --version $env:PATCH_VERSION
}

function Update-Pom-File() {
  Write-Host 'Update artifacts locations in pom.xml'
  $sbomJsonPath = Get-Item "$sourcesDirectory/build/bom.json"
  $netScannerGlobalToolPath = Get-Item "$artifactsFolder/dotnet-sonarscanner.$env:PATCH_VERSION.nupkg"  # dotnet-sonarscanner.9.0-rc.nupkg or dotnet-sonarscanner.9.0.nupkg
  $pomFile = './pom.xml'
  (Get-Content $pomFile) `
    -Replace 'netFrameworkScannerZipPath', "$netFrameworkScannerZipPath" `
    -Replace 'netScannerZipPath', "$netScannerZipPath" `
    -Replace 'netScannerGlobalToolPath', "$netScannerGlobalToolPath" `
    -Replace 'netFrameworkScannerChocoPath', "$artifactsFolder/sonarscanner-net-framework.$env:PATCH_VERSION.nupkg" `
    -Replace 'netScannerChocoPath', "$artifactsFolder/sonarscanner-net.$env:PATCH_VERSION.nupkg" `
    -Replace 'sbomPath', "$sbomJsonPath" `
  | Set-Content $pomFile
}

# Read the version from the Version.props file and initialize the global variables.
$artifactsFolder = "$sourcesDirectory/build"
$netFrameworkScannerZipPath = Get-Item "$artifactsFolder/sonarscanner-net-framework.zip"
$netScannerZipPath = Get-Item "$artifactsFolder/sonarscanner-net.zip"

Update-Choco-Package $netFrameworkScannerZipPath 'net-framework'
Update-Choco-Package $netScannerZipPath 'net'
Update-Pom-File
