#
# This script is designed to be run in an Azure DevOps pipeline and is responsible 
# for generating a self-signed SSL certificate, exporting it to a PKCS12 keystore, 
# and importing it into the Java keystore. 
#
# It also updates the system's trusted certificates based on the operating system.
#
# This certificate is used for the SslTest integration tests.
# 

# Set strict error handling
$ErrorActionPreference = "Stop"

# Define variables
$SslCert = "$env:BUILD_SOURCESDIRECTORY/certs/system-trusted.crt"
$SslKey = "$env:BUILD_SOURCESDIRECTORY/certs/system-trusted.key"
$SslKeystorePath = "$env:BUILD_SOURCESDIRECTORY/certs/system-trusted.p12"
$SslKeystorePassword = "changeit"

# Create the certs directory
New-Item -ItemType Directory -Force -Path "$env:BUILD_SOURCESDIRECTORY/certs" | Out-Null

# Generate a self-signed certificate
& openssl req `
    -newkey rsa:2048 `
    -x509 `
    -sha256 `
    -addext "subjectAltName = DNS:localhost" `
    -nodes `
    -out $SslCert `
    -subj "/C=CS/ST=U/L=U/O=U/OU=U" `
    -keyout $SslKey

# Export the certificate to a PKCS12 keystore
& openssl pkcs12 `
    -export `
    -out $SslKeystorePath `
    -inkey $SslKey `
    -in $SslCert `
    -passout pass:$SslKeystorePassword

# Import the certificate into the Java keystore
& "$env:JAVA_HOME_17_X64/bin/keytool" `
    -import `
    -storepass $SslKeystorePassword `
    -noprompt `
    -cacerts `
    -alias "system-trusted" `
    -file $SslCert

# Update the system's trusted certificates based on the OS
if ($env:AGENT_OS -eq "Linux") {
    & sudo cp $SslCert /usr/local/share/ca-certificates/system-trusted.crt
    & sudo update-ca-certificates
} elseif ($env:AGENT_OS -eq "Darwin") { # MacOS
    & sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain $SslCert
} else {
    Write-Error "Running on an unsupported or unknown OS: $env:AGENT_OS"
    exit 1
}

# Set Azure DevOps pipeline variables for the next steps
Write-Host "##vso[task.setvariable variable=SSL_KEYSTORE_PATH]$SslKeystorePath"
Write-Host "##vso[task.setvariable variable=SSL_KEYSTORE_PASSWORD]$SslKeystorePassword"
