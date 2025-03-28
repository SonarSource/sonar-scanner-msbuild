param (
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]
    $Passthrough
)

# Check if the SonarScanner for .NET build exists
if (-Not (Test-Path -Path "build/sonarscanner-net.zip")) {
    Write-Host "Build SonarScanner for .NET"
    pwsh scripts/its-build.ps1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build SonarScanner for .NET FAILED."
        exit $LASTEXITCODE
    }
}
else {
    Write-Host "SonarScanner for .NET already built"
}

# Change directory to 'its'
Set-Location -Path "$PSScriptRoot/../its"

# Run Maven with the specified test include pattern
mvn verify -DtestInclude="**/sonarqube/ScannerTest*" @Passthrough
