#!/bin/sh

set -euo pipefail
set -x

# if the version of the Scanner is "DEV", we build the version deployed in repox using the current assembly version and the build number (CI_BUILD_NUMBER)
get_version() {
  if [ "DEV" == "$1" ]; then
    local assemblyInfoFile=../AssemblyInfo.Shared.cs
    local version=`sed -n "s|\[assembly: AssemblyVersion(\"\([0-9.]*\)\")\]|\1|p" $assemblyInfoFile`
  
    if [ -z $version ]; then
      echo "Failed to find assembly version in $assemblyInfoFile"
	  exit 1
    fi
	
	output="${version}-build${CI_BUILD_NUMBER}"
  else 
    output=$1
  fi
}

cd its

get_version $SCANNER_VERSION
SCANNER_VERSION=$output

get_version $SCANNER_PAYLOAD_VERSION
SCANNER_PAYLOAD_VERSION=$output

mvn verify -Dsonar.runtimeVersion=$SQ_VERSION -DscannerForMSBuild.version=$SCANNER_VERSION -DscannerForMSBuildPayload.version=$SCANNER_PAYLOAD_VERSION