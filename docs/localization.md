# Adding or fixing a language

Diplomacy & Intrigue follows the game's own language setting. Every string the mod shows is
written once in English in the code, behind a key, and each language folder holds the
translations of those keys. A player who has chosen Deutsch gets Deutsch; the mod adds no
language selector of its own.

This page is for someone who has never seen the repository. Everything here is one folder and
one file.

## What is where

```
module/DiplomacyIntrigue/ModuleData/Languages/
  EN/                          English: the source. Generated from the code - do not edit.
    language_data.xml
    di_strings.xml            596 keys, the whole of the mod
    std_module_strings.xml
  DE/  FR/  RU/  ...           one folder per language the game itself offers
    language_data.xml
    di_strings.xml            empty until someone fills it in
```

The thirteen folders are the languages Bannerlord offers: `EN`, `BR`, `CNs`, `CNt`, `DE`, `FR`,
`IT`, `JP`, `KO`, `PL`, `RU`, `SP`, `TR`. The game's own `id` for each - `Deutsch`, `Русский`,
`Español (LA)` - is already in that folder's `language_data.xml`, copied from the game install, so
do not retype it.

`VI` is deliberately absent: Vietnamese is a community patch over the game, not one of its
languages, and it needs a separate font mod to draw the accents.

## Filling in a language

1. Copy `Languages/EN/di_strings.xml` to `Languages/<your code>/di_strings.xml`. Keep the file
   name; `language_data.xml` already points at it.
2. Change the tag near the top to your language, exactly as `language_data.xml` spells it:

   ```xml
   <tag language="Deutsch" />
   ```

3. Translate the `text` of each `<string>`. Leave the `id` alone: it is the key the code asks for,
   and changing it breaks the lookup for that one string only - silently, in your language.

   ```xml
   <string id="DI_REALM_REALM" text="Realm" />
   <string id="DI_REALM_VASSAL_OF_NAME" text="Vassal of {NAME}" />
   ```

4. Keep every `{VARIABLE}` exactly as it is, in every language. `{NAME}`, `{AMOUNT}`,
   `{KINGDOM}`, `{SCORE}` are filled in by the game with a name, a figure or a realm. A
   translation that drops one leaves a hole; one that invents a variable shows
   `{AMOUNT}` on screen. The check below refuses both.
5. Reorder freely. Word order is not the same in every language, which is why no sentence in this
   mod is built by joining pieces: each is one string with named variables, so
   `Vassal of {NAME}` may become `{NAME} (Vasall)`.
6. Run the check:

   ```powershell
   pwsh ./scripts/check-localization.ps1
   ```

   It compares your file with the English key by key: a missing key, a key nobody uses, a
   duplicate, and any `{VARIABLE}` that does not match. `build.ps1` and `deploy.ps1` run it too,
   so a file that fails cannot reach a player.
7. Send a pull request, or the folder, if you would rather not use git.

## Words the mod already has a word for

The game is translated into every one of these languages, and reading like the game matters more
than reading like a translation. Before inventing a word, look for the game's own:

- kingdom, clan, fief, lord, ruler, court
- influence, gold, denars
- war, peace, truce, alliance, defensive pact, non-aggression pact, tributary, vassal
- treaty, claim, casus belli, exhaustion, legitimacy, spy network

They live in the installed game at
`Modules/Native/ModuleData/Languages/<code>/std_module_strings_xml_<code>.xml` (the file name
carries the language's own suffix, e.g. `ger-DE`, `rus`, `spa`). Reusing them means a player who
has read the game's own tooltip reads our sentence the same way.

The mod's own terms, with the sense they carry here:

| English | what it means here |
|---|---|
| realm | a kingdom or the rebel faction of a civil war - one political body, not a building |
| hegemon | a kingdom holding at least one vassalage treaty |
| hold | 0-100, how willing a vassal is to keep answering its patron |
| defiance | a mark left by ignoring a call to arms; a third one brings a civil war |
| rising | the rebel side of a civil war |
| court intrigue | grievances, blocs and offices inside one realm |
| exposure | how likely a spy operation is to be caught |
| counter-intelligence | the budget a realm spends to catch others' spies |
| statecraft | the six political skills, and what offices they win |

## What is not translated, and why

- **Log lines, telemetry and `diplomacy.*` console output** stay English. They are read by the
  developer and by `analyse-log.py`, and a translated log cannot be grepped.
- **Numbers are never words.** A figure on screen is the figure the AI used; a translation
  changes words, never values (the mod's own rule, and the reason every figure travels as a
  `{VARIABLE}`).
- **Punctuation-only labels** - `-`, `+`, `x`, `=`, `?` - are structure, not text.

## The prefab text

Some labels live in the mod's prefab XML rather than in code. A Gauntlet widget's `Text` is a
plain string that the widget never looks a key up in, so those labels are being moved into
view-model properties one panel at a time. `scripts/check-localization.ps1` reports any that are
left; until that work is finished, a few short English labels stay English in every language.
