param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$DesktopDir = [Environment]::GetFolderPath('DesktopDirectory')
)
$ErrorActionPreference = 'Stop'
$InstallerDir = Join-Path $ProjectRoot 'Installer'
$IcoPath = Join-Path $InstallerDir 'HowToVibeCoder.ico'
if (-not (Test-Path -LiteralPath $IcoPath)) { throw "Missing icon: $IcoPath" }
$StageRoot = Join-Path $InstallerDir ('stage_tmp/' + [Guid]::NewGuid().ToString('N'))
$GameOut = Join-Path $StageRoot 'game'
$PublishOut = Join-Path $StageRoot 'setup'
New-Item -ItemType Directory -Force $GameOut, $PublishOut | Out-Null

Write-Host '[1/4] Publishing current game sources with .NET runtime...'
dotnet publish (Join-Path $ProjectRoot 'HowToVibeCoder.csproj') -c Release -r win-x64 --self-contained true -p:ApplicationIcon=$IcoPath -o $GameOut --nologo
if ($LASTEXITCODE -ne 0) { throw 'Game publish failed.' }
# Include all assets, including audio and artwork not covered by the game project.
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'assets') -Destination $GameOut -Recurse -Force
Copy-Item -LiteralPath $IcoPath -Destination (Join-Path $GameOut 'HowToVibeCoder.ico') -Force
foreach ($required in @('HowToVibeCoder.exe', 'HowToVibeCoder.dll', 'coreclr.dll', 'assets/ui/water.png', 'assets/ui/title.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GameOut $required))) { throw "Missing game file: $required" }
}

Write-Host '[2/4] Packing clean game payload...'
$PayloadZip = Join-Path $InstallerDir 'payload.zip'
Compress-Archive -Path (Join-Path $GameOut '*') -DestinationPath $PayloadZip -CompressionLevel Optimal -Force

Write-Host '[3/4] Publishing standalone installer with supplied icon...'
dotnet publish (Join-Path $InstallerDir 'HowToVibeCoder_Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishOut --nologo
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
$PublishedSetupExe = Join-Path $PublishOut 'HowToVibeCoder_Setup.exe'
if (-not (Test-Path -LiteralPath $PublishedSetupExe)) { throw 'Installer executable missing.' }

Write-Host '[4/4] Copying verified build output to Desktop...'
New-Item -ItemType Directory -Force $DesktopDir, (Join-Path $InstallerDir 'publish') | Out-Null
Copy-Item -LiteralPath $PublishedSetupExe -Destination (Join-Path $InstallerDir 'publish/HowToVibeCoder_Setup.exe') -Force
Copy-Item -LiteralPath $PublishedSetupExe -Destination (Join-Path $DesktopDir 'HowToVibeCoder_Setup.exe') -Force
Get-FileHash -LiteralPath (Join-Path $DesktopDir 'HowToVibeCoder_Setup.exe') -Algorithm SHA256
