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

Write-Verbose "Starting SonarQube Pre-Build Setup Step"

Write-Verbose -Verbose "sonarServerUrl = $sonarServerUrl"
Write-Verbose -Verbose "sonarDbConnectionString = $sonarDbUrl"
Write-Verbose -Verbose "sonarDbUsername = $sonarDbUsername"
Write-Verbose -Verbose "sonarDbPassword = $sonarDbPassword"
Write-Verbose -Verbose "SonarProjectKey = $sonarProjectKey"
Write-Verbose -Verbose "SonarProjectName = $sonarProjectName"
Write-Verbose -Verbose "SonarProjectVersion = $sonarProjectVersion"
Write-Verbose -Verbose "SonarPropertiesFile = $sonarProjecopertiesFile"


import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"

. ./SonarQubeHelper.ps1

#
# These variables need to be updated when deploying different versions of sonar-runner / sonarqube.msbuild.runner
#
$currentDir = (Get-Item -Path ".\" -Verbose).FullName
$sonarRunnerDir = [System.IO.Path]::Combine($currentDir, "sonar-runner-dist-2.4", "sonar-runner-2.4", "bin")
$sonarRunnerPath = [System.IO.Path]::Combine($sonarRunnerDir, "sonar-runner.bat")
$sonarMsBuildRunnerDir = [System.IO.Path]::Combine($currentDir, "SonarQube.MSBuild.Runner-0.9")
$sonarMsBuildRunnerPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.MSBuild.Runner.exe")

$targetsFileInitialPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.Integration.ImportBefore.targets")
$propertiesFile = [System.IO.Path]::Combine($sonarRunnerDir, "../", "conf")

if (![System.IO.File]::Exists($sonarMsBuildRunnerPath))
{
	throw "Could not find $sonarMsBuildRunnerPath"
}

if (![System.IO.File]::Exists($sonarRunnerPath))
{
	throw "Could not find $sonarRunnerPath"
}

SetTaskContextVaraible "sonarMsBuildRunnerPath" $sonarMsBuildRunnerPath
CreatePropertiesFile $propertiesFile $sonarServerUrl $sonarDbUrl $sonarDbUsername $sonarDbPassword
CopyTargetsFile $targetsFileInitialPath

#TODO: do we really need this env var and will it create versioning problems? 
if (-Not $env:Path.Contains($sonarRunnerDir))
{
    Write-Verbose -Verbose "PATH is being updated to point at the sonar-runner at $sonarRunnerDir"
    $env:Path = $env:Path + ";" + $sonarRunnerDir
}

#TODO: do we need to set SONAR_RUNNER_OPTS to avoid out of memory issues? Will this work if we set it as a per-process env variable?

Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath with arguments /k:$sonarProjectKey /n:$sonarProjectName /v:$sonarProjectVersion /r:$sonarProjectPropertiesFile" 
Invoke-BatchScript $sonarMsBuildRunnerPath –Arguments "/k:$sonarProjectKey /n:$sonarProjectName /v:$sonarProjectVersion /r:$sonarProjectPropertiesFile"



