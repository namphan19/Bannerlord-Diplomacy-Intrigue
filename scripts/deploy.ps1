#requires -Version 7
<#
    Builds Diplomacy & Intrigue, checks that the game could actually load it, and only
    then copies the module tree into the game Modules folder.

    The pre-flight check runs BEFORE anything is written to the game folder. A module
    assembly the game cannot load produces no log of its own - the game reports only
    "could not be loaded correctly due to a dependency conflict" - so catching it here
    saves a launch cycle and avoids leaving a broken module installed.

    Usage:  pwsh ./scripts/deploy.ps1 [-Configuration Release] [-GameFolder "D:\...\Mount and Blade II Bannerlord"]
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GameFolder = $env:BANNERLORD_GAME_DIR
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

# Both names matter. The official launcher hosts the game inside its own process
# (Launcher.Library.Program.Main ends by calling the game Main), so a session started
# from it appears only as TaleWorlds.MountAndBlade.Launcher. During run 04 this guard
# saw nothing while a 30-minute balance run was in progress - it would have overwritten
# the DLL underneath it.
$running = Get-Process -Name "Bannerlord*", "TaleWorlds.MountAndBlade.Launcher" -ErrorAction SilentlyContinue
if ($running) {
    # Deliberately a refusal and not a kill. Someone may be several hours into a
    # balance run, and force-killing the game looks exactly like a crash to them:
    # the window vanishes and Windows writes a dump. Close it deliberately instead.
    $names = ($running | Select-Object -ExpandProperty ProcessName -Unique) -join ", "
    throw "Bannerlord is running ($names). Close the game first - do not force-kill it, someone may be playing."
}

# 1. Build only - nothing touches the game folder yet.
$buildArgs = @("build", (Join-Path $repo "DiplomacyIntrigue.sln"), "-c", $Configuration, "--nologo")
if ($GameFolder) { $buildArgs += "-p:GameFolder=$GameFolder" }

dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

# 2. Pre-flight: would the game load this assembly at all?
Write-Host ""
Write-Host "Load pre-flight check..." -ForegroundColor Cyan
$probeArgs = @("run", "--nologo", "--project", (Join-Path $repo "tools/LoadProbe"))
dotnet @probeArgs
if ($LASTEXITCODE -ne 0) {
    throw "Pre-flight check failed - the game would not load this module. Nothing was copied to the game folder."
}

# 3. Deploy. Incremental, so this is a copy rather than a rebuild.
$deployArgs = $buildArgs + "-p:DeployToGame=true"
dotnet @deployArgs
if ($LASTEXITCODE -ne 0) { throw "Deploy failed with exit code $LASTEXITCODE." }

Write-Host ""
Write-Host "Deployed. Enable 'Diplomacy & Intrigue' in the launcher." -ForegroundColor Green
