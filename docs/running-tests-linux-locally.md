# Running Tests on Linux Locally

There are currently two ways to run tests on Linux locally:

- Using WSL (Windows Subsystem for Linux)
- Using Docker (via WSL)

The provided scripts should be run from Windows.

Both methods will run unit tests by default. To run integration tests, use the `-Its` switch:

For WSL:
```pwsh
scripts/run-test-wsl.ps1 -Its
```

For Docker:
```pwsh
scripts/run-test-docker.ps1 -Its
```

## Preparing for Integration Tests

Before running integration tests using either method, ensure the scanner is built and packaged.
You can accomplish this by using the `scripts/its-build.ps1` script.

## Running Tests with WSL

### Prerequisites

To run tests using WSL, ensure the following are installed on your WSL instance:

- .NET SDK 9
- Maven
- Java 17
- Unzip
- PowerShell

### Execution

To execute the tests within WSL, use the `scripts/run-test-wsl.ps1` script. This script will:

- Detect if it is being run from Windows and, if so, invoke itself within WSL.
- Handle the configuration of all necessary environment variables.
- Run the integration tests

## Running Tests with Docker

### Prerequisites

Ensure Docker is installed on your system. To install Docker, you must first install [WSL](https://learn.microsoft.com/en-us/windows/wsl/install). Once WSL is installed, you can install Docker using the following commands:

```sh
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER # To avoid needing to use `sudo` with each Docker command
sudo apt-get update && sudo apt-get install docker-compose-plugin
```

### Execution

To execute the tests within Docker, use the `scripts/run-test-docker.ps1` script. This script will:

- Automatically build the Docker image if it has not been built already.
- Run the integration tests
