param ([string]$SourcesDirectory, [string]$BuildConfiguration)

. .\scripts\utils.ps1

function Run-Tests-With-Coverage ([string]$ProjectPath) {
    $ProjectNameLiteral = '$(ProjectName)'  #AltCover will replace this MsBuild-style variable with actual project name. The '' deals with PowerShell evaluation
    dotnet test $ProjectPath --configuration $BuildConfiguration --results-directory "$SourcesDirectory\TestResults" -l trx --no-build --no-restore --filter "TestCategory!=NoWindows" /p:AltCover=true,AltCoverForce=true,AltCoverVisibleBranches=true,AltCoverAssemblyFilter="testhost|AltCover|Microsoft|protobuf|Humanizer|GraphQL|StructuredLogger|Test",AltCoverAttributeFilter="ExcludeFromCodeCoverage",AltCoverReport="$SourcesDirectory/Coverage/$ProjectNameLiteral.xml"
    Test-ExitCode "ERROR: Unit tests for '$ProjectPath' FAILED."
}

dotnet --info
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Common.Test\SonarScanner.MSBuild.Common.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PostProcessor.Test\SonarScanner.MSBuild.PostProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PreProcessor.Test\SonarScanner.MSBuild.PreProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Shim.Test\SonarScanner.MSBuild.Shim.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.IntegrationTest\SonarScanner.MSBuild.Tasks.IntegrationTest.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.UnitTest\SonarScanner.MSBuild.Tasks.UnitTest.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Test\SonarScanner.MSBuild.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.TFS.Test\SonarScanner.MSBuild.TFS.Test.csproj
