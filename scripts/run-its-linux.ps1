param (
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]
    $Passthrough
)

# Change directory to 'its'
Set-Location -Path "$PSScriptRoot/../its"

# Run Maven with the specified test include pattern
mvn verify -DtestInclude="**/sonarqube/ScannerTest*" @Passthrough
