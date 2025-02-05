param ($sourcesDirectory, $buildConfiguration)

. .\scripts\utils.ps1

$path = "$sourcesDirectory\coverage\"
If(!(test-path -PathType container $path))
{
      New-Item -ItemType Directory -Path $path
}

function Run-Tests-With-Coverage {
    param (
    $projectPath
    )
    dotnet test $projectPath --configuration $buildConfiguration --results-directory "$sourcesDirectory\TestResults" -l trx --no-build --no-restore
    Test-ExitCode "ERROR: Unit tests for '$projectPath' FAILED."
}
dotnet --info
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Common.Test\SonarScanner.MSBuild.Common.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PostProcessor.Test\SonarScanner.MSBuild.PostProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.PreProcessor.Test\SonarScanner.MSBuild.PreProcessor.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Shim.Test\SonarScanner.MSBuild.Shim.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.TFS.Test\SonarScanner.MSBuild.TFS.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.IntegrationTest\SonarScanner.MSBuild.Tasks.IntegrationTest.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Test\SonarScanner.MSBuild.Test.csproj
Run-Tests-With-Coverage Tests\SonarScanner.MSBuild.Tasks.UnitTest\SonarScanner.MSBuild.Tasks.UnitTest.csproj # This one needs to be last to convert the coverage results, see Tests/Directory.Build.targets