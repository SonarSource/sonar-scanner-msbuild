cd its


mvn verify -Prun-its -Dsonar.runtimeVersion=$SQ_VERSION -DscannerForMSBuild.version=$SCANNER_VERSION -DscannerForMSBuildPayload.version=$SCANNER_PAYLOAD_VERSION