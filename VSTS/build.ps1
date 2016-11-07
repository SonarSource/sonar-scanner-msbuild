[cmdletbinding()] 
param (
	[switch]$Clean,
	[switch]$Build,
	[switch]$Package,
	[switch]$Publish
)

# ----------------------------------------------------------------------
# --- Variables
# ----------------------------------------------------------------------
# Formatting
$sectionDisplayFormat = (("-"*25) + "[{0}]" + ("-"*25))
# Directories
$buildOutputDir = "$PSScriptRoot\.buildOutput"
$distributionDir = "$buildOutputDir\dist"
# Visual Studio MarketPlace
$publisherName = "amaurysonarsource"
$extensionId = "sonarqube-scanner-msbuild"
$extensionVersion = "1.0.0"
$vsixFile = "$distributionDir\$publisherName.$extensionId-$extensionVersion.vsix"

# Clean
function Clean
{
	Write-Output ($sectionDisplayFormat -f "Clean")

	If (Test-Path $buildOutputDir) 
	{
		Write-Output "Deleting folder" $buildOutputDir
		Remove-Item $buildOutputDir -recurse
	}
}

function PatchExtensionVersion
{
	param(
		[Parameter(Mandatory=$True)]
		[string]$jsonFile
	)
	
	$content = Get-Content $jsonFile -raw | ConvertFrom-Json
	$content.version = $extensionVersion
	$content | ConvertTo-Json -Depth 100 | Set-Content $jsonFile
}

function PatchTaskVersion
{
	param(
		[Parameter(Mandatory=$True)]
		[string]$jsonFile
	)
	
	$splittedVersion = $extensionVersion -split '\.'
	
	$content = Get-Content $jsonFile -raw | ConvertFrom-Json
	$content.version.Major = [int]$splittedVersion[0]
	$content.version.Minor = [int]$splittedVersion[1]
	$content.version.Patch = [int]$splittedVersion[2]
	$content | ConvertTo-Json -Depth 100 | Set-Content $jsonFile
}

# Build
function Build
{	
	$tasksDir = "$PSScriptRoot\Tasks"
	$extensionDir = "$PSScriptRoot\Extension"
	
	# SonarQube Scanner for MSBuild
	$sqScannerFilePath = "$buildOutputDir\sonarqube-scanner-msbuild.zip"
	$sqScannerShortVersion = "2.2"
	$sqScannerFullVersion = "$sqScannerShortVersion.0.24"
	$sqScannerFileUrl = "https://github.com/SonarSource-VisualStudio/sonar-scanner-msbuild/releases/download/$sqScannerShortVersion/sonar-scanner-msbuild-$sqScannerFullVersion.zip"

	Write-Output ($sectionDisplayFormat -f "Build")

	Write-Output "Creating folder" $buildOutputDir
	New-Item -Path $buildOutputDir -ItemType Directory

	Write-Output "Patching extension version"
	PatchExtensionVersion "$extensionDir\vss-extension.json"
	Write-Output "Copying extension files"
	Copy-Item "$extensionDir\*" $buildOutputDir -recurse
	
	Write-Output "Patching 'SonarQubeScannerMsBuildBegin' task version"
	PatchTaskVersion "$tasksDir\SonarQubeScannerMsBuildBegin\task.json"
	Write-Output "Patching 'SonarQubeScannerMsBuildEnd' task version"
	PatchTaskVersion "$tasksDir\SonarQubeScannerMsBuildEnd\task.json"
	Write-Output "Copying tasks files"
	Copy-Item "$tasksDir\SonarQubeScannerMsBuildBegin" $buildOutputDir -recurse
	Copy-Item "$tasksDir\SonarQubeScannerMsBuildEnd" $buildOutputDir -recurse
	Copy-Item "$tasksDir\Common\SonarQubeHelper.ps1" "$buildOutputDir\SonarQubeScannerMsBuildBegin"
	Copy-Item "$tasksDir\Common\SonarQubeHelper.ps1" "$buildOutputDir\SonarQubeScannerMsBuildEnd"

	Write-Output "Downloading SonarQube Scanner for MSBuild"
	(New-Object System.Net.WebClient).DownloadFile($sqScannerFileUrl, $sqScannerFilePath)

	Write-Output "Extracting SonarQube Scanner for MSBuild"
	Add-Type -AssemblyName "System.IO.Compression.FileSystem"
	[IO.Compression.ZipFile]::ExtractToDirectory($sqScannerFilePath, "$buildOutputDir\SonarQubeScannerMsBuildBegin\SonarQubeScannerMsBuild")
}

# Package
function Package
{
	Write-Output ($sectionDisplayFormat -f "Package")

	Write-Output "Creating vsix"	
	tfx extension create --manifest-globs vss-extension.json --publisher $publisherName --extension-id $extensionId --root $buildOutputDir --output-path $vsixFile
	
	If (-Not (Test-Path $vsixFile))
	{
		throw "something went wrong during the packaging step"
	}	
}

# Publish
function Publish
{	
	Write-Output ($sectionDisplayFormat -f "Publish")
	
	Write-Output "Publishing vsix extension to the marketplace"
	tfx extension publish --vsix $vsixFile --share-with $publisherName
}

# ----------------------------------------------------------------------
# --- Execute
# ----------------------------------------------------------------------
If ($Clean) {
	Clean
}

If ($Build) {
	Build
}

If ($Package) {
	Package
}

If ($Publish) {
	Publish
}