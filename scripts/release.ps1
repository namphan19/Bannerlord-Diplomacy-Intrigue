#requires -Version 7
<#
    Packages a release zip for Nexus: artifacts/release/DiplomacyIntrigue-v<version>.zip.

    Why this is not build.ps1 plus a zip: build.ps1 compiles against whatever game is installed, and
    on 2026-09-26 the lead's Steam install moved to the beta branch (v1.5.3). v0.1.0 was zipped 33
    minutes later from that build and shipped to players on v1.4.8. It happened to be harmless, but
    enum values are compiled in as numbers and a renumbered enum would have broken every comparison
    without an error anywhere. So the DLL shipped here is always the one scripts/compile-check.sh
    builds against BUTR's v1.4.8 reference assemblies, whatever the local game is.

    What it checks: the save-data rules, a clean tree for src/ and module/ (a release is a commit),
    and that SubModule.xml carries the version Directory.Build.props declares. What it cannot: that
    the game loads the DLL on v1.4.8 - nothing on this machine runs v1.4.8.

    Usage:  pwsh ./scripts/release.ps1
#>
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "check-save-ids.ps1")
if ($LASTEXITCODE -ne 0) { throw "Save-data check failed - see above." }

$dirty = git -C $repo status --porcelain -- src module Directory.Build.props
if ($dirty) { throw "Uncommitted changes under src/ or module/ - commit first, a release is a commit:`n$dirty" }
$commit = (git -C $repo rev-parse --short HEAD).Trim()

[xml] $props = Get-Content (Join-Path $repo "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.ModuleVersion } | Select-Object -First 1).ModuleVersion
[xml] $sub = Get-Content (Join-Path $repo "module/DiplomacyIntrigue/SubModule.xml")
if ($sub.Module.Version.value -ne "v$version") {
    throw "SubModule.xml says $($sub.Module.Version.value), Directory.Build.props says $version."
}

$zip = Join-Path $repo "artifacts/release/DiplomacyIntrigue-v$version.zip"
if (Test-Path $zip) { throw "$zip already exists - a shipped zip is never overwritten; bump the version." }

# Git's own bash: from PowerShell a bare "bash" can resolve to WSL's.
$bash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) "bin/bash.exe"
if (-not (Test-Path $bash)) { throw "Git Bash not found at $bash." }
$cache = Join-Path ([IO.Path]::GetTempPath()) "di-compile-check"
& $bash (Join-Path $repo "scripts/compile-check.sh") ($cache -replace '\\', '/')
if ($LASTEXITCODE -ne 0) { throw "Build against v1.4.8 reference assemblies failed." }

$stage = Join-Path ([IO.Path]::GetTempPath()) "di-release-$version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$mod = Join-Path $stage "DiplomacyIntrigue"
Copy-Item (Join-Path $repo "module/DiplomacyIntrigue") $mod -Recurse
$bin = Join-Path $mod "bin/Win64_Shipping_Client"
if (Test-Path $bin) { Remove-Item $bin -Recurse -Force }
New-Item -ItemType Directory -Force $bin | Out-Null
Copy-Item (Join-Path $cache "out/DiplomacyIntrigue.dll"), (Join-Path $cache "out/DiplomacyIntrigue.pdb") $bin

New-Item -ItemType Directory -Force (Split-Path $zip) | Out-Null
Compress-Archive -Path $mod -DestinationPath $zip

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
Write-Host "Release v$version from $commit -> $zip" -ForegroundColor Green
Write-Host "SHA256 $hash"
Get-ChildItem $mod -Recurse -File | ForEach-Object { "  " + $_.FullName.Substring($stage.Length + 1) }
