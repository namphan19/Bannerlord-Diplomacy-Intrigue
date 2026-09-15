# Working on Diplomacy & Intrigue

A large gameplay mod for **Mount & Blade II: Bannerlord v1.4.8**, adding three connected
political systems: inter-kingdom diplomacy, court intrigue, espionage. The user is the
project lead and makes the design calls; you build, verify, and report honestly.

This file holds what is **always true**. For where the work currently stands and what to do
next, read [docs/STATUS.md](docs/STATUS.md) — that is the point-in-time document.

---

## 1. Facts that cost a day to learn. Do not re-derive them.

**The module targets `net472`. Not `net6.0`.**
`bin/Win64_Shipping_Client` contains a `Microsoft.NETCore.App` folder, which makes the game
look like .NET 6. That folder belongs to the Gaming.Desktop (GDK) build. The Win64 shipping
client is a **.NET Framework 4.7.2** host, and the game's own assemblies target
netstandard2.0. A `net6.0` module builds fine, passes a naive load test, and then fails in
game with *"submodule could not be loaded correctly due to a dependency conflict"* — a
message that names neither the cause nor the culprit.

**A module that fails to load cannot log anything.** Its code never runs, and
`Module.LoadSubModules` runs before any `OnSubModuleLoad`, so ButterLib's logging is not up
and the game's own `MBDebug.Print` output is discarded. Nothing reaches any file. That is
what `tools/LoadProbe` exists for — run it before blaming the code.

**The retail game has no developer console.** The `diplomacy.*` commands work through the
GABS bridge (`bannerlord.core.run_command`), not through a console the user can open. Never
tell the user to press Alt+~ and type something; give them the Ctrl+D menu or a file.

**Campaign event listeners do not fire in registration order.** A live trace showed
`CallToArmsBehavior` handling `WarDeclared` before `CoreBehavior` did, for the same event.
Nothing currently depends on order. Do not add anything that does.

**A changed module DLL blocks startup with a "Mod change detected" prompt.** It appears
before anything loads — no mod log, no GABP bridge — so an automated deploy-and-test loop
stalls with no error anywhere. It has to be dismissed by sending Enter to that window;
there is a ready watcher pattern in the session scratchpad, and the symptom to recognise is
`games_connect` timing out while the process is alive with that window title.

**Never force-kill Bannerlord.** `deploy.ps1` refuses to run while the game is open, and
that guard is the point - the lead may be playing, and a balance run can be hours long.
Routing around it with `Get-Process Bannerlord* | Stop-Process -Force` has already killed two
of the lead's launches 14 seconds into startup, which is indistinguishable from a crash: the
window disappears and Windows writes a dump for `TaleWorlds.MountAndBlade.Launcher.exe`.

Use `mcp__gabs__games_stop`, and only for a session you started. If the game is running and
you did not start it, **ask** before stopping it. When a "crash" is reported, check whether a
dump sits ~10-20 seconds after a mod-log line that stops at `OnSubModuleLoad complete` -
that pattern is an external termination, not a fault in the module.

**Launch through `games_start`, not by hand.** A manually launched game writes no bridge
record GABS recognises, so the bridge never connects even though the game is running fine.

**The game throttles hard when its window is unfocused** — roughly two in-game hours per real
minute. A campaign day takes about an hour of real time in the background. This is why
long-run verification cannot be done from a tool call.

## 2. Build, deploy, verify

```bash
pwsh ./scripts/build.ps1     # compile only, never touches the game folder
pwsh ./scripts/deploy.ps1    # pre-flight check, then install into the game
dotnet run --project tools/LoadProbe   # would the game load this assembly?
dotnet run --project tools/ApiDump -- "TypeNameOrFilter"   # real v1.4.8 API surface
```

`deploy.ps1` runs LoadProbe **before** copying anything and refuses to install a module the
game could not load. It also refuses while Bannerlord is running — stop the game first
(`mcp__gabs__games_stop`).

**Write against the real API.** There is no public Bannerlord API documentation and names
move between versions. `tools/ApiDump` dumps the actual public surface of any game type into
`artifacts/api/`. Use it instead of guessing; it has already caught `MBRandom` living in
`TaleWorlds.Core`, `Settlement.GetPosition2D` rather than `Position2D`, and
`BesiegerCamp.MapFaction` being available directly.

### Verifying in the live game

The GABS MCP server drives the running game. The loop that works:

1. `games_stop` → `deploy.ps1` → `games_start`
2. `bannerlord.core.load_save` with a save name, then wait for `Session launched` in the log
3. `bannerlord.core.set_cheat_mode` true — needed before any `campaign.*` command
4. Drive with `bannerlord.core.run_command`, read the mod log, `ui.take_screenshot` for UI

