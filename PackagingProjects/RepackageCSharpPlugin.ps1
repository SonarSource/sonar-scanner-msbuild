param([string]$buildConfiguration="Debug")



Add-Type -AssemblyName "System.IO.Compression.FileSystem"

. .\RepackageCSharpPluginHelper.ps1


$payloadPath = PrepareExistingJar
EnsureBuild $buildConfiguration
ZipNewPayload $payloadPath
CreateNewJar 




