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

# 1. Save-data check, then build only - nothing touches the game folder yet. The check is
#    here as well as in build.ps1 because this script builds on its own, and this is the
#    last point before the DLL can reach someone's save.
& (Join-Path $PSScriptRoot "check-save-ids.ps1")
if ($LASTEXITCODE -ne 0) { throw "Save-data check failed - nothing was copied to the game folder." }

& (Join-Path $PSScriptRoot "check-localization.ps1")
if ($LASTEXITCODE -ne 0) { throw "Localization check failed - nothing was copied to the game folder." }

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

# 3b. The Vietnamese folder only ships where the community patch is.
#
# Vietnamese is not a vanilla language: its id, its name and its strings come from the
# community patch, and so does the font that draws the accents (4.2 R2, R5). Without the
# patch there is no Vietnamese language for our strings to join, and AC2 wants the language
# list to look exactly as it did. Whether the launcher would actually gain an entry is 4.2
# ST-1(b) and it is NOT verified - it needs a second install, and this machine has the patch.
#
# So rather than ship it and hope, the folder is removed from the deployed module when the
# patch is absent. That makes AC2 true by construction instead of by argument, and it cannot
# be got wrong by a player: no patch, no folder. Nothing of the patch's is touched or read
# apart from one file's existence.
# The folder MSBuild actually deployed to. $GameFolder is empty when BANNERLORD_GAME_DIR is unset and
# Directory.Build.props found the install itself, which used to skip this whole block silently.
$resolved = $GameFolder
if (-not $resolved) { $resolved = (dotnet msbuild (Join-Path $repo "src/DiplomacyIntrigue/DiplomacyIntrigue.csproj") -getProperty:GameFolder -nologo 2>$null | Select-Object -Last 1).Trim() }
if ($resolved) {
    $GameFolder = $resolved
    $patch = Join-Path $GameFolder "Modules\Native\ModuleData\Languages\VI\language_data.xml"
    $ours = Join-Path $GameFolder "Modules\DiplomacyIntrigue\ModuleData\Languages\VI"
    if (-not (Test-Path $patch) -and (Test-Path $ours)) {
        Remove-Item $ours -Recurse -Force
        Write-Host ""
        Write-Host "VI removed from the deployed module: no Vietnamese community patch on this" -ForegroundColor Yellow
        Write-Host "install, so the folder would join a language that does not exist (story 4.2 AC2)." -ForegroundColor Yellow
    } elseif (Test-Path $patch) {
        Write-Host ""
        Write-Host "VI kept: the community patch is present, so the folder joins its language." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Deployed. Enable 'Diplomacy & Intrigue' in the launcher." -ForegroundColor Green
