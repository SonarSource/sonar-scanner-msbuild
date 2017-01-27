$ErrorActionPreference = "Stop"

function testExitCode(){
    If($LASTEXITCODE -ne 0) {
        write-host -f green "lastexitcode: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

if ($env:IS_PULLREQUEST -eq "true") { 
    write-host -f green "in a pull request"
    
    #download MSBuild
    $url = "https://github.com/SonarSource-VisualStudio/sonar-msbuild-runner/releases/download/2.0/MSBuild.SonarQube.Runner-2.0.zip"
    $output = ".\MSBuild.SonarQube.Runner.zip"    
    Invoke-WebRequest -Uri $url -OutFile $output
    unzip -o .\MSBuild.SonarQube.Runner.zip
    testExitCode

    .\MSBuild.SonarQube.Runner begin /k:sonar-scanner-msbuild /n:"SonarQube Scanner for MSBuild" /v:latest `
        /d:sonar.host.url=$env:SONAR_HOST_URL `
        /d:sonar.login=$env:SONAR_TOKEN `
        /d:sonar.github.pullRequest=$env:PULL_REQUEST `
        /d:sonar.github.repository=$env:GITHUB_REPO `
        /d:sonar.github.oauth=$env:GITHUB_TOKEN `
        /d:sonar.analysis.mode=issues `
        /d:sonar.scanAllFiles=true
    testExitCode

    & $env:NUGET_PATH restore SonarQube.Scanner.MSBuild.sln
    testExitCode
    & $env:MSBUILD_PATH SonarQube.Scanner.MSBuild.sln /t:rebuild /p:Configuration=Release
    testExitCode

    .\MSBuild.SonarQube.Runner end /d:sonar.login=$env:SONAR_TOKEN
    testExitCode

} else {
    if (($env:GITHUB_BRANCH -eq "master") -or ($env:GITHUB_BRANCH -eq "refs/heads/master")) {
        write-host -f green "Building master branch"

        #generate build version from the build number
        $buildversion="$env:BUILD_NUMBER"

        $branchName = "$env:GITHUB_BRANCH"
        $sha1 = "$env:GIT_SHA1"

        #Append build number to the versions
        (Get-Content .\build\Version.props) -replace '<AssemblyFileVersion>\$\(MainVersion\)\.0</AssemblyFileVersion>', "<AssemblyFileVersion>`$(MainVersion).$buildversion</AssemblyFileVersion>" | Set-Content .\build\Version.props
        (Get-Content .\build\Version.props) -replace '<AssemblyInformationalVersion>Version:\$\(AssemblyFileVersion\) Branch:not-set Sha1:not-set</AssemblyInformationalVersion>', "<AssemblyInformationalVersion>Version:`$(AssemblyFileVersion) Branch:$branchName Sha1:$sha1</AssemblyInformationalVersion>" | Set-Content .\build\Version.props
        & $env:MSBUILD_PATH  .\build\ChangeVersion.proj
        testExitCode

        #build
        & $env:NUGET_PATH restore SonarQube.Scanner.MSBuild.sln
        testExitCode
        & $env:MSBUILD_PATH SonarQube.Scanner.MSBuild.sln /p:configuration=Release /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=$env:CERT_PATH
        testExitCode

        #get version number
        [xml]$versionProps = Get-Content .\build\Version.props
        $version  = $versionProps.Project.PropertyGroup.MainVersion+".$buildversion"
        write-host -f green "version: $version"

        $implZipPath    = Get-Item .\DeploymentArtifacts\CSharpPluginPayload\Release\SonarQube.MSBuild.Runner.Implementation.zip
        $scannerZipPath = Get-Item .\DeploymentArtifacts\BuildAgentPayload\Release\SonarQube.Scanner.MSBuild.zip
        
        #DeployOnRepox $scannerZipPath "" $version
        write-host -f green  "replace zip filenames in pom.xml"
        (Get-Content .\pom.xml) -replace 'implZipPath', "$implZipPath" | Set-Content .\pom.xml
        (Get-Content .\pom.xml) -replace 'scannerZipPath', "$scannerZipPath" | Set-Content .\pom.xml
            
        write-host -f green  "set version $version in pom.xml"
        $command = "mvn versions:set -DgenerateBackupPoms=false -DnewVersion='$version'"
        iex $command
        write-host -f green  "set version $version in env VAR PROJECT_VERSION for artifactory buildinfo metadata"
        $env:PROJECT_VERSION=$version
        write-host -f green  "set the buildnumber to this job build number"
        $env:BUILD_ID=$env:BUILD_NUMBER
        write-host -f green  "Deploy to repox with $version"    
        $command = 'mvn deploy -Pdeploy-sonarsource -B -e -V'
        iex $command

        #create empty file to trigger qa
        new-item -path . -name qa.properties -type "file"
		
    } else {
        write-host -f green "not on master"
    }

}



