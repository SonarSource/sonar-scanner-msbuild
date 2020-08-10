function Package-Net46Scanner(){
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-net46")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-net46" -Type Directory}
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets" -Type Directory}
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\Microsoft.VisualStudio.Setup.Configuration.Interop.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\Newtonsoft.Json.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\SonarScanner.MSBuild.Common.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\SonarScanner.MSBuild.Shim.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\SonarScanner.MSBuild.TFS.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\SonarScanner.MSBuild.TFSProcessor.exe" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\SonarScanner.MSBuild.TFSProcessor.exe.config" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\System.Runtime.InteropServices.RuntimeInformation.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net46\System.ValueTuple.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46\Targets" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\net46\*" -Exclude "*.pdb" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\net46\SonarScanner.MSBuild.Tasks.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-net46"
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$scannerCliDownloadDir\$scannerCliArtifact", "$fullBuildOutputDir\sonarscanner-msbuild-net46")
    Compress-Archive -Path $fullBuildOutputDir\sonarscanner-msbuild-net46\* -DestinationPath $fullBuildOutputDir\sonarscanner-msbuild-net46.zip -Force
}

function Package-NetScanner([string]$tfm)
{
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-$tfm")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-$tfm" -Type Directory}
    if (!(Test-Path -path "$fullBuildOutputDir\sonarscanner-msbuild-$tfm\Targets")) {New-Item "$fullBuildOutputDir\sonarscanner-msbuild-$tfm\Targets" -Type Directory}
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm\Targets" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarQube.Analysis.xml" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.Common.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.PostProcessor.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.PreProcessor.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.runtimeconfig.json" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$tfm\SonarScanner.MSBuild.Shim.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\netcoreapp3.0\Newtonsoft.Json.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\netstandard2.0\SonarScanner.MSBuild.Tasks.dll" -Destination "$fullBuildOutputDir\sonarscanner-msbuild-$tfm"
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$scannerCliDownloadDir\$scannerCliArtifact", "$fullBuildOutputDir\sonarscanner-msbuild-$tfm")
    Compress-Archive -Path $fullBuildOutputDir\sonarscanner-msbuild-$tfm\* -DestinationPath $fullBuildOutputDir\sonarscanner-msbuild-$tfm.zip -Force
}
function Download-ScannerCli() {
    $artifactoryUrlEnv = "ARTIFACTORY_URL"
    
    $artifactoryUrl = [environment]::GetEnvironmentVariable($artifactoryUrlEnv, "Process")
    if (!$artifactoryUrl) {
        Write-Host "Could not find ARTIFACTORY_URL variable, defaulting to repox URL.";
        $artifactoryUrl = "https://repox.jfrog.io/repox";
    }

    $scannerCliUrl = $artifactoryUrl + "/sonarsource-public-releases/org/sonarsource/scanner/cli/sonar-scanner-cli/$scannerCliVersion/$scannerCliArtifact";

    if (!(Test-Path -LiteralPath $scannerCliDownloadDir)) {
        New-Item -Path $scannerCliDownloadDir -ItemType Directory -ErrorAction Stop -Force
    }

    if (!(Test-Path -LiteralPath $scannerCliDownloadDir\$scannerCliArtifact)) {
        Invoke-WebRequest -Uri $scannerCliUrl -OutFile $scannerCliDownloadDir\$scannerCliArtifact
    }
}