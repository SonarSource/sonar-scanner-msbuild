
# Create a .properties file that both the msbuild runner and the sonar-runner use
# Remarks: the file must use the system default encoding otherwise the sonar-runner doesn't seem to be able to read it
function CreatePropertiesFile
{
    param([string][ValidateNotNullOrEmpty()]$propertiesFileDir,
		  [string][ValidateNotNullOrEmpty()]$serverUrl,
	      [string]$serverUsername,
		  [string]$serverPassword,
		  [string]$dbUrl,
		  [string]$dbUsername,
		  [string]$dbPassword)
	
    Write-Verbose -Verbose "Creating the .propertiesFile in $propertiesFileDir"
    
	if (![System.IO.Directory]::Exists($propertiesFileDir))
    {
        throw "Cannot find directory: $propertiesFileDir"
    }

    $propertiesFilePath = [System.IO.Path]::Combine($propertiesFileDir, "sonar-runner.properties")
	Remove-Item $propertiesFilePath -ErrorAction SilentlyContinue

     try
     {
        $stream = New-Object System.IO.StreamWriter($propertiesFilePath, $false, ([System.Text.Encoding]::Default))
         
        $stream.WriteLine("sonar.host.url=$serverUrl")
        $stream.WriteLine("sonar.jdbc.url=$dbUrl")            
        $stream.WriteLine("sonar.jdbc.username=$dbUsername")
        $stream.WriteLine("sonar.jdbc.password=$dbPassword")

		if (![String]::IsNullOrEmpty($serverUsername))
		{
			$stream.WriteLine("sonar.login=$serverUsername")
		}

		if (![String]::IsNullOrEmpty($serverPassword))
		{
			$stream.WriteLine("sonar.password=$serverPassword")
		}
    }
    finally
    {
        $stream.Flush()
        $stream.Dispose()
    }
   

	Write-Verbose -Verbose "Created the sonar-runner.properties file at: $propertiesFilePath"
}

function CopyFile
{
    param([string][ValidateNotNullOrEmpty()]$source,
          [string][ValidateNotNullOrEmpty()]$destination)

    # in case the directory doesn't exist create it
	New-Item -ItemType File -Path $destination -Force
	Copy-Item $source $destination -Force
}


# MsBuild will load targets files from some well known locations, namely "$(MSBuildUserExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.targets\ImportBefore\*" 
# (where MSBuildUserExtensionsPath translates to  %localAppData%/Microsoft/MSBuild )
# Since we don't know what msbuild toolset will be used to build the solution we'll copy the targets file in well-known location for msbuild 14, 12 and 4
function CopyTargetsFile
{
    param([string][ValidateNotNullOrEmpty()]$targetsFileSourcePath)
    
	#TODO: need to intercept the msbuild tools version or ask for it from the user or figure out a solution for all cases
	# or as an alternative use CustomBeforeMicrosoftCSharpTargets property which we can pass to msbuild which does not seem to be tied to 14.0
	$localAppDataDir = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)

	$targetsFileDestinationPath14 = [System.IO.Path]::Combine($localAppDataDir, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore", "SonarQube.Integration.ImportBefore.targets")
    $targetsFileDestinationPath12 = [System.IO.Path]::Combine($localAppDataDir, "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore", "SonarQube.Integration.ImportBefore.targets")
    $targetsFileDestinationPath4 = [System.IO.Path]::Combine($localAppDataDir, "Microsoft", "MSBuild", "4.0", "Microsoft.Common.targets", "ImportBefore", "SonarQube.Integration.ImportBefore.targets")

	Write-Verbose -Verbose "Copying the targets file to $targetsFileDestinationPath"

    CopyFile $targetsFileSourcePath $targetsFileDestinationPath14
    CopyFile $targetsFileSourcePath $targetsFileDestinationPath12
    CopyFile $targetsFileSourcePath $targetsFileDestinationPath4
}


# Set a variable in a property bag that is accessible by all steps
# To retrieve the variable use $val = Get-Variable $distributedTaskContext "varName"
function SetTaskContextVaraible
{
    param([string][ValidateNotNullOrEmpty()]$varName, 
          [string][ValidateNotNullOrEmpty()]$varValue)
    
    Write-Host "##vso[task.setvariable variable=$varName;]$varValue"
}

function GetCredentialsFromEndpoint
{
	param([string][ValidateNotNullOrEmpty()]$connectedServiceName, 
	$sonarUrl, 
	$unsername, 
	$password)

	$serviceEndpoint = Get-ServiceEndpoint -Context $distributedTaskContext -Name $connectedServiceName

	if (!$serviceEndpoint)
	{
		throw "A Connected Service with name '$ConnectedServiceName' could not be found.  Ensure that this Connected Service was successfully provisioned using the services tab in the Admin UI."
	}

	$authScheme = $serviceEndpoint.Authorization.Scheme
	if ($authScheme -eq 'UserNamePassword')
	{
		$sonarUrl = $serviceEndpoint.Url
		$username = $serviceEndpoint.Authorization.Parameters.UserName
		$password = $serviceEndpoint.Authorization.Parameters.Password
    
		Write-Host "SonarUrl= $sonarUrl"
		Write-Host "Username= $username"
		# don't print the password as it will be in clear!
	}
	else
	{
		throw "The authorization scheme $serviceEndpoint.Authorization.Scheme is not supported for a SonarQube server."
	}
}









            
            