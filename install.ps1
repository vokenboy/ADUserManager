# ADUserManager Diegimo Programa
# Usage: powershell -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/vokenboy/ADUserManager/main/install.ps1 | iex"

$repo = "vokenboy/adusermanager"
$assetName = "ADUserManager.zip"
$installDir = "$env:LOCALAPPDATA\ADUserManager"
$tempZip = "$env:TEMP\ADUserManager.zip"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ADUserManager Diegimo Programa" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Get latest release download URL
Write-Host "Gaunama naujausia versija..." -ForegroundColor Yellow
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{ "User-Agent" = "ADUserManager-Installer" }
    $asset = $release.assets | Where-Object { $_.name -eq $assetName }
    if (-not $asset) {
        throw "Nerasta $assetName naujausioje versijoje."
    }
    $downloadUrl = $asset.browser_download_url
    Write-Host "Rasta versija: $($release.tag_name)" -ForegroundColor Green
} catch {
    Write-Host "Klaida: Nepavyko gauti versijos informacijos. $_" -ForegroundColor Red
    Read-Host "Paspauskite Enter, kad išeitumėte"
    exit 1
}

# Download
Write-Host "Atsisiunčiama $assetName..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
} catch {
    Write-Host "Klaida: Atsisiuntimas nepavyko. $_" -ForegroundColor Red
    Read-Host "Paspauskite Enter, kad išeitumėte"
    exit 1
}

# Extract
Write-Host "Diegiama į $installDir..." -ForegroundColor Yellow
try {
    if (Test-Path $installDir) {
        Remove-Item -Path $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Expand-Archive -Path $tempZip -DestinationPath $installDir -Force
} catch {
    Write-Host "Klaida: Išskleidimas nepavyko. $_" -ForegroundColor Red
    Read-Host "Paspauskite Enter, kad išeitumėte"
    exit 1
}

# Unblock all files (removes Mark of the Web to avoid ASR blocks)
Write-Host "Atblokuojami failai..." -ForegroundColor Yellow
Get-ChildItem -Path $installDir -Recurse | Unblock-File

# Clean up
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

# Add to PATH
Write-Host "Pridedama į PATH..." -ForegroundColor Yellow
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($userPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$userPath;$installDir", "User")
    $env:Path += ";$installDir"
    Write-Host "Sėkmingai pridėta į PATH" -ForegroundColor Green
} else {
    Write-Host "Jau yra PATH" -ForegroundColor Green
}

# Launch
$exe = Get-ChildItem -Path $installDir -Filter "ADUserManager.exe" -Recurse | Select-Object -First 1
if ($exe) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "  Diegimas Baigtas!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Dabar galite paleisti 'ADUserManager' iš bet kurio terminalo" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Paleidžiama ADUserManager..." -ForegroundColor Green
    try {
        Start-Process $exe.FullName
    } catch {
        Write-Host ""
        Write-Host "Įspėjimas: Nepavyko automatiškai paleisti programos" -ForegroundColor Yellow
        Write-Host "Tai paprastai dėl Windows saugumo nustatymų" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Norėdami paleisti rankiniu būdu:" -ForegroundColor Cyan
        Write-Host "  1. Eikite į: $installDir" -ForegroundColor White
        Write-Host "  2. Dukart spustelėkite ADUserManager.exe" -ForegroundColor White
        Write-Host "  3. Jei Windows blokuoja, spustelėkite 'Daugiau informacijos', tada 'Vis tiek paleisti'" -ForegroundColor White
        Write-Host ""
        Read-Host "Paspauskite Enter, kad išeitumėte"
    }
} else {
    Write-Host ""
    Write-Host "Diegimas baigtas, bet nerasta ADUserManager.exe" -ForegroundColor Yellow
    Write-Host "Patikrinkite: $installDir" -ForegroundColor Yellow
    Read-Host "Paspauskite Enter, kad išeitumėte"
}
