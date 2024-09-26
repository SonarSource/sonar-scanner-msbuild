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
    New-Item -Force -Path "$PSScriptRoot/testcontext" -Name "nuspec" -ItemType Directory
    New-Item -Force -Path "$PSScriptRoot/testcontext" -Name "build" -ItemType Directory
    New-Item -Force -Path "$PSScriptRoot/testcontext/build" -Name "sonarscanner-net.zip" -type File
    New-Item -Force -Path "$PSScriptRoot/testcontext/build" -Name "sonarscanner-net-framework.zip" -type File
    New-Item -Force -Path "$PSScriptRoot/testcontext/scripts/version" -Name "Version.props" -type File

    Set-Location "$PSScriptRoot/testcontext"

    function CheckVersion([string] $packageFileName, [string] $nuspecFileName, [string] $expectedVersion) {
        Rename-Item -Path "build/$packageFileName" -NewName 'temp.zip'
        Expand-Archive 'build/temp.zip' -DestinationPath 'build/temp'

        Get-Content -Raw "build/temp/$nuspecFileName" | Should -Match $expectedVersion

        Remove-Item -Path 'build/temp.zip'
        Remove-Item -Path 'build/temp' -Recurse
    }

    function Set-Version([string] $version, [string] $prereleaseSuffix) {
        Set-Content -Path 'scripts/version/Version.props' -Value "<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <MainVersion>$version</MainVersion>
    <BuildNumber>123456789</BuildNumber>
    <PrereleaseSuffix>$prereleaseSuffix</PrereleaseSuffix>
  </PropertyGroup>
</Project>"
    }
}

AfterAll {
    Set-Location $PSScriptRoot
    Remove-Item -Path "$PSScriptRoot/testcontext" -Recurse
}

Describe 'Choco package generation' {
    Describe 'Main' {
        It 'Sets short and long versions' -TestCases @(
            @{ Version = '1.2.3'; PreReleaseSuffix = '-rc'; ExpectedShortVersion = '1.2.3-rc'; ExpectedFullVersion = '1.2.3-rc.99116' }
            @{ Version = '1.2.3'; PreReleaseSuffix = ''; ExpectedShortVersion = '1.2.3'; ExpectedFullVersion = '1.2.3.99116' }
        ) {
            Set-Version $Version $PreReleaseSuffix

            . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

            $shortVersion | Should -Be $ExpectedShortVersion
            $fullVersion | Should -Be $ExpectedFullVersion
        }
    }

    Describe 'Update-Choco-Package' {
        BeforeEach {
            Remove-Item -Path 'nuspec' -Recurse
            Copy-Item -Path "../../nuspec/chocolatey/" -Destination "nuspec/chocolatey" -Recurse
        }

        It 'Sets the package name, version and url' -TestCases @(
            @{ Version = '1.2.3'; PreReleaseSuffix = '-rc'; ExpectedShortVersion = '1.2.3-rc'; ExpectedFullVersion = '1.2.3-rc.99116' }
            @{ Version = '1.2.3'; PreReleaseSuffix = ''; ExpectedShortVersion = '1.2.3'; ExpectedFullVersion = '1.2.3.99116' }
        )  {
            $unzipLocation = '$(Split-Path -parent $MyInvocation.MyCommand.Definition)'
            $expectedChocoInstallContents = @('Install-ChocolateyZipPackage "sonarscanner-net-framework" `',
            "    -Url ""https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/$ExpectedFullVersion/sonar-scanner-$ExpectedFullVersion-net-framework.zip"" ``",
            "    -UnzipLocation ""$unzipLocation"" ``",
            "    -ChecksumType 'sha256' ``",
            "    -Checksum E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")
            Set-Version $Version $PreReleaseSuffix
            . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

            Update-Choco-Package "$PSScriptRoot/testcontext/build/sonarscanner-net-framework.zip" 'net-framework'

            Get-Content 'nuspec/chocolatey/chocolateyInstall-net-framework.ps1' | Should -Be $expectedChocoInstallContents
            "build/sonarscanner-net-framework.$ExpectedShortVersion.nupkg" | Should -Exist
            CheckVersion "sonarscanner-net-framework.$ExpectedShortVersion.nupkg" 'sonarscanner-net-framework.nuspec' "<version>$ExpectedShortVersion</version>"
        }
    }
}
