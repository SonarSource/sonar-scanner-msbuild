[CmdletBinding(DefaultParameterSetName = 'None')]
param(
	# The chosen Service Endpoint name, we will extract the Url, Username and Password from this 
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $connectedServiceName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectKey,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectName,
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $projectVersion,	
    [string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $sources,	
    [string]$configFile
)

. $PSScriptRoot\SonarQubeHelper.ps1

# Retrieves the url, username and password from the specified generic endpoint.
# Only UserNamePassword authentication scheme is supported for SonarQube.
function GetEndpointData
{
	param([string][ValidateNotNullOrEmpty()]$connectedServiceName)

	$serviceEndpoint = Get-ServiceEndpoint -Context $distributedTaskContext -Name $connectedServiceName

	if (!$serviceEndpoint)
	{
		throw "A Connected Service with name '$ConnectedServiceName' could not be found. Ensure that this Connected Service was successfully provisioned using the Services tab in the Admin UI."
	}

	$authScheme = $serviceEndpoint.Authorization.Scheme
	if ($authScheme -ne 'UserNamePassword')
	{
		throw "The authorization scheme $authScheme is not supported for a SonarQube server."
	}

    return $serviceEndpoint
}

function CreateCommandLineArgs
{
    param(
          [ValidateNotNullOrEmpty()][string]$projectKey,
          [ValidateNotNullOrEmpty()][string]$projectName,
          [ValidateNotNullOrEmpty()][string]$projectVersion,
          [string]$serverUrl,
	      [string]$serverUsername,
		  [string]$serverPassword,
		  [string]$sources,
          [string]$configFile
    )

    $sb = New-Object -TypeName "System.Text.StringBuilder"; 

    [void]$sb.Append(" -Dsonar.projectKey=" + (EscapeArg($projectKey)));

	[void]$sb.Append(" -Dsonar.projectName=" + (EscapeArg($projectName)));

	[void]$sb.Append(" -Dsonar.projectVersion=" + (EscapeArg($projectVersion)));

    if ([String]::IsNullOrWhiteSpace($serverUrl))
    {   
        throw "Please setup a SonarQube Server endpoint and specify the SonarQube Url as the Server Url" 
	}

	[void]$sb.Append(" -Dsonar.host.url=" + (EscapeArg($serverUrl))) 

    if (![String]::IsNullOrWhiteSpace($serverUsername))
    {
        [void]$sb.Append(" -Dsonar.login=" + (EscapeArg($serverUsername))) 
    }

    if (![String]::IsNullOrWhiteSpace($serverPassword))
    {
        [void]$sb.Append(" -Dsonar.password=" + (EscapeArg($serverPassword))) 
    }

    [void]$sb.Append(" -Dsonar.sources=" + (EscapeArg($sources))) 
    [void]$sb.Append(" -Dsonar.projectBaseDir=" + (EscapeArg($(Get-Item -Path ".\" -Verbose).FullName))) 

    if (IsFilePathSpecified $configFile)
    {
        if (![System.IO.File]::Exists($configFile))
        {
            throw "Could not find the specified configuration file: $configFile" 
        }

        [void]$sb.Append(" -Dproject.settings=" + (EscapeArg($configFile))) 
    }

    return $sb.ToString();
}

# During PR builds only an "issues mode" analysis is allowed. The resulting issues are posted as code review comments. 
# The feature can be toggled by the user and is OFF by default.  
ExitOnPRBuild

$serviceEndpoint = GetEndpointData $connectedServiceName
Write-Verbose "Server Url: $($serviceEndpoint.Url)"

$arguments = CreateCommandLineArgs $projectKey $projectName $projectVersion $serviceEndpoint.Url $serviceEndpoint.Authorization.Parameters.UserName $serviceEndpoint.Authorization.Parameters.Password $sources $configFile
Write-Verbose "Arguments: $arguments"

$scannerPath = [System.IO.Path]::Combine($PSScriptRoot, "sonar-scanner\bin\sonar-scanner.bat")

Invoke-BatchScript $scannerPath -Arguments $arguments
