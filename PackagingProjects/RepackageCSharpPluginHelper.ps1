$payloadFilename = "SonarQube.MSBuild.Runner.Implementation.zip";


#### Logging Helpers ############
function WriteMessage
{
   param([string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $message)
   Write-Host -ForegroundColor Green $message
}

function WriteDetail
{
   param([string][Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()] $message)
   Write-Host -ForegroundColor Gray $message
}

##### Path helpers ##############

function GetWorkingDir()
{
    return Convert-Path . 
}

function GetTempJarFolder()
{
    $sourceDir = GetWorkingDir
    return [System.IO.Path]::Combine($sourceDir, "TempJarDir")
}

function GetBuildOutputDir()
{
    $sourceDir = GetWorkingDir
    return [System.IO.Path]::Combine($sourceDir, "../", "DeploymentArtifacts", "CSharpPlugingPayload", $configuration)
}


##### Preparig the existing jar ##########

#
# Gets the existing jar file. This should be in the working directory.
#
function GetSourceJarPath
{
    
    $sourceDir = GetWorkingDir
    $sourceJarPath = [System.IO.Directory]::GetFiles($sourceDir, "*.jar")

    if ($sourceJarPath.Count -eq 0)
    {
        throw "Before using this script please copy a CSharp plugin (the jar file) to the same directory as the script: $sourceDir"
    }

    if ($sourceJarPath.Count > 1)
    {
        throw "Too many jar files in the same directory as the script. Expecting only one"
    }

    WriteMessage "Found the source plugin jar file: $sourceJarPath"

    return $sourceJarPath
}



#
# Unzips the existing jar file to a well known directory 
#
function UnzipJar
{
    param([Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$sourceJarPath)
    
    $tempJarDir = GetTempJarFolder
    if ([System.IO.Directory]::Exists($tempJarDir))
    {
        Write-Host -ForegroundColor Gray "Removing old $tempJarDir"
        Remove-Item $tempJarDir -Recurse
    }

    try
    {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($sourceJarPath, $tempJarDir)
    }
    catch [System.IO.InvalidDataException]
    {
        throw "A jar file was found but it not apear to be an archive:  $sourceJarPath"
    }

    WriteMessage "Unzipped the jar file to $tempJarDir"
}


#
# Check and delete the existing payload from the unzipped jar 
#
function DeleteExistingPayload
{
    $tempJarDir = GetTempJarFolder
    $tempPayloadZipPath = [System.IO.Path]::Combine($tempJarDir, "static", $payloadFilename);

    if (![System.IO.File]::Exists($tempPayloadZipPath))
    {
        throw "Could not find $payloadFilename inside the C# plugin jar. Did the plugin structure change?"
    }

    [System.IO.File]::Delete($tempPayloadZipPath)

    WriteMessage "Deleted the existing payload"

    return $tempPayloadZipPath
   
}

#
# Orchestrate finding and unzipping the source jar and deleting the payload from it
#
function PrepareExistingJar
{
    $sourceJarPath = GetSourceJarPath
    UnzipJar $sourceJarPath
    return DeleteExistingPayload 
}


##### Create the new jar #####

#
# Ensures the specified config build exists by checking the output dir
#
function EnsureBuild
{
    param([Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$configuration)

    if (($configuration -ne "Debug") -and ($configuration -ne "Release"))
    {
        throw "$configuration is an invalid build configuration. Expecting Debug or Release"
    }

    # TODO: actually invoke msbuild ? 
    $sourcePayloadDirectory = GetBuildOutputDir
    $buildExists = Test-Path "$sourcePayloadDirectory\*"

    if ($buildExists -eq $false) # checks for files inside the directory
    {
        throw "Could not find the deployment package at $sourcePayloadDirectory. Have you built the solution in $configuration ?"    
    }

    WriteMessage "Detected a $configuration build"
}

#
# Zip the new payload inside the prepared jar
#
function ZipNewPayload
{
    param([Parameter(Mandatory=$true)][ValidateNotNullOrEmpty()][string]$payloadPath)
    
    $sourcePayloadDirectory = GetBuildOutputDir
    
    # create the new payload zip inside the expanded payloadPath
    [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePayloadDirectory, $payloadPath, [System.IO.Compression.CompressionLevel]::Optimal, $false);

    WriteMessage "Zipped the new payload..."
}

#
# Re-create the jar and add the date to the filename
#
function CreateNewJar
{
    Write-Host -ForegroundColor Green "Re-creating the jar..."
        
    $sourceJarPath = GetSourceJarPath
    $sourceDir = GetWorkingDir
    $tempJarDir = GetTempJarFolder

    $jarResultPattern = [System.IO.Path]::GetFileNameWithoutExtension($sourceJarPath) + " {0}.jar"

    $newJarFilename = [String]::Format(
        $jarResultPattern, 
        [String]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:MMM.d}", [System.DateTime]::UtcNow));


    $destinationPath = [System.IO.Path]::Combine($sourceDir, "../", "DeploymentArtifacts", $newJarFilename);

    if (Test-Path $destinationPath)
    {
        Write-Host -ForegroundColor Gray "Removing old jar file"
        Remove-Item $destinationPath 
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempJarDir, $destinationPath, [System.IO.Compression.CompressionLevel]::Fastest, $false);

    Write-Host -ForegroundColor Gray "New jar file: $destinationPath"
    Write-Host -BackgroundColor DarkGreen "Success!! - the new jar can be found in ../DeploymentArtifacts"

}

 
