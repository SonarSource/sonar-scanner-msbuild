# SonarQubeHelper tests
& $PSScriptRoot\L0\Common-SonarQubeHelpers\InvokeRestApi.ps1
& $PSScriptRoot\L0\Common-SonarQubeHelpers\IsPRBuild.ps1
& $PSScriptRoot\L0\Common-SonarQubeHelpers\ServerVersion.ps1

# SonarQube Scanner for MSBuild Begin tests
& $PSScriptRoot\L0\SonarQubeScannerMsBuildBegin\CreateCommandLineArgs.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildBegin\UpdateArgsForPrAnalysis.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildBegin\DisableAnalysisOnPrBuild.ps1

# SonarQube Scanner for MSBuild End tests
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\DisableAnalysisOnPrBuild.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\TopLevelOrchestration.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\PRCA\ReportProcessorTests.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\PRCA\PostCommentsTests.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\PRCA\OrchestratorTests.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\PRCA\InvokeTests.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\SonarQubeMetrics.ps1
& $PSScriptRoot\L0\SonarQubeScannerMsBuildEnd\SummaryReport.ps1