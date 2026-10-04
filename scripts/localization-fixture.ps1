#requires -Version 7
<#
    Writes a handful of German strings into the DEPLOYED module's language folder, so a
    language session has something to show. Removes them again with -Remove.

    Why this exists. Story 4.1 AC2 asks whether a second language really switches, and the
    12 non-English folders ship empty on purpose (lead, 2026-10-03). An empty folder proves
    nothing: every key falls back to the English the code carries, which is R1 working, and a
    screen full of English looks exactly like the mod ignoring the language. That is what the
    lead's first test on 2026-10-04 showed, and the log says why:

        [11:15:45] Native/.../Languages/DE/de_functions.xml        the game switched to German
        [11:15:46] DiplomacyIntrigue/.../Languages/DE/di_strings.xml   and opened OUR DE file

    The plumbing is proven. What is left to see is text, and text needs a translation.

    This is a test fixture and NOT a translation. It is machine-written, unreviewed by a native
    reader, and it exists to answer "does the text switch", not to be shipped - the same
    argument the lead accepted for shipping the folders empty. -Remove puts the folder back to
    the repo's empty file.

    Usage:
      pwsh ./scripts/localization-fixture.ps1 -Write        # German keys into the deployed DE folder
      pwsh ./scripts/localization-fixture.ps1 -Remove       # back to the repo's empty file
      pwsh ./scripts/localization-fixture.ps1 -Write -GameDir "D:\...\Mount & Blade II Bannerlord"
      pwsh ./scripts/localization-fixture.ps1 -Show         # print what would be written

    The game does not need restarting after this: it reloaded every module's strings at runtime
    when the language changed (rgl_log 2026-10-04, 11:15:43 -> 11:15:46).
#>
param(
    [ValidateSet('Write', 'Remove', 'Show')]
    [string]$Action = 'Show',
    [string]$Language = 'DE',
    [string]$GameDir = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = $env:BANNERLORD_GAME_DIR
}
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    foreach ($candidate in @(
        'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord',
        'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
        'D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord')) {
        if (Test-Path $candidate) { $GameDir = $candidate; break }
    }
}
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw 'Game directory not found. Pass -GameDir.'
}

$target = Join-Path $GameDir "Modules\DiplomacyIntrigue\ModuleData\Languages\$Language\di_strings.xml"
$repoEmpty = Join-Path $repo "module\DiplomacyIntrigue\ModuleData\Languages\$Language\di_strings.xml"

# Key -> German. Each one is a label a player reads on the Realm tab, the Court tab or the
# Kingdom screen's tab strip, so a single screen shows several at once. Word order is not the
# English word order in two of them ("ein Königreich gehorcht {NAME}" for "one kingdom answers
# to {NAME}"), which is the whole point of AC2 and R3.
$fixture = [ordered]@{
    'DI_REALM_REALM_2'                        = 'Reich'
    'DI_COURT_COURT'                          = 'Hof'
    'DI_COURT_THE_COURT'                      = 'Der Hof'
    'DI_COURT_COURT_BLOCS'                    = 'Phe des Hofes'
    'DI_COURT_OFFICES_CHOOSE_A_SEAT_THEN_A'   = 'ÄMTER – ZUERST DER SITZ, DANN DAS HAUS'
    'DI_REALM_ONE_KINGDOM_ANSWERS_TO_NAME_2'   = 'ein Königreich gehorcht {NAME}'
    'DI_REALM_OUR_CLAIMS_TITLE'               = 'Unsere Ansprüche'
    'DI_REALM_OUR_AGREEMENTS_TITLE'           = 'Unsere Vereinbarungen'
    'DI_REALM_TRIBUTE_PER_PERIOD'             = 'Tribut je Zeitraum'
    'DI_REALM_ENTITLES_LAND_2'                = 'berechtigt zu Land'
    'DI_REALM_AGES_OUT_IN_DAYS_MAX_2'         = 'verfällt in {MAX} Tagen'
    'DI_REALM_WRITE_A_REPORT_TO_FILE'         = 'Bericht in Datei schreiben'
    'DI_MENU_OUR_CLAIMS_2'                    = 'Unsere Ansprüche'
    'DI_INTEL_ODDS_ARE_THE_ONES_THE_ROLL'     = 'Jede Chance auf diesem Tab ist die, mit der gewürfelt wird. Auch die KI-Häuser lesen dieselben Zahlen.'
}

