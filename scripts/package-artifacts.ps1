function Package-NetFrameworkScanner {
    param (
        [Bool]$SignAssemblies = $false
    )

    $destination = "$fullBuildOutputDir\sonarscanner-net-framework"
    $destinationTargets = "$destination\Targets"
    $sourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild.TFS.Classic\bin\Release\net462"

    if (!(Test-Path -Path $destination)) { New-Item $destination -Type Directory }
    if (!(Test-Path -Path $destinationTargets)) { New-Item $destinationTargets -Type Directory }

    Copy-Item -Path "$sourceRoot\Microsoft.CodeCoverage.IO.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\Newtonsoft.Json.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Common.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFS.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFSProcessor.exe" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.TFSProcessor.exe.config" -Destination $destination
    Copy-Item -Path "$sourceRoot\System.Runtime.InteropServices.RuntimeInformation.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\System.ValueTuple.dll" -Destination $destination
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination $destinationTargets -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\net462\*" -Exclude "*.pdb" -Destination $destination -Recurse
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\net462\SonarScanner.MSBuild.Tasks.dll" -Destination $destination

    Expand-Archive -Path "$scannerCliDownloadDir\$scannerCliArtifact" -DestinationPath $destination -Force

    if ($SignAssemblies) {
        Sign-Assemblies -Pattern "$destination\Sonar*" -TargetName ".NET Framework assemblies"
    }

    Create-Archive $destination
}

function Package-NetScanner {
    param (
        [Bool]$SignAssemblies = $false
    )

    $sourceRoot = "$PSScriptRoot\..\src\SonarScanner.MSBuild\bin\Release\netcoreapp3.1"
    $destination = "$fullBuildOutputDir\sonarscanner-net"
    $destinationTargets = "$destination\Targets"

    if (!(Test-Path -Path $destination)) { New-Item $destination -Type Directory }
    if (!(Test-Path -Path $destinationTargets)) { New-Item $destinationTargets -Type Directory }

    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\Targets\*" -Destination $destinationTargets -Recurse
    Copy-Item -Path "$sourceRoot\SonarQube.Analysis.xml" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Common.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.PostProcessor.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.PreProcessor.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.runtimeconfig.json" -Destination $destination
    Copy-Item -Path "$sourceRoot\SonarScanner.MSBuild.Shim.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\Google.Protobuf.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\Newtonsoft.Json.dll" -Destination $destination
    Copy-Item -Path "$sourceRoot\ICSharpCode.SharpZipLib.dll" -Destination $destination
    Copy-Item -Path "$PSScriptRoot\..\src\SonarScanner.MSBuild.Tasks\bin\Release\netstandard2.0\SonarScanner.MSBuild.Tasks.dll" -Destination $destination

    Expand-Archive -Path "$scannerCliDownloadDir\$scannerCliArtifact" -DestinationPath $destination -Force

    if ($SignAssemblies) {
        Sign-Assemblies -Pattern "$destination\Sonar*" -TargetName ".NET assemblies"
    }
    
    Create-Archive $destination
}

function Create-Archive {
    param (
        [string]$source
    )

    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Host "Creating zip archive for $source using Compress-Archive"
        Compress-Archive -Path "$source\*" -DestinationPath "$source.zip" -Force
    } elseif ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        Write-Host "Creating zip archive for $source using tar.exe"
        # Don't use Compress-Archive because https://github.com/SonarSource/sonar-scanner-msbuild/issues/2086
        # This is propably fixed in Powershell 7
        tar -c -a -C "$source" --options "zip:compression-level=9" -f "$source" *
    } else {
        throw "Creating zip archive is not supported on this platform $([System.Environment]::OSVersion.Platform) with Powershell version $($PSVersionTable.PSVersion)"
    }
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
