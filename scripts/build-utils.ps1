. (Join-Path $PSScriptRoot "utils.ps1")

function Get-NuGetPath {
    return Get-ExecutablePath -name "nuget.exe" -envVar "NUGET_PATH"
}

function Get-MsBuildPath() {

    Write-Debug "Trying to find 'msbuild.exe' using 'MSBUILD_PATH' environment variable"
    $msbuildEnv = "MSBUILD_PATH"
    $msbuildPath = [environment]::GetEnvironmentVariable($msbuildEnv, "Process")

    if (!$msbuildPath) {
        Write-Debug "Environment variable not found"
        Write-Debug "Trying to find path using 'vswhere.exe'"

        # Sets the path to MSBuild into an the MSBUILD_PATH environment variable
        # All subsequent builds after this command will use this version of MsBuild!
        $path = "C:\Program Files\Microsoft Visual Studio\2022\Professional"
        if ($path) {
            $msbuildPath = Join-Path $path "MSBuild\Current\Bin\MSBuild.exe"
            [environment]::SetEnvironmentVariable($msbuildEnv, $msbuildPath)
        }
    }

    if (Test-Path $msbuildPath) {
        Write-Debug "Found 'msbuild.exe' at '${msbuildPath}'"
        return $msbuildPath
    }

    throw "'msbuild.exe' located at '${msbuildPath}' doesn't exist"
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

    # see https://github.com/microsoft/azure-pipelines-tasks/issues/3762
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
    [Parameter(Mandatory = $true, Position = 0)][string]$solutionPath,
    [parameter(ValueFromRemainingArguments = $true)][array]$remainingArgs) {

    $solutionName = Split-Path $solutionPath -leaf
    Write-Header "Building solution ${solutionName}"

    if (Test-Debug) {
        $remainingArgs += "/v:detailed"
    }
    else {
        $remainingArgs += "/v:quiet"
    }

    $msbuildExe = Get-MsBuildPath
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