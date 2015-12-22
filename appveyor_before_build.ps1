. ./appveyor_helpers.ps1

#
# Copies the C# plugin so that the SonarQube MSBuild Scanner packaging projects will patch it. The packaging project is invoked by Appveyor as part of the regular build.
#
function CopyCsharpPluginForPatching
{
    Add-AppveyorMessage -Message "Copying the C# plugin for patching"
        
    $csPluginCleanJar = FindSingleFile "$mavenLocalRepository\org\sonarsource\dotnet\sonar-csharp-plugin" "*SNAPSHOT.jar"
   
    $destinationFile = [System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "PackagingProjects\CSharpPluginPayload", "csharp_plugin.jar");
    [System.IO.File]::Copy($csPluginCleanJar, $destinationFile)
}


echo ("BLA... " + $bla)
if (!$bla)
{
    echo "hi"
    Set-AppveyorBuildVariable -Name "bla" -Value "set"
}

exit
Add-AppveyorMessage -Message "Building the latest working C# plugin"
DownloadAndBuildFromGitHub "SonarSource/sonar-csharp" "master"

CopyCsharpPluginForPatching

