# Unit tests for the generate-packages.ps1 script.
# Preconditions:
# - Install chocolatey: https://docs.chocolatey.org/en-us/choco/setup/#install-with-powershellexe
# - Install Pester: Install-Module -Name Pester -Force -SkipPublisherCheck (https://pester.dev/docs/introduction/installation#windows)
# Running:
# - Open a PowerShell terminal.
# - Generate binaries: .\scripts\its-build.ps1
# - Package the scanner: nuget pack nuspec\netcoreglobaltool\dotnet-sonarscanner.nuspec
# - Run the tests: Invoke-Pester -Output Detailed .\scripts\generate-packages.tests.ps1

BeforeAll {
    New-Item -Force -Path "$PSScriptRoot" -Name "testcontext" -ItemType Directory
    New-Item -Force -Path "$PSScriptRoot/testcontext" -Name "build" -ItemType Directory
    New-Item -Force -Path "$PSScriptRoot/testcontext/build" -Name "sonarscanner-net.zip" -type File
    New-Item -Force -Path "$PSScriptRoot/testcontext/build" -Name "sonarscanner-net-framework.zip" -type File
    New-Item -Force -Path "$PSScriptRoot/testcontext/scripts/version" -Name "Version.props" -type File

    Set-Location "$PSScriptRoot/testcontext"
}

AfterAll {
    Set-Location $PSScriptRoot
    Remove-Item -Path "$PSScriptRoot/testcontext" -Recurse
}

Describe 'Main' {
    It 'Given a release candidate version, sets short and long versions' {
        Set-Content -Path "scripts\version\Version.props" -Value '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MainVersion>1.2.3</MainVersion>
    <BuildNumber>4</BuildNumber>
    <PrereleaseSuffix>-rc</PrereleaseSuffix>
  </PropertyGroup>
</Project>'

        . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

        $shortVersion | Should -Be '1.2.3-rc'
        $fullVersion | Should -Be '1.2.3-rc.99116'
    }

    It 'Given a stable version, sets short and long versions' {
        Set-Content -Path "scripts\version\Version.props" -Value '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MainVersion>1.2.3</MainVersion>
    <BuildNumber>4</BuildNumber>
    <PrereleaseSuffix></PrereleaseSuffix>
  </PropertyGroup>
</Project>'

        . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

        $shortVersion | Should -Be '1.2.3'
        $fullVersion | Should -Be '1.2.3.99116'
    }
}