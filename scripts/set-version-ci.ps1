& "$PSScriptRoot\set-version.ps1" -Version "${env:SHORT_VERSION}" -BuildNumber "${env:BUILD_BUILDID}" -Branch "${env:BUILD_SOURCEBRANCHNAME}" -Sha1 "${env:BUILD_SOURCEVERSION}"
