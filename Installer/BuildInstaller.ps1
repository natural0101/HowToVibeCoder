param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$DesktopDir = [Environment]::GetFolderPath('DesktopDirectory')
)
$ErrorActionPreference = 'Stop'
$InstallerDir = Join-Path $ProjectRoot 'Installer'
$IcoPath = Join-Path $InstallerDir 'HowToVibeCoder.ico'
if (-not (Test-Path -LiteralPath $IcoPath)) { throw "Missing icon: $IcoPath" }

$StageBase = Join-Path $InstallerDir 'stage_tmp'
$PayloadZip = Join-Path $InstallerDir 'payload.zip'
$PublishBackupDir = Join-Path $InstallerDir 'publish'

# Always purge any stale staging or duplicate publish folders first
foreach ($stale in @($StageBase, $PublishBackupDir, $PayloadZip)) {
    if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Recurse -Force -ErrorAction SilentlyContinue }
}

$StageRoot = Join-Path $StageBase 'current'
$GameOut = Join-Path $StageRoot 'game'
$PublishOut = Join-Path $StageRoot 'setup'
New-Item -ItemType Directory -Force $GameOut, $PublishOut | Out-Null

try {
    Write-Host '[1/4] Publishing current game sources with .NET runtime...'
    dotnet publish (Join-Path $ProjectRoot 'HowToVibeCoder.csproj') -c Release -r win-x64 --self-contained true -p:ApplicationIcon=$IcoPath -o $GameOut --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Game publish failed.' }
    # Include all assets, including audio and artwork not covered by the game project.
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'assets') -Destination $GameOut -Recurse -Force
    Copy-Item -LiteralPath $IcoPath -Destination (Join-Path $GameOut 'HowToVibeCoder.ico') -Force
    foreach ($required in @('HowToVibeCoder.exe', 'HowToVibeCoder.dll', 'coreclr.dll', 'assets/ui/archive.png',
        'assets/models/char_vibe_coder.glb', 'assets/models/char_partner_90s.glb',
        'assets/models/char_hr_veronika.glb', 'assets/models/fps_hands_cs2.glb', 'assets/models/weapon_uzi_90s.glb')) {
        if (-not (Test-Path -LiteralPath (Join-Path $GameOut $required))) { throw "Missing game file: $required" }
    }

    Write-Host '[2/4] Packing clean game payload...'
    Compress-Archive -Path (Join-Path $GameOut '*') -DestinationPath $PayloadZip -CompressionLevel Optimal -Force

    Write-Host '[3/4] Publishing standalone installer with supplied icon...'
    dotnet publish (Join-Path $InstallerDir 'HowToVibeCoder_Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishOut --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
    $PublishedSetupExe = Join-Path $PublishOut 'HowToVibeCoder_Setup.exe'
    if (-not (Test-Path -LiteralPath $PublishedSetupExe)) { throw 'Installer executable missing.' }

    Write-Host '[4/4] Moving single verified installer to Desktop (purging any old installers)...'
    New-Item -ItemType Directory -Force $DesktopDir | Out-Null
    foreach ($old in @(
        (Join-Path $DesktopDir 'HowToVibeCoder_Installer_*.exe'),
        (Join-Path $DesktopDir 'HowToVibeCoder.bat'),
        (Join-Path $DesktopDir 'How to Vibe Coder (Играть).lnk')
    )) {
        Get-Item -Path $old -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
    $DesktopExe = Join-Path $DesktopDir 'HowToVibeCoder_Setup.exe'
    Copy-Item -LiteralPath $PublishedSetupExe -Destination $DesktopExe -Force
    Get-FileHash -LiteralPath $DesktopExe -Algorithm SHA256
}
finally {
    # Always delete temporary staging & intermediate zip so zero gigabytes pile up on disk
    foreach ($temp in @($StageBase, $PayloadZip, $PublishBackupDir)) {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
