. (Join-Path $PSScriptRoot "..\utils.ps1")

function Get-NuGetPath {
    return Get-ExecutablePath -name "nuget.exe" -envVar "NUGET_PATH"
}

function Get-VsWherePath {
    return Get-ExecutablePath -name "vswhere.exe" -envVar "VSWHERE_PATH"
}

function Get-MsBuildPath([ValidateSet("15.0")][string]$msbuildVersion) {

    Write-Debug "Trying to find 'msbuild.exe 15' using 'MSBUILD_PATH' environment variable"
    $msbuild15Env = "MSBUILD_PATH"
    $msbuild15Path = [environment]::GetEnvironmentVariable($msbuild15Env, "Process")

    if (!$msbuild15Path) {
        Write-Debug "Environment variable not found"
        Write-Debug "Trying to find path using 'vswhere.exe'"

        # Sets the path to MSBuild 15 into an the MSBUILD_PATH environment variable
        # All subsequent builds after this command will use MSBuild 15!
        # Test if vswhere.exe is in your path. Download from: https://github.com/Microsoft/vswhere/releases
        $path = "C:\\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise"
        if ($path) {
            $msbuild15Path = Join-Path $path "MSBuild\15.0\Bin\MSBuild.exe"
            [environment]::SetEnvironmentVariable($msbuild15Env, $msbuild15Path)
        }
    }

    if (Test-Path $msbuild15Path) {
        Write-Debug "Found 'msbuild.exe 15' at '${msbuild15Path}'"
        return $msbuild15Path
    }

    throw "'msbuild.exe 15' located at '${msbuild15Path}' doesn't exist"
}

function Get-VsTestPath {
    return Get-ExecutablePath -name "VSTest.Console.exe" -envVar "VSTEST_PATH"
}

function Get-CodeCoveragePath {
    $vstest_exe = Get-VsTestPath
    $codeCoverageDirectory = Join-Path (Get-ChildItem $vstest_exe).Directory "..\..\..\..\.."
    return Get-ExecutablePath -name "CodeCoverage.exe" -directory $codeCoverageDirectory -envVar "CODE_COVERAGE_PATH"
}

# NuGet
function Restore-Packages (
    [Parameter(Mandatory = $true, Position = 0)][ValidateSet("14.0", "15.0")][string]$msbuildVersion,
    [Parameter(Mandatory = $true, Position = 1)][string]$solutionPath) {

    $solutionName = Split-Path $solutionPath -Leaf
    Write-Header "Restoring NuGet packages for ${solutionName}"

    $msbuildBinDir = Split-Path -Parent (Get-MsBuildPath $msbuildVersion)

    # see https://github.com/Microsoft/vsts-tasks/issues/3762
    # it seems for mixed .net standard and .net framework, we need both dotnet restore and nuget restore...
    if (Test-Debug) {
        Exec { & (Get-NuGetPath) restore $solutionPath -MSBuildPath $msbuildBinDir -Verbosity detailed `
        } -errorMessage "ERROR: Restoring NuGet packages FAILED."
        Exec { & dotnet restore $solutionPath `
        }  -
    }
    else {
        Exec { & (Get-NuGetPath) restore $solutionPath -MSBuildPath $msbuildBinDir `
        } -errorMessage "ERROR: Restoring NuGet packages with nuget FAILED."
        Exec { & dotnet restore $solutionPath `
        }  -errorMessage "ERROR: Restoring NuGet packages with dotnet FAILED."
    }
}

# Build
function Invoke-MSBuild (
    [Parameter(Mandatory = $true, Position = 0)][ValidateSet("14.0", "15.0")][string]$msbuildVersion,
    [Parameter(Mandatory = $true, Position = 1)][string]$solutionPath,
    [parameter(ValueFromRemainingArguments = $true)][array]$remainingArgs) {

    $solutionName = Split-Path $solutionPath -leaf
    Write-Header "Building solution ${solutionName}"

    if (Test-Debug) {
        $remainingArgs += "/v:detailed"
    }
    else {
        $remainingArgs += "/v:quiet"
    }

    $msbuildExe = Get-MsBuildPath $msbuildVersion
    Exec { & $msbuildExe $solutionPath $remainingArgs `
    } -errorMessage "ERROR: Build FAILED."
}

# Tests
function Clear-TestResults() {
    If (Test-Path TestResults){
        Remove-Item TestResults -Recurse
    }
}

function Clear-ExtraFiles() {
    # Clean up extra test results
    Get-ChildItem -path "TestResults" -Recurse -Include *.trx `
        | Where-Object { $_ -Match ".+\\.+\.trx" } `
        | Remove-Item
    Get-ChildItem -path "TestResults" -Recurse -Include *.coverage `
        | Where-Object { $_ -NotMatch "([a-f0-9]+[-])+[a-f0-9]+\\" } `
        | Remove-Item
}

function Invoke-UnitTests([string]$binPath, [bool]$failsIfNotTest) {
    Write-Header "Running unit tests"

    Clear-TestResults

    Write-Debug "Running unit tests for"
    $testFiles = @()
    $testDirs = @()
    Get-ChildItem ".\tests" -Recurse -Include "*Tests.dll" `
        | Where-Object { $_.DirectoryName -Match "bin" } `
        | ForEach-Object {
            $currentFile = $_
            Write-Debug "   - ${currentFile}"
            $testFiles += $currentFile
            $testDirs += $currentFile.Directory
        }
    $testDirs = $testDirs | Select-Object -Uniq

    & (Get-VsTestPath) /Enablecodecoverage /Parallel /Logger:trx $testFiles
    Test-ExitCode "ERROR: Unit Tests execution FAILED."

    Clear-ExtraFiles
}

# Coverage
function Invoke-CodeCoverage() {
    Write-Header "Creating coverage report"

    $codeCoverageExe = Get-CodeCoveragePath

    Write-Host "Generating code coverage reports for"
    Get-ChildItem "TestResults" -Recurse -Include "*.coverage" | ForEach-Object {
        Write-Host "    -" $_.FullName

        $filePathWithNewExtension = $_.FullName + "xml"
        if (Test-Path $filePathWithNewExtension) {
            Write-Debug "Coveragexml report already exists, removing it"
            Remove-Item -Force $filePathWithNewExtension
        }

        Exec { & $codeCoverageExe analyze /output:$filePathWithNewExtension $_.FullName `
        } -errorMessage "ERROR: Code coverage reports generation FAILED."
    }
}