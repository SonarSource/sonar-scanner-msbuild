$StoreFolder = Join-Path $Env:USERPROFILE ".sonar\ssl"
$StorePath = Join-Path $StoreFolder "s4net-self-signed.pfx"
$Password = "changeit"

if (-not (Test-Path $StoreFolder)) {
    New-Item -ItemType Directory -Path $StoreFolder | Out-Null
}

Write-Host "Generating self-signed certificate in $StoreFolder"

$rsa = [System.Security.Cryptography.RSA]::Create()
# Create a certificate request
$certRequest = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest("CN=localhost", $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

# Add extensions to the certificate request
# Key Usage
$keyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment
$certRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension($keyUsage, $true)))

# This is required for Java to trust the certificate
# https://stackoverflow.com/questions/63086356
$sanBuilder = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
$sanBuilder.AddDnsName("localhost")
$sanBuilder.AddIpAddress([System.Net.IPAddress]::Parse("127.0.0.1"))

# Build the SAN extension and add it to the certificate request
$sanExtension = $sanBuilder.Build()
$certRequest.CertificateExtensions.Add($sanExtension)

# Enhanced Key Usage
$oidCollection = New-Object System.Security.Cryptography.OidCollection
$oidCollection.Add([System.Security.Cryptography.Oid]::FromFriendlyName("Server Authentication", [System.Security.Cryptography.OidGroup]::EnhancedKeyUsage)) | Out-Null
$certRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oidCollection, $true)))

# Create a self-signed certificate
$validFrom = [System.DateTimeOffset]::Now.AddDays(-1)
$validTo = $validFrom.AddYears(100)
$generatedCert = $certRequest.CreateSelfSigned($validFrom, $validTo)
$generatedCert.FriendlyName = "s4net-self-signed"

# Export the certificate to a PFX file
[System.IO.File]::WriteAllBytes($StorePath, $generatedCert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password))

Write-Host "$StorePath created"
Write-Host "Trust the certificate by importing it into the Trusted Root Certification Authorities store"

& "$PSScriptRoot\trust-self-signed-certificate.ps1" -StorePath $StorePath -Password $Password

Write-Host "Certificate imported into the Trusted Root Certification Authorities store"

Write-Host "Setting SSL_KEYSTORE_PATH & SSL_KEYSTORE_PASSWORD environment variables"
[System.Environment]::SetEnvironmentVariable("SSL_KEYSTORE_PATH", $StorePath, [System.EnvironmentVariableTarget]::User)
[System.Environment]::SetEnvironmentVariable("SSL_KEYSTORE_PASSWORD", $Password, [System.EnvironmentVariableTarget]::User)

