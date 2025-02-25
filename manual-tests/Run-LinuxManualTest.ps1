param (
    [int]$Timeout = 60,
    [string]$SonarToken="squ_bfc537f7ccf92a3f7cd2c22710c97939e83b45de"
)

Push-Location "$PSScriptRoot"
try {
    $scannerNetPath = wsl wslpath -a ../build/sonarscanner-net
    docker run `
        --env TIMEOUT=$Timeout `
        --env SONAR_TOKEN=squ_bfc537f7ccf92a3f7cd2c22710c97939e83b45de `
        --volume ${scannerNetPath}:/scanner-net `
        --network host `
        --rm `
        linux-manual-test
}
finally {
    Pop-Location
}
