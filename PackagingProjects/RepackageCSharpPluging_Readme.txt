To create an up-to-date version of the C# plugin:

1. Copy an existing version of the C# plugin to the same directory as the script RepackageCSharpPlugin.ps1
2. Build the solution in DEBUG
3. Run the script RepackageCSharpPlugin.ps1
4. Go to ../DeploymentArtifacts and find the newly created jar
5. Copy this file to the SQ server and delete the old one
6. Restart SQ server (e.g. restart the service)


Note: Optionally you can build your solution in Release and run the script with a param: RepackageCSharpPlugin.ps1 Release