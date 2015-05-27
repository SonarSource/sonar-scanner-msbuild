Write-Verbose "Starting SonarQube PostBuild Step"

import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"

$sonarMsBuildRunnerPath = Get-Variable $distributedTaskContext "sonarMsBuildRunnerPath"
Write-Verbose -Verbose "sonarMsBuildRunnerPath = $sonarMsBuildRunnerPath"

if (!$sonarMsBuildRunnerPath -or ![System.IO.File]::Exists($sonarMsBuildRunnerPath))
{
	throw "The SonarQube MsBuild Runner executable could not be found. Does your build definition include the SonarQube Pre-Build step?"
}

Write-Verbose -Verbose "Executing $sonarMsBuildRunnerPath without arguments"
Invoke-BatchScript $sonarMsBuildRunnerPath 


