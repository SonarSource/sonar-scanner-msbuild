function Get-Version {
    $versionPropsXml = [xml](Get-Content -Path scripts/version/Version.props)
    return $versionPropsXml.Project.PropertyGroup.MainVersion
}

Write-host "Create tools directory"
$toolsPath = "C:\tools"
if (-Not [System.IO.Directory]::Exists($toolsPath)){
  New-Item -Path "C:\" -Name "tools" -ItemType "directory"
}

$NUM_RETRIES = 5
for ($num = 1 ; $num -le $NUM_RETRIES ; $num++)
{
  try
  {
    Write-host "Download Mend tool, attempt $num/$NUM_RETRIES"
    $MendAgentPath = "$toolsPath\wss-unified-agent.jar"
    Invoke-WebRequest -Uri https://unified-agent.s3.amazonaws.com/wss-unified-agent.jar -OutFile $MendAgentPath
    break
  }
  catch
  {
    if ([System.IO.File]::Exists($MendAgentPath))
    {
      Remove-Item -Path $MendAgentPath
    }
    Write-host "Download failed with error: $_"

    if($num -lt $NUM_RETRIES)
    {
      Write-host "Will wait 5s before retry."
      Start-Sleep -Seconds 5
    }
  }
}

Write-Host "Validating Mend agent certificate signature..."
$cert = 'Signed by "CN=whitesource software inc, O=whitesource software inc, STREET=79 Madison Ave, L=New York, ST=New York, OID.2.5.4.17=10016, C=US"'
if (-Not (& "$env:JAVA_HOME_11_X64\bin\jarsigner.exe" -verify -strict -verbose $MendAgentPath |  Select-String -Pattern $cert -CaseSensitive -Quiet)){
  Write-Host "wss-unified-agent.jar signature verification failed."
  exit 1
}

# Mend agent needs the following environment variables:
# - WS_APIKEY
# - WS_PRODUCTNAME
# - WS_PROJECTNAME

$env:WS_PROJECTNAME = "$env:WS_PRODUCTNAME $(Get-Version)"

Write-Host "Running the Mend unified agent for $env:WS_PROJECTNAME..."
& "$env:JAVA_HOME_11_X64\bin\java.exe" -jar $MendAgentPath -c "$PSScriptRoot\wss-unified-agent.config"
