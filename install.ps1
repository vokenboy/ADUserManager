# ADUserManager Installer
# Usage: powershell -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/vokenboy/adusermanager/main/install.ps1 | iex"

$repo = "vokenboy/adusermanager"
$assetName = "ADUserManager.zip"
$installDir = "$env:LOCALAPPDATA\ADUserManager"
$tempZip = "$env:TEMP\ADUserManager.zip"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ADUserManager Installer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Get latest release download URL
Write-Host "Fetching latest release..." -ForegroundColor Yellow
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{ "User-Agent" = "ADUserManager-Installer" }
    $asset = $release.assets | Where-Object { $_.name -eq $assetName }
    if (-not $asset) {
        throw "Could not find $assetName in the latest release."
    }
    $downloadUrl = $asset.browser_download_url
    Write-Host "Found version: $($release.tag_name)" -ForegroundColor Green
} catch {
    Write-Host "Error: Failed to fetch release info. $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Download
Write-Host "Downloading $assetName..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
} catch {
    Write-Host "Error: Download failed. $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Extract
Write-Host "Installing to $installDir..." -ForegroundColor Yellow
try {
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Expand-Archive -Path $tempZip -DestinationPath $installDir -Force
} catch {
    Write-Host "Error: Extraction failed. $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Unblock all files (removes Mark of the Web to avoid ASR blocks)
Write-Host "Unblocking files..." -ForegroundColor Yellow
Get-ChildItem -Path $installDir -Recurse | Unblock-File

# Clean up
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

# Launch
$exe = Get-ChildItem -Path $installDir -Filter "ADUserManager.exe" -Recurse | Select-Object -First 1
if ($exe) {
    Write-Host ""
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host "Launching ADUserManager..." -ForegroundColor Green
    Start-Process $exe.FullName
} else {
    Write-Host ""
    Write-Host "Installation complete, but could not find ADUserManager.exe" -ForegroundColor Yellow
    Write-Host "Check: $installDir" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
}
