# Story 4.2 ST-2 - the term sheet.
#
# The patch's string ids are hashed (01zQKaF3), so a term cannot be looked up by id. It can be
# looked up by its English: join each VI file to the EN file of the same name, then search the
# English side. This writes a UTF-8 file because the console mangles Vietnamese (H?nh d?ng m?t),
# and a mangled term in a glossary is worse than no term.
param([string]$Out = "$PSScriptRoot\term-sheet.md")

$ErrorActionPreference = 'Stop'
$game = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord'

# Terms our screens actually use, with what we call them in the mod's own English.
$terms = @(
  @{ en = 'Kingdom';      note = 'the state, not a ruler' }
  @{ en = 'Clan';         note = '' }
  @{ en = 'Influence';    note = '' }
  @{ en = 'Treaty';       note = '' }
  @{ en = 'Peace';        note = '' }
  @{ en = 'War';          note = '' }
  @{ en = 'Party';        note = 'a war party on the map' }
  @{ en = 'Army';         note = '' }
  @{ en = 'Siege';        note = '' }
  @{ en = 'Fief';         note = '' }
  @{ en = 'Town';         note = '' }
  @{ en = 'Castle';       note = '' }
  @{ en = 'Village';      note = '' }
  @{ en = 'Denars';       note = '' }
  @{ en = 'Diplomacy';    note = 'a tab name' }
  @{ en = 'Realm';        note = 'a tab name' }
  @{ en = 'Court';        note = 'a tab name' }
  @{ en = 'Clans';        note = 'a tab name' }
  @{ en = 'Fiefs';        note = 'a tab name' }
  @{ en = 'Armies';       note = 'a tab name' }
  @{ en = 'Policies';     note = 'a tab name' }
  @{ en = 'Ruler';        note = 'an office title' }
  @{ en = 'Envoy';        note = 'an office title' }
  @{ en = 'Steward';      note = 'an office title' }
  @{ en = 'Treasurer';    note = 'an office title' }
  @{ en = 'Spymaster';    note = 'an office title' }
  @{ en = 'Tribute';      note = '' }
  @{ en = 'Vassal';       note = '' }
  @{ en = 'Loyalty';      note = '' }
  @{ en = 'Heir';         note = '' }
  @{ en = 'Claimant';     note = '' }
  @{ en = 'Settler';      note = '' }
  # Terms the mod invents. The patch has no equivalent, so a miss is expected and is the point:
  # it says where the lead has to choose rather than follow.
  @{ en = 'Realm';        note = 'our tab; the mod has no vanilla equivalent' }
  @{ en = 'Court';        note = 'our tab and our concept; the patch uses "triều đình" in prose' }
  @{ en = 'Hold';         note = 'our concept: the bond between a vassal and its patron' }
  @{ en = 'Hegemon';      note = 'our concept' }
  @{ en = 'Grievance';    note = 'our concept' }
  @{ en = 'Casus Belli';  note = 'our concept' }
  @{ en = 'Legitimacy';   note = '' }
  @{ en = 'Pretender';    note = '' }
  @{ en = 'Handler';      note = 'our name for the spymaster' }
  @{ en = 'Treasury';     note = 'an office title' }
  @{ en = 'Manor';        note = '' }
  @{ en = 'Fealty';       note = '' }
  @{ en = 'Vassalage';    note = '' }
  @{ en = 'Exhaustion';   note = '' }
  @{ en = 'Renown';       note = '' }
  @{ en = 'Counter-intelligence'; note = 'our concept' }
  @{ en = 'Governor';     note = '' }
  @{ en = 'Warden';       note = '' }
  @{ en = 'Settlement';   note = '' }
)

function Read-Entries([string]$path) {
  $map = @{}
  if (-not (Test-Path $path)) { return $map }
  $t = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
  foreach ($m in [regex]::Matches($t, '<string\s+id="([^"]+)"\s+text="([^"]*)"')) {
    $map[$m.Groups[1].Value] = $m.Groups[2].Value
  }
  return $map
}

# id -> (english, vietnamese) across every module the patch touched.
#
# There is no EN folder in a vanilla module: English is the base file sitting directly in
# ModuleData/Languages/, and every other language is a subfolder of it. Reading
# Languages/EN/std_native_strings_xml.xml finds nothing, which is how this first found zero
# strings and looked like the patch's ids were stale.
$joined = New-Object System.Collections.Generic.List[object]
foreach ($mod in Get-ChildItem "$game\Modules" -Directory) {
  $viDir = Join-Path $mod.FullName 'ModuleData\Languages\VI'
  $baseDir = Join-Path $mod.FullName 'ModuleData\Languages'
  if (-not (Test-Path $viDir)) { continue }
  foreach ($vi in Get-ChildItem $viDir -Filter '*.xml') {
    $enName = $vi.Name -replace '-VI\.xml$', '.xml'
    $en = Read-Entries (Join-Path $baseDir $enName)
    $viMap = Read-Entries $vi.FullName
    foreach ($id in $viMap.Keys) {
      if ($en.ContainsKey($id)) {
        $joined.Add([pscustomobject]@{
          Module = $mod.Name; Id = $id; En = $en[$id]; Vi = $viMap[$id]
        })
      }
    }
  }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# Bảng thuật ngữ — bản vá cộng đồng')
[void]$sb.AppendLine()
[void]$sb.AppendLine('Sinh bởi `scripts/term-sheet.ps1` từ các file `VI/*-VI.xml` của bản vá, nối với file')
[void]$sb.AppendLine('tiếng Anh cùng tên qua id băm. Ngày: ' + (Get-Date -Format 'yyyy-MM-dd'))
[void]$sb.AppendLine()
[void]$sb.AppendLine('Dung lượng nối được: ' + $joined.Count + ' chuỗi trên ' +
  (($joined | Select-Object -ExpandProperty Module -Unique) -join ', '))
[void]$sb.AppendLine()
[void]$sb.AppendLine('| Tiếng Anh | Tiếng Việt của bản vá | Chỗ dùng |')
[void]$sb.AppendLine('|---|---|---|')

foreach ($term in $terms) {
  $exact = ($joined | Where-Object { $_.En -eq $term.en } | ForEach-Object { $_.Vi } | Select-Object -Unique)
  if ($exact) {
    [void]$sb.AppendLine('| ' + $term.en + ' | **' + ($exact -join '** / **') + '** | ' + $term.note)
    continue
  }
  # A word inside a long sentence proves the patch uses the word, but quoting the whole sentence
  # fills the sheet with story text. Take the shortest sentence that contains it instead.
  $loose = $joined | Where-Object { $_.En -match ('\b' + [regex]::Escape($term.en) + '\b') } |
           Sort-Object -Property @{ Expression = { $_.En.Length } } |
           Select-Object -First 1
  if ($loose) {
    $excerpt = if ($loose.En.Length -le 70) { $loose.En } else { '(trong câu dài) ' + $loose.En.Substring(0, 60) + '…' }
    [void]$sb.AppendLine('| ' + $term.en + ' | _' + $excerpt + '_ → ' + $loose.Vi + ' | ' + $term.note)
  } else {
    [void]$sb.AppendLine('| ' + $term.en + ' | **_(bản vá không có)_** | ' + $term.note)
  }
}

[System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
"wrote $Out  ($($joined.Count) joined strings)"