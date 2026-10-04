#requires -Version 7
<#
    Packages a release: artifacts/release/DiplomacyIntrigue-v<version>.zip for Nexus, and the same
    module folder unzipped at artifacts/release/v<version>/DiplomacyIntrigue, which is what
    scripts/workshop.ps1 uploads to Steam. One staged folder for both, so the two stores can never
    carry different builds of the same version.

    Why this is not build.ps1 plus a zip: build.ps1 compiles against whatever game is installed, and
    on 2026-09-26 the lead's Steam install moved to the beta branch (v1.5.3). v0.1.0 was zipped 33
    minutes later from that build and shipped to players on v1.4.8. It happened to be harmless, but
    enum values are compiled in as numbers and a renumbered enum would have broken every comparison
    without an error anywhere. So the DLL shipped here is always the one scripts/compile-check.sh
    builds against BUTR's v1.4.8 reference assemblies, whatever the local game is.

    The build is a release build (DI_RELEASE_BUILD=1): player-facing defaults, today telemetry off
    (TODO decision 11). Every other build keeps the defaults the balance runs were measured with.

    What it checks: the save-data rules, a clean tree for src/ and module/ (a release is a commit),
    and that SubModule.xml and SubModule.ModuleVersion carry the version Directory.Build.props
    declares. What it cannot: that the game loads the DLL on v1.4.8 - nothing on this machine runs
    v1.4.8.

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
# The in-game "loaded" notice and the log header read this constant, not the XML.
$codeVersion = Select-String -Path (Join-Path $repo "src/DiplomacyIntrigue/SubModule.cs") `
    -Pattern 'ModuleVersion = "([^"]+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value }
if ($codeVersion -ne $version) {
    throw "SubModule.cs says $codeVersion, Directory.Build.props says $version."
}

$zip = Join-Path $repo "artifacts/release/DiplomacyIntrigue-v$version.zip"
if (Test-Path $zip) { throw "$zip already exists - a shipped zip is never overwritten; bump the version." }
$stage = Join-Path $repo "artifacts/release/v$version"
if (Test-Path $stage) { throw "$stage already exists - a shipped folder is never overwritten; bump the version." }

# Git's own bash: from PowerShell a bare "bash" can resolve to WSL's.
# Walk up from git.exe: it sits in Git\cmd from PowerShell but in Git\mingw64\bin from Git Bash.
$bash = $null
$dir = Split-Path (Get-Command git).Source
while ($dir -and -not $bash) {
    $candidate = Join-Path $dir "bin/bash.exe"
    if ((Test-Path $candidate) -and (Test-Path (Join-Path $dir "git-bash.exe"))) { $bash = $candidate }
    $dir = Split-Path $dir
}
if (-not $bash) { throw "Git Bash not found above $((Get-Command git).Source)." }
$cache = Join-Path ([IO.Path]::GetTempPath()) "di-compile-check"
$env:DI_RELEASE_BUILD = "1"
try {
    & $bash (Join-Path $repo "scripts/compile-check.sh") ($cache -replace '\\', '/')
    if ($LASTEXITCODE -ne 0) { throw "Build against v1.4.8 reference assemblies failed." }
}
finally {
    Remove-Item Env:DI_RELEASE_BUILD -ErrorAction SilentlyContinue
}

$mod = Join-Path $stage "DiplomacyIntrigue"
Copy-Item (Join-Path $repo "module/DiplomacyIntrigue") $mod -Recurse
# VI joins the community patch's Vietnamese; for everyone else it would define a second, broken
# language, and what the launcher does then was never verified (story 4.2 AC2). It is never in a
# release - a player who has the patch gets it from a separate download, once the strings exist.
$vi = Join-Path $mod "ModuleData/Languages/VI"
if (Test-Path $vi) { Remove-Item $vi -Recurse -Force }
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
Write-Host "Staged for Steam Workshop: $mod  (scripts/workshop.ps1)"
