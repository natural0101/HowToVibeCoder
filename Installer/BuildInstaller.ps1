param(
    [string]$ProjectRoot = "C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder",
    [string]$DesktopDir = "C:\Users\se-20\OneDrive\Рабочий стол"
)

$ErrorActionPreference = "Stop"
$InstallerDir = Join-Path $ProjectRoot "Installer"
$IcoPath = Join-Path $InstallerDir "HowToVibeCoder.ico"
$PayloadZip = Join-Path $InstallerDir "payload.zip"

Write-Host "[1/4] Building Icon Generator & Generating HowToVibeCoder.ico..." -ForegroundColor Cyan
dotnet build "$InstallerDir\HowToVibeCoder_Setup.csproj" -c Release --nologo
$SetupBinExe = Join-Path $InstallerDir "bin\Release\net10.0-windows\HowToVibeCoder_Setup.exe"
if (-not (Test-Path $SetupBinExe)) {
    $SetupBinExe = Get-ChildItem -Path "$InstallerDir\bin\Release" -Filter "HowToVibeCoder_Setup.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
}
& $SetupBinExe --generate-icon $IcoPath

Write-Host "[2/4] Packing Game Release Folder into payload.zip..." -ForegroundColor Cyan
$ReleaseDir = Join-Path $ProjectRoot "bin\Release\net10.0"
if (-not (Test-Path (Join-Path $ReleaseDir "HowToVibeCoder.exe"))) {
    $DebugDir = Join-Path $ProjectRoot "bin\Debug\net10.0"
    if (Test-Path (Join-Path $DebugDir "HowToVibeCoder.exe")) {
        $ReleaseDir = $DebugDir
    }
}

if (Test-Path $PayloadZip) {
    Remove-Item $PayloadZip -Force
}

if (Test-Path $ReleaseDir) {
    Compress-Archive -Path "$ReleaseDir\*" -DestinationPath $PayloadZip -CompressionLevel Optimal -Force
    Write-Host "Packed payload.zip from $ReleaseDir" -ForegroundColor Green
} else {
    # Create minimal staging zip so single-file build always embeds valid zip
    $StageDir = Join-Path $InstallerDir "stage_tmp"
    New-Item -ItemType Directory -Path $StageDir -Force | Out-Null
    Set-Content -Path (Join-Path $StageDir "README.txt") -Value "How to Vibe Coder — Floor 1 (Cloud Entropic Tower)"
    Compress-Archive -Path "$StageDir\*" -DestinationPath $PayloadZip -Force
    Remove-Item $StageDir -Recurse -Force
}

Write-Host "[3/4] Publishing Single-File Custom GUI Installer HowToVibeCoder_Setup.exe..." -ForegroundColor Cyan
$PublishOut = Join-Path $InstallerDir "publish"
dotnet publish "$InstallerDir\HowToVibeCoder_Setup.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishOut --nologo

$PublishedSetupExe = Join-Path $PublishOut "HowToVibeCoder_Setup.exe"
$DesktopSetupExe = Join-Path $DesktopDir "HowToVibeCoder_Setup.exe"
Copy-Item $PublishedSetupExe $DesktopSetupExe -Force

Write-Host "[4/4] Creating 1-Click Launcher HowToVibeCoder.bat on Desktop..." -ForegroundColor Cyan
$DesktopBat = Join-Path $DesktopDir "HowToVibeCoder.bat"
$BatContent = @"
@echo off
chcp 65001 >nul
title How to Vibe Coder — Этаж 1 (Cloud Entropic Tower)
if exist "C:\Games\HowToVibeCoder\HowToVibeCoder.exe" (
    cd /d "C:\Games\HowToVibeCoder"
    start "" "C:\Games\HowToVibeCoder\HowToVibeCoder.exe" %*
    exit /b 0
)
if exist "$ProjectRoot\bin\Release\net10.0\HowToVibeCoder.exe" (
    cd /d "$ProjectRoot\bin\Release\net10.0"
    start "" "$ProjectRoot\bin\Release\net10.0\HowToVibeCoder.exe" %*
    exit /b 0
)
if exist "$ProjectRoot\bin\Debug\net10.0\HowToVibeCoder.exe" (
    cd /d "$ProjectRoot\bin\Debug\net10.0"
    start "" "$ProjectRoot\bin\Debug\net10.0\HowToVibeCoder.exe" %*
    exit /b 0
)
echo Запуск установщика HowToVibeCoder_Setup.exe...
start "" "$DesktopSetupExe"
"@
Set-Content -Path $DesktopBat -Value $BatContent -Encoding UTF8

Write-Host "DONE! Created:" -ForegroundColor Green
Write-Host "  -> $DesktopSetupExe" -ForegroundColor Yellow
Write-Host "  -> $DesktopBat" -ForegroundColor Yellow
