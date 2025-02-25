#!/usr/bin/env bats

setup() {
    # Check if SONAR_TOKEN is set
    if [ -z "$SONAR_TOKEN" ]; then
        echo "SONAR_TOKEN is not set"
        skip "Skipping all tests due to missing SONAR_TOKEN"
    fi

    curl http://localhost:9000/batch/index > /dev/null 2>&1
    if [ $? -ne 0 ]; then
        echo "SonarQube is not running"
        skip "Skipping all tests due to missing SonarQube"
    fi

    curl https://localhost:4443/batch/index > /dev/null 2>&1
    if [ $? -ne 0 ]; then
        echo "SonarQube is not running with a trusted certificate"
        skip "Skipping all tests due to missing SonarQube with a trusted certificate"
    fi
}

@test "Untrusted Certificate" {
    run dotnet /scanner-net/SonarScanner.MSBuild.dll begin /k:ManualTest /d:sonar.host.url=https://localhost:5443 /d:sonar.token=$SONAR_TOKEN /d:sonar.verbose=true
    [ "$status" -ne 0 ]
    [[ "$output" =~ "System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception." ]]
    [[ "$output" =~ "System.Security.Authentication.AuthenticationException: The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot" ]]
}

@test "Certificate trusted by the system" {
    run dotnet /scanner-net/SonarScanner.MSBuild.dll begin /k:ManualTest /d:sonar.host.url=https://localhost:4443 /d:sonar.token=$SONAR_TOKEN /d:sonar.verbose=true
    [ "$status" -eq 0 ]
    [[ "$output" =~ "Pre-processing succeeded." ]]

    run dotnet build --no-incremental
    [ "$status" -eq 0 ]

    run dotnet /scanner-net/SonarScanner.MSBuild.dll end /d:sonar.token=$SONAR_TOKEN
    [ "$status" -eq 0 ]
    [[ "$output" =~ "EXECUTION SUCCESS" ]]
}

@test "Given truststore" {
    run dotnet /scanner-net/SonarScanner.MSBuild.dll begin /k:ManualTest /d:sonar.host.url=https://localhost:6443 /d:sonar.token=$SONAR_TOKEN /d:sonar.verbose=true /d:sonar.scanner.truststorePath=/manual-tests/truststore.p12 /d:sonar.scanner.truststorePassword=itchange
    [ "$status" -eq 0 ]
    [[ "$output" =~ "Pre-processing succeeded." ]]

    run dotnet build --no-incremental
    [ "$status" -eq 0 ]

    run dotnet /scanner-net/SonarScanner.MSBuild.dll end /d:sonar.token=$SONAR_TOKEN
    [ "$status" -eq 0 ]
    [[ "$output" =~ "-Djavax.net.ssl.trustStore=/manual-tests/truststore.p12" ]]
    [[ "$output" =~ "-Djavax.net.ssl.trustStorePassword=itchange" ]]
    [[ "$output" =~ "EXECUTION SUCCESS" ]]
}

@test "Given truststore use default password" {
    run dotnet /scanner-net/SonarScanner.MSBuild.dll begin /k:ManualTest /d:sonar.host.url=https://localhost:7443 /d:sonar.token=$SONAR_TOKEN /d:sonar.verbose=true /d:sonar.scanner.truststorePath=/manual-tests/defaultpass.p12
    [ "$status" -eq 0 ]
    [[ "$output" =~ "Pre-processing succeeded." ]]

    run dotnet build --no-incremental
    [ "$status" -eq 0 ]

    run dotnet /scanner-net/SonarScanner.MSBuild.dll end /d:sonar.token=$SONAR_TOKEN
    [ "$status" -eq 0 ]
    [[ "$output" =~ "-Djavax.net.ssl.trustStore=/manual-tests/defaultpass.p12" ]]
    [[ "$output" =~ "-Djavax.net.ssl.trustStorePassword=changeit" ]]
    [[ "$output" =~ "EXECUTION SUCCESS" ]]
}
