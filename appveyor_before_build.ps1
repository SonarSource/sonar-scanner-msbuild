. .\appveyor_helpers.ps1

#
# Copies the C# plugin so that the SonarQube MSBuild Scanner packaging projects will patch it. The packaging project is invoked by Appveyor as part of the regular build.
#
function CopyCsharpPluginForPatching
{
    Add-AppveyorMessage -Message "Copying the C# plugin for patching"
        
    $csPluginCleanJar = FindSingleFile "$snapshotDirectory\sonar-csharp-master\target\" "*.jar"
   
    $destinationFile = [System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "PackagingProjects\CSharpPluginPayload", "csharp_plugin.jar");
    [System.IO.File]::Copy($csPluginCleanJar, $destinationFile)
}

if (!$env:APPVEYOR)
{    
    $LOCAL_DEBUG_RUN = 1
}

Add-AppveyorMessage -Message "Building the latest working C# plugin"
DownloadAndMavenBuildFromGitHub "SonarSource/sonar-csharp" "master"

CopyCsharpPluginForPatching

