function Get-Version {
    $versionPropsXml = [xml](Get-Content -Path scripts/version/Version.props)
    return $versionPropsXml.Project.PropertyGroup.MainVersion
}

# Mend agent needs the following environment variables:
# - WS_APIKEY
# - WS_PRODUCTNAME
# - WS_PROJECTNAME

$env:WS_PROJECTNAME = "$env:WS_PRODUCTNAME $(Get-Version).Test"

Write-Host "Running the Mend unified agent for $env:WS_PROJECTNAME..."
& "$env:JAVA_HOME\bin\java.exe" -jar $env:MEND_AGENT_PATH -c "$PSScriptRoot\wss-unified-agent.config"
exit $LASTEXITCODE
