. ./appveyor_helpers.ps1

####################### Local run (Debug) configuration ###################################

# Set these input variable that would normally come from AppVeyor yml
if ($LOCAL_DEBUG_RUN)
{      
    $env:SQ_Version = "5.2"   
    $env:MSBuild_Version = "14.0"
    $env:APPVEYOR_BUILD_FOLDER = "C:\Users\bgavril\source\repos\sonar-msbuild-runner"
    $env:configuration = "Debug"

    # This script does not build the C# plugin (unlike appveyor) - please provide the path to it for the debug run
    $localCSharpPluginPath = $env:APPVEYOR_BUILD_FOLDER + "\DeploymentArtifacts\sonar-csharp-plugin-4.2.Debug.bgavril.jar";
}

####################### Settings ###################################

# URL prefix from where to download SonarQube (nb: building SQ takes too long)
$sonarQubeDownloadUrlPrefix = "https://sonarsource.bintray.com/Distribution/sonarqube/sonarqube-";
# Where the SonarQube zip files are downloaded (easy to cache)
$sonarQubeDownloadPath = "c:\sonarqube\downloads";
# Path to the SonarQube server - can be cached but then server would not be clean
$sonarQubeInstallPath = "c:\sonarqube";
# By default the server can be reached at localhost:9000. Using localhost. is for Fiddler to be able to capture traffic. 
$sonarQubeBaseUrl = "http://localhost.:9000";
# Key for the project under test
$sqProjectKey = "ProjectUnderTest";
# Name of the quality profile used for analysis
$testQualityProfileName = "ProfileForTest";

#
# True for versions 5+
#
function IsVersion5OrGreater
{
    $majorVersion = $env:SQ_Version.Split('.')[0]
    return $majorVersion -cge 5
}

#
# Download and unzip SonarSqube to a well-known directory. 
#
# Remark: when performing a local run, do nothing if the directory exists 
#
function FetchAndUnzipSonarQube
{
    $sqVersion = $env:SQ_Version;
    $sqUrl = $sonarQubeDownloadUrlPrefix + $sqVersion + ".zip";    
    $sqInstallPath = [System.IO.Path]::Combine($sonarQubeInstallPath, "sonarqube-" + $sqVersion)
    
    if ($LOCAL_DEBUG_RUN -and [System.IO.Directory]::Exists($sqInstallPath))
    {
        LogAppveyorMessage -Message "SonarQube $version is already present in $sqInstallPath"
        
    }
    else
    {
        if ([System.IO.Directory]::Exists($sqInstallPath))
        {
            throw "Not expecting the directory $sqInstallPath to exist"
        }

        LogAppveyorMessage -Message "Downloading SonarQube from $sqUrl"
    
        $file = [System.IO.Path]::Combine($sonarQubeDownloadPath, "sonarqube-" + $sqVersion + ".zip")               
        FetchAndUnzip $sqUrl $sonarQubeInstallPath $file $false
    }

    return $sqInstallPath;
}


#
# Adds the C# plugin to the list of Appveyor artifacts
#
function PublishPatchedCsharpPlugin
{    
    if ($LOCAL_DEBUG_RUN)
    {
        return $localCSharpPluginPath;
    }

    $pluginPath = FindSingleFile ([System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "DeploymentArtifacts")) "*.jar"    
    Push-AppveyorArtifact $pluginPath 

    return $pluginPath;
}

#
# Copy the C# plugin to the SQ plugins directory
#
function InstallCsPlugin
{
    param ([Parameter(Mandatory=$true)][string]$sqServerPath, [Parameter(Mandatory=$true)][string]$csPluginPath) 

    if (![System.IO.File]::Exists($csPluginPath))
    {
        throw "Could not find the C# plugin at $csPluginPath"
    }

    $destination = [System.IO.Path]::Combine($sqServerPath, "extensions", "plugins", [System.IO.Path]::GetFileName($csPluginPath)); 
    [System.IO.File]::Copy($csPluginPath, $destination, $true);
}




function WaitForSonarQubeToHaveStarted
{
    $command = { InvokeGetRestMethod "/api/projects/index?search=projectundertest"} 
    Retry $command -maxRetries 30 -retryDelay 1 -Verbose
}

function InvokeGetRestMethod
{
    param ([Parameter(Mandatory=$true)][string]$query,  [bool]$asAdmin=$false)

    $request = $sonarQubeBaseUrl + $query;
    
    if ($asAdmin)
    {
       $authHeader = GetSonarQubeAdminAuthHeader
       $allheaders = @{Authorization = $authHeader}   
    }  

    $response = Invoke-RestMethod $request -Method Get -TimeoutSec 30 -Headers $allheaders

    return $response
}

function InvokePostRestMethod
{
    param ([Parameter(Mandatory=$true)][string]$query, [bool]$asAdmin=$false)

    $requestUrl = $sonarQubeBaseUrl +  $query;
    Write-Host "POST $requestUrl"

    if ($asAdmin)
    {
       $authHeader = GetSonarQubeAdminAuthHeader
       $allheaders = @{Authorization = $authHeader}   
    }    

    $response = Invoke-RestMethod $requestUrl -Method Post -TimeoutSec 30 -Headers $allheaders
    

    return $response
}

#
# Upload a file
#
# Remark: Multipart upload is not supported out of the box by powershell's Invoke-RestMethod, but System.Net.Http can do it
#
function InvokeUpload {
     param ([Parameter(Mandatory=$true)][string]$query, [Parameter(Mandatory=$true)][string]$formFieldName, [Parameter(Mandatory=$true)][String]$fileToUpload)
    
    [System.Reflection.Assembly]::LoadWithPartialName('System.Net.Http') | Out-Null

    $requestUrl = $sonarQubeBaseUrl + $query;   

    try
    {
        $fileContent = [System.IO.File]::ReadAllText($fileToUpload);
        $client = (New-Object System.Net.Http.HttpClient)
        $stringContent = New-Object System.Net.Http.StringContent @(,$fileContent)
        
        $basicAuthValue = GetSonarQubeAdminAuthHeader
        $client.DefaultRequestHeaders.Add("Authorization", $basicAuthValue)


        $formData = New-Object System.Net.Http.MultipartFormDataContent
        $formData.Add($stringContent, $formFieldName, "UNUSED");
        
        return $client.PostAsync($requestUrl, $formData).Result
        
    }
    finally
    {
        $client.Dispose()
        $formData.Dispose()        
    }
}

#
# Gets the default admin:admin basic auth header that can be used in APIs that require elevation
#
function GetSonarQubeAdminAuthHeader
{
        $user = "admin"
        $pass= "admin"

        $pair = "$($user):$($pass)"

        $encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
        $basicAuthValue = "Basic $encodedCreds"

        return $basicAuthValue        
}

function UploadTestQualityProfile
{
    echo "Finding the quality profile on disk"
    $qualityProfile = FindSingleFile ([System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "Tests", "IntegrationTestProject")) "TestQualityProfile.xml"

    $response = InvokeUpload "/api/profiles/restore" "backup" $qualityProfile
}


function SetTestQualityProfileAsDefault
{
    if (IsVersion5OrGreater)
    {
        $response = InvokePostRestMethod "/api/qualityprofiles/set_default?language=cs&profileName=$testQualityProfileName" $true            
    }
    else
    {
        $response = InvokePostRestMethod "/api/profiles/set_as_default?language=cs&name=$testQualityProfileName" $true    
    }
}

