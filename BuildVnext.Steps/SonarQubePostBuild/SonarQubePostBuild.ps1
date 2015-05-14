Write-Verbose "Starting SonarQube PostBuild Step"

# Import the Task.Common dll that has all the cmdlets 
import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"

#TODO: move from using env variables to using the VSO property bag
$sonarServerUrl = $env:sonarServerUrl 

Write-Verbose -Verbose "sonarServerUrl = $sonarServerUrl"

if (!$sonarServerUrl)
{
	throw "The SonarQube Server Url could be found. Does your build definition have a SonarQube Pre-Build step?"
}

# TODO: output an md file

# TODO: currently there is a copy of the msbuild runner in this task but we could avoid this duplication 
# by passing the path to the msbuild runner via a task variable or an env variable 
$currentDir = (Get-Item -Path ".\" -Verbose).FullName
$sonarMsBuildRunnerDir = [System.IO.Path]::Combine($currentDir, "SonarQube.MSBuild.Runner-0.9")
$sonarMsBuildRunnerPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.MSBuild.Runner.exe")

if (![System.IO.File]::Exists($sonarMsBuildRunnerPath))
{
	throw "Internal Error. Could not find $sonarMsBuildRunnerPath"
}

Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath without arguments"
Invoke-BatchScript $sonarMsBuildRunnerPath 


