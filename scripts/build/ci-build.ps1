<#

.SYNOPSIS
This script controls the build process on the CI server.

#>

[CmdletBinding(PositionalBinding = $false)]
param (
    # GitHub related parameters
    [string]$githubRepo = $env:GITHUB_REPO,
    [string]$githubToken = $env:GITHUB_TOKEN,
    [string]$githubPullRequest = $env:PULL_REQUEST,
    [string]$githubIsPullRequest = $env:IS_PULLREQUEST,
    [string]$githubBranch = $env:GITHUB_BRANCH,
    [string]$githubSha1 = $env:GIT_SHA1,
    # GitHub PR related parameters
    [string]$githubPRBaseBranch = $env:GITHUB_BASE_BRANCH,
    [string]$githubPRTargetBranch = $env:GITHUB_TARGET_BRANCH,

    # SonarQube related parameters
    [string]$sonarCloudUrl = $env:SONARCLOUD_HOST_URL,
    [string]$sonarCloudToken = $env:SONARCLOUD_TOKEN,

    # Build related parameters
    [string]$buildNumber = $env:BUILD_NUMBER,
    [string]$certificatePath = $env:CERT_PATH,

    # Artifactory related parameters
    [string]$repoxUserName = $env:ARTIFACTORY_DEPLOY_USERNAME,
    [string]$repoxPassword = $env:ARTIFACTORY_DEPLOY_PASSWORD,

    # Others
    [string]$appDataPath = $env:APPDATA
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

if ($PSBoundParameters['Verbose'] -Or $PSBoundParameters['Debug']) {
    $global:DebugPreference = "Continue"
}

function Get-BranchName() {
    if ($githubBranch.StartsWith("refs/heads/")) {
        return $githubBranch.Substring(11)
    }

    return $githubBranch
}

function Get-DotNetVersion() {
    [xml]$versionProps = Get-Content "${PSScriptRoot}\..\version\Version.props"
    $fullVersion = $versionProps.Project.PropertyGroup.MainVersion + "." + $versionProps.Project.PropertyGroup.BuildNumber

    Write-Debug ".Net version is '${fullVersion}'"

    return $fullVersion
}

function Set-DotNetVersion() {
    Write-Header "Updating version in .Net files"

    $branchName = Get-BranchName
    Write-Debug "Setting build number ${buildNumber}, sha1 ${githubSha1} and branch ${branchName}"

    Invoke-InLocation (Join-Path $PSScriptRoot "..\version") {
        $versionProperties = "Version.props"
        (Get-Content $versionProperties) `
                -Replace '<Sha1>.*</Sha1>', "<Sha1>$githubSha1</Sha1>" `
                -Replace '<BuildNumber>\d+</BuildNumber>', "<BuildNumber>$buildNumber</BuildNumber>" `
                -Replace '<BranchName>.*</BranchName>', "<BranchName>$branchName</BranchName>" `
            | Set-Content $versionProperties

        Invoke-MSBuild "15.0" "ChangeVersion.proj"

        $version = Get-DotNetVersion
        Write-Host "Version successfully set to '${version}'"
    }
}

function Get-LeakPeriodVersion() {
    [xml]$versionProps = Get-Content "${PSScriptRoot}\..\version\Version.props"
    $mainVersion = $versionProps.Project.PropertyGroup.MainVersion

    Write-Debug "Leak period version is '${mainVersion}'"

    return $mainVersion
}

function Get-ScannerMsBuildPath() {
    $currentDir = (Resolve-Path .\).Path
    $scannerMsbuild = Join-Path $currentDir "SonarScanner.MSBuild.exe"

    if (-Not (Test-Path $scannerMsbuild)) {
        Write-Debug "Scanner for MSBuild not found, downloading it"

        # This links always redirect to the latest released scanner
        $downloadLink = "https://repox.sonarsource.com/sonarsource-public-releases/org/sonarsource/scanner/msbuild/" +
            "sonar-scanner-msbuild/%5BRELEASE%5D/sonar-scanner-msbuild-%5BRELEASE%5D-net46.zip"
        $scannerMsbuildZip = Join-Path $currentDir "\SonarScanner.MSBuild.zip"

        Write-Debug "Downloading scanner from '${downloadLink}' at '${currentDir}'"
        (New-Object System.Net.WebClient).DownloadFile($downloadLink, $scannerMsbuildZip)

        # perhaps we could use other folder, not the repository root
        Expand-ZIPFile $scannerMsbuildZip $currentDir

        Write-Debug "Deleting downloaded zip"
        Remove-Item $scannerMsbuildZip -Force
    }

    Write-Debug "Scanner for MSBuild found at '$scannerMsbuild'"
    return $scannerMsbuild
}

function Invoke-SonarBeginAnalysis([array][parameter(ValueFromRemainingArguments = $true)]$remainingArgs) {
    Write-Header "Running SonarCloud Analysis begin step"

    if (Test-Debug) {
        $remainingArgs += "/d:sonar.verbose=true"
    }

    Exec { & (Get-ScannerMsBuildPath) begin `
        /k:sonarscanner-msbuild `
        /n:"SonarScanner for MSBuild" `
        /d:sonar.host.url=$sonarCloudUrl `
        /d:sonar.login=$sonarCloudToken `
        /o:sonarsource `
        /d:sonar.cs.vstest.reportsPaths="**\*.trx" `
        /d:sonar.cs.vscoveragexml.reportsPaths="**\*.coveragexml" `
        $remainingArgs `
    } -errorMessage "ERROR: SonarCloud Analysis begin step FAILED."
}

function Invoke-SonarEndAnalysis() {
    Write-Header "Running SonarCloud Analysis end step"

    Exec { & (Get-ScannerMsBuildPath) end `
        /d:sonar.login=$sonarCloudToken `
    } -errorMessage "ERROR: SonarCloud Analysis end step FAILED."
}

function Publish-Artifacts() {
    $artifactsFolder = ".\DeploymentArtifacts\BuildAgentPayload\Release"

    $classicScannerZipPath = Get-Item "$artifactsFolder\\sonarscanner-msbuild-net46.zip"
    $dotnetScannerZipPath = Get-Item "$artifactsFolder\\sonarscanner-msbuild-netcoreapp2.0.zip"
    $dotnetScannerGlobalToolPath = Get-Item "$artifactsFolder\\dotnet-sonarscanner.$leakPeriodVersion.nupkg"

    $version = Get-DotNetVersion

    Write-Host "Generating the chocolatey packages"
    $classicZipHash = (Get-FileHash $classicScannerZipPath -Algorithm SHA256).hash
    $net46ps1 = "nuspec\chocolatey\chocolateyInstall-net46.ps1"
    (Get-Content $net46ps1) `
            -Replace '-Checksum "not-set"', "-Checksum $classicZipHash" `
        | Set-Content $net46ps1

    $dotnetZipHash = (Get-FileHash $dotnetScannerZipPath -Algorithm SHA256).hash
    $netcoreps1 = "nuspec\chocolatey\chocolateyInstall-netcoreapp2.0.ps1"
    (Get-Content $netcoreps1) `
            -Replace '-Checksum "not-set"', "-Checksum $dotnetZipHash" `
        | Set-Content $netcoreps1

    Exec { & choco pack nuspec\chocolatey\sonarscanner-msbuild-net46.nuspec `
        --outputdirectory $artifactsFolder `
        --version $version `
    } -errorMessage "ERROR: Creation of the net46 chocolatey package FAILED."
    Exec { & choco pack nuspec\chocolatey\sonarscanner-msbuild-netcoreapp2.0.nuspec `
        --outputdirectory $artifactsFolder `
        --version $version `
    } -errorMessage "ERROR: Creation of the net46 chocolatey package FAILED."

    Write-Host "Update artifacts locations in pom.xml"
    $pomFile = ".\pom.xml"
    $currentDir = (Get-Item -Path ".\").FullName
    (Get-Content $pomFile) `
            -Replace 'classicScannerZipPath', "$classicScannerZipPath" `
            -Replace 'dotnetScannerZipPath', "$dotnetScannerZipPath" `
            -Replace 'dotnetScannerGlobalToolPath', "$dotnetScannerGlobalToolPath" `
            -Replace 'classicScannerChocoPath', "$currentDir\\$artifactsFolder\\sonarscanner-msbuild-net46.$version.nupkg" `
            -Replace 'dotnetScannerChocoPath', "$currentDir\\$artifactsFolder\\sonarscanner-msbuild-netcoreapp2.0.$version.nupkg" `
        | Set-Content $pomFile

    Exec { & mvn org.codehaus.mojo:versions-maven-plugin:2.2:set "-DnewVersion=${version}" `
        -DgenerateBackupPoms=false -B -e `
    } -errorMessage "ERROR: Maven set version FAILED."

    Write-Host "Deploying artifacts to repox"
    # Set the version used by Jenkins to associate artifacts to the right version
    $env:PROJECT_VERSION = $version
    $env:BUILD_ID=$buildNumber

    Exec { & mvn deploy -Pdeploy-sonarsource -B -e -V `
    } -errorMessage "ERROR: Deployment FAILED."
}

function Invoke-DotNetBuild() {
    Set-DotNetVersion

    $skippedAnalysis = $false
    $leakPeriodVersion = Get-LeakPeriodVersion

    if ($isPullRequest) {
        Invoke-SonarBeginAnalysis `
            /d:sonar.analysis.prNumber=$githubPullRequest `
            /d:sonar.analysis.sha1=$githubSha1 `
            /d:sonar.pullrequest.key=$githubPullRequest `
            /d:sonar.pullrequest.branch=$githubPRBaseBranch `
            /d:sonar.pullrequest.base=$githubPRTargetBranch `
            /d:sonar.pullrequest.provider=github `
            /d:sonar.pullrequest.github.repository=$githubRepo `
            /v:$leakPeriodVersion
    }
    elseif ($isMaster) {
        Invoke-SonarBeginAnalysis `
            /v:$leakPeriodVersion `
            /d:sonar.analysis.buildNumber=$buildNumber `
            /d:sonar.analysis.pipeline=$buildNumber `
            /d:sonar.analysis.sha1=$githubSha1 `
            /d:sonar.analysis.repository=$githubRepo
    }
    elseif ($isMaintenanceBranch -or $isFeatureBranch) {
        Invoke-SonarBeginAnalysis `
            /v:$leakPeriodVersion `
            /d:sonar.analysis.buildNumber=$buildNumber `
            /d:sonar.analysis.pipeline=$buildNumber `
            /d:sonar.analysis.sha1=$githubSha1 `
            /d:sonar.analysis.repository=$githubRepo `
            /d:sonar.branch.name=$branchName
    }
    else {
        $skippedAnalysis = $true
    }

    Restore-Packages "15.0" $solutionName
    Invoke-MSBuild "15.0" $solutionName `
        /bl:"${binPath}\msbuild.binlog" `
        /consoleloggerparameters:Summary `
        /m `
        /p:configuration=$buildConfiguration `
        /p:DeployExtension=false `
        /p:ZipPackageCompressionLevel=normal `
        /p:defineConstants="SignAssembly" `
        /p:SignAssembly=true `
        /p:AssemblyOriginatorKeyFile=$certificatePath

    Invoke-UnitTests $binPath $true
    Invoke-CodeCoverage

    if (-Not $skippedAnalysis) {
        Invoke-SonarEndAnalysis
        Publish-Artifacts $leakPeriodVersion
    }
}

function Initialize-QaStep() {
    Write-Header "Queueing QA job"

    New-Item -Path . -Name qa.properties -Type "file"
    Write-Host "Triggering QA job"
}

try {
    . (Join-Path $PSScriptRoot "build-utils.ps1")

    $buildConfiguration = "Release"
    $binPath = "bin\${buildConfiguration}"
    $solutionName = "SonarScanner.MSBuild.sln"
    $branchName = Get-BranchName
    $isMaster = $branchName -eq "master"
    # See https://xtranet.sonarsource.com/display/DEV/Release+Procedures for info about maintenance branches
    $isMaintenanceBranch = $branchName -like 'branch-*'
    $isFeatureBranch = $branchName -like 'feature/*'
    $isPullRequest = $githubIsPullRequest -eq "true"

    Write-Debug "Solution to build: ${solutionName}"
    Write-Debug "Build configuration: ${buildConfiguration}"
    Write-Debug "Bin folder to use: ${binPath}"
    Write-Debug "Branch: ${branchName}"
    if ($isMaster) {
        Write-Debug "Build kind: master"
    }
    elseif ($isPullRequest) {
        Write-Debug "Build kind: PR"
        Write-Debug "PR: ${githubPullRequest}"
        Write-Debug "PR source: ${githubPRBaseBranch}"
        Write-Debug "PR target: ${githubPRTargetBranch}"
    }
    elseif ($isMaintenanceBranch) {
        Write-Debug "Build kind: maintenance branch"
    }
    elseif ($isFeatureBranch) {
        Write-Debug "Build kind: feature branch"
    }
    else {
        Write-Debug "Build kind: branch"
    }

    Invoke-InLocation "${PSScriptRoot}\..\.." {
        Invoke-DotNetBuild
    }

    if ($isPullRequest -or $isMaster -or $isMaintenanceBranch) {
        Invoke-InLocation "${PSScriptRoot}\..\.." { Initialize-QaStep }
    }

    Write-Host -ForegroundColor Green "SUCCESS: BUILD job was successful!"
    exit 0
}
catch {
    Write-Host -ForegroundColor Red $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}