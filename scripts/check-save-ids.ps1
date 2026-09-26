#requires -Version 7
<#
    Checks the save-data declarations against the rules in CLAUDE.md §3, from the source.
    build.ps1 and deploy.ps1 run it before compiling; it touches nothing.

    A save definition that is wrong compiles, loads and plays - and then breaks the save, or
    the save after it. A missing container definition "crashes on save, the single most common
    way to break a Bannerlord mod"; a duplicate id corrupts data without any error. Neither is
    caught by the compiler, LoadProbe or a short test session, so this looks at the source.

    What it checks:
      1. Inside each savable class, no SaveableProperty/SaveableField id is used twice.
      2. Every class with a saved member is registered with AddClassDefinition.
      3. Every List<T>/Dictionary<K,V> a saved member holds has a ConstructContainerDefinition.
      4. Every enum of this mod that a saved member holds is registered with AddEnumDefinition.
      5. No definer id is used twice, class and enum ids together (the definer adds its base to
         both, and they are believed to share one id space - CLAUDE.md §3), and every id stays
         inside the reserved block (1-99 above the base).

    What it does not check: that an id was not *renumbered* since the last release (that needs
    the previous release's ids; review the diff), nor anything outside ModSaveDefiner's own
    types. It reads source with regular expressions, which suits the plain one-attribute-per-
    member style the Models use; an unusual declaration it cannot parse is reported, not
    skipped.

    Usage:  pwsh ./scripts/check-save-ids.ps1 [-Source src/DiplomacyIntrigue]
    Exit code 0 when every rule holds, 1 otherwise.
#>
param(
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) "src/DiplomacyIntrigue")
)

$ErrorActionPreference = "Stop"
$problems = [System.Collections.Generic.List[string]]::new()

function Normalize([string]$type) {
    # One spelling per type: no spaces, no namespaces, so List<Models.Treaty> and
    # System.Collections.Generic.List<Treaty> compare equal.
    return [regex]::Replace(($type -replace '\s', ''), '(?:\w+\.)+(\w+)', '$1')
}

function Strip([string]$line) {
    # Drops string literals and // comments, so braces and keywords inside them are not counted.
    $noStrings = [regex]::Replace($line, '"(?:\\.|[^"\\])*"', '""')
    $at = $noStrings.IndexOf("//")
    if ($at -ge 0) { return $noStrings.Substring(0, $at) }
    return $noStrings
}

$files = Get-ChildItem -Path $Source -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

# ----- The definer -----------------------------------------------------------------------

$definerFile = $files | Where-Object { (Get-Content $_.FullName -Raw) -match 'class\s+\w+\s*:\s*SaveableTypeDefiner' } |
    Select-Object -First 1
if (-not $definerFile) { throw "No SaveableTypeDefiner found under $Source." }
$definer = Get-Content $definerFile.FullName -Raw

$classIds = @{}; $enumIds = @{}; $containers = @{}
foreach ($m in [regex]::Matches($definer, 'AddClassDefinition\(\s*typeof\(\s*([\w\.]+)\s*\)\s*,\s*(\d+)\s*\)')) {
    $name = $m.Groups[1].Value.Split('.')[-1]
    if ($classIds.ContainsKey($name)) { $problems.Add("Definer: class $name is registered twice.") }
    $classIds[$name] = [int]$m.Groups[2].Value
}
foreach ($m in [regex]::Matches($definer, 'AddEnumDefinition\(\s*typeof\(\s*([\w\.]+)\s*\)\s*,\s*(\d+)\s*\)')) {
    $name = $m.Groups[1].Value.Split('.')[-1]
    if ($enumIds.ContainsKey($name)) { $problems.Add("Definer: enum $name is registered twice.") }
    $enumIds[$name] = [int]$m.Groups[2].Value
}
foreach ($m in [regex]::Matches($definer, 'ConstructContainerDefinition\(\s*typeof\(\s*(.+?)\s*\)\s*\)\s*;')) {
    $containers[(Normalize $m.Groups[1].Value)] = $true
}

$allIds = @($classIds.GetEnumerator() | ForEach-Object { [pscustomobject]@{ Name = $_.Key; Id = $_.Value; Kind = "class" } }) +
          @($enumIds.GetEnumerator()  | ForEach-Object { [pscustomobject]@{ Name = $_.Key; Id = $_.Value; Kind = "enum" } })
foreach ($group in ($allIds | Group-Object Id | Where-Object Count -gt 1)) {
    $problems.Add("Definer: id $($group.Name) is used by " + (($group.Group | ForEach-Object { "$($_.Kind) $($_.Name)" }) -join " and ") + ".")
}
foreach ($entry in $allIds) {
    if ($entry.Id -lt 1 -or $entry.Id -gt 99) {
        $problems.Add("Definer: $($entry.Kind) $($entry.Name) has id $($entry.Id), outside the reserved block 1-99.")
    }
}

