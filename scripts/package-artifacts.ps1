function Package-NetFramework() {
    $destination = "$fullBuildOutputDir\sonarscanner-net-framework"
    $destinationTargets = "$destination\Targets"
    $sourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462"

    if (!(Test-Path -path $destination)) {New-Item $destination -Type Directory}
    if (!(Test-Path -path "$destinationTargets")) {New-Item "$destinationTargets" -Type Directory}

    Copy-Item -Path "$sourceRoot\Microsoft.CodeCoverage.IO.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\Newtonsoft.Json.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Common.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFS.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFSProcessor.exe" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFSProcessor.exe.config" -Destination $destination
    Copy-Item -Path "$sourceRoot\System.Runtime.InteropServices.RuntimeInformation.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\System.ValueTuple.dll" -Destination $destination
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$destinationTargets" -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\net462\*" -Exclude "*.pdb" -Destination $destination -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\net462\SonarScanner.MSBuild.Tasks.dll" -Destination $destination
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$scannerCliDownloadDir\$scannerCliArtifact", $destination)
    Compress-Archive -Path "$destination\*" -DestinationPath "$destination.zip" -Force
}

function Package-NetScanner() {
    $sourcetfm = "netcoreapp3.1"
    $sourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\$sourcetfm"
    $destination = "$fullBuildOutputDir\sonarscanner-net"
    $destinationTargets = "$destination\Targets"

    if (!(Test-Path -path "$destination")) {New-Item "$destination" -Type Directory}
    if (!(Test-Path -path "$destinationTargets")) {New-Item "$destinationTargets" -Type Directory}
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination "$destinationTargets" -Recurse
    Copy-Item -Path "$sourceRoot\SonarQube.Analysis.xml" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Common.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.PostProcessor.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.PreProcessor.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.runtimeconfig.json" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\Google.Protobuf.dll" -Destination "$destination"
    Copy-Item -Path "$sourceRoot\Newtonsoft.Json.dll" -Destination "$destination"
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\netstandard2.0\SonarScanner.MSBuild.Tasks.dll" -Destination "$destination"
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$scannerCliDownloadDir\$scannerCliArtifact", "$destination")
    Compress-Archive -Path "$destination\*" -DestinationPath "$destination.zip" -Force
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