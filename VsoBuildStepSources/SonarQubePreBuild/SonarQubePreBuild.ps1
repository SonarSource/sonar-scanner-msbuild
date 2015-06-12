[CmdletBinding(DefaultParameterSetName = 'None')]
param(
	[string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $connectedServiceName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectKey,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectVersion,	
	[string]$dbUrl,
	[string]$dbUsername,
	[string]$dbPassword
)

Write-Verbose "Starting SonarQube Pre-Build Setup Step"

Write-Verbose -Verbose "dbConnectionString = $dbUrl"
Write-Verbose -Verbose "dbUsername = $dbUsername"
Write-Verbose -Verbose "projectKey = $projectKey"
Write-Verbose -Verbose "projectName = $projectName"
Write-Verbose -Verbose "connectedServiceName = $connectedServiceName"

import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"

. ./SonarQubeHelper.ps1

$serviceEndpoint = GetEndpointData $connectedServiceName

Write-Verbose -Verbose "serverUrl = $($serviceEndpoint.Url)"
Write-Verbose -Verbose "serverUsername = $($serviceEndpoint.Authorization.Parameters.UserName)"

#
# These variables need to be updated when deploying different versions of sonar-runner / sonarqube.msbuild.runner
#
$currentDir = (Get-Item -Path ".\" -Verbose).FullName
$sonarRunnerDir = [System.IO.Path]::Combine($currentDir, "sonar-runner-dist-2.4", "sonar-runner-2.4", "bin")
$sonarRunnerPath = [System.IO.Path]::Combine($sonarRunnerDir, "sonar-runner.bat")
$sonarMsBuildRunnerDir = [System.IO.Path]::Combine($currentDir, "SonarQube.MSBuild.Runner-0.9")
$sonarMsBuildRunnerPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.MSBuild.Runner.exe")

$targetsFileInitialPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.Integration.ImportBefore.targets")
$propertiesFileDir = [System.IO.Path]::Combine($sonarRunnerDir, "..\", "conf")

if (![System.IO.File]::Exists($sonarRunnerPath))
{
	throw "Could not find $sonarRunnerPath"
}

SetTaskContextVaraible "sonarMsBuildRunnerPath" $sonarMsBuildRunnerPath
CreatePropertiesFile $propertiesFileDir $serviceEndpoint.Url $serviceEndpoint.Authorization.Parameters.UserName $serviceEndpoint.Authorization.Parameters.Password $dbUrl $dbUsername $dbPassword
CopyTargetsFile $targetsFileInitialPath

SetSonarOptsEnvVarIfNeeded

#TODO: remove this env variable by passing it in directly to the msbuild runner 
UpdatePathIfNeeded $sonarRunnerDir

$arguments = [System.String]::Format("/k:""{0}"" /n:""{1}"" /v:""{2}""", $projectKey, $projectName, $projectVersion)
Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath with arguments $arguments"
Invoke-BatchScript $sonarMsBuildRunnerPath –Arguments $arguments







