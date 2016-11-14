param 
(
    [Parameter(Mandatory=$true, HelpMessage="Test or Production")]
    [ValidateSet("Test", "Production")]
    [string]$environment,
    [Parameter(Mandatory=$true, HelpMessage="The three number version for this release")]
    [string]$version
)

$ErrorActionPreference = "Stop"

$extensionsDirectoryPath = Join-Path $PSScriptRoot "Extensions"
$buildDirectoryPath = Join-Path $PSScriptRoot "build"
$buildArtifactsPath = Join-Path $buildDirectoryPath "Artifacts"
$buildTempPath = Join-Path $buildDirectoryPath "Temp"
$tasksTempPath = Join-Path -Path $buildTempPath -ChildPath "Extensions" | Join-Path -ChildPath "SonarQube" | Join-Path -ChildPath "Tasks"

function UpdateTfxCli
{
    Write-Host "Updating tfx-cli..."
    & npm up -g tfx-cli
}

function PrepareBuildDirectory
{
    if (Test-Path $buildDirectoryPath) 
    {        
        $buildDirectory = Get-Item "$buildDirectoryPath"
        Write-Host "Cleaning $buildDirectory..."
        Remove-Item $buildDirectory -Force -Recurse
    }
    
    Write-Host "Creating build directory..."
    New-Item -Type Directory -Path $buildTempPath | Out-Null
    Copy-Item $extensionsDirectoryPath -Destination $buildTempPath -Recurse
}

function CopyCommonTaskItems
{
   Write-Host "Copying common task components into each task"
   # for each task
   ForEach($TaskPath in Get-ChildItem -Path $tasksTempPath -Exclude "Common")
   {
      # Copy common task items into each task 
      ForEach($CommonFile in Get-ChildItem -Path (Join-Path $tasksTempPath "Common") -File) 
      {
         Copy-Item -Path $CommonFile.FullName -Destination $TaskPath | Out-Null 
      }
   }
   Remove-Item (Join-Path $tasksTempPath "Common") -Force -Recurse
}

function DownloadSonarQubeScanner
{
    param
    (
        [Parameter(Mandatory=$True)]
		[string]$scannerDownloadLink
    )

    Write-Host "Downloading SonarQube Scanner for MSBuild..."
    # Download zip
    $scannerZipPath = Join-Path $buildTempPath "scanner-msbuild.zip"
	(New-Object System.Net.WebClient).DownloadFile($scannerDownloadLink, $scannerZipPath)
	
    # Extract zip
    Add-Type -AssemblyName "System.IO.Compression.FileSystem"
    $scannerPath = Join-Path $buildTempPath "scanner-msbuild"
	[IO.Compression.ZipFile]::ExtractToDirectory($scannerZipPath, $scannerPath)

    # Remove zip file
    Remove-Item $scannerZipPath -Force

    return $scannerPath
}

function CopyScannerFiles
{
    param
    (
        [Parameter(Mandatory=$True)]
		[string]$scannerPath
    )

    # Copy specific content
    Copy-Item $scannerPath -Destination "$tasksTempPath\ScannerMsBuildBegin\SonarQubeScannerMsBuild" -Recurse
    Copy-Item "$scannerPath\sonar-scanner-2.8" -Destination "$tasksTempPath\ScannerCli\sonar-scanner" -Recurse
}

function UpdateExtensionManifestOverrideFile
{
    param
    (
		[Parameter(Mandatory=$True)]
		[string]$extensionBuildTempPath,
        [Parameter(Mandatory=$True)]
		[string]$environment,
        [Parameter(Mandatory=$True)]
		[string]$version
	)

    Write-Host "Finding environment-specific manifest overrides..."
    $overridesSourceFilePath = "$extensionBuildTempPath\extension-manifest.$environment.json"
    $overridesSourceFile = Get-ChildItem -Path $overridesSourceFilePath
    if ($overridesSourceFile -eq $null) 
    {
        Write-Error "Could not find the extension-manifest override file: $overridesSourceFilePath"
        return $null
    }

    Write-Host "Using $overridesSourceFile for overriding the standard extension-manifest.json, updating version to $version..."
    $manifest = ConvertFrom-JSON -InputObject (Get-Content $overridesSourceFile -Raw)
    $manifest.version = $version

    Remove-Item "$extensionBuildTempPath\extension-manifest.*.json" -Force

    $overridesFilePath = "$extensionBuildTempPath\extension-manifest.$environment.$version.json"
    ConvertTo-JSON $manifest -Depth 6 | Out-File $overridesFilePath -Encoding ASCII # tfx-cli doesn't support UTF8 with BOM
    Get-Content $overridesFilePath | Write-Host
    return Get-Item $overridesFilePath
}

