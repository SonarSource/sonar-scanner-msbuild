& "$PSScriptRoot\set-version.ps1" -Version "${env:SHORT_VERSION}" -BuildNumber "${env:Build_BuildId}" -Branch "${env:Build_SourceBranchName}" -Sha1 "${env:Build_SourceVersion}"
