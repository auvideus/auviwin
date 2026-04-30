<#
.SYNOPSIS
    Creates and installs a local self-signed code-signing certificate for MSIX sideloading.
    Prompts for UAC elevation automatically (required to trust in LocalMachine\TrustedPeople).

.DESCRIPTION
    - Creates a code-signing cert with subject CN=AuviWin Dev in CurrentUser\My
    - Exports it to certs\dev.pfx (password: dev)
    - Imports the pfx into LocalMachine\TrustedPeople so MSIX deployment trusts it
      (MSIX deployment runs as SYSTEM and only checks LocalMachine stores)

.NOTES
    The cert CN must exactly match the <Identity Publisher> in App/Package.appxmanifest.
    To rename the app, update $CertSubject here and in the manifest.
#>

param(
    [string]$CertSubject  = "CN=AuviWin Dev",
    [string]$CertPassword = "dev",
    [string]$CertPfx      = "certs\dev.pfx"
)

$ErrorActionPreference = "Stop"

# ── Self-elevate if not already running as admin ──────────────────────────────
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "Requesting elevation (UAC)..."
    $argList = @("-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    foreach ($key in $PSBoundParameters.Keys) {
        $argList += "-$key"
        $argList += "`"$($PSBoundParameters[$key])`""
    }
    Start-Process powershell -Verb RunAs -ArgumentList $argList -Wait
    exit $LASTEXITCODE
}

Set-Location $PSScriptRoot\..

Write-Host "=== AuviWin dev certificate setup ===" -ForegroundColor Cyan

# Check for existing cert (can live in either store)
$existing = @("Cert:\LocalMachine\My", "Cert:\CurrentUser\My") |
    ForEach-Object { Get-ChildItem $_ -ErrorAction SilentlyContinue } |
    Where-Object { $_.Subject -eq $CertSubject -and $_.NotAfter -gt (Get-Date) } |
    Select-Object -First 1

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

# Trust for MSIX sideloading — must be LocalMachine\TrustedPeople
# (MSIX deployment runs as SYSTEM and ignores CurrentUser stores)
$trusted = Get-ChildItem "Cert:\LocalMachine\TrustedPeople" -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -eq $cert.Thumbprint }

if (-not $trusted) {
    Write-Host "Importing into LocalMachine\TrustedPeople..."
    Import-PfxCertificate `
        -FilePath $CertPfx `
        -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" `
        -Password $securePassword | Out-Null
    Write-Host "Trusted." -ForegroundColor Green
} else {
    Write-Host "Already trusted in LocalMachine\TrustedPeople." -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Certificate CN must match <Identity Publisher> in App/Package.appxmanifest."
