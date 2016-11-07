[CmdletBinding()]
param()

. $PSScriptRoot\..\..\lib\Initialize-Test.ps1


# This test ensures the top level functionality of the task is invoked


# Arrange   
Register-Mock InvokeMSBuildRunnerPostTest 
Register-Mock CreateAndUploadReport 
Register-Mock BreakBuildOnQualityGateFailure 
Register-Mock IsFeatureEnabled {$true}
Register-Mock HandleCodeAnalysisReporting
Register-Mock Import-Module

# Act
. $PSScriptRoot\..\..\..\Tasks\SonarQubeScannerMsBuildEnd\SonarQubePostTest.ps1    

# Assert
Assert-WasCalled InvokeMSBuildRunnerPostTest
Assert-WasCalled CreateAndUploadReport
Assert-WasCalled BreakBuildOnQualityGateFailure
Assert-WasCalled HandleCodeAnalysisReporting

Unregister-Mock InvokeMSBuildRunnerPostTest 
Unregister-Mock CreateAndUploadReport 
Unregister-Mock BreakBuildOnQualityGateFailure 
Unregister-Mock IsFeatureEnabled 
Unregister-Mock HandleCodeAnalysisReporting
Unregister-Mock Import-Module