# The repository's own key set, so a renamed key is caught here rather than as English on a
# screen. The check script does the same thing for the whole file; this is the fixture's own guard.
$english = @{}
if (Test-Path (Join-Path $repo 'module\DiplomacyIntrigue\ModuleData\Languages\EN\di_strings.xml')) {
    $en = [System.IO.File]::ReadAllText((Join-Path $repo 'module\DiplomacyIntrigue\ModuleData\Languages\EN\di_strings.xml'))
    foreach ($m in [regex]::Matches($en, '<string\s+id="(?<id>[^"]*)"\s+text="(?<text>[^"]*)"\s*/>')) {
        $english[$m.Groups['id'].Value] = $m.Groups['text'].Value
    }
}

function Get-Variables([string]$text) {
    return @([regex]::Matches($text, '\{([A-Z][A-Z0-9_]*)\}') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

$problems = @()
foreach ($key in $fixture.Keys) {
    if (-not $english.ContainsKey($key)) { $problems += "$key is not a key in EN/di_strings.xml"; continue }
    $want = Get-Variables $english[$key]
    $have = Get-Variables $fixture[$key]
    if (($want -join ',') -ne ($have -join ',')) {
        $problems += "${key}: English wants {$(($want -join ', '))}, the German has {$(($have -join ', '))}"
    }
}
if ($problems.Count -gt 0) {
    Write-Host 'Fixture refused - it would not match the English:' -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}

Write-Host "Fixture: $($fixture.Count) keys, all present in EN and all carrying the same {VARIABLES}." -ForegroundColor DarkGray
Write-Host "  DI_REALM_ONE_KINGDOM_ANSWERS_TO_YOU_2 is left out on purpose: AC2 also asks that one" -ForegroundColor DarkGray
Write-Host "  missing key falls back to English and changes nothing else." -ForegroundColor DarkGray

if ($Action -eq 'Show') {
    foreach ($key in $fixture.Keys) { Write-Host ("  {0,-45} {1}" -f $key, $fixture[$key]) }
    Write-Host "`nNothing written. Target would be: $target"
    exit 0
}

if ($Action -eq 'Remove') {
    Copy-Item $repoEmpty $target -Force
    Write-Host "Restored the repo's empty $Language/di_strings.xml -> $target" -ForegroundColor Green
    exit 0
}

# Write. UTF-8 without a BOM, which is what tools/Localize emit produces for EN, so the fixture
# is the same shape a translator's file would be.
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<!-- TEST FIXTURE, written by scripts/localization-fixture.ps1 on ' +
    (Get-Date -Format 'yyyy-MM-dd HH:mm') + '. Not a translation and not for release: machine-written,')
[void]$sb.AppendLine('     unreviewed by a native reader, and it exists only to answer story 4.1 AC2 - does the')
[void]$sb.AppendLine('     text switch when the game switches language. scripts/localization-fixture.ps1 -Remove')
[void]$sb.AppendLine('     puts the repo''s empty file back. The shipped folders stay empty (lead, 2026-10-03). -->')
[void]$sb.AppendLine('<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" type="string">')
[void]$sb.AppendLine('  <tags>')
[void]$sb.AppendLine("    <tag language=""Deutsch"" />")
[void]$sb.AppendLine('  </tags>')
[void]$sb.AppendLine('  <strings>')
foreach ($key in $fixture.Keys) {
    $text = $fixture[$key].Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;').Replace('"', '&quot;')
    [void]$sb.AppendLine("    <string id=""$key"" text=""$text"" />")
}
[void]$sb.AppendLine('  </strings>')
[void]$sb.AppendLine('</base>')

$dir = Split-Path -Parent $target
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllText($target, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

# Read it back and prove the characters survived: an umlaut written through the wrong encoding
# is a fixture that silently stops being German.
$read = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)
$umlauts = ([regex]::Matches($read, '[\u00C0-\u00FF]')).Count
$written = ([regex]::Matches($read, '<string\s+id=')).Count
Write-Host "Wrote $written German keys -> $target" -ForegroundColor Green
Write-Host "  $umlauts non-ASCII characters read back (a run through the wrong encoding would be 0)." -ForegroundColor DarkGray
if ($umlauts -eq 0) { Write-Host '  SUSPECT: no non-ASCII survived - check the encoding before trusting this.' -ForegroundColor Red; exit 1 }
Write-Host '  Switch the game to Deutsch. No restart: it reloads every module''s strings at runtime.' -ForegroundColor Green
