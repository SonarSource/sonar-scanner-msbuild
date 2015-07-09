param([string]$buildConfiguration="Debug")

# import GAC assembly
Add-Type -AssemblyName "System.IO.Compression.FileSystem"

if (($buildConfiguration -ne "Debug") -and ($buildConfiguration -ne "Release"))
{
    throw "$buildConfiguration is an invalid build configuration. Expecting Debug or Release"
}

#### Logging Helpers ############
function WriteMessage
{
   param([string][Parameter(Mandatory=$true)] $message)
   Write-Host -ForegroundColor Green $message
}

function WriteImporantMessage
{
   param([string][Parameter(Mandatory=$true)] $message)

   # Because this script is executed by [VS-hosted] msbuild emphasize messages by adding newlines
   Write-Host " "
   Write-Host $message
   Write-Host " "
}

##### Zip Helpers ##############

#
# The Folder.CopyHere method for Shell Objects allows configuration based on a combination of flags.
# Docs here: https://msdn.microsoft.com/en-us/library/windows/desktop/bb787866(v=vs.85).aspx
# The value bellow (1556) consists of
#    (4)    - no progress dialog
#    (16)   - respond with "yes to all" to any dialog box
#    (512)  - Do not confirm the creation of a new directory
#    (1024) - Do not display an UI in case of error
$CopyHereOptions = 1556

#
# Adds a file to a zip archive using the windows shell. If the archive does not exist it gets created.
#
# Remarks: 
#    1. The .net API to zip a folder does not create a proper jar file (SQ crashes) but the shell zipping seems to work 
#    3. The $zipfilename param must be a full path file with the .zip extension
#    2. The code is based on:
#       http://blogs.msdn.com/b/daiken/archive/2007/02/12/compress-files-with-windows-powershell-then-package-a-windows-vista-sidebar-gadget.aspx
function ZipViaShell
{
	param([string]$zipfilename, [string]$sourceDir)

	if(-not (test-path($zipfilename)))
	{
		set-content $zipfilename ("PK" + [char]5 + [char]6 + ("$([char]0)" * 18))
		(dir $zipfilename).IsReadOnly = $false	
	}
	
	$shellApplication = new-object -com shell.application
	$zipPackage = $shellApplication.NameSpace($zipfilename)
	
    $files = Get-ChildItem $sourceDir
    foreach ($fileOrDir in $files)
    {
        $zipPackage.CopyHere($fileOrDir.FullName, $CopyHereOptions) 
        Start-sleep -milliseconds 500 
    }
}


##### Path helpers ##############

function GetWorkingDir()
{
    return $PSScriptRoot
}

function GetTempJarFolder()
{
    $sourceDir = GetWorkingDir
    return [System.IO.Path]::Combine($sourceDir, "TempJarDir")
}

function GetBuildOutputDir()
{
    $sourceDir = GetWorkingDir
    $path= [System.IO.Path]::Combine($sourceDir, "../../", "DeploymentArtifacts", "CSharpPluginPayload", $buildConfiguration)
    return Resolve-Path $path
}

function GetNewJarDestinationDir()
{
    $sourceDir = GetWorkingDir
    $path = [System.IO.Path]::Combine($sourceDir, "../../", "DeploymentArtifacts")
    return Resolve-Path $path
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
        WriteImporantMessage "Before using this script please copy a CSharp plugin (the jar file) to the same directory as the script: $sourceDir"
		exit
    }

    if ($sourceJarPath.Count > 1)
    {
        WriteImporantMessage "Too many jar files in the same directory as the script. Expecting only one"
		exit 
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
	$payloadFilename = "SonarQube.MSBuild.Runner.Implementation.zip";

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
    $sourcePayloadDirectory = GetBuildOutputDir
    $buildExists = Test-Path "$sourcePayloadDirectory\*"

    if ($buildExists -eq $false) # checks for files inside the directory
    {
        throw "Could not find the deployment package at $sourcePayloadDirectory. Have you built the solution in $buildConfiguration ?"    
    }

    WriteMessage "Detected a $buildConfiguration build"
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

    $newJarFilename = [System.IO.Path]::GetFileNameWithoutExtension($sourceJarPath) + "." + $env:USERNAME + ".zip"

	$destinationDir = GetNewJarDestinationDir
    $destinationPath = [System.IO.Path]::Combine($destinationDir, $newJarFilename);

    if (Test-Path $destinationPath)
    {
        Write-Host -ForegroundColor Gray "Removing old jar file"
        Remove-Item $destinationPath 
    }

    ZipViaShell $destinationPath $tempJarDir

    $destinatioPathAsJar = [System.IO.Path]::ChangeExtension($destinationPath, ".jar")
    if (Test-Path $destinatioPathAsJar)
    {
        Remove-Item $destinatioPathAsJar 
    }

    [System.IO.File]::Move($destinationPath, $destinatioPathAsJar);


    WriteImporantMessage "Success!! The jar file can be found in $destinationDir"
    
}

WriteMessage "Starting the packing script using the $buildConfiguration configuration"

$payloadPath = PrepareExistingJar
EnsureBuild
ZipNewPayload $payloadPath
CreateNewJar 




