param(
    [string]$ServerUrl = "http://localhost:8080",
    [string]$AssetName = "ActiveManager.zip",
    [string]$InstallDir = "$env:LOCALAPPDATA\ActiveManager",
    [switch]$NoLaunch
)

$tempZip = Join-Path $env:TEMP $AssetName
$normalizedServerUrl = $ServerUrl.TrimEnd("/")
$downloadUrl = "$normalizedServerUrl/$AssetName"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ActiveManager Local Installation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server: $normalizedServerUrl" -ForegroundColor Cyan
Write-Host "Package: $downloadUrl" -ForegroundColor Cyan
Write-Host ""

# Download package from a local/LAN HTTP server.
Write-Host "Downloading package..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
} catch {
    Write-Host "Error: Failed to download $downloadUrl" -ForegroundColor Red
    Write-Host "Check that the HTTP server is running and that it contains the $AssetName file." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Extract the package into the local app folder.
Write-Host "Installing to $InstallDir..." -ForegroundColor Yellow
try {
    if (Test-Path $InstallDir) {
        Remove-Item -Path $InstallDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Expand-Archive -Path $tempZip -DestinationPath $InstallDir -Force
} catch {
    Write-Host "Error: Extraction failed. $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Unblocking files..." -ForegroundColor Yellow
Get-ChildItem -Path $InstallDir -Recurse | Unblock-File

Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

Write-Host "Adding to PATH..." -ForegroundColor Yellow
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$InstallDir", "User")
    $env:Path += ";$InstallDir"
    Write-Host "Successfully added to PATH" -ForegroundColor Green
} else {
    Write-Host "Already present in PATH" -ForegroundColor Green
}

$exe = Get-ChildItem -Path $InstallDir -Filter "ActiveManager.exe" -Recurse | Select-Object -First 1
if (-not $exe) {
    Write-Host ""
    Write-Host "Installation finished, but ActiveManager.exe was not found" -ForegroundColor Yellow
    Write-Host "Check: $InstallDir" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "You can now launch 'ActiveManager' from any terminal" -ForegroundColor Cyan

if ($NoLaunch) {
    Write-Host "Automatic launch skipped." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Launching ActiveManager..." -ForegroundColor Green
try {
    Start-Process $exe.FullName
} catch {
    Write-Host ""
    Write-Host "Warning: Failed to launch the application automatically" -ForegroundColor Yellow
    Write-Host "To launch it manually, go to: $InstallDir" -ForegroundColor White
    Read-Host "Press Enter to exit"
}
