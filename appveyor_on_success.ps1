. ./appveyor_helpers.ps1

function DeployOnRepox()
{
    param ([Parameter(Mandatory=$true)][string]$file, [Parameter(Mandatory=$false)][string]$classifier, [Parameter(Mandatory=$true)][string]$version)

    echo "Deploy $file on repox with version $version"

    $command = 'mvn --% --batch-mode --quiet --settings "maven-settings.xml" "org.apache.maven.plugins:maven-deploy-plugin:2.8.2:deploy-file" ' +
        '-DrepositoryId=releases ' +
        '-Durl=' + $env:ARTIFACTORY_URL + '/' + $env:ARTIFACTORY_DEPLOY_REPO + ';buildNumber=' + $env:APPVEYOR_BUILD_NUMBER + ' ' +
        '"-DgroupId=org.sonarsource.scanner.msbuild" ' +
        '"-DartifactId=sonar-scanner-msbuild" ' +
        '-Dclassifier=' + $classifier + ' ' +
        '-Dpackaging=zip ' + 
        '-Dversion=' + $version + ' ' + 
        '-Dfile=' + $file
    iex $command
    CheckLastExitCode
}



    if ($env:APPVEYOR_REPO_BRANCH -eq "master")
    {
        $strPath = FindSingleFile ([System.IO.Path]::Combine($PSScriptRoot, "DeploymentArtifacts", "BuildAgentPayload", "Release")) "SonarQube.Scanner.MSBuild.exe"
        $Assembly = [Reflection.Assembly]::Loadfile($strPath)

        $AssemblyName = $Assembly.GetName()
        $Assemblyversion = $AssemblyName.version.ToString()

        $FinalVersion = $Assemblyversion + '-build' + $env:APPVEYOR_BUILD_NUMBER

        # Upload artifacts on repox
        $implZipPath = FindSingleFile ([System.IO.Path]::Combine($PSScriptRoot, "DeploymentArtifacts", "CSharpPluginPayload", "Release")) "SonarQube.MSBuild.Runner.Implementation.zip"
        DeployOnRepox $implZipPath "impl" $FinalVersion
        $scannerZipPath = FindSingleFile ([System.IO.Path]::Combine($PSScriptRoot, "DeploymentArtifacts", "BuildAgentPayload", "Release")) "SonarQube.Scanner.MSBuild.zip"
        DeployOnRepox $scannerZipPath "" $FinalVersion
    }

