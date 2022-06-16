function Package-Net46Scanner(){
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-net46")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-net46" -Type Directory}
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets" -Type Directory}
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\Microsoft.VisualStudio.Setup.Configuration.Interop.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\Newtonsoft.Json.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\SonarScanner.MSBuild.Common.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\SonarScanner.MSBuild.Shim.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\SonarScanner.MSBuild.TFS.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\SonarScanner.MSBuild.TFSProcessor.exe" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\SonarScanner.MSBuild.TFSProcessor.exe.config" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\System.Runtime.InteropServices.RuntimeInformation.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462\System.ValueTuple.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\net462\*" -Exclude "*.pdb" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\net462\SonarScanner.MSBuild.Tasks.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$scannerCliDownloadDir\$scannerCliArtifact", "$fullBuildOutputDir\sonarscanner-msbuild-net46")
    Compress-Archive -Path $fullBuildOutputDir\sonarscanner-msbuild-net46\* -DestinationPath $fullBuildOutputDir\sonarscanner-msbuild-net46.zip -Force
}

function Package-NetScanner([string]$sourcetfm, [string]$targettfm)
{
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm" -Type Directory}
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm\Targets")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm\Targets" -Type Directory}
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm\Targets" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarQube.Analysis.xml" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.Common.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.PostProcessor.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.PreProcessor.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.runtimeconfig.json" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm\SonarScanner.MSBuild.Shim.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\netcoreapp3.1\Newtonsoft.Json.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\netstandard2.0\SonarScanner.MSBuild.Tasks.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Copy-Item -Path "$scannerCliDownloadDir\$scannerCliArtifact" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$targettfm"
    Compress-Archive -Path $fullBuildOutputDir\sonarscanner-msbuild-$targettfm\* -DestinationPath $fullBuildOutputDir\sonarscanner-msbuild-$targettfm.zip -Force
}
function Download-ScannerCli() {
    $artifactoryUrlEnv = "ARTIFACTORY_URL"
    
    $artifactoryUrl = [environment]::GetEnvironmentVariable($artifactoryUrlEnv, "Process")
    if (!$artifactoryUrl) {
        Write-Host "Could not find ARTIFACTORY_URL variable, defaulting to repox URL.";
        $artifactoryUrl = "https://repox.jfrog.io/repox";
    }

    if (!(Test-Path -LiteralPath $scannerCliDownloadDir)) {
        New-Item -Path $scannerCliDownloadDir -ItemType Directory -ErrorAction Stop -Force
    }

    mvn org.apache.maven.plugins:maven-dependency-plugin:3.2.0:get -DremoteRepositories=$artifactoryUrl -Dartifact="org.sonarsource.scanner.cli:sonar-scanner-cli:${scannerCliVersion}:zip" -Dtransitive=false

    mvn org.apache.maven.plugins:maven-dependency-plugin:3.2.0:copy -Dartifact="org.sonarsource.scanner.cli:sonar-scanner-cli:${scannerCliVersion}:zip" -DoutputDirectory="${scannerCliDownloadDir}"
}