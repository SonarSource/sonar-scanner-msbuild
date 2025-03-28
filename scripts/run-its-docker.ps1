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
    --volume ${sonarHome}:/home/sonar/.sonar `
    --volume its-target:/app/its/target `
    $fullImageName `
    ./scripts/run-its-linux.ps1 -PdockerIts
