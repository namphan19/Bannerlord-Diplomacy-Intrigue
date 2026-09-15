#requires -Version 7
<#
    Builds Diplomacy & Intrigue and copies the whole module tree into the game Modules folder.
    This WRITES INTO THE GAME INSTALL. Close Bannerlord first.

    Usage:  pwsh ./scripts/deploy.ps1 [-Configuration Release] [-GameFolder "D:\...\Mount and Blade II Bannerlord"]
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GameFolder = $env:BANNERLORD_GAME_DIR
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

if (Get-Process -Name "Bannerlord*" -ErrorAction SilentlyContinue) {
    throw "Bannerlord is running. Close the game before deploying."
}

$buildArgs = @("build", (Join-Path $repo "DiplomacyIntrigue.sln"), "-c", $Configuration, "--nologo", "-p:DeployToGame=true")
if ($GameFolder) { $buildArgs += "-p:GameFolder=$GameFolder" }

dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Deploy failed with exit code $LASTEXITCODE." }

Write-Host "Deployed. Enable 'Diplomacy & Intrigue' in the launcher." -ForegroundColor Green
