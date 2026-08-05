param ([string]$SourcesDirectory, [string]$BuildConfiguration)

function Test-ExitCode([string]$errorMessage = "ERROR: Command FAILED.") {
    if ((-not $?) -or ($lastexitcode -ne 0)) {
        throw $errorMessage
    }
}

function Run-Tests-With-Coverage ([string]$ProjectPath) {
    $ProjectNameLiteral = '$(ProjectName)'  #AltCover will replace this MsBuild-style variable with actual project name. The '' deals with PowerShell evaluation
    # Built as its own string first, then passed as a single quoted argument: $SourcesDirectory/$ProjectNameLiteral only interpolate
    # reliably this way. Embedded inside an unquoted /p:Key=Val,... token passed straight to the dotnet native executable, pwsh's
    # native-argument-passing left both variables un-interpolated, silently misdirecting every coverage report (unlike Windows
    # PowerShell 5.1, which Azure Pipelines used, where this same pattern happened to interpolate correctly).
    $msbuildProperties = "AltCover=true,AltCoverForce=true,AltCoverVisibleBranches=true,AltCoverAssemblyFilter=testhost|AltCover|Microsoft|protobuf|Humanizer|GraphQL|StructuredLogger|Test,AltCoverAttributeFilter=ExcludeFromCodeCoverage,AltCoverReport=$SourcesDirectory/Coverage/$ProjectNameLiteral.xml"
    dotnet test $ProjectPath --configuration $BuildConfiguration --results-directory "$SourcesDirectory\TestResults" -l trx --no-build --no-restore --filter "TestCategory!=NoWindows" "/p:$msbuildProperties"
    Test-ExitCode "ERROR: Unit tests for '$ProjectPath' FAILED."
}

dotnet --info
# PackagingTest runs during Build stage.
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Common.Test\SonarScanner.MSBuild.Common.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PostProcessor.Test\SonarScanner.MSBuild.PostProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PreProcessor.Test\SonarScanner.MSBuild.PreProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Shim.Test\SonarScanner.MSBuild.Shim.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.IntegrationTest\SonarScanner.MSBuild.Tasks.IntegrationTest.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.UnitTest\SonarScanner.MSBuild.Tasks.UnitTest.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Test\SonarScanner.MSBuild.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.TFS.Test\SonarScanner.MSBuild.TFS.Test.csproj
