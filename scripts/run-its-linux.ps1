param (
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]
    $Passthrough
)

dotnet test C:\work\projects\sonar-scanner-msbuild\Tests\SonarScanner.MSBuild.Tasks.IntegrationTest\SonarScanner.MSBuild.Tasks.IntegrationTest.csproj --framework net9.0

