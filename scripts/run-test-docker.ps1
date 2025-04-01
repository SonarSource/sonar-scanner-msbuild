param (
    [switch]
    $BuildImage = $false,
    [switch]
    $Its = $False,
    [string]
    $TestToRun
)

# Define the image name and tag
$imageName = "scanner-msbuild-its-docker"
$imageTag = "latest"
$fullImageName = "${imageName}:$imageTag"

# Check if the image exists
$imageExists = docker images --quiet $fullImageName

$scannerRoot = "$PSScriptRoot/.." -replace '\\', '/'
$scannerRoot = wsl wslpath -a "$scannerRoot"
$sonarHome = (Join-Path -Path $Env:USERPROFILE -ChildPath ".sonar") -replace '\\', '/'
$sonarHome = wsl wslpath -a "$sonarHome"
$mavenHome = (Join-Path -Path $Env:USERPROFILE -ChildPath ".m2") -replace '\\', '/'
$mavenHome = wsl wslpath -a "$mavenHome"

if (-Not $imageExists -or $BuildImage) {
    $dockerfilePath = "$PSScriptRoot/Dockerfile" -replace '\\', '/'
    $dockerfilePath = wsl wslpath -a "$dockerfilePath"
    Write-Host "Docker image '$fullImageName' does not exist. Building the image..."
    docker build --tag $fullImageName $scannerRoot --file $dockerfilePath
}
else {
    Write-Host "Docker image '$fullImageName' already exists."
}

$TestToRun =  $null -ne $TestToRun ? $TestToRun : ($Its ? "IT" : "UT")

# Run the container
docker run `
    --tty `
    --rm `
    --workdir /app `
    --user ($Its ? "sonar" : "root") `
    --volume ${scannerRoot}:/app `
    --volume ${mavenHome}:/home/sonar/.m2 `
    --volume ${sonarHome}:/home/sonar/.sonar `
    --volume nuget-cache:/root/.nuget `
    --volume its-target:/app/its/target `
    --env ARTIFACTORY_USER=$env:ARTIFACTORY_USER `
    --env ARTIFACTORY_PASSWORD=$env:ARTIFACTORY_PASSWORD `
    $fullImageName `
    ./scripts/run-test-linux.ps1 -TestToRun $TestToRun
