#!/bin/bash

if [ ! -f build/sonarscanner-net.zip ]; then
    echo "Build SonarScanner for .NET"
    pwsh scripts/its-build.ps1
else
    echo "SonarScanner for .NET already built"
fi

cd its

mvn verify -Dtest=ScannerMSBuildTest#testCSharpSdkLatest
