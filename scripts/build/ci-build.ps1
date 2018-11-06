$ErrorActionPreference = "Stop"

function testExitCode(){
    If($LASTEXITCODE -ne 0) {
        write-host -f green "lastexitcode: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# NB: the .Net framework defaults to TLS v1 which is no longer supported by GitHub
# See https://githubengineering.com/crypto-removal-notice/
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
Write-Debug "Current security protocol: $([System.Net.ServicePointManager]::SecurityProtocol)"
$scannerMsbuildVersion = "4.2.0.1214"
(New-Object System.Net.WebClient).DownloadFile("https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$scannerMsbuildVersion/sonar-scanner-msbuild-$scannerMsbuildVersion-net46.zip", 
    ".\sonar-scanner-msbuild.zip")

unzip -o .\sonar-scanner-msbuild.zip
testExitCode

#generate build version from the build number
$buildversion="$env:BUILD_NUMBER"
$branchName = "$env:GITHUB_BRANCH"
$sha1 = "$env:GIT_SHA1"

$versionProperties = "scripts\version\Version.props"
[xml]$versionProps = Get-Content $versionProperties
$mainVersion = $versionProps.Project.PropertyGroup.MainVersion

#Append build number to the versions
(Get-Content $versionProperties) `
        -Replace '<Sha1>.*</Sha1>', "<Sha1>$sha1</Sha1>" `
        -Replace '<BuildNumber>\d+</BuildNumber>', "<BuildNumber>$buildversion</BuildNumber>" `
        -Replace '<BranchName>.*</BranchName>', "<BranchName>$branchName</BranchName>" `
    | Set-Content $versionProperties
& $env:MSBUILD_PATH  .\scripts\version\ChangeVersion.proj
testExitCode

#get version number
$version  = $mainVersion+".$buildversion"
write-host -f green "version: $version"

function restore() {
    # see https://github.com/Microsoft/vsts-tasks/issues/3762
    # it seems for mixed .net standard and .net framework, we need both dotnet restore and nuget restore...
    & dotnet restore
    & $env:NUGET_PATH restore
}

function deploy([string] $version) {
    #DeployOnRepox $classicScannerZipPath "" $version
    $classicScannerZipPath = Get-Item .\DeploymentArtifacts\BuildAgentPayload\Release\sonarscanner-msbuild-net46.zip
    $dotnetScannerZipPath  = Get-Item .\DeploymentArtifacts\BuildAgentPayload\Release\sonarscanner-msbuild-netcoreapp2.0.zip
    $dotnetScannerGlobalToolPath  = Get-Item .\DeploymentArtifacts\BuildAgentPayload\Release\dotnet-sonarscanner.$mainVersion.nupkg

    write-host -f green  "replace zip filenames in pom.xml"
    (Get-Content .\pom.xml) -replace 'classicScannerZipPath', "$classicScannerZipPath" | Set-Content .\pom.xml
    (Get-Content .\pom.xml) -replace 'dotnetScannerZipPath', "$dotnetScannerZipPath" | Set-Content .\pom.xml
    (Get-Content .\pom.xml) -replace 'dotnetScannerGlobalToolPath', "$dotnetScannerGlobalToolPath" | Set-Content .\pom.xml

    write-host -f green  "set version $version in pom.xml"
    $command = "mvn versions:set -DgenerateBackupPoms=false -DnewVersion='$version'"
    Invoke-Expression $command
    write-host -f green  "set version $version in env VAR PROJECT_VERSION for artifactory buildinfo metadata"
    $env:PROJECT_VERSION=$version
    write-host -f green  "set the buildnumber to this job build number"
    $env:BUILD_ID=$env:BUILD_NUMBER
    write-host -f green  "Deploy to repox with $version"
    $command = 'mvn deploy -Pdeploy-sonarsource -B -e -V'
    Invoke-Expression $command

    #create empty file to trigger qa
    new-item -path . -name qa.properties -type "file"
}

function runTests() {
    . (Join-Path $PSScriptRoot "ci-runTests.ps1")
    Clear-TestResults
    Invoke-Tests
    Clear-ExtraFiles
    Invoke-CodeCoverage
}

if ($env:IS_PULLREQUEST -eq "true") {
    write-host -f green "in a pull request"

    restore
    testExitCode
    & $env:MSBUILD_PATH SonarScanner.MSBuild.sln /t:rebuild /p:Configuration=Release
    testExitCode
    #run tests
    runTests

    deploy -version $version

} else {
    if (($env:GITHUB_BRANCH -eq "master") -or ($env:GITHUB_BRANCH -eq "refs/heads/master")) {
        write-host -f green "Building master branch"

        # scanner begin
        .\SonarScanner.MSBuild begin `
            /k:sonarscanner-msbuild `
            /n:"SonarScanner for MSBuild" `
            /v:$mainVersion `
            /d:sonar.host.url=$env:SONARCLOUD_HOST_URL `
            /d:sonar.login=$env:SONARCLOUD_TOKEN `
            /o:sonarsource `
            /d:sonar.cs.vstest.reportsPaths="**\*.trx" `
            /d:sonar.cs.vscoveragexml.reportsPaths="**\*.coveragexml" `
            /d:sonar.analysis.buildNumber=$env:BUILD_NUMBER `
            /d:sonar.analysis.pipeline=$env:BUILD_NUMBER `
            /d:sonar.analysis.sha1=$env:GIT_SHA1 `
            /d:sonar.analysis.repository=$env:GITHUB_REPO
        testExitCode

        # build
        restore
        testExitCode
        & $env:MSBUILD_PATH SonarScanner.MSBuild.sln /p:configuration=Release /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=$env:CERT_PATH /p:defineConstants="SignAssembly"
        testExitCode

        # tests
        runTests

        # scanner end
        .\SonarScanner.MSBuild end /d:sonar.login=$env:SONAR_TOKEN
        testExitCode

        # deploy
       deploy -version $version

    } else {
        write-host -f green "not on master"

        #build
        restore
        testExitCode
        & $env:MSBUILD_PATH SonarScanner.MSBuild.sln /p:configuration=Release /v:m /p:defineConstants=SignAssembly /p:SignAssembly=true /p:AssemblyOriginatorKeyFile=$env:CERT_PATH /p:defineConstants="SignAssembly"
        testExitCode

        runTests
    }

}


