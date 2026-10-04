#requires -Version 7
<#
    Checks the localization keys against the rules in CLAUDE.md §4 and story 4.1, from the
    source. build.ps1 and deploy.ps1 run it before compiling; it touches nothing and needs no
    game.

    A wrong key does not fail a build. It fails in a player's game, in their language, as text
    that is blank or as a raw "{=DI_...}" - and only for the people who read that language, which
    is why it survives every test session run in English. Nothing else in the project would
    notice it: the strings file is data, the engine falls back silently, and LoadProbe only
    proves the assembly loads.

    What it checks:
      1. A key in code with no entry in the English strings file (R1: a missing key must degrade
         to English, which only happens if the key IS the one the code asked for).
      2. An entry in the English file that no DiText call asks for - a translation nobody will
         ever read, and a sign the key was renamed on one side only.
      3. A key used twice with two different English texts. One key, one meaning: this is the
         mistake no translation file can be checked for later (R2).
      4. A DiText call whose key or English is not a literal, so neither this script nor
         tools/Localize can read it.
      5. A variable passed but never used in the English, or used in the English but never
         passed. The first leaves a hole in a sentence; the second prints "{AMOUNT}" on screen.
      6. A translation whose {VARIABLES} are not exactly the English's (AC4). A language that
         drops {KINGDOM} shows a blank where a name belongs.
      7. A key without the DI_ prefix, so one of ours can never shadow a vanilla string id.
      8. A source file that spells the engine's id form wrong. The only place that builds it is
         Core/DiText.cs, and on 2026-10-04 it built `{DI_}DI_REALM_REALM_2` instead of
         `{=DI_REALM_REALM_2}` - the prefix inside the braces instead of in front of the key. Every
         one of the 641 keys was broken and every screen drew the raw token, while rules 1-7 all
         passed, because the bug is in the format the engine is handed rather than in the key or
         the English. Rule 8 is the whole reason the ninth rule exists.
      9. A duplicate id inside one strings file.

    Reported, not failed: a literal left in a prefab's Text attribute (rule below). A widget's
    Text is a plain string, so it cannot be keyed where it stands and each one has to move into a
    view-model property first - story 4.1 ST-4, unfinished. It is listed so the gap is visible on
    every build rather than discovered by a player in another language, and it becomes a failure
    when the prefab work lands. Everything else above is a failure today.

    Usage:  pwsh ./scripts/check-localization.ps1 [-Source src/DiplomacyIntrigue] [-Module module/DiplomacyIntrigue]
    Exit code 0 when every rule holds, 1 otherwise.
#>
param(
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) "src/DiplomacyIntrigue"),
    [string]$Module = (Join-Path (Split-Path -Parent $PSScriptRoot) "module/DiplomacyIntrigue")
)

$ErrorActionPreference = "Stop"
$problems = [System.Collections.Generic.List[string]]::new()

function Strip([string]$line) {
    # Drops // comments so a key mentioned in prose is not read as a key in use.
    $at = $line.IndexOf("//")
    if ($at -ge 0) { return $line.Substring(0, $at) }
    return $line
}

# ----- The keys the code asks for -------------------------------------------------------------

$used = @{}          # key -> @{ English; Variables; Sites }
$callPattern = 'DiText\.(?:T|O)\(\s*"(?<key>(?:[^"\\]|\\.)*)"\s*,\s*"(?<english>(?:[^"\\]|\\.)*)"'
$barePattern = 'DiText\.(?:T|O)\('

$files = Get-ChildItem -Path $Source -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

