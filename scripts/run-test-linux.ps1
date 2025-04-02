param (
    [string]
    $TestToRun = "UT"
)

if ($TestToRun -eq "IT") {
    # Change directory to 'its'
    Set-Location -Path "$PSScriptRoot/../its"
    # Run Maven with the specified test include pattern
    $testIncludes = @(
        "**/sonarqube/ScannerTest*", 
        "**/sonarqube/SslTest*"
    )
    $testIncludeParam = $testIncludes -join ','
    
    # Run Maven with the specified test include pattern
    mvn verify -Dtest="$testIncludeParam"
} else {
    # Change directory to 'its'
    Set-Location -Path "$PSScriptRoot/.."

    foreach ($testProject in @(Get-ChildItem -Path ./Tests -Recurse -Filter "*Test.csproj" -Name)) {
        Write-Host "Building $testProject..."
        dotnet build ./Tests/$testProject --verbosity quiet --framework net9.0 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed for $testProject. Exiting..."
            exit $LASTEXITCODE
        }
        dotnet test ./Tests/$testProject --no-build --framework net9.0 --logger "console;verbosity=minimal" --filter "Testcategory!=NoUnixNeedsReview" --results-directory "/tmp/TestResults"
    }
}
