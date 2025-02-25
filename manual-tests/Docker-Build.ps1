Push-Location "$PSScriptRoot"

try {
    docker build -f Dockerfile -t linux-manual-test .
}
finally {
    Pop-Location
}