foreach ($file in $files) {
    # The whole file at once, because one line can hold two calls (`female ? T(..) : T(..)`) and
    # a call can wrap over six. Counting parens to the call's own close is what tells a variable's
    # own `Foo(1)` from the end of the call.
    $text = Get-Content $file.FullName -Raw

    # A key written in a comment - the helper's own documentation shows the form - is not a key in
    # use, and reading it as one would fail the build over an example.
    $text = [regex]::Replace($text, '(?m)//.*$', '')

    $calls = [regex]::Matches($text, $callPattern)
    $bare = [regex]::Matches($text, $barePattern).Count

    foreach ($m in $calls) {
        $key = $m.Groups['key'].Value
        $english = $m.Groups['english'].Value
        $line = ($text.Substring(0, $m.Index) -split "`n").Count
        $where = "$($file.Name):$line"

        # Everything from the English's closing quote to the call's closing paren.
        $depth = 1
        $at = $m.Index + $m.Length
        $end = $text.Length
        while ($at -lt $text.Length) {
            if ($text[$at] -eq '(') { $depth++ }
            elseif ($text[$at] -eq ')') { $depth--; if ($depth -eq 0) { $end = $at; break } }
            $at++
        }
        $args = $text.Substring($m.Index + $m.Length, $end - ($m.Index + $m.Length))

        # A variable name is never a key: keys start with DI_ and variables never do. Without that
        # exclusion a nested DiText call's key reads as a variable of the outer one, and the
        # argument list of a call that wraps another call spans it.
        $passed = [regex]::Matches($args, '\(\s*"(?!DI_)([A-Z][A-Z0-9_]*)"\s*,') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        $wanted = [regex]::Matches($english, '\{([A-Z][A-Z0-9_]*)\}') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

        # Two kinds of value are not sentences, and a key on either of them breaks something:
        # a name the engine resolves ("SetDefaultSelectedItem", "DiPeaceTable") and an event id
        # ("player_voluntary"). Both were keyed by the first pass on 2026-10-03 - the first breaks
        # reflection and the second breaks the log - so the shape is refused rather than trusted.
        if ($english -notmatch '[a-z]{3}') {
            $problems.Add("${where}: $key is keyed with `"$english`", which has no lowercase word in it - that reads as a name (a prefab, a method by reflection), not as text.")
        }
        if ($english -match '^[a-z][a-z0-9]*(_[a-z0-9]+)+$') {
            $problems.Add("${where}: $key is keyed with `"$english`", which is an event or route id. Ids stay English: the log and analyse-log.py read them.")
        }

        foreach ($v in $passed) {
            if ($wanted -notcontains $v) { $problems.Add("${where}: $key passes {${v}} but the English never uses it.") }
        }
        foreach ($v in $wanted) {
            if ($passed -notcontains $v) { $problems.Add("${where}: $key uses {${v}} in the English but never passes it - the player would see the placeholder.") }
        }

        if (-not $used.ContainsKey($key)) {
            $used[$key] = @{ English = $english; Variables = @($wanted); Sites = @($where) }
        }
        else {
            $entry = $used[$key]
            if ($entry.English -ne $english) {
                $problems.Add("${where}: key $key is used with two different English texts:`n    $($entry.English)`n    $english")
            }
            $entry.Sites += $where
            foreach ($v in $wanted) { if ($entry.Variables -notcontains $v) { $entry.Variables += $v } }
        }
    }

    if ($calls.Count -lt $bare) {
        $problems.Add("$($file.Name): $bare DiText call(s), of which only $($calls.Count) could be read - a key or an English that is not a literal.")
    }
}

foreach ($key in ($used.Keys | Sort-Object)) {
    if ($key -notlike "DI_*") {
        $problems.Add("$key ($($used[$key].Sites[0])): a key without the DI_ prefix, so it could shadow a vanilla string id.")
    }
}

# ----- How the id is spelled -------------------------------------------------------------------

# The one thing no other rule here can see: the shape of the string handed to the engine. Rules 1-7
# all read the key and the English as two literals, and they are right about both, while the code
# built `{DI_}DI_REALM_REALM_2` - the prefix inside the braces rather than in front of the key -
# and the engine drew that token verbatim on every screen (2026-10-04, live, all 641 keys).
#
# The rule is deliberately narrow, because it is the only shape rule here that is not about a key:
# Core/DiText.cs is the one place in the mod that spells an id, it has to spell the sigil `{=` as a
# literal, and no file may contain the string the mistake produces. An earlier draft refused any
# `"{"` concatenated with anything, which also refuses the legitimate `{VARIABLE}` placeholder in
# DiText.Substitute - a rule that cannot tell an id from a placeholder is a rule that cries wolf,
# and a rule people learn to skip is worse than no rule.
$diText = Join-Path $Source "Core\DiText.cs"
if (Test-Path $diText) {
    $diTextSource = [regex]::Replace((Get-Content $diText -Raw), '(?m)//.*$', '')
    # A string literal that opens with `{=` - a brace, an equals sign. Two fixed characters, never
    # assembled: the one thing about the engine's id form that can be read off the source.
    $sigil = [regex]::Match($diTextSource, '"\{=[^"]*"')
    if ($sigil.Success) {
        Write-Host "Core\DiText.cs spells the engine's id sigil as a literal ($($sigil.Value) + key + `"}`" + english). Rule 8 satisfied." -ForegroundColor DarkGray
    }
    else {
        $problems.Add("Core\DiText.cs does not spell the engine's id sigil as a literal. It must build `{=KEY}English`: a brace, an equals sign, the key, then the brace. This is the bug of 2026-10-04, where `{DI_}KEY` left every screen drawing a raw token.")
    }
}

foreach ($file in $files) {
    $shape = [regex]::Replace((Get-Content $file.FullName -Raw), '(?m)//.*$', '')
    foreach ($m in [regex]::Matches($shape, '\$"[^"\r\n]*\{DI_\}')) {
        $problems.Add("$($file.Name): `$`"$($m.Value)`" puts DI_ inside the braces. The engine's id form is `{=DI_...}`.")
    }
    foreach ($m in [regex]::Matches($shape, '"\{DI_\}"')) {
        $problems.Add("$($file.Name): `"$($m.Value)`" is not the engine's id form. It is `{=DI_...}` - the sigil is `{=`, and it is what the resolver reads.")
    }
}

# ----- The strings files ---------------------------------------------------------------------

function Read-Strings([string]$path) {
    $map = @{}
    if (-not (Test-Path $path)) { return $map }
    foreach ($m in [regex]::Matches((Get-Content $path -Raw), '<string\s+id="(?<id>[^"]*)"\s+text="(?<text>[^"]*)"\s*/>')) {
        $id = $m.Groups['id'].Value
        if ($map.ContainsKey($id)) { $problems.Add("$([System.IO.Path]::GetFileName($path)): id $id appears twice in the file.") }
        $map[$id] = $m.Groups['text'].Value
    }
    return $map
}

$englishPath = Join-Path $Module "ModuleData\Languages\EN\di_strings.xml"
if (-not (Test-Path $englishPath)) {
    $problems.Add("No English strings file at $englishPath. tools/Localize emit --apply writes it from the DiText calls.")
} else {
    $english = Read-Strings $englishPath
    foreach ($key in ($used.Keys | Sort-Object)) {
        if (-not $english.ContainsKey($key)) {
            $problems.Add("$key ($($used[$key].Sites[0])) is used in code but has no entry in di_strings.xml - every language would fall back to English for it.")
        }
    }
    foreach ($id in ($english.Keys | Sort-Object)) {
        if (-not $used.ContainsKey($id)) {
            $problems.Add("$id is in di_strings.xml but no DiText call asks for it - a translation nobody will read.")
        }
    }
}

# Every other language: the same key set, or the same variables.
$languagesRoot = Join-Path $Module "ModuleData\Languages"
if (Test-Path $languagesRoot) {
    foreach ($dir in Get-ChildItem $languagesRoot -Directory | Where-Object Name -ne 'EN') {
        $stringsPath = Join-Path $dir.FullName "di_strings.xml"
        if (-not (Test-Path $stringsPath)) { continue }

        foreach ($m in [regex]::Matches((Get-Content $stringsPath -Raw), '<string\s+id="(?<id>[^"]*)"\s+text="(?<text>[^"]*)"\s*/>')) {
            $id = $m.Groups['id'].Value
            if (-not $used.ContainsKey($id)) {
                $problems.Add("$($dir.Name)/di_strings.xml: $id is translated but no DiText call asks for it.")
                continue
            }
            $source = $used[$id].English
            $want = [regex]::Matches($source, '\{([A-Z][A-Z0-9_]*)\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
            $have = [regex]::Matches($m.Groups['text'].Value, '\{([A-Z][A-Z0-9_]*)\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
            $missing = @($want | Where-Object { $have -notcontains $_ })
            $extra = @($have | Where-Object { $want -notcontains $_ })
            if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
                $problems.Add("$($dir.Name): $id has the wrong variables. English wants $($want -join ', '); this one has $($have -join ', ').")
            }
        }

        if (-not (Test-Path (Join-Path $dir.FullName "language_data.xml"))) {
            $problems.Add("$($dir.Name): no language_data.xml, so the game will not load the folder whatever is in it.")
        }
    }
}

$enData = Join-Path $Module "ModuleData\Languages\EN\language_data.xml"
if ((Test-Path $languagesRoot) -and -not (Test-Path $enData)) {
    $problems.Add("EN: no language_data.xml. The English strings load without it; the other languages need the same file to be found.")
}

# ----- Prefab text ------------------------------------------------------------------------------

# A widget's Text is a plain string: TextWidget.SetText hands it to TwoDimension.Text.Value and
# TextParser.Parse never reads a key (story 4.1 §9, verified by IL). So a literal here cannot be
# translated where it stands, and each one has to move into a view-model property. All 92 have
# (story 4.1 ST-4), and this is the guard that keeps them there.
#
# A label of punctuation alone is left alone deliberately: "-" and "+" on a button are structure,
# the way a number's sign is, and a translator asked to render them would only be able to make
# them worse.
$warnings = [System.Collections.Generic.List[string]]::new()
$prefabs = Join-Path $Module "GUI\Prefabs"
if (Test-Path $prefabs) {
    foreach ($file in Get-ChildItem $prefabs -Recurse -Filter *.xml) {
        $lines = Get-Content $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($m in [regex]::Matches($lines[$i], '\b[A-Za-z_]*Text\s*=\s*"(?<value>[^"]*)"')) {
                $value = $m.Groups['value'].Value
                if ($value.StartsWith("@")) { continue }
                if ($value.Length -eq 0) { continue }
                if ($value -match '^[\s\-+–—:;,. /|?!*+=<>()^%&…·]+$') { continue }
                $problems.Add("$($file.Name):$($i + 1): the prefab still holds the literal `"$value`". Move it into a view-model property and bind Text=""@Property"".")
            }
        }
    }
}

