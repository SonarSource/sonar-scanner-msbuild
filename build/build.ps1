$ErrorActionPreference = "Stop"

function testExitCode(){
    If($LASTEXITCODE -ne 0) {
        write-host -f green "lastexitcode: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

#download MSBuild
$url = "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/3.0.2.656/sonar-scanner-msbuild-3.0.2.656.zip"
$output = ".\sonar-scanner-msbuild.zip"    
Invoke-WebRequest -Uri $url -OutFile $output
unzip -o .\sonar-scanner-msbuild.zip
testExitCode

#generate build version from the build number
$buildversion="$env:BUILD_NUMBER"
$branchName = "$env:GITHUB_BRANCH"
$sha1 = "$env:GIT_SHA1"
#Append build number to the versions
(Get-Content .\build\Version.props) -replace '<AssemblyFileVersion>\$\(MainVersion\)\.0</AssemblyFileVersion>', "<AssemblyFileVersion>`$(MainVersion).$buildversion</AssemblyFileVersion>" | Set-Content .\build\Version.props
(Get-Content .\build\Version.props) -replace '<AssemblyInformationalVersion>Version:\$\(AssemblyFileVersion\) Branch:not-set Sha1:not-set</AssemblyInformationalVersion>', "<AssemblyInformationalVersion>Version:`$(AssemblyFileVersion) Branch:$branchName Sha1:$sha1</AssemblyInformationalVersion>" | Set-Content .\build\Version.props
& $env:MSBUILD_PATH  .\build\ChangeVersion.proj
testExitCode 

#get version number
[xml]$versionProps = Get-Content .\build\Version.props
$version  = $versionProps.Project.PropertyGroup.MainVersion+".$buildversion"
write-host -f green "version: $version"    

function restore() {
    # see https://github.com/Microsoft/vsts-tasks/issues/3762
    # it seems for mixed .net standard and .net framework, we need both dotnet restore and nuget restore...
    & dotnet restore
    & $env:NUGET_PATH restore
}

function deploy([string] $version) {
    #DeployOnRepox $scannerZipPath "" $version
    $scannerZipPath = Get-Item .\DeploymentArtifacts\BuildAgentPayload\Release\SonarQube.Scanner.MSBuild.zip
    
    write-host -f green  "replace zip filenames in pom.xml"
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
}

function runTests() {
    . (Join-Path $PSScriptRoot "ci-runTests.ps1")
    Invoke-Tests
    Invoke-CodeCoverage
}

if ($env:IS_PULLREQUEST -eq "true") { 
    write-host -f green "in a pull request"

    .\SonarQube.Scanner.MSBuild begin /k:sonar-scanner-msbuild /n:"SonarQube Scanner for MSBuild" /v:latest `
        /d:sonar.host.url=$env:SONAR_HOST_URL `
        /d:sonar.login=$env:SONAR_TOKEN `
        /d:sonar.github.pullRequest=$env:PULL_REQUEST `
        /d:sonar.github.repository=$env:GITHUB_REPO `
        /d:sonar.github.oauth=$env:GITHUB_TOKEN `
        /d:sonar.analysis.mode=issues `
        /d:sonar.scanAllFiles=true `
        /d:sonar.analysis.buildNumber=$env:BUILD_NUMBER `
        /d:sonar.analysis.pipeline=$env:BUILD_NUMBER `
        /d:sonar.analysis.sha1=$env:GIT_SHA1 `
        /d:sonar.analysis.repository=$env:GITHUB_REPO
    testExitCode

    restore
    testExitCode
    & $env:MSBUILD_PATH SonarQube.Scanner.MSBuild.sln /t:rebuild /p:Configuration=Release
    testExitCode
    #run tests
    runTests

    .\SonarQube.Scanner.MSBuild end /d:sonar.login=$env:SONAR_TOKEN
    testExitCode

    deploy -version $version

} else {
    if (($env:GITHUB_BRANCH -eq "master") -or ($env:GITHUB_BRANCH -eq "refs/heads/master")) {
        write-host -f green "Building master branch"

        #start analysis
        .\SonarQube.Scanner.MSBuild begin /k:sonar-scanner-msbuild /n:"SonarQube Scanner for MSBuild" /v:master `
            /d:sonar.host.url=$env:SONAR_HOST_URL `
            /d:sonar.login=$env:SONAR_TOKEN `
            /d:sonar.cs.vscoveragexml.reportsPaths="**\*.coveragexml" `
            /d:sonar.analysis.buildNumber=$env:BUILD_NUMBER `
            /d:sonar.analysis.pipeline=$env:BUILD_NUMBER `
            /d:sonar.analysis.sha1=$env:GIT_SHA1 `
            /d:sonar.analysis.repository=$env:GITHUB_REPO    
        testExitCode

        #build
        restore
        testExitCode
        & $env:MSBUILD_PATH SonarQube.Scanner.MSBuild.sln /p:configuration=Release /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=$env:CERT_PATH
        testExitCode

        runTests

        #end analysis
        .\SonarQube.Scanner.MSBuild end /d:sonar.login=$env:SONAR_TOKEN
        testExitCode
       
       deploy -version $version
		
    } else {
        write-host -f green "not on master"

        #build
        restore
        testExitCode
        & $env:MSBUILD_PATH SonarQube.Scanner.MSBuild.sln /p:configuration=Release /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=$env:CERT_PATH
        testExitCode

        runTests
    }

}


