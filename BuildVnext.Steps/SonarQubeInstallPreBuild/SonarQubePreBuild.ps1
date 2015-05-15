[CmdletBinding(DefaultParameterSetName = 'None')]
param(
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $sonarProjectKey,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $sonarProjectName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $sonarProjectVersion,
    [string]$sonarProjectPropertiesFile,  #TODO: do we really need this ? In any case we would use a checked in file;
	[string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()]$sonarServerUrl, 
	[string]$sonarDbUrl,
	[string]$sonarDbUsername,
	[string]$sonarDbPassword
)

. ./SonarQubeHelper.ps1

Write-Verbose "Starting SonarQube Pre-Build Setup Step"

Write-Verbose -Verbose "sonarServerUrl = $sonarServerUrl"
Write-Verbose -Verbose "sonarDbConnectionString = $sonarDbUrl"
Write-Verbose -Verbose "sonarDbUsername = $sonarDbUsername"
Write-Verbose -Verbose "sonarDbPassword = $sonarDbPassword"
Write-Verbose -Verbose "SonarProjectKey = $sonarProjectKey"
Write-Verbose -Verbose "SonarProjectName = $sonarProjectName"
Write-Verbose -Verbose "SonarProjectVersion = $sonarProjectVersion"
Write-Verbose -Verbose "SonarPropertiesFile = $sonarProjectPropertiesFile"

#TODO: the mechanism for passing around variables is not working so using process env variables instead
$env:sonarServerUrl = $sonarServerUrl

# Import the Task.Common dll that has the Invoke-BatchScript cmdlet
import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"

#TODO: discuss about eliminating the version numbers in the dir names
$currentDir = (Get-Item -Path ".\" -Verbose).FullName
$sonarRunnerDir = [System.IO.Path]::Combine($currentDir, "sonar-runner-dist-2.4", "sonar-runner-2.4")
$sonarRunnerBinDir = [System.IO.Path]::Combine($sonarRunnerDir, "bin")
$sonarRunnerConfDir = [System.IO.Path]::Combine($sonarRunnerDir, "conf")
$sonarRunnerPath = [System.IO.Path]::Combine($sonarRunnerBinDir, "sonar-runner.bat")
$sonarMsBuildRunnerDir = [System.IO.Path]::Combine($currentDir, "SonarQube.MSBuild.Runner-0.9")
$sonarMsBuildRunnerPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.MSBuild.Runner.exe")
$targetsFileInitialPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.Integration.ImportBefore.targets")


if (![System.IO.File]::Exists($sonarMsBuildRunnerPath))
{
	throw "Could not find the msbuild runner: $sonarMsBuildRunnerPath"
}

if (![System.IO.File]::Exists($sonarRunnerPath))
{
	throw "Could not find the sonar-runner: $sonarRunnerPath"
}

CreatePropertiesFile $sonarRunnerConfDir $sonarServerUrl $sonarDbUrl $sonarDbUsername $sonarDbPassword


#TODO: need to intercept the msbuild tools version or ask for it from the user or figure out a solution for all cases
# or as an alternative use CustomBeforeMicrosoftCSharpTargets property which we can pass to msbuild which does not seem to be tied to 14.0
$msbuildToolVersion = "14.0" 
$localAppDataDir = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)

# msbuild will load targets files from some well known locations, namely "$(MSBuildUserExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.targets\ImportBefore\*" 
# and the MSBuildUserExtensionsPath property is %localAppData%/Microsoft/MSBuild 

#TODO: only tested for msbuild 14.0
$targetsFileDestinationPath = [System.IO.Path]::Combine($localAppDataDir, "Microsoft", "MSBuild", $msbuildToolVersion, "Microsoft.Common.targets", "ImportBefore", "SonarQube.Integration.ImportBefore.targets")

Write-Verbose -Verbose "Using the SonarQube.MSBuild runner at $sonarMsBuildRunnerPath"

#TODO: do we really need this env var and will it create versioning problems? 
if (-Not $env:Path.Contains($sonarRunnerDir))
{
    Write-Verbose -Verbose "PATH is being updated to point at the sonar-runner at $sonarRunnerDir"
    $env:Path = $env:Path + ";" + $sonarRunnerBinDir
}

#TODO: do we need to set SONAR_RUNNER_OPTS to avoid out of memory issues? Will this work if we set it as a per-process env variable?

Write-Verbose -Verbose "Copying the targets file to $targetsFileDestinationPath"
# in case the directory doesn't exist create it
New-Item -ItemType File -Path $targetsFileDestinationPath -Force
Copy-Item $targetsFileInitialPath $targetsFileDestinationPath -force

#TODO: defensive programming: kill any instance of the runner before / after running it
Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath with arguments /k:$sonarProjectKey /n:$sonarProjectName /v:$sonarProjectVersion /r:$sonarProjectPropertiesFile" 
Invoke-BatchScript $sonarMsBuildRunnerPath –Arguments "/k:$sonarProjectKey /n:$sonarProjectName /v:$sonarProjectVersion /r:$sonarProjectPropertiesFile"

