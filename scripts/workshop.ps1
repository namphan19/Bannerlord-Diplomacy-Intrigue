#requires -Version 7
<#
    Updates the mod's Steam Workshop item from the folder scripts/release.ps1 staged.

    Prepares by default, and uploads only with -Upload. An upload is public the moment it lands: 17
    players were subscribed when this was written (2026-10-03), and Steam pushes an update to all of
    them on their next launch. So the default run writes the task file, checks it, and prints what
    an upload would send; the lead runs -Upload.

    Why not the "Bannerlord: Mod Uploader" tool (Workshop item 3634477895) that published the item:
    it uploads from the game's own Modules\DiplomacyIntrigue folder, which holds whatever
    deploy.ps1 last installed - on this machine a build against the local v1.5.3 game, or a test
    build. The release must be the DLL built against the v1.4.8 references (release.ps1's header),
    so this script points TaleWorlds' own uploader, which that tool wraps, at the staged folder.

    Why an update and never a create: the item exists. The Mod Uploader created it on 2026-09-30
    (its log, bin\Win64_Shipping_Client\steam_workshop_uploader.txt: "Item created. Item ID is
    3810668052"). Another CreateItem would publish a second, duplicate item.

    The task file follows TaleWorlds' documented format (GetItem + UpdateItem), the same one the Mod
    Uploader's templates/UpdateExisting.xml uses. It is written with XmlWriter so a change note's
    line breaks survive as &#xA; - a raw newline inside an XML attribute is read back as a space.

    NOT YET RUN WITH -Upload. The prepare path was run on 2026-10-03; the upload path follows the
    documented invocation (TaleWorlds.MountAndBlade.SteamWorkshop.exe <task.xml>, from the game's
    bin folder, Steam running and logged in as the item's owner) and has not been exercised.

    Usage:
      pwsh ./scripts/workshop.ps1 -ChangeNotes docs/release/CHANGELOG-v0.3.0.txt
      pwsh ./scripts/workshop.ps1 -ChangeNotes ... -Description docs/release/workshop-description.bbcode
      pwsh ./scripts/workshop.ps1 -ChangeNotes ... -Upload
#>
param(
    [Parameter(Mandatory)] [string] $ChangeNotes,
    [string] $Description,
    [string] $Image,
    [switch] $Upload
)
$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

# The published item. Never change this to create a new one; see the header.
$itemId = "3810668052"

[xml] $props = Get-Content (Join-Path $repo "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.ModuleVersion } | Select-Object -First 1).ModuleVersion
$mod = Join-Path $repo "artifacts/release/v$version/DiplomacyIntrigue"
if (-not (Test-Path (Join-Path $mod "SubModule.xml"))) {
    throw "No staged release at $mod - run scripts/release.ps1 first."
}
[xml] $sub = Get-Content (Join-Path $mod "SubModule.xml")
if ($sub.Module.Version.value -ne "v$version") {
    throw "The staged SubModule.xml says $($sub.Module.Version.value), not v$version."
}

function Resolve-Input([string] $path) {
    if ([string]::IsNullOrEmpty($path)) { return $null }
    $full = if ([IO.Path]::IsPathRooted($path)) { $path } else { Join-Path $repo $path }
    if (-not (Test-Path $full)) { throw "Not found: $full" }
    return (Resolve-Path $full).Path
}

$notesText = Get-Content (Resolve-Input $ChangeNotes) -Raw
$descText = if ($Description) { Get-Content (Resolve-Input $Description) -Raw } else { $null }
# Steam's limit on a description; an over-long one fails the whole update, content included.
if ($descText -and $descText.Length -gt 8000) {
    throw "The description is $($descText.Length) characters; Steam allows 8,000."
}
$imagePath = Resolve-Input $(if ($Image) { $Image } else { "docs/release/workshop-preview.jpg" })
# Steam refuses a preview over 1 MB.
if ((Get-Item $imagePath).Length -gt 1MB) { throw "The preview image is over 1 MB: $imagePath" }

$game = $env:BANNERLORD_GAME_DIR
if (-not $game) { $game = "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord" }
$bin = Join-Path $game "bin\Win64_Shipping_Client"
$uploader = Join-Path $bin "TaleWorlds.MountAndBlade.SteamWorkshop.exe"
if (-not (Test-Path $uploader)) { throw "TaleWorlds' uploader not found at $uploader (set BANNERLORD_GAME_DIR)." }

$task = Join-Path $repo "artifacts/release/workshop-update-v$version.xml"
$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$w = [System.Xml.XmlWriter]::Create($task, $settings)
try {
    $w.WriteStartElement("Tasks")
    $w.WriteStartElement("GetItem")
    $w.WriteStartElement("ItemId"); $w.WriteAttributeString("Value", $itemId); $w.WriteEndElement()
    $w.WriteEndElement()
    $w.WriteStartElement("UpdateItem")
    $w.WriteStartElement("ModuleFolder"); $w.WriteAttributeString("Value", (Resolve-Path $mod).Path); $w.WriteEndElement()
    $w.WriteStartElement("ChangeNotes"); $w.WriteAttributeString("Value", $notesText.Trim()); $w.WriteEndElement()
    if ($descText) {
        $w.WriteStartElement("ItemDescription"); $w.WriteAttributeString("Value", $descText.Trim()); $w.WriteEndElement()
    }
    $w.WriteStartElement("Image"); $w.WriteAttributeString("Value", $imagePath); $w.WriteEndElement()
    $w.WriteEndElement()
    $w.WriteEndElement()
}
finally { $w.Close() }

# Read it back the way the uploader will, so an escaping fault shows here and not on Steam.
[xml] $check = Get-Content $task -Raw
if ($check.Tasks.UpdateItem.ChangeNotes.Value -ne $notesText.Trim()) { throw "The change notes did not survive the round trip." }

$files = Get-ChildItem $mod -Recurse -File
Write-Host "Workshop item $itemId <- Diplomacy & Intrigue v$version" -ForegroundColor Cyan
Write-Host "  folder       $mod ($($files.Count) files, $([math]::Round(($files | Measure-Object Length -Sum).Sum / 1KB)) KB)"
$files | ForEach-Object { "    " + $_.FullName.Substring($mod.Length + 1) }
Write-Host "  preview      $imagePath"
Write-Host "  change notes $($notesText.Trim().Length) characters"
Write-Host "  description  $(if ($descText) { "$($descText.Trim().Length) characters - REPLACES the page text" } else { 'unchanged' })"
Write-Host "  task file    $task"

if (-not $Upload) {
    Write-Host "Prepared only. Run again with -Upload to publish (Steam must be running)." -ForegroundColor Yellow
    return
}

if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { throw "Steam is not running." }
Push-Location $bin
try {
    & $uploader $task
    if ($LASTEXITCODE -ne 0) { throw "The uploader exited with $LASTEXITCODE - see $bin\steam_workshop_uploader.txt" }
}
finally { Pop-Location }
Write-Host "Done. The uploader's own log: $bin\steam_workshop_uploader.txt" -ForegroundColor Green
