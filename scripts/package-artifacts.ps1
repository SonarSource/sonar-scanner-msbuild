function Package-NetFrameworkScanner {
    param (
        [Bool]$SignAssemblies = $false
    )

    $Destination = "$FullBuildOutputDir\sonarscanner-net-framework"
    $DestinationTargets = "$Destination\Targets"
    $DestinationLicenses = "$Destination\licenses"
    $DestinationThirdPartyLicenses = "$DestinationLicenses\THIRD_PARTY_LICENSES"

    $SourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462"

    if (!(Test-Path -Path $Destination)) { New-Item $Destination -Type Directory }
    if (!(Test-Path -Path $DestinationTargets)) { New-Item $DestinationTargets -Type Directory }
    if (!(Test-Path -Path $DestinationLicenses)) { New-Item $DestinationLicenses -Type Directory }
    if (!(Test-Path -Path $DestinationThirdPartyLicenses)) { New-Item $DestinationThirdPartyLicenses -Type Directory }

    Copy-Item -Path "$SourceRoot\Microsoft.CodeCoverage.IO.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\Newtonsoft.Json.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.Common.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.TFS.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.TFSProcessor.exe" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.TFSProcessor.exe.config" -Destination $Destination
    Copy-Item -Path "$SourceRoot\System.Runtime.InteropServices.RuntimeInformation.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\System.ValueTuple.dll" -Destination $Destination
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination $DestinationTargets -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\net462\*" -Exclude "*.pdb" -Destination $Destination -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\net462\SonarScanner.MSBuild.Tasks.dll" -Destination $Destination
    Copy-Item -Path "$PSScriptRoot\..\LICENSE.txt" -Destination $DestinationLicenses
    Copy-Item -Path "$PSScriptRoot\..\Licenses\THIRD_PARTY_LICENSES\*" -Destination $DestinationThirdPartyLicenses

    Expand-Archive -Path "$scannerCliDownloadDir\$scannerCliArtifact" -DestinationPath $Destination -Force

    if ($SignAssemblies) {
        Sign-Assemblies -Pattern "$Destination\Sonar*" -TargetName ".NET Framework assemblies"
    }

    # Don't use Compress-Archive because https://github.com/SonarSource/sonar-scanner-msbuild/issues/2086
    # This is propably fixed in Powershell 7
    tar -c -a -C "$Destination" --options "zip:compression-level=9" -f "$Destination.zip" *
}

function Package-NetScanner {
    param (
        [Bool]$SignAssemblies = $false
    )

    $SourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\netcoreapp3.1"
    $Destination = "$FullBuildOutputDir\sonarscanner-net"
    $DestinationTargets = "$Destination\Targets"
    $DestinationLicenses = "$Destination\licenses"
    $DestinationThirdPartyLicenses = "$DestinationLicenses\THIRD_PARTY_LICENSES"

    if (!(Test-Path -Path $Destination)) { New-Item $Destination -Type Directory }
    if (!(Test-Path -Path $DestinationTargets)) { New-Item $DestinationTargets -Type Directory }
    if (!(Test-Path -Path $DestinationLicenses)) { New-Item $DestinationLicenses -Type Directory }
    if (!(Test-Path -Path $DestinationThirdPartyLicenses)) { New-Item $DestinationThirdPartyLicenses -Type Directory }

    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination $DestinationTargets -Recurse
    Copy-Item -Path "$SourceRoot\SonarQube.Analysis.xml" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.Common.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.PostProcessor.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.PreProcessor.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.runtimeconfig.json" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\SonarScanner.MSBuild.TFS.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\Google.Protobuf.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\Newtonsoft.Json.dll" -Destination $Destination
    Copy-Item -Path "$SourceRoot\ICSharpCode.SharpZipLib.dll" -Destination $Destination
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\netstandard2.0\SonarScanner.MSBuild.Tasks.dll" -Destination $Destination
    Copy-Item -Path "$PSScriptRoot\..\LICENSE.txt" -Destination $DestinationLicenses
    Copy-Item -Path "$PSScriptRoot\..\Licenses\THIRD_PARTY_LICENSES\Google.Protobuf-LICENSE.txt" -Destination $DestinationThirdPartyLicenses
    Copy-Item -Path "$PSScriptRoot\..\Licenses\THIRD_PARTY_LICENSES\Newtonsoft.Json-LICENSE.txt" -Destination $DestinationThirdPartyLicenses
    Copy-Item -Path "$PSScriptRoot\..\Licenses\THIRD_PARTY_LICENSES\SharpZipLib-LICENSE.txt" -Destination $DestinationThirdPartyLicenses

    Expand-Archive -Path "$scannerCliDownloadDir\$scannerCliArtifact" -DestinationPath $Destination -Force

    if ($SignAssemblies) {
        Sign-Assemblies -Pattern "$Destination\Sonar*" -TargetName ".NET assemblies"
    }

    # Don't use Compress-Archive because https://github.com/SonarSource/sonar-scanner-msbuild/issues/2086
    # This is propably fixed in Powershell 7
    tar -c -a -C "$Destination" --options "zip:compression-level=9" -f "$Destination.zip" *
}

function Sign-Assemblies {
    param (
        [string]$Pattern,
        [string]$TargetName
    )
    Write-Host "Signing $TargetName"
    Get-ChildItem -Path $Pattern -Include @("*.dll","*.exe") |
        Foreach-Object {
            & signtool sign /du https://www.sonarsource.com/ /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 /csp "DigiCert Signing Manager KSP" /kc "$env:SM_KP" /f "$env:SM_CLIENT_CRT_FILE" $_.FullName
        }
    Write-Host "[Completed] Signing $TargetName"
}

function Download-ScannerCli {
    $ArtifactoryUrlEnv = "ARTIFACTORY_URL"

    $ArtifactoryUrl = [environment]::GetEnvironmentVariable($ArtifactoryUrlEnv, "Process")
    if (!$ArtifactoryUrl) {
        Write-Host "Could not find ARTIFACTORY_URL variable, defaulting to repox URL.";
        $ArtifactoryUrl = "https://repox.jfrog.io/repox";
    }

    if (!(Test-Path -LiteralPath $scannerCliDownloadDir)) {
        New-Item -Path $scannerCliDownloadDir -ItemType Directory -ErrorAction Stop -Force
    }

    mvn org.apache.maven.plugins:maven-dependency-plugin:3.2.0:get -DremoteRepositories=$ArtifactoryUrl -Dartifact="org.sonarsource.scanner.cli:sonar-scanner-cli:${scannerCliVersion}:zip" -Dtransitive=false

    mvn org.apache.maven.plugins:maven-dependency-plugin:3.2.0:copy -Dartifact="org.sonarsource.scanner.cli:sonar-scanner-cli:${scannerCliVersion}:zip" -DoutputDirectory="${scannerCliDownloadDir}"
}