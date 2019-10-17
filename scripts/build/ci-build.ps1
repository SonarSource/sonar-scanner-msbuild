<#

.SYNOPSIS
This script controls the build process on the CI server.

#>

[CmdletBinding(PositionalBinding = $false)]
param (
    # GitHub related parameters
    [string]$githubBranch = $env:BUILD_SOURCEBRANCH,
    [string]$githubSha1 = $env:BUILD_SOURCEVERSION,

    # Build related parameters
    [string]$buildNumber = $env:BUILD_BUILDID,
    [string]$certificatePath = $env:CERT_PATH,

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

function Generate-Artifacts() {
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

    #Write-Host "Deploying artifacts to repox"
    # Set the version used by Jenkins to associate artifacts to the right version
    #$env:PROJECT_VERSION = $version
    #$env:BUILD_ID=$buildNumber

    #Exec { & mvn deploy -Pdeploy-sonarsource -B -e -V `
    #} -errorMessage "ERROR: Deployment FAILED."
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
    $isPullRequest = $env:BUILD_REASON -eq "PullRequest"

    Write-Debug "Solution to build: ${solutionName}"
    Write-Debug "Build configuration: ${buildConfiguration}"
    Write-Debug "Bin folder to use: ${binPath}"
    Write-Debug "Branch: ${branchName}"
    if ($isMaster) {
        Write-Debug "Build kind: master"
    }
    elseif ($isPullRequest) {
        Write-Debug "Build kind: PR"
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
	
	Set-DotNetVersion

    $leakPeriodVersion = Get-LeakPeriodVersion
    Generate-Artifacts $leakPeriodVersion

    #if ($isPullRequest -or $isMaster -or $isMaintenanceBranch) {
     #   Invoke-InLocation "${PSScriptRoot}\..\.." { Initialize-QaStep }
    #}

    Write-Host -ForegroundColor Green "SUCCESS: BUILD job was successful!"
    exit 0
}
catch {
    Write-Host -ForegroundColor Red $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}