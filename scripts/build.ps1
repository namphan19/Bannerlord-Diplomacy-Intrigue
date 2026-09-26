#requires -Version 7
<#
    Builds Diplomacy & Intrigue without touching the game folder.
    Output lands in module/DiplomacyIntrigue/bin/Win64_Shipping_Client.

    Usage:  pwsh ./scripts/build.ps1 [-Configuration Release]
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

# Save data first: a broken definition compiles and plays, and breaks the save (CLAUDE.md §3).
& (Join-Path $PSScriptRoot "check-save-ids.ps1")
if ($LASTEXITCODE -ne 0) { throw "Save-data check failed - see above." }

dotnet build (Join-Path $repo "DiplomacyIntrigue.sln") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

Write-Host "Build OK -> module/DiplomacyIntrigue/bin/Win64_Shipping_Client" -ForegroundColor Green
