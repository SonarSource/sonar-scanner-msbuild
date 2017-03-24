#!/bin/sh

set -euo pipefail
set -x

# if the version of the Scanner is "DEV", we build the version deployed in repox using the current assembly version and the build number (CI_BUILD_NUMBER)
get_version() {
  if [ "DEV" == "$1" ]; then
    local assemblyInfoFile=../AssemblyInfo.Shared.cs
    local version=`sed -bn "s|\[assembly: AssemblyVersion(\"\([0-9.]*\)\")\]|\1|p" $assemblyInfoFile | sed 's/\r//'`
  
    if [ -z $version ]; then
      echo "Failed to find assembly version in $assemblyInfoFile"
	  exit 1
    fi
	
	output="${version}.${CI_BUILD_NUMBER}"
  else 
    output=$1
  fi
}

cd its

get_version $SCANNER_VERSION
if [ "DEV" == "$SCANNER_VERSION" ]; then
   #store version for artifact promotion
   echo "ARTIFACT=org/sonarsource/scanner/msbuild/sonar-scanner-msbuild/$output" > ../artifact.properties
fi
SCANNER_VERSION=$output

get_version $SCANNER_PAYLOAD_VERSION
SCANNER_PAYLOAD_VERSION=$output

# default versions of csharp and vbnet plugin are defined in the pom file
mvn -B -e verify -Dsonar.runtimeVersion=$SQ_VERSION -DscannerForMSBuild.version=$SCANNER_VERSION -DscannerForMSBuildPayload.version=$SCANNER_PAYLOAD_VERSION -DfxcopVersion=LATEST_RELEASE
