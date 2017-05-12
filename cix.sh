#!/bin/sh

set -euo pipefail
set -x

# use the version deployed in repox using the current assembly version and the build number (CI_BUILD_NUMBER)
version=`sed -n "s|\[assembly: AssemblyVersion(\"\([0-9.]*\)\")\]|\1|p" AssemblyInfo.Shared.cs | sed 's/\r//'`

if [ -z $version ]; then
  echo "Failed to find assembly version"
exit 1
fi
	
SCANNER_VERSION="${version}.${CI_BUILD_NUMBER}"

#store version for artifact promotion
echo "ARTIFACT=org/sonarsource/scanner/msbuild/sonar-scanner-msbuild/$SCANNER_VERSION" > artifact.properties

cd its
mvn -B -e verify -Dsonar.runtimeVersion=$SQ_VERSION -DscannerForMSBuild.version=$SCANNER_VERSION -Dmsbuild.path=$MSBUILD_PATH
