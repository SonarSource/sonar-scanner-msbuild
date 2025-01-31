param (
    [string]
    [Parameter(Mandatory = $true)]
    $StorePath,
    [string]
    [Parameter(Mandatory = $true)]
    $Password
)

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Script is not running as Administrator. Restarting with elevated rights..."
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -StorePath `"$StorePath`" -Password `"$Password`"" -Verb RunAs -Wait
    exit
}

# Import the certificate into the Trusted Root Certification Authorities store
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($StorePath, $Password, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)

$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
$store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
$store.Add($cert)
$store.Close()