function UpdateTaskManifests
{
    param
    (
		[Parameter(Mandatory=$True)]
		[string]$extensionBuildTempPath,
        [Parameter(Mandatory=$True)]
		[string]$version
	)

    $taskManifestFiles = Get-ChildItem $extensionBuildTempPath -Include "task.json" -Recurse
    foreach ($taskManifestFile in $taskManifestFiles)
    {
        Write-Host "Updating version to $version in $taskManifestFile..."
        $task = ConvertFrom-JSON -InputObject (Get-Content $taskManifestFile -Raw)
        $netVersion = [System.Version]::Parse($version)
        $task.version.Major = $netVersion.Major
        $task.version.Minor = $netVersion.Minor
        $task.version.Patch = $netVersion.Build
        
        $task.helpMarkDown = "Version: $version. [More Information](http://redirect.sonarsource.com/doc/install-configure-scanner-tfs-ts.html)"
        
        ConvertTo-JSON $task -Depth 6 | Out-File $taskManifestFile -Encoding UTF8
    }
}

function OverrideExtensionLogo($extensionBuildTempPath, $environment) 
{
    $extensionLogoOverrideFile = Get-Item "$extensionBuildTempPath\extension-icon.$environment.png" -ErrorAction SilentlyContinue
    if ($extensionLogoOverrideFile) 
    {
        $directory = Split-Path $extensionLogoOverrideFile
        $target = Join-Path $directory "extension-icon.png"
        Write-Host "Replacing extension logo with $extensionLogoOverrideFile..."
        Move-Item $extensionLogoOverrideFile $target -Force
    }
    
    Remove-Item "$extensionBuildTempPath\extension-icon.*.png" -Force
}

function OverrideTaskLogos
{
    param
    (
		[Parameter(Mandatory=$True)]
		[string]$extensionBuildTempPath,
        [Parameter(Mandatory=$True)]
		[string]$environment
	)

    $taskLogoOverrideFiles = Get-ChildItem $extensionBuildTempPath -Include "icon.$environment.png" -Recurse
    foreach ($logoOverrideFile in $taskLogoOverrideFiles)
    {
        $directory = Split-Path $logoOverrideFile
        $target = Join-Path $directory "icon.png"
        Write-Host "Replacing task logo $target with $logoOverrideFile..."
        Move-Item $logoOverrideFile $target -Force
    }
    
    Get-ChildItem $extensionBuildTempPath -Include "icon.*.png" -Recurse | Remove-Item -Force
}

function Pack
{
    param
    (
		[Parameter(Mandatory=$True)]
		[string]$extensionName
	)

    Write-Host "Packing $extensionName..."
    $extensionBuildTempPath = Get-ChildItem $buildTempPath -Include $extensionName -Recurse
    Write-Host "Found extension working directory $extensionBuildTempPath"
    
    $overridesFile = UpdateExtensionManifestOverrideFile $extensionBuildTempPath $environment $version
    OverrideExtensionLogo $extensionBuildTempPath $environment
    
    UpdateTaskManifests $extensionBuildTempPath $version
    OverrideTaskLogos $extensionBuildTempPath $environment
    
    Write-Host "Creating VSIX using tfx..."
    & tfx extension create --root $extensionBuildTempPath --manifest-globs extension-manifest.json --overridesFile $overridesFile --outputPath "$buildArtifactsPath\$environment" --no-prompt
}

UpdateTfxCli
PrepareBuildDirectory
CopyCommonTaskItems
$scannerPath = DownloadSonarQubeScanner "https://github.com/SonarSource-VisualStudio/sonar-scanner-msbuild/releases/download/2.2/sonar-scanner-msbuild-2.2.0.24.zip" 
CopyScannerFiles $scannerPath
Pack "SonarQube"