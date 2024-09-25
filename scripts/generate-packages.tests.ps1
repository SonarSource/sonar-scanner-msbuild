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

Describe 'Update-Choco-Package' {
    It 'Given a release candidate version, sets the package name and the url' {
        $chocoInstallPath = 'choco-install.ps1'
        Set-Content -Path "scripts\version\Version.props" -Value '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MainVersion>1.2.3</MainVersion>
    <BuildNumber>4</BuildNumber>
    <PrereleaseSuffix>-rc</PrereleaseSuffix>
  </PropertyGroup>
</Project>'

        Set-Content -Path $chocoInstallPath -Value 'Install-ChocolateyZipPackage "sonarscanner-net-framework" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/__PackageVersion__/sonar-scanner-__PackageVersion__-net-framework.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType ''sha256'' `
    -Checksum "not-set"'

        . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

        Update-Choco-Package $netFrameworkScannerZipPath $chocoInstallPath '..\..\nuspec\chocolatey\sonarscanner-net-framework.nuspec'

        Get-Content -Raw $chocoInstallPath | Should -BeExactly 'Install-ChocolateyZipPackage "sonarscanner-net-framework" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/1.2.3-rc.99116/sonar-scanner-1.2.3-rc.99116-net-framework.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType ''sha256'' `
    -Checksum E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
'

        'build/sonarscanner-net-framework.1.2.3-rc.nupkg' | Should -Exist
    }

    It 'Given a stable version, sets the package name and the url' {
        $chocoInstallPath = 'choco-install.ps1'

        Set-Content -Path "scripts\version\Version.props" -Value '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MainVersion>1.2.3</MainVersion>
    <BuildNumber>4</BuildNumber>
    <PrereleaseSuffix></PrereleaseSuffix>
  </PropertyGroup>
</Project>'
        Set-Content -Path $chocoInstallPath -Value 'Install-ChocolateyZipPackage "sonarscanner-net-framework" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/__PackageVersion__/sonar-scanner-__PackageVersion__-net-framework.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType ''sha256'' `
    -Checksum "not-set"'

        . $PSScriptRoot/generate-packages.ps1 -sourcesDirectory . -buildId 99116

        Update-Choco-Package $netFrameworkScannerZipPath $chocoInstallPath '..\..\nuspec\chocolatey\sonarscanner-net-framework.nuspec'

        Get-Content -Raw $chocoInstallPath | Should -BeExactly 'Install-ChocolateyZipPackage "sonarscanner-net-framework" `
    -Url "https://github.com/SonarSource/sonar-scanner-msbuild/releases/download/1.2.3.99116/sonar-scanner-1.2.3.99116-net-framework.zip" `
    -UnzipLocation "$(Split-Path -parent $MyInvocation.MyCommand.Definition)" `
    -ChecksumType ''sha256'' `
    -Checksum E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
'

        'build/sonarscanner-net-framework.1.2.3.nupkg' | Should -Exist
    }
}