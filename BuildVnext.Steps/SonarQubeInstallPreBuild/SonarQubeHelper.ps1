
function CreatePropertiesFile
{
    param([ValidateNotNullOrEmpty()][string]$propertiesFileDir,
		  [ValidateNotNullOrEmpty()][string]$sonarServerUrl,
		  [string]$sonarDbUrl,
		  [string]$sonarDbUsername,
		  [string]$sonarDbPassword)
	
	if (![System.IO.Directory]::Exists($propertiesFileDir))
    {
        throw "Cannot find directory: $propertiesFileDir"
    }

    $propertiesFilePath = [System.IO.Path]::Combine($propertiesFileDir, "sonar-runner.properties")
	Remove-Item $propertiesFilePath -ErrorAction SilentlyContinue

	try
	{
		$stream = New-Object System.IO.StreamWriter($propertiesFilePath, $false, [System.Text.Encoding]::Default);
		$stream.WriteLine("sonar.host.url=$sonarServerUrl")
		$stream.WriteLine("sonar.jdbc.url=$sonarDbUrl")
		$stream.WriteLine("sonar.jdbc.username=$sonarDbUsername")
		$stream.WriteLine("sonar.jdbc.password=$sonarDbPassword")

	}
	finally
	{
		if ($stream -ne $NULL)
		{
			$stream.Dispose()
		}
	}


	#"sonar.host.url=$sonarServerUrl"               > $propertiesFilePath   # SilentlyContinue text to the new file 
	#"sonar.jdbc.url=$sonarDbUrl"                   >> $propertiesFilePath # append text to an existing file
	#"sonar.jdbc.username=$sonarDbUsername"         >> $propertiesFilePath
	#"sonar.jdbc.password=$sonarDbPassword"         >> $propertiesFilePath

	Write-Verbose -Verbose "Created the sonar-runner.properties file at: $propertiesFilePath"
}




            
            