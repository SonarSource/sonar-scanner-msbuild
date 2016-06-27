. ./appveyor_helpers.ps1

if (IsPRCABuild) 
{
	MSBuild.SonarQube.Runner.exe end
}
