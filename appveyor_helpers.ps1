$ErrorActionPreference = "Stop"
$mavenLocalRepository = "$env:USERPROFILE\.m2\repository"

function CheckLastExitCode
{
    param ([int[]]$SuccessCodes = @(0))

    if ($SuccessCodes -notcontains $LastExitCode)
	{
        $msg = @"
EXE RETURNED EXIT CODE $LastExitCode
CALLSTACK:$(Get-PSCallStack | Out-String)
"@
        throw $msg
    }
}

function FetchAndUnzip
{
    param ([Parameter(Mandatory=$true)][string]$url, 
           [Parameter(Mandatory=$true)][string]$unzipDir, 
           [string]$downloadPath, 
           [bool]$overwrite=$true)

	if ([String]::IsNullOrEmpty($downloadPath))
	{
		$downloadPath = [System.IO.Path]::GetTempFileName()
	}

    LogAppveyorMessage "Downloading archive from $url to $downloadPath and unzipping to $unzipDir"
	
    $downloadDir = [System.IO.Path]::GetDirectoryName($downloadPath)
    if (-not(Test-Path $downloadDir))
	{
		mkdir $downloadDir | Out-Null
	}

    try
    {
        $clnt = new-object System.Net.WebClient        
        
        if (![System.IO.File]::Exists($downloadPath) -or $overwrite)
        {
            $clnt.DownloadFile($url,$downloadPath)
        }
    }
    catch [System.Exception]
    {        
        $innerException = $_.Exception.innerexception
        throw "Could not download $url because $innerException"
    }
    finally 
    {
        $clnt.Dispose()
    }

    if (-not(Test-Path $unzipDir))
	{
		mkdir $unzipDir | Out-Null
	}
	[System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
	[System.IO.Compression.ZipFile]::ExtractToDirectory($downloadPath, $unzipDir)
}


function DownloadAndBuildFromGitHub
{
	param ([string]$Project, [string]$Sha1)


	$url = "https://github.com/$Project/archive/$Sha1.zip"
	$tmp = "c:\snapshot"

    echo ("Fetching [" + $Project + ":" + $Sha1 + "] by downloading $url to $tmp")
    

	if (Test-Path $tmp)
	{
		Cmd /C "rmdir /S /Q $tmp"
	}

	FetchAndUnzip $url $tmp "" $true

	echo ("Build [" + $Project + ":" + $Sha1 + "]")	

	pushd $tmp\*

	try
	{
		# remove -q to see the build details
		mvn install "--batch-mode" "-DskipTests" "-q"
		CheckLastExitCode
	}
	finally
	{
		popd
	}
}

function FindSingleFile
{
    param ([string]$dir, [string]$fileMask)
 
    $files = [System.IO.Directory]::GetFiles($dir, $fileMask, [System.IO.SearchOption]::AllDirectories);
    $fileCount = $files.Count;

    if ( $fileCount -ne 1)
    {
        throw "Expecting to find a single file $fileMask under $dir but found $fileCount"
    }

    return $files[0];
}

#
# Adds a message to the Appveyor "Messages" tab. 
# 
function LogAppveyorMessage
{
    param ([string]$message)
    
    if ($LOCAL_DEBUG_RUN)
    {
        return;
    }

    Add-AppveyorMessage $message
}

#
# Locates msbuild by version. Throws if that version of msbuild cannot be found
#
function FindMSBuildPath
{
    param ([Parameter(Mandatory=$true)][string]$version)    

    $msbuilDir = Resolve-Path HKLM:\SOFTWARE\Microsoft\MSBuild\ToolsVersions\$version -ErrorAction SilentlyContinue | Get-ItemProperty -Name MSBuildToolsPath 
    if ([System.String]::IsNullOrWhiteSpace($msbuilDir.MSBuildToolsPath))
    {
        throw "Could not find msbuild version $version"
    }

    return ([System.IO.Path]::Combine($msbuilDir.MSBuildToolsPath, "msbuild.exe"));
}

function FindLatestMsBuildPath
{

    $latestMsbuild = 
        Get-ChildItem -Path "HKLM:\SOFTWARE\Wow6432Node\Microsoft\MSBuild\ToolsVersions\" | # get all versions of msbuild
        Where { $_.Name -match '\\\d+.\d+$' } | # but only those that have a version in the style "d.d" (all major ones do)
        Sort-Object -property  @{Expression={[System.Convert]::ToDecimal($_.Name.Substring($_.Name.LastIndexOf("\") + 1))}} -Descending |  # order by version as a number
        Select-Object -First 1 | 
        Get-ItemProperty -Name MSBuildToolsPath 

    return  ([System.IO.Path]::Combine($latestMsbuild.MSBuildToolsPath, "msbuild.exe"));
    
}

#
# Runs the process and waits for it to finish. If the process fails the whole script will fail. 
#
function RunProcessAndWaitForFinish
{
    param ([string]$processPath, [string[]]$arguments)    

    "Executing $processPath with args $arguments"
    $sw = [Diagnostics.Stopwatch]::StartNew()

    # Start process $processPath and redirect both std out and std err to the current std out / std err
    & $processPath $arguments 2>&1 | Out-Host

    $sw.Stop()
    "Completed after " + $sw.ElapsedMilliseconds + "ms"
}

#
# Retry a specific command
#
function Retry
{
    [CmdletBinding()]
    param (    
    [Parameter(ValueFromPipeline,Mandatory)]$command,
    [int]$maxRetries = 3, 
    [int]$retryDelay = 1)    

    $success = $false
    $attemptNumber = 1

    while (!$success)
    {
        try
        {
            Write-Verbose "Calling $command"
            $result = & $command
            $success = $true
        }
        catch
        {
            if ($attemptNumber -ige $maxRetries) 
            {
                Write-Verbose "Calling $command failed after $attemptNumber retries"
                throw
            }
            else
            {
                Write-Verbose "Calling $command failed. Attempt number $attemptNumber. Retrying after $retryDelay seconds..."
                Start-Sleep $retryDelay
                $attemptNumber++
            }
        }
    }

    return $result
}

function Assert
{
    param ([bool]$condition, [string]$message)

    if (!$condition)
    {
        throw $message
    }
}


