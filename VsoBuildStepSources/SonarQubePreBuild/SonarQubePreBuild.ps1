[CmdletBinding(DefaultParameterSetName = 'None')]
param(
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectKey,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectVersion,
	[string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()]$serverUrl, 
	[string]$serverUsername,
	[string]$serverPassword,
	[string]$dbUrl,
	[string]$dbUsername,
	[string]$dbPassword
)

Write-Verbose "Starting SonarQube Pre-Build Setup Step"

Write-Verbose -Verbose "serverUrl = $serverUrl"
Write-Verbose -Verbose "dbConnectionString = $dbUrl"
Write-Verbose -Verbose "dbUsername = $dbUsername"
Write-Verbose -Verbose "dbPassword = $dbPassword" 
Write-Verbose -Verbose "projectKey = $projectKey"
Write-Verbose -Verbose "projectName = $projectName"
Write-Verbose -Verbose "dbUsername = $dbUsername"
Write-Verbose -Verbose "dbPassword = $dbPassword" 

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
CreatePropertiesFile $propertiesFile $serverUrl $serverUsername $serverPassword $dbUrl $dbUsername $dbPassword
CopyTargetsFile $targetsFileInitialPath

#TODO: remove this env variable by passing it in directly to the msbuild runner 
if (-Not $env:Path.Contains($sonarRunnerDir))
{
    Write-Verbose -Verbose "PATH is being updated to point at the sonar-runner at $sonarRunnerDir"
    $env:Path = $env:Path + ";" + $sonarRunnerDir
}

#TODO: do we need to set SONAR_RUNNER_OPTS to avoid out of memory issues? Will this work if we set it as a per-process env variable?

Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath with arguments /k:$projectKey /n:$projectName /v:$projectVersion /r:$projectPropertiesFile" 
Invoke-BatchScript $sonarMsBuildRunnerPath –Arguments "/k:$projectKey /n:$projectName /v:$projectVersion /r:$projectPropertiesFile"