# ----- Enums declared by the mod -----------------------------------------------------------

$modEnums = @{}
foreach ($file in $files) {
    foreach ($line in Get-Content $file.FullName) {
        $code = Strip $line
        if ($code -match '^\s*(?:(?:public|internal|private|protected)\s+)*enum\s+(\w+)') { $modEnums[$Matches[1]] = $true }
    }
}

# ----- Saved members, class by class -------------------------------------------------------

$classPattern = '^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial)\s+)*(?:class|struct)\s+(\w+)'
$attrPattern = '\[\s*Saveable(?:Property|Field)\(\s*(\d+)\s*\)\s*\]'
$declPattern = '^\s*(?:(?:public|internal|private|protected|readonly|static)\s+)*(?<type>[\w\.]+(?:<[^;{=]*>)?(?:\[\])?)\s+(?<name>\w+)\s*(?:\{|;|=)'

$saved = @{}   # class name -> list of @{ Id; Type; Name; Where }
foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    $stack = [System.Collections.Generic.List[object]]::new()   # @{ Name; Depth }
    $depth = 0
    $pendingClass = $null
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $code = Strip $lines[$i]
        if ($code -match $classPattern) { $pendingClass = $Matches[1] }

        if ($code -match $attrPattern) {
            $id = [int]$Matches[1]
            $rest = $code.Substring($code.IndexOf($Matches[0]) + $Matches[0].Length)
            $j = $i
            while ($rest.Trim().Length -eq 0 -and $j + 1 -lt $lines.Count) { $j++; $rest = Strip $lines[$j] }
            $owner = if ($stack.Count -gt 0) { $stack[$stack.Count - 1].Name } else { $null }
            $where = "$($file.Name):$($i + 1)"
            if ($rest -match $declPattern -and $owner) {
                if (-not $saved.ContainsKey($owner)) { $saved[$owner] = [System.Collections.Generic.List[object]]::new() }
                $saved[$owner].Add(@{ Id = $id; Type = (Normalize $Matches['type']); Name = $Matches['name']; Where = $where })
            } else {
                $problems.Add("$($where): a saved member this script cannot parse - check it by hand, or simplify the declaration.")
            }
        }

        foreach ($ch in $code.ToCharArray()) {
            if ($ch -eq '{') {
                $depth++
                if ($pendingClass) { $stack.Add(@{ Name = $pendingClass; Depth = $depth }); $pendingClass = $null }
            } elseif ($ch -eq '}') {
                if ($stack.Count -gt 0 -and $stack[$stack.Count - 1].Depth -eq $depth) { $stack.RemoveAt($stack.Count - 1) }
                $depth--
            }
        }
    }
}

foreach ($class in $saved.Keys) {
    $members = $saved[$class]

    foreach ($group in ($members | Group-Object { $_.Id } | Where-Object Count -gt 1)) {
        $problems.Add("$($class): save id $($group.Name) is used by " + (($group.Group | ForEach-Object { "$($_.Name) ($($_.Where))" }) -join " and ") + ".")
    }

    if (-not $classIds.ContainsKey($class)) {
        $problems.Add("$($class) has saved members but no AddClassDefinition in $($definerFile.Name).")
    }

    foreach ($member in $members) {
        $type = $member.Type
        if ($type -match '^(List|Dictionary|HashSet|Queue|Stack)<') {
            if (-not $containers.ContainsKey($type)) {
                $problems.Add("$($class).$($member.Name) ($($member.Where)) holds $type, which has no ConstructContainerDefinition in $($definerFile.Name) - the save will crash.")
            }
            foreach ($arg in [regex]::Matches($type, '[<,](\w+)') | ForEach-Object { $_.Groups[1].Value }) {
                if ($modEnums.ContainsKey($arg) -and -not $enumIds.ContainsKey($arg)) {
                    $problems.Add("$($class).$($member.Name) ($($member.Where)) holds enum $arg, which has no AddEnumDefinition.")
                }
            }
        } elseif ($modEnums.ContainsKey($type) -and -not $enumIds.ContainsKey($type)) {
            $problems.Add("$($class).$($member.Name) ($($member.Where)) is enum $type, which has no AddEnumDefinition.")
        }
    }
}

# ----- Report --------------------------------------------------------------------------------

if ($problems.Count -gt 0) {
    Write-Host "Save-data check FAILED ($($problems.Count)):" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    exit 1
}

$memberCount = ($saved.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
Write-Host ("Save-data check OK: {0} savable classes, {1} saved members, {2} enums, {3} containers." -f `
    $saved.Count, $memberCount, $enumIds.Count, $containers.Count) -ForegroundColor Green
exit 0
