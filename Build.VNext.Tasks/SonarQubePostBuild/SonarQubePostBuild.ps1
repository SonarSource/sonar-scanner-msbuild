Write-Verbose "Starting SonarQube PostBuild Step"


# Import the Task.Common dll that has all the cmdlets 
import-module "Microsoft.TeamFoundation.DistributedTask.Task.Common"


#TODO: can we detect that the pre-build step has run before? 
#TODO: output an md file


$currentDir = (Get-Item -Path ".\" -Verbose).FullName

#TODO: currently there is a copy of the msbuild runner in this task but we could avoid this duplication 
# by passing the path to the msbuild runner via a task variable or an env variable 
$sonarMsBuildRunnerDir = [System.IO.Path]::Combine($currentDir, "SonarQube.MSBuild.Runner-0.9")
$sonarMsBuildRunnerPath = [System.IO.Path]::Combine($sonarMsBuildRunnerDir, "SonarQube.MSBuild.Runner.exe")

if (![System.IO.File]::Exists($sonarMsBuildRunnerPath))
{
	throw "Could not find $sonarMsBuildRunnerPath."
}

Write-Verbose -Verbose "!Executing $sonarMsBuildRunnerPath without arguments"
Invoke-BatchScript $sonarMsBuildRunnerPath 


