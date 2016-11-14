param (
    [Parameter(Mandatory=$true, HelpMessage="Test or Production")]
    [ValidateSet("Test", "Production")]
    [string]
    $environment,
    [Parameter(Mandatory=$true, HelpMessage="The three number version for this release")]
    [string]
    $version,
    [Parameter(Mandatory=$true, HelpMessage="Get a personal token following the instructions https://www.visualstudio.com/en-us/integrate/extensions/publish/command-line")]
    [string]
    $accessToken,
    [string]
    $shareWith="sonarsource"
)

$ErrorActionPreference = "Stop"

$buildArtifactsPath = "$PSScriptRoot\build\Artifacts"

function UpdateTfxCli
{
    Write-Output "Updating tfx-cli..."
    & npm up -g tfx-cli
}

function PublishVsix
{
    param
    (
		[Parameter(Mandatory=$True)]
		[string]$vsixFile,
        [Parameter(Mandatory=$True)]
		[string]$environment
	)

    if ($environment -eq "Production")
    {
        Write-Output "Publishing $vsixFile to everyone (public extension)..."
        & tfx extension publish --vsix $vsixFile --token $accessToken --no-prompt
    } 
    elseif ($environment -eq "Test") 
    {
        Write-Output "Publishing $vsixFile as a private extension, sharing with $shareWith using access token $accessToken"
        & tfx extension publish --vsix $vsixFile --token $accessToken --share-with $shareWith --no-prompt
    } 
    else
    {
        Write-Error "The valid environments are 'Production' and 'Test'"
    }
}

function PublishAllExtensions
{
    param
    (
        [Parameter(Mandatory=$True)]
		[string]$environment
	)

    $environmentArtifactsPath = "$buildArtifactsPath\$environment"
    Write-Output "Looking for VSIX file(s) to publish in $environmentArtifactsPath..."

    $vsixFiles = Get-ChildItem $environmentArtifactsPath -Include "*$version.vsix" -Recurse
    if ($vsixFiles) 
    {
        foreach ($vsixFile in $vsixFiles) 
        {
            PublishVsix $vsixFile $environment
        }
    } 
    else 
    {
        Write-Error "There were no VSIX files found for *$version.vsix in $environmentArtifactsPath"
    }
}


UpdateTfxCli
PublishAllExtensions $environment