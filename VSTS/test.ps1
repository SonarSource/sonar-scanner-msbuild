$testsRoot = "$PSScriptRoot\Tests\L0"

$tasksRoot = "$PSScriptRoot\build\Temp\Extensions\SonarQube\Tasks"
$beginTaskFolder = "$tasksRoot\ScannerMsBuildBegin"
$endTaskFolder = "$tasksRoot\ScannerMsBuildEnd"

$ErrorActionPreference = "Stop"

if (-Not (Test-Path $beginTaskFolder) -Or 
    -Not (Test-Path $endTaskFolder))
{
    throw "Before running the tests, please, run "".\pack.ps1 [environment] [version]"""
}

# SonarQubeHelper tests
Write-Output "Testing SonarQubeHelper"
& $testsRoot\Common-SonarQubeHelpers\InvokeRestApi.ps1
& $testsRoot\Common-SonarQubeHelpers\IsPRBuild.ps1
& $testsRoot\Common-SonarQubeHelpers\ServerVersion.ps1

# SonarQube Scanner for MSBuild Begin tests
Write-Output "Testing SonarQube Scanner for MSBuild Begin Task"
& $testsRoot\SonarQubeScannerMsBuildBegin\CreateCommandLineArgs.ps1
& $testsRoot\SonarQubeScannerMsBuildBegin\UpdateArgsForPrAnalysis.ps1
& $testsRoot\SonarQubeScannerMsBuildBegin\DisableAnalysisOnPrBuild.ps1

# SonarQube Scanner for MSBuild End tests
Write-Output "Testing SonarQube Scanner for MSBuild End Task"
& $testsRoot\SonarQubeScannerMsBuildEnd\DisableAnalysisOnPrBuild.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\TopLevelOrchestration.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\PRCA\ReportProcessorTests.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\PRCA\PostCommentsTests.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\PRCA\OrchestratorTests.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\PRCA\InvokeTests.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\SonarQubeMetrics.ps1
& $testsRoot\SonarQubeScannerMsBuildEnd\SummaryReport.ps1