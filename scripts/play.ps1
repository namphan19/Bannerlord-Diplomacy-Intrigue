# Launches the game the way that is known to work on this machine.
#
# Why this exists: the official launcher does not spawn the game as a child process - it
# calls the starter's entry point inside its own process (see Launcher.Library.Program.Main,
# which ends in TaleWorlds.Starter.Library.Program.Main). On 2026-09-15 every launcher-hosted
# start died before the main menu with an unhandled exception in the engine's animation cache,
# while every direct start of Bannerlord.exe and of BLSE Standalone reached the menu. Until
# that is root-caused, this is the launch path to use.
#
# It reads the module selection out of the launcher's own LauncherData.xml, so ticking and
# unticking mods in the launcher UI still works as usual - just close the launcher instead of
# pressing Play, and run this.
#
# Usage:  pwsh ./scripts/play.ps1              (BLSE - preferred, gives readable crash reports)
#         pwsh ./scripts/play.ps1 -NoBlse      (plain Bannerlord.exe, for isolating BLSE)
#         pwsh ./scripts/play.ps1 -Without DiplomacyIntrigue    (bisect a suspected mod)

param(
    [switch] $NoBlse,
    [string[]] $Without = @()
)

$ErrorActionPreference = "Stop"

$gameDir = if ($env:BANNERLORD_GAME_DIR) { $env:BANNERLORD_GAME_DIR }
           else { "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord" }
$binDir = Join-Path $gameDir "bin\Win64_Shipping_Client"

if (Get-Process -Name "Bannerlord*", "TaleWorlds*" -ErrorAction SilentlyContinue) {
    throw "The game is already running. Close it first - do not force-kill it."
}

$launcherData = Join-Path ([Environment]::GetFolderPath("MyDocuments")) `
                          "Mount and Blade II Bannerlord\Configs\LauncherData.xml"
if (-not (Test-Path $launcherData)) { throw "No LauncherData.xml at $launcherData" }

[xml] $data = Get-Content $launcherData
$selected = @($data.UserData.SingleplayerData.ModDatas.UserModData |
              Where-Object { $_.IsSelected -eq "true" } |
              ForEach-Object { $_.Id } |
              Where-Object { $Without -notcontains $_ })

if ($selected.Count -eq 0) { throw "No modules are selected in the launcher." }

# The engine's own format: ids separated by '*', wrapped in _MODULES_ markers.
$moduleArg = "_MODULES_*" + ($selected -join "*") + "*_MODULES_"

$exe = if ($NoBlse) { "Bannerlord.exe" } else { "Bannerlord.BLSE.Standalone.exe" }
$exePath = Join-Path $binDir $exe
if (-not (Test-Path $exePath)) { throw "Missing $exePath" }

Write-Host "Launching $exe with $($selected.Count) modules:" -ForegroundColor Cyan
$selected | ForEach-Object { Write-Host "  - $_" }
if ($Without.Count -gt 0) { Write-Host "Excluded: $($Without -join ', ')" -ForegroundColor Yellow }

$proc = Start-Process -FilePath $exePath -ArgumentList "/singleplayer", $moduleArg `
                      -WorkingDirectory $binDir -PassThru
Write-Host "Started pid=$($proc.Id). Mod log: Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs" `
           -ForegroundColor Green
