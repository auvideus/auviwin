<#
.SYNOPSIS
    Publishes the app and packages it as a signed MSIX.

.DESCRIPTION
    1. dotnet publish (self-contained, single-file, win-x64)
    2. Generates MSIX logo assets via tools/IconGen
    3. Stages: exe + AppxManifest.xml + Assets/
    4. makeappx pack
    5. signtool sign with the dev pfx

.OUTPUTS
    dist\AuviWin.msix — ready to install via Add-AppxPackage or double-click

.NOTES
    Requires the Windows SDK (makeappx.exe / signtool.exe).
    Run tools\cert.ps1 once first (as Administrator) to create the signing cert.
#>

param(
    [string]$CertPfx      = "certs\dev.pfx",
    [string]$CertPassword = "dev",
    [string]$PublishDir   = "publish",
    [string]$StagingDir   = "staging",
    [string]$DistDir      = "dist"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

Write-Host "=== AuviWin MSIX packaging ===" -ForegroundColor Cyan

# ── 1. Find Windows SDK tools ─────────────────────────────────────────────────

function Find-SdkTool([string]$Name) {
    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) { throw "Windows SDK not found at $kitsRoot. Install the Windows SDK." }

    $latest = Get-ChildItem $kitsRoot -Directory |
        Where-Object { $_.Name -match '^\d' } |
        Sort-Object { [version]$_.Name } |
        Select-Object -Last 1

    if (-not $latest) { throw "No SDK version directories found under $kitsRoot." }

    $tool = Join-Path $latest.FullName "x64\$Name"
    if (-not (Test-Path $tool)) { throw "$Name not found at $tool. Is the Windows SDK installed?" }
    Write-Host "  Found $Name`: $tool"
    return $tool
}

$makeappx = Find-SdkTool "makeappx.exe"
$signtool  = Find-SdkTool "signtool.exe"

# ── 2. dotnet publish ─────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Publishing..."
$dotnet = if ($IsWindows) { "C:\Program Files\dotnet\dotnet.exe" } else { "dotnet" }
& $dotnet publish App\App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# ── 3. Generate MSIX logo assets ──────────────────────────────────────────────

Write-Host ""
Write-Host "Generating MSIX assets..."
$assetsDir = Join-Path $StagingDir "Assets"
& $dotnet run --project tools\IconGen -- --assets $assetsDir
if ($LASTEXITCODE -ne 0) { throw "IconGen --assets failed." }

# ── 4. Stage package layout ───────────────────────────────────────────────────

Write-Host ""
Write-Host "Staging package layout..."
Remove-Item -Recurse -Force $StagingDir\* -Exclude "Assets" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $StagingDir | Out-Null

# Manifest (must be named AppxManifest.xml in the package)
Copy-Item "App\Package.appxmanifest" "$StagingDir\AppxManifest.xml" -Force

# Executable (find the .exe produced by the publish)
$exe = Get-ChildItem $PublishDir -Filter "*.exe" | Select-Object -First 1
if (-not $exe) { throw "No .exe found in $PublishDir after publish." }
Copy-Item $exe.FullName "$StagingDir\" -Force
Write-Host "  Staged: $($exe.Name)"

# ── 5. makeappx pack ─────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Packing MSIX..."
New-Item -ItemType Directory -Force $DistDir | Out-Null
$msix = Join-Path $DistDir "AuviWin.msix"
& $makeappx pack /d $StagingDir /p $msix /overwrite /nv
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed." }
Write-Host "  Packed: $msix"

# ── 6. Sign ───────────────────────────────────────────────────────────────────

if (-not (Test-Path $CertPfx)) {
    throw "Signing certificate not found at '$CertPfx'. Run: powershell tools\cert.ps1 (as Administrator)"
}

Write-Host ""
Write-Host "Signing..."
& $signtool sign /fd SHA256 /f $CertPfx /p $CertPassword "$msix"
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed." }
Write-Host "  Signed." -ForegroundColor Green

# ── Done ──────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Package: $((Resolve-Path $msix).Path)"
Write-Host ""
Write-Host "To install:  task install"
Write-Host "             — or —"
Write-Host "             double-click $msix"