# Every property a prefab binds has to exist, or the widget binds to nothing and draws an empty
# label - which no session in English would catch, because English is what the literal said.
$bound = @{}
foreach ($file in Get-ChildItem $prefabs -Recurse -Filter *.xml) {
    foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw), '\b(?:Text|TooltipText|HeaderText|ButtonText)\s*=\s*"@(?<name>\w+)"')) {
        $bound[$m.Groups['name'].Value] = $true
    }
}
if ($bound.Count -gt 0) {
    $declared = @{}
    foreach ($file in $files) {
        foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw),
                     '\[DataSourceProperty\][^\r\n]*\b(?:public|internal)\s+[\w\.<>\[\]]+\s+(?<name>\w+)')) {
            $declared[$m.Groups['name'].Value] = $true
        }
        # Mixin properties carry the attribute on its own line, above the declaration.
        foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw),
                     '\[DataSourceProperty\]\s*\r?\n\s*public\s+[\w\.<>\[\]]+\s+(?<name>\w+)')) {
            $declared[$m.Groups['name'].Value] = $true
        }
    }
    foreach ($name in ($bound.Keys | Sort-Object)) {
        if (-not $declared.ContainsKey($name)) {
            $problems.Add("a prefab binds @${name}, and no [DataSourceProperty] of that name exists in the mod. The widget would draw an empty label.")
        }
    }
}

# ----- Report ------------------------------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host "Localization check FAILED ($($problems.Count)):" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}

$variableCount = ($used.Values | ForEach-Object { $_.Variables.Count } | Measure-Object -Sum).Sum
Write-Host ("Localization check OK: {0} keys in code, {1} of them carry {2} variables." -f `
    $used.Count, ($used.Values | Where-Object { $_.Variables.Count -gt 0 }).Count, $variableCount) -ForegroundColor Green

if ($warnings.Count -gt 0) {
    Write-Host ("Prefab text not keyed yet ({0}, story 4.1 ST-4 - artifacts/localization/prefabs.csv has the worklist):" -f $warnings.Count) -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host "  - $w" -ForegroundColor DarkYellow }
}

exit 0