function VerifyAnalysisResults
{    
    WaitForAllBackgroundTasksToComplete

    $response = InvokeGetRestMethod "/api/issues/search?hideRules=true"

<# SONARMBRU-212: re-enable once the C# profile exporter returns information for SonarLint.
   Currently it doesn't, which means we get different results when building in MSBuild 12 and 14.
   In MSBuild 12, the SonarLint rules are still run post-process by the SonarLint.Runner,
   but in MSBuild 14 they are not.

    # Check only the expected rules were broken
    $expectedRuleViolations = @("fxcop:DoNotPassLiteralsAsLocalizedParameters", "fxcop:DoNotRaiseReservedExceptionTypes", "csharpsquid:S2228", "csharpsquid:S1134")

    $brokenRules = $response.issues.rule
    $ruleDiff = Compare-Object $response.issues.rule $expectedRuleViolations

    Assert ($ruleDiff.Length -eq 0) "Expected 4 rules to be broken. Rules found: $brokenRules. Expected: $expectedRuleViolations"
    Assert ($response.total -eq 4) ("There should only be 4 issues. Actual: " + $response.total)
#>
    
    foreach ($issue in $issues)
    {
        Assert ($issue.project -eq $sqProjectKey) "An issue was found not belonging to the project under test. Instead, it belongs to " + $issue.project
    }
}

function WaitForAllBackgroundTasksToComplete
{
    # Async execution happens on SQ 5+ only
    if (IsVersion5OrGreater)
    {
        $tasksInQueue = $true        
        while ($tasksInQueue)
        {
            $response = InvokeGetRestMethod "/api/ce/queue" $true
            $validResponse = $response.PSobject.Properties.name -match "tasks"

            if (!$validResponse)
            {
                throw "The internal API /api/ce/queue did not produce the expected response"
            }

            if ($response.tasks)
            {
                Start-Sleep -s 1
            }
            else
            {
                $tasksInQueue = $false
            }
        }
    }
}

function BuildAndAnalyzeProjectUnderTest
{
    echo "Step 5.1: Finding msbuild"
    $msbuildPath = FindLatestMsBuildPath
    echo "Using the latest msbuild found from $msbuildPath"

    echo "Step 5.2: Locating the MSBuild.SonarQube.Runner.exe"
    $bootstrapperPath = FindSingleFile ([System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "DeploymentArtifacts", "BuildAgentPayload", $env:configuration)) "MSBuild.SonarQube.Runner.exe"
    
    echo "Step 5.3: Locating the project under test"
    $projectUnderTestPath = FindSingleFile ([System.IO.Path]::Combine($env:APPVEYOR_BUILD_FOLDER, "Tests", "IntegrationTestProject")) "ProjectUnderTest.sln"

    echo "Step 5.4: Waiting for SQ to have started"
    WaitForSonarQubeToHaveStarted
        
    echo "Step 5.5: Upload the test quality profile and make it default"
    UploadTestQualityProfile
    SetTestQualityProfileAsDefault

    echo "Step 5.6: Begining analysis"
    RunProcessAndWaitForFinish $bootstrapperPath @("begin", "/k:$sqProjectKey", "/n:$sqProjectKey", "/v:1")

    echo "Step 5.7: Building the project under test"
    RunProcessAndWaitForFinish $msbuildPath @($projectUnderTestPath, "/v:n")

    echo "Step 5.8: Ending analysis"
    RunProcessAndWaitForFinish $bootstrapperPath "end"

}

function RunContinousIntegrationTest
{
    echo "Step 1: Publishing the CS plugin as a build artifact"
    $csPluginPath = PublishPatchedCsharpPlugin

    echo "Step 2: Installing SonarQube"
    $sqInstallPath = FetchAndUnzipSonarQube

    echo "Step 3: Installing the CS plugin"
    InstallCsPlugin $sqInstallPath $csPluginPath

    echo "Step 4: Starting the SonarQube server"
    StartSonarQubeServer $sqInstallPath

    echo "Step 5: Building and analyzing the project under test"
    BuildAndAnalyzeProjectUnderTest

    echo "Step 6: Verifing analysis results"
    VerifyAnalysisResults

    echo "Validation Complete!"
    #StopSonarQubeServer
}



if (IsPRCABuild)
{    
    MSBuild.SonarQube.Runner.exe end
}
else
{
    RunContinousIntegrationTest    
}

