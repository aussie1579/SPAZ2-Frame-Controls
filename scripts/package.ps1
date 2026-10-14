# Builds the plugin and produces two release zips in dist/:
#   SPAZ2FrameControls-<ver>.zip           mod only, for people who already have BepInEx 5 installed
#   SPAZ2FrameControls-<ver>-with-BepInEx.zip  BepInEx 5 + the Unity 5.4 entrypoint fix + the mod, extract and play
#
# Usage: pwsh scripts/package.ps1

$ErrorActionPreference = 'Stop'

$BepInExVersion = '5.4.23.5'
$BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"
$BepInExLicenseUrl = 'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE'

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$cache = Join-Path $root 'obj\package-cache'

[xml]$proj = Get-Content (Join-Path $src 'SPAZ2FrameControls.csproj')
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version

# Prefer the 64-bit SDK; a 32-bit dotnet earlier on PATH may have no SDKs installed.
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

# Build without deploying to a local game install.
& $dotnet build $src -c Release -p:GameDir=__none__
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$dll = Join-Path $src 'bin\Release\SPAZ2FrameControls.dll'

New-Item -ItemType Directory -Force $cache | Out-Null
$bepZip = Join-Path $cache "BepInEx_win_x64_$BepInExVersion.zip"
$bepLicense = Join-Path $cache 'BepInEx-LICENSE.txt'
if (-not (Test-Path $bepZip)) { Invoke-WebRequest $BepInExUrl -OutFile $bepZip }
if (-not (Test-Path $bepLicense)) { Invoke-WebRequest $BepInExLicenseUrl -OutFile $bepLicense }

if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
$modStage = Join-Path $dist 'stage-mod'
$fullStage = Join-Path $dist 'stage-full'

# Mod only.
$pluginDir = Join-Path $modStage 'BepInEx\plugins\SPAZ2FrameControls'
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item $dll $pluginDir
Copy-Item (Join-Path $root 'README.md') (Join-Path $pluginDir 'README.md')
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $pluginDir 'LICENSE.txt')

# Full package: BepInEx + entrypoint fix + mod.
New-Item -ItemType Directory -Force $fullStage | Out-Null
Expand-Archive $bepZip -DestinationPath $fullStage
Copy-Item -Recurse (Join-Path $modStage 'BepInEx') $fullStage -Force
Copy-Item $bepLicense (Join-Path $fullStage 'BepInEx\BepInEx-LICENSE.txt')
$cfgDir = Join-Path $fullStage 'BepInEx\config'
New-Item -ItemType Directory -Force $cfgDir | Out-Null
# BepInEx fills in every other setting on first launch. Unity 5.4 needs the chainloader to start
# after game scripts are registered, or the game shows a black screen.
@'
[Preloader.Entrypoint]

Assembly = UnityEngine.dll
Type = Camera
Method = .cctor
'@ | Set-Content -Encoding utf8 (Join-Path $cfgDir 'BepInEx.cfg')

$modZip = Join-Path $dist "SPAZ2FrameControls-$version.zip"
$fullZip = Join-Path $dist "SPAZ2FrameControls-$version-with-BepInEx.zip"
Compress-Archive -Path (Join-Path $modStage '*') -DestinationPath $modZip
Compress-Archive -Path (Join-Path $fullStage '*') -DestinationPath $fullZip
Remove-Item -Recurse -Force $modStage, $fullStage

Write-Host "Packaged:`n  $modZip`n  $fullZip"