Saves used for testing: `di_phase1_full` (richest state), `di_treaty_test`, `di_phase0_test`.

**What the bridge cannot do:** click buttons inside a `MultiSelectionInquiry` — GABS only
indexes map-layer widgets, so menu navigation past the root needs a human. Screenshots do
confirm rendering.

**What no tool can do:** advance `CampaignTime.Now`. `diplomacy.tick_days` and
`diplomacy.ai_week` drive the real upkeep and the real AI evaluation, but the clock stays
put — so inside them treaties never expire, claims never age out and clan influence never
regenerates. A long `ai_week` run therefore under-reports wars. **This has already produced
one false conclusion in this project.** Treat any long-run number from those commands as
suspect and say so.

## 3. Rules that must not be broken

**Save data is frozen once shipped.** Never renumber or reuse a `SaveableProperty` id, never
reuse a save-definer local id for a different type, never change the definer base id
(`2749100`, block `2749100`–`2749199`). Adding a new savable type means a class definition
**and** a container definition in `ModSaveDefiner` — a missing container definition crashes
on save, which is the single most common way to break a Bannerlord mod. Bump
`ModState.CurrentSchemaVersion` only when the *meaning* of existing data changes; adding a
field that defaults sensibly does not need it.

**No throw crosses the engine boundary.** A `SubModule` hook, a campaign event handler or a
Harmony patch that throws takes the whole game down — not just the mod. Every one of them
catches, logs, and continues. `SubModule.Healthy` is false when startup failed; systems
check it and stay inert rather than half-running.

**Prefer events and `GameModel` overrides. Harmony is the last resort.** Rules for
`Patches/`: one patched method per file, a header stating *what* it changes, *why* no event
exists, and the *game version verified against*; a `try/catch` that degrades to vanilla. Two
patches exist today and both follow this. Do not add a third without exhausting the
alternatives.

**The AI plays by the same rules as the player.** A project decision, enforced in code:
`ClaimRegistry`, `TreatyRegistry`, `PeaceTable` and `CallToArms` take no "is this the player"
argument anywhere. No hidden modifiers. A number shown in the UI is the number the AI used.

**One resolver per concept.** Legitimacy was computed in two places that disagreed, twice,
and both times a kingdom honouring a treaty was punished as an aggressor.
`CasusBelli.Resolve` is now the only resolver. When a value is derived in more than one
place, that is the bug, not the symptom.

## 4. Style

- Comments explain **why**, not what. Where a decision was made against an obvious
  alternative, the comment says which alternative and why it lost.
- Constants live in one file per pillar (`Diplomacy/DiplomacyConstants.cs`) so a balance pass
  edits one file. An un-tuned constant says so in its own doc comment.
- All player-facing text is English. The design docs are English.
- **Reply to the user in Vietnamese.** The lead writes in Vietnamese; the codebase is not.

## 5. Honesty requirements

This project has a standing expectation, set by the lead and by several corrections already:

- **Never claim something is verified when it is not.** State plainly what was tested, how,
  and what remains unverified. Several entries in `docs/ROADMAP.md` are marked unverified on
  purpose.
- **If a comment or doc claims a rationale that turns out to be wrong, fix it.** An earlier
  pass justified two constants as "measured" when the measurement was confounded; leaving a
  false rationale in code is worse than leaving none.
- **Correct your own earlier statements when they affected decisions.** The `net6.0` mistake
  propagated into the build, the docs and a day of debugging; saying so plainly was part of
  fixing it.

## 6. Where things are

| | |
|---|---|
| [docs/STATUS.md](docs/STATUS.md) | **where the work stands, what to do next** |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | layering, the runtime facts in full, save rules, how the game is hooked |
| [docs/ROADMAP.md](docs/ROADMAP.md) | all four phases, what is done, what is verified, acceptance criteria |
| [docs/design/01-diplomacy.md](docs/design/01-diplomacy.md) | Phase 1 spec, formulas, and the lead's decisions |
| [docs/design/02-intrigue.md](docs/design/02-intrigue.md) | Phase 2 spec — grievances, loyalty, blocs, legitimacy, civil war |
| [docs/design/03-espionage.md](docs/design/03-espionage.md) | Phase 3 spec — networks, missions, exposure as diplomacy |
| Game install | `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` (auto-detected; override with `BANNERLORD_GAME_DIR`) |
| Mod logs | `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\` |
| Mod reports | `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Reports\` |

Two files outside the repo were modified to make testing work, both with `.bak-*` backups
beside them: the launcher load order (`Documents\...\Configs\LauncherData.xml`, enabling
ButterLib/UIExtenderEx/MCM which were off) and the GABS launch script
(`Desktop\Agent_Bannerlord\Bannerlord.GABS\launch-bannerlord.ps1`, whose `_MODULES_` list is
hardcoded and does not read LauncherData).
