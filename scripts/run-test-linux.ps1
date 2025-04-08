param (
    [string]
    $TestToRun = "UT",
    [string]
    $TestFilter
)

if ($TestToRun -eq "IT") {
    # Change directory to 'its'
    Set-Location -Path "$PSScriptRoot/../its"
    # Run Maven with the specified test include pattern
    $testIncludes = @(
        "**/sonarqube/ScannerTest*", 
        "**/sonarqube/SslTest*",
        "**/sonarqube/JreProvisioningTest*",
        "**/sonarcloud/JreProvisioningTest*"
    )
    $testIncludeParam = $testIncludes -join ','

    if (![string]::IsNullOrWhiteSpace($TestFilter)) {
        $testIncludeParam = $TestFilter
    }
    
    # Run Maven with the specified test include pattern
    mvn verify -Dtest="$testIncludeParam"
} else {
    # Change directory to 'its'
    Set-Location -Path "$PSScriptRoot/.."


    if (![string]::IsNullOrWhiteSpace($TestFilter)) {
        $TestFilter = "Testcategory!=NoUnixNeedsReview & Testcategory!=NoLinux & $TestFilter"
    } else {
        $TestFilter = "Testcategory!=NoUnixNeedsReview & Testcategory!=NoLinux"
    }

    $solutionFile = "$PSScriptRoot/../SonarScanner.MSBuild.sln"
    # Parse the .sln file to extract project paths
    $testProjects = Select-String -Path $solutionFile -Pattern "Project.*=.*" | ForEach-Object {
        # Extract the project path from the line
        if ($_ -match '.*"([^"]+\.csproj)"') {
            $matches[1]
        }
    } | Where-Object { $_ -like "Tests/*" -or $_ -like "Tests\*" }

    # Ensure test projects were found
    if (-Not $testProjects) {
        Write-Host "No test projects found in the solution file under the 'Tests' folder." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Building tests projects..."
    foreach ($testProject in $testProjects) {
        Write-Host "Building $testProject..."
        $buildOutput = dotnet build $testProject --verbosity quiet --framework net9.0  2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed for $testProject. Exiting..."
            Write-Host "Error details:" -ForegroundColor Red
            Write-Host $buildOutput
            exit $LASTEXITCODE
        }
    }

    Write-Host "Running tests with filter: $TestFilter"
    dotnet test --no-build --framework net9.0 --logger "console;verbosity=minimal" --filter "$TestFilter" --results-directory "/tmp/TestResults"
}
