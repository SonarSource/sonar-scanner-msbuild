#!/bin/sh

set -euo pipefail

cd its
mvn verify -Dsonar.runtimeVersion=$SQ_VERSION -DscannerForMSBuild.version=$SCANNER_VERSION -DscannerForMSBuildPayload.version=$SCANNER_PAYLOAD_VERSION