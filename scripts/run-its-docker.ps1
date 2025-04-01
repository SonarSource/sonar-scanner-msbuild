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

if (-Not $imageExists) {
    $dockerfilePath = "$PSScriptRoot/Dockerfile" -replace '\\', '/'
    $dockerfilePath = wsl wslpath -a "$dockerfilePath"
    Write-Host "Docker image '$fullImageName' does not exist. Building the image..."
    docker build --tag $fullImageName $scannerRoot --file $dockerfilePath
}
else {
    Write-Host "Docker image '$fullImageName' already exists."
}

# Run the container
docker run `
    --rm `
    --workdir /app `
    --volume ${scannerRoot}:/app `
    --volume ${mavenHome}:/home/sonar/.m2 `
    --volume ${sonarHome}:/home/sonar/.sonar `
    --volume its-target:/app/its/target `
    --env ARTIFACTORY_USER=$env:ARTIFACTORY_USER `
    --env ARTIFACTORY_PASSWORD=$env:ARTIFACTORY_PASSWORD `
    $fullImageName `
    ./scripts/run-its-linux.ps1
