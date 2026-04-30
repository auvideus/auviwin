<#
.SYNOPSIS
    Creates and installs a local self-signed code-signing certificate for MSIX sideloading.
    Run once, as Administrator.

.DESCRIPTION
    - Creates a code-signing cert with subject CN=AuviWin Dev in LocalMachine\My
    - Exports it to certs\dev.pfx (password: dev)
    - Imports the pfx into LocalMachine\TrustedPeople so Windows trusts it for sideloading

.NOTES
    The cert CN must exactly match the <Identity Publisher> in App/Package.appxmanifest.
    To rename the app, update $CertSubject here and in the manifest.
    Runs without Administrator privileges (uses CurrentUser certificate stores).
#>

param(
    [string]$CertSubject  = "CN=AuviWin Dev",
    [string]$CertPassword = "dev",
    [string]$CertPfx      = "certs\dev.pfx"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

Write-Host "=== AuviWin dev certificate setup ===" -ForegroundColor Cyan

# Check for existing cert
$existing = Get-ChildItem "Cert:\CurrentUser\My" -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $CertSubject -and $_.NotAfter -gt (Get-Date) }

if ($existing) {
    Write-Host "Certificate already exists: $CertSubject ($($existing.Thumbprint))" -ForegroundColor Green
    $cert = $existing
} else {
    Write-Host "Creating certificate: $CertSubject"
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $CertSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -FriendlyName "AuviWin MSIX Dev Signing" `
        -NotAfter (Get-Date).AddYears(10)
    Write-Host "Created: $($cert.Thumbprint)" -ForegroundColor Green
}

# Export to PFX
$securePassword = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
New-Item -ItemType Directory -Force (Split-Path $CertPfx) | Out-Null
Export-PfxCertificate -Cert $cert -FilePath $CertPfx -Password $securePassword | Out-Null
Write-Host "Exported to: $CertPfx"

# Trust for MSIX sideloading
$trusted = Get-ChildItem "Cert:\CurrentUser\TrustedPeople" -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint }

if (-not $trusted) {
    Write-Host "Importing into CurrentUser\TrustedPeople..."
    Import-PfxCertificate `
        -FilePath $CertPfx `
        -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" `
        -Password $securePassword | Out-Null
    Write-Host "Trusted." -ForegroundColor Green
} else {
    Write-Host "Already trusted in CurrentUser\TrustedPeople." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Certificate CN must match <Identity Publisher> in App/Package.appxmanifest."
