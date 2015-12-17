. ./appveyor_helpers.ps1

function PatchCsharpPlugin
{
    Add-AppveyorMessage -Message "Copying the C# plugin for patching"
        
    $csPluginCleanJar = FindSingleFile "$mavenLocalRepository\org\sonarsource\dotnet\sonar-csharp-plugin" "*SNAPSHOT.jar"
   
    $destinationFile = [System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "PackagingProjects\CSharpPluginPayload", "csharp_plugin.jar");
    [System.IO.File]::Copy($csPluginCleanJar, $destinationFile)
}


#Add-AppveyorMessage -Message "Building the latest working C# plugin"
Build "SonarSource/sonar-csharp" "master"

PatchCsharpPlugin

