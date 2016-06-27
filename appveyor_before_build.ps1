. ./appveyor_helpers.ps1

if (IsPRCABuild)
{
    echo "Running PR-CA build"
    
    $env:JAVA_HOME="C:\Program Files\Java\jdk1.8.0"

    echo "Install the msbuild-sonarqube-runner version 1.0 via Chocolaty"
    choco install msbuild-sonarqube-runner -version 1.0.0.20150831 -y
    CheckLastExitCode
    
    if (Test-Path "c:\sonarqube\")
    {
        Remove-Item c:\sonarqube\pr-analysis-tmp\* -recurse -force 
    }

    echo "Cloning https://github.com/dbolkensteyn/pr-analysis.git"

    # It's very important to have -q here because git spews out info messages to the error stream and it confuses powershell
    git clone https://github.com/dbolkensteyn/pr-analysis.git c:\sonarqube\pr-analysis-tmp -q
    CheckLastExitCode

    echo "Starting SonarQube"
    StartSonarQubeServer "c:\sonarqube\pr-analysis-tmp\sonarqube-5.1.2"
    WaitForSonarQubeToHaveStarted

    echo "Starting the analysis"
    MSBuild.SonarQube.Runner.exe begin /k:foo /n:foo /v:1.0 /d:sonar.analysis.mode=preview /d:sonar.github.pullRequest=%APPVEYOR_PULL_REQUEST_NUMBER% /d:sonar.github.repository=%APPVEYOR_REPO_NAME% /d:sonar.github.login=SonarLint /d:sonar.github.oauth=8d9ce27732443bf109f
}



