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
that guard is the point - the lead may be playing, and a balance run can be hours long. Use
`mcp__gabs__games_stop`, and only for a session you started. If the game is running and you
did not start it, **ask** before stopping it.

**The official launcher hosts the game in its own process.** It does not spawn a child:
`Launcher.Library.Program.Main` ends by calling `TaleWorlds.Starter.Library.Program.Main` in
the same process (verified by IL, v1.4.8). So a crash dump named
`TaleWorlds.MountAndBlade.Launcher.exe` **is the game crashing**, not the launcher, and a
launcher-hosted session is indistinguishable from a direct one in a log - which is why
`OnSubModuleLoad` now records `host=`.

A correction worth keeping: a dump landing ~14-20s after a mod log that stops at
`OnSubModuleLoad complete` was once written down here as the signature of an external
termination. It is not. That is simply how long a launcher-hosted start takes to reach the
crash below, and reading it the other way cost a wrong diagnosis reported to the lead.

**Reading a crash with no stack trace.** Windows' `CLR20r3` record in the Application event
log gives P4 = faulting assembly, P7 = MethodDef token (hex), P8 = IL offset. Resolve the
token with Cecil against the game assembly to get the method name:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-30)} |
  Where-Object ProviderName -match 'Error Reporting|Application Error'
# then match MetadataToken.ToUInt32() -eq 0x06000000 -bor <P7> over ModuleDefinition.GetTypes()
```

**A crash on the game's main thread is not in the mod log, and it looks like a hang.** An
earlier version of this said `SubModule.InstallCrashLogging` catches unhandled exceptions into
the mod log. It does not catch one thrown during a campaign tick. The engine's native handler
takes it first and opens its crash-report dialog. The process then sits `Responding` at near
zero CPU, with every GABS main-thread tool timing out, until someone dismisses the dialog.
On 2026-09-23 that was read as the game stalling in the background, and it was a crash. Two
places have the real answer:

- `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_errors_<pid>.txt` gives the
  moment and a native stack. A `MonoMod.Utils` frame there means a Harmony-patched method was
  on the path.
- `%LOCALAPPDATA%\CrashDumps\<exe>.<pid>.dmp` holds the managed exception itself, message and
  stack. `dotnet run --project tools/DumpProbe -- <dump>` prints it with ClrMD, against the
  local .NET Framework DAC. It took the 2026-09-23 crash from "unknown" to the exact vanilla
  method and cast in one run.

Two things to know about the dialog itself:

- **The exception can be read while the dialog is still up.** Run
  `dotnet run --project tools/DumpProbe -- --pid <pid>`: a game sitting on its crash dialog still
  holds the exception on the faulting thread, and no dump exists until the dialog closes.
- **Closing the dialog is not a force-kill, and it must not upload anything.** It is a `#32770`
  window titled `*_*`, owned by the game's pid, asking *"Would you like to upload these files
  now?"*. Answer **No**, by posting `WM_COMMAND` with `IDNO` (7) to it. The process then exits on
  its own and Windows writes the dump. Never answer Yes: that sends files to TaleWorlds.

`InstallCrashLogging` still catches exceptions on other threads, and faults that happen before
the module loads.

**A map faction must be a `Kingdom` whenever the clan is in one.** Vanilla casts
`MapFaction` to `Kingdom` without checking at about 25 places: `GainKingdomInfluenceAction`,
hourly party AI, fief elections, lord conversations and more. It assumes a clan inside a kingdom
answers with that kingdom. Redirecting a rebel clan's map faction to the clan itself crashed the
game two seconds into the first civil war (2026-09-23). The internal war's rising is a real
`Kingdom` for this reason (design 07 §3c–§3d).

**Destroying a kingdom destroys every clan still in its list.** `DestroyKingdomAction` runs
`DestroyClanAction` on each clan in `kingdom.Clans`. A kingdom's clan, fief, hero and war-party
lists are `[CachedData]`: never saved, and rebuilt on load from `Clan.Kingdom`. Anything that
fills those lists by hand, as the rising does, must empty them before destroying the kingdom.

**Vanilla destroys, on every load, any kingdom whose `Leader.MapFaction` is not itself.** The
check is `ClanVariablesCampaignBehavior.OnSessionLaunched`. A kingdom whose ruling clan belongs
to another kingdom survives a reload only if the map-faction redirect is already in place when
that runs, which means rebuilding it in `SyncData`, not at session launch.

**A hegemon is derived, never stored.** Any kingdom holding one active `Vassalage` treaty is
one; `Hegemony.IsHegemon` reads the treaties and there is deliberately no flag, no title
record and no list. Several hegemons coexist by construction, and a link ending makes the
hegemony end with no event having to fire. Do not add a stored "is hegemon" anywhere - that
would be a second source of truth for something already derivable, which is the mistake
`CasusBelli.Resolve` exists to prevent.

**A diagnostic that drives only part of a tick lies convincingly.**
`diplomacy.tick_days` used to run exhaustion and claims but not the treaty upkeep, so a
vassalage `Hold` sat unchanged through 20 simulated days and looked like a broken drift - the
campaign's own daily handler had been calling it correctly all along. It now runs the full
daily set, and `ai_week` - which kept its own partial list until the run-06 review - shares
it (`DebugCommands.RunDailyUpkeep`). If a value looks frozen under a debug command, check the
command before the system.

**Launch through `games_start`, not by hand.** A manually launched game writes no bridge
record GABS recognises, so the bridge never connects even though the game is running fine.
Enabling the `Bannerlord.GABS` module in the launcher's own mod list does **not** fix this,
which is worth knowing before trying: the mod's server comes up and listens (confirmed on
2026-09-16, `127.0.0.1:4825` owned by the game's pid, module present in the `OnSubModuleLoad`
list), but it authenticates with the `GABP_TOKEN` environment variable **GABS sets when it
spawns the process**. A launcher-started game generates its own token that nothing outside it
knows, and `games_connect` refuses with *"no runtime claim exists"* even with
`forceTakeover: true`. There is no way in after the fact - the game has to be restarted
through `games_start`.

**`games_stop` can report success while the game is still running.** Under BLSE Standalone
the pid GABS tracks as the workload is not the process that owns the game window: it reported
*"workload pid 19884 is gone"* twice while pid 18664 — `Bannerlord.BLSE.Standalone`, holding
the `Mount and Blade II Bannerlord - Singleplayer` window title — kept running and kept
answering bridge calls. Confirm a stop against the game itself, with
`Get-Process Bannerlord*` and `bannerlord.core.get_game_state`, not against what GABS says.
The `deploy.ps1` guard matches `Bannerlord*`, so a process left over this way blocks the next
deploy, and the rule against force-killing still applies — ask the lead to close the window.

**`bannerlord.core.load_save` only works from the main menu.** Called while a campaign is
already running it returns `"Loading save: <name>"` and does nothing at all: the world carries
on unchanged, which is easy to miss because the reply looks like success. Verify with
`diplomacy.wars` or `core.get_campaign_time` that the state actually moved. Getting back to a
clean save mid-session therefore means restarting the game — there is no quit-to-menu tool on
the bridge.

**GABS itself can crash the game.** On 2026-09-17 `games_start` returned `started_bridge_pending`
(the first connect was cancelled during backoff), a `games_connect` followed, and ~20 seconds
later the process died: `CLR20r3` with P4 = `Lib.GAB`, an `InvalidOperationException` in
`TcpConnection.SendMessageAsync` - the bridge answering on a connection that had closed. Not the
mod. After a pending start, wait for the game rather than connecting over it, and **never leave
the GABS module loaded for an unattended run**: launch those with `scripts/play.ps1`.

**The player hero dies of old age by illness, not outright.** `AgingCampaignBehavior` makes an old
main hero ill (`Campaign.MainHeroIllDays != -1`), then drains hit points daily until
`KillMainHeroWithIllness`; with no heir the campaign ends and a run stalls on the Game Over
screen. Resetting the age alone does not cure an illness already under way - run 06 lost a
session to exactly that. `diplomacy.test_set_player_age` resets both, for test saves only.

**A long run is reachable from a tool call, but only through `Campaign.SpeedUpMultiplier`.**
`bannerlord.core.set_time_speed` picks the *mode* and tops out at `UnstoppableFastForward`; the
factor that mode is multiplied by is a separate property, it defaults to **4**, and no vanilla
console command sets it (`campaign.set_speed_up_multiplier` and `campaign.set_campaign_speed`
both do not exist in v1.4.8). That last claim may be too strong: on 2026-09-25
`core/list_commands` listed a `campaign.set_campaign_speed_multiplier`. What it sets has not been
checked; `test_set_speed` is the lever known to work. `diplomacy.test_set_speed <1-50>` sets it. Measured 2026-09-20 on
`di_review_0919_b`, from consecutive weekly `[SNAPSHOT]` timestamps:

| Multiplier | In-game days per real minute | One in-game year |
|---|---|---|
| 4 (default) | 3.0 | ~28 min |
| 50 | 33 | ~2.5 min |

It buys wall clock per tick, not a different tick: everything the campaign does still happens,
so a machine that cannot keep up drops frames rather than slowing the clock. Use it to *reach* a
world state, not to measure how fast one arrives.

An earlier version of this entry said the game throttles to roughly two in-game hours per real
minute when its window is unfocused, and that long-run verification therefore could not be done
from a tool call. The second half is simply wrong, and the first half did not reproduce: at
multiplier 4 the rate was 3.0 days/minute both before and after the window was brought to the
foreground, identical to the decimal. Focus may still matter — the measurement window was short
and the foreground may not have been held — but it is not the lever that was being looked for.

## 2. Build, deploy, verify

```bash
pwsh ./scripts/build.ps1     # compile only, never touches the game folder
pwsh ./scripts/deploy.ps1    # pre-flight check, then install into the game
pwsh ./scripts/play.ps1      # launch the way that works (BLSE Standalone, launcher's own mod list)
pwsh ./scripts/play.ps1 -Without DiplomacyIntrigue   # same, minus a mod: bisect a crash
dotnet run --project tools/LoadProbe   # would the game load this assembly?
dotnet run --project tools/ApiDump -- "TypeNameOrFilter"   # real v1.4.8 API surface
pwsh ./scripts/check-save-ids.ps1   # the save-data rules of §3, read from source; build and deploy run it first
scripts/compile-check.sh     # no game on this box (Linux, cloud): compile against NuGet reference assemblies
```

`compile-check.sh` builds against BUTR's `Bannerlord.ReferenceAssemblies.Core` 1.4.8.119303 and
the framework packages at `SubModule.xml`'s versions, into a temp folder. A clean result there
means the code compiles against the real v1.4.8 API, **nothing more**: LoadProbe still needs the
real install, and nothing has run. `tools/ApiDump` works the same way with
`BANNERLORD_GAME_DIR` pointed at that script's stand-in game folder. On a cloud session the .NET
SDK comes from Ubuntu's own repository (`apt-get install dotnet-sdk-8.0`); Microsoft's install
script is blocked there.

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

Useful commands beyond `status`/`wars`/`treaties`: `diplomacy.strength` (every kingdom ranked
by the strength the formulas read, with its sphere), `diplomacy.hegemony` (every sphere, each
link's hold and the terms pulling it), `diplomacy.submission_value A | B`,
`diplomacy.offer_peace <winner> | <loser> | vassalage, prisoners` (drives the real peace-table
route rather than fabricating a treaty), `diplomacy.war_value`, `diplomacy.peace_allowance`,
`diplomacy.tribute_value A | B` (every gate of a tribute demand, and B's court house by house:
who paying would leave a defection risk).
Court intrigue: `diplomacy.grievances`, `loyalty`, `blocs`, `legitimacy`, `pretenders` (with
who would stand at the next succession), and `court_bands` (a court exactly as its
Encyclopedia page describes it: bands only, plus the exact ledger while the player's house holds a
live ReadCourt on that realm). Espionage: `diplomacy.networks`, `mission_odds <clan> | <kingdom>`, `missions`, `bribes` (every
bribe still on the record and whether it binds anybody), `counter_intelligence [kingdom]` (every
realm's defence term by term), `ai_espionage [kingdom]` (each AI realm's espionage plan for the week,
a dry run), and the levers `test_set_network`, `test_counter_budget <kingdom> | <denars>`,
`test_launch_mission <clan> | <kingdom> | <type> [| settlement or hero]` and
`test_resolve_mission <clan> | <kingdom> [| success|failure|exposed]`.
Statecraft (design 08): `diplomacy.statecraft [kingdom]` (each realm's six office-holders, the
medians, and for one realm every term they feed), and the levers `test_set_skill hero | skill | value`,
`test_add_perk hero | perk`, `test_statecraft on|off` (the MCM switch for this session - the A/B
control; a flip is not logged and the `[RUN]` header keeps the value at launch, so prove an
"off" run by its zero `skill_xp` events). Civil war: `diplomacy.internal_wars`, and
`civil_war_prices <kingdom>` (every house's price to change sides, line by line, and whether the
other leader would pay it).
Test-only levers for reaching a state: `diplomacy.test_set_speed <1-50>` (see §1),
`diplomacy.test_set_player_age`, `diplomacy.sign_treaty`, `test_player_rule <kingdom>` (hands a
kingdom's throne to the player's house, outside any civil war), `test_demand_tribute A | B` (one
demand through the weekly scan's own body - on a player-ruled B it opens the inquiry), and for UI the screen openers
`test_open_kingdom`, `test_open_encyclopedia <kingdom>`, `test_court_select <clan>`, and
`test_intel open` (the Clan screen's Intelligence tab, then driven verb by verb through the same
methods its buttons call). For a
civil war: `test_start_internal_war`, `test_end_internal_war`, `test_change_side <clan> [| unpaid]`,
`test_concede <kingdom> | crown|rising`, and `test_player_side <kingdom> | crown|rising|ruler`,
which puts the player's house where the Court tab can be seen from each side. To reach the
prompts a civil war puts to the player: `test_player_join <kingdom>` (the player's house as a
vassal, before any war), `test_set_legitimacy <kingdom> | <value>`, `test_imprison <prisoner> | <captor>`
(the 30-day captivity rule), and the vanilla `campaign.add_hero_relation <id> | <id> | <value>`,
which sets a relation between two NPCs (use string ids such as `lord_4_3`; names are ambiguous).
The daily tick that `tick_days` runs includes the trigger, so a met trigger rises on the next
`tick_days 1` - only the 365-day cooldown after a war needs the real clock. Note that `sign_treaty` with
`Vassalage` calls `TreatyRegistry.Sign` **directly** — it skips `Hegemony.Submit`, so the link
it makes has no starting Hold, no call to arms and no sibling reconciliation. It is a treaty
row, not a submission, and it cannot be used to test anything downstream of `Submit`.

Saves used for testing: `di_phase1_full` (richest state), `di_treaty_test`, `di_phase0_test`.

**An inquiry addressed to the player stops the clock**, and a long run then looks stalled: the
mod log goes quiet with no error. On 2026-09-24 it was an AI peace offer to the player's kingdom.
Check with `bannerlord.core.check_blockers` (`inquiry_active`), read it with `ui/get_inquiry`,
and answer with `ui/answer_inquiry`. If the log also stops and the bridge times out, it is a
crash, not an inquiry (§1).

**"Save and Exit" writes over the save that was loaded.** `di_civilwar_test` was overwritten
this way on 2026-09-24, at the moment a session was closed from the game's own menu.
`games_stop` does not save. Load a test save expecting that the last person who played it may
have saved over it.

`bannerlord.kingdom.get_clan` fails on v1.4.8 (*Method not found:
`Clan.get_CommanderLimit()`*). Use `bannerlord.kingdom.get_kingdom`, or the mod's own
`diplomacy.loyalty <kingdom>`, which lists a court's clans.

**What the bridge has not been shown to do:** click buttons inside a `MultiSelectionInquiry`.
An earlier version of this said GABS only indexes map-layer widgets and that any menu past the
root needs a human; that is too strong — on 2026-09-20 `ui/click_widget` drove the whole of
character creation. The intro video does not need one either - the bridge has
`core/skip_video` (confirmed 2026-09-23). The inquiry case specifically is untested.
Screenshots do confirm rendering.

**Driving the Kingdom screen, learned 2026-09-26.** `ui/answer_inquiry` takes `affirmative`,
not `accept` - a wrong key is read as false and silently answers **No**. The same call dismisses
a scene notification ("… joined the Kingdom of …", raised by `ChangeKingdomAction`), but a
second one queued behind a Kingdom Decisions popup stayed on screen with no inquiry the bridge
could see; test on a save where the player is already placed rather than joining mid-session.
A Diplomacy-tab row is selected with `ui/call_viewmodel_method_at_index` (layer `KingdomScreen`,
list `Diplomacy.PlayerTruces` or `PlayerWars`, method `OnSelect`), and the mod's own buttons
are clicked by their text. **The mod's mixin properties are invisible to
`ui/get_viewmodel_property`**, which reflects the vanilla VM type; read those through a
`diplomacy.*` command that shares the resolver. When the machine has no display attached
(Windows reports a 640×480 `WinDisc` screen), no real mouse or keyboard input reaches the game,
so nothing below a scroll fold and no Esc key can be exercised.

**What no tool can do:** advance `CampaignTime.Now`. `diplomacy.tick_days` and
`diplomacy.ai_week` drive the real upkeep and the real AI evaluation, but the clock stays
put — so inside them treaties never expire, claims never age out, clan influence never
regenerates, and a trust record still inside its grace period never leaves it (the grace is
measured in dates since the last positive change: on 2026-09-25 `tick_days 70` moved one grudge
by 10.5 and left another, whose pair had traded tribute recently, exactly where it was). A long `ai_week` run therefore under-reports wars. **This has already produced
one false conclusion in this project.** Treat any long-run number from those commands as
suspect and say so.

## 3. Rules that must not be broken

**Save data is frozen once shipped.** Never renumber or reuse a `SaveableProperty` id, never
reuse a save-definer local id for a different type, never change the definer base id
(`2749100`, block `2749100`–`2749199`). `Treaty` currently uses ids **1-17** (14 `Hold`, 15
defiance marks, 16 last defiance, 17 the revolt clock), so the next free id there is **18**. `TrustRecord` uses **1-6** (5 `LastPositiveChange`, 6
`LastOfferRefused`), next free **7**. `ModState` uses
properties **1-17** (11 `Grievances`, 12 `Legitimacy`, 13 `Pretenders`, 14 `InternalWars`,
15 `SpyNetworks`, 16 `SpyMissions`, 17 `CounterIntelligenceBudgets`), next free **18**. The definer's class ids run to **17**
(10 `Grievance`, 11 `KingdomLegitimacy`, 12 `Pretender`, 13 `InternalWar`, 14 `InternalWarMember`,
15 `SpyNetwork`, 16 `SpyMission`, 17 `CounterIntelligenceBudget`), next free **18**. **Only 18 and 19
are left below the enum block**: the definer adds its base to class and enum ids alike, and they are
believed to share one id space (not verified - the reference assemblies carry no method bodies), so
the class after 19 takes **28** or above rather than risk 20. Enums are **20-27**
(26 `GrievanceType`, 27 `InternalWarOutcome`), next free **28**. `Grievance` uses
properties 1-5, `KingdomLegitimacy` 1-5, `Pretender` 1-4, `InternalWar` 1-14 (13 `Faction`,
14 `SideChanges`, next free **15**), `InternalWarMember` 1, `SpyNetwork` 1-8 (next free **9**), `SpyMission` 1-11 (next free **12**),
`CounterIntelligenceBudget` 1-3 (next free **4**). A new *value* on an enum the definer
already registers is safe (`GrievanceType.SuccessionPassedOver = 9` and `ForgedLetters = 10` were
added that way; next free value there is **11**);
renumbering or reusing one is not. Adding a new savable type means a class definition
**and** a container definition in `ModSaveDefiner` — a missing container definition crashes
on save, which is the single most common way to break a Bannerlord mod.
`scripts/check-save-ids.ps1` checks what can be read from source - an id used twice, a class,
enum or container left out of the definer - and `build.ps1` and `deploy.ps1` refuse to go on
when it fails. It cannot see a renumbering; review the diff of any `Saveable` line. Bump
`ModState.CurrentSchemaVersion` only when the *meaning* of existing data changes; adding a
field that defaults sensibly does not need it.

**No throw crosses the engine boundary.** A `SubModule` hook, a campaign event handler or a
Harmony patch that throws takes the whole game down — not just the mod. Every one of them
catches, logs, and continues. `SubModule.Healthy` is false when startup failed; systems
check it and stay inert rather than half-running.

**Prefer events and `GameModel` overrides. Harmony is the last resort.** Rules for
`Patches/`: one patched method per file, a header stating *what* it changes, *why* no event
exists, and the *game version verified against*; a `try/catch` that degrades to vanilla. Six
patch files exist today, one method each, and all follow this. Phase 1's three guard war
initiation: `DeclareWarDecision_IsAllowed_Patch` and the two `DeclareWarAction_*` backstops,
which were one file patching both methods until 2026-09-26 and now share one answer,
`TreatyEnforcement.WhyWarActionRefused`. `KingdomDecision_DetermineSupportOption_Patch`
(Phase 2.3 bloc voting), `Clan_MapFaction_Patch` and `Hero_MapFaction_Patch` (Phase 2.6
internal war) record in their headers the evidence that no event or `GameModel` could do the
job. The mod log's `Harmony patched N methods:` line, written at startup, names every method
actually patched. Do not add a seventh without the same evidence.

**The AI plays by the same rules as the player.** A project decision, enforced in code:
`ClaimRegistry`, `TreatyRegistry`, `PeaceTable` and `CallToArms` take no "is this the player"
argument anywhere. No hidden modifiers. A number shown in the UI is the number the AI used.

**One resolver per concept.** Legitimacy was computed in two places that disagreed, twice,
and both times a kingdom honouring a treaty was punished as an aggressor.
`CasusBelli.Resolve` is now the only resolver. When a value is derived in more than one
place, that is the bug, not the symptom.

**What a political act costs follows the skills that do it.** The lead's rule of 2026-09-26, for
every mechanism built from then on: the act costs **influence and gold together**, each part
scaled by the skill doing the work through the one function `StatecraftTerms.PriceFactor`
(×0.5 to ×2), and the prices are set **high** - the lead judged a first draft at 78 influence
for answering a grievance far too cheap. Design 09 §0 has it in full; design 08 rule 10 is where
it meets the statecraft terms. Acts built before it are not retrofitted without the lead's say.

## 4. Style

- Comments explain **why**, not what. Where a decision was made against an obvious
  alternative, the comment says which alternative and why it lost.
- Constants live in one file per pillar (`Diplomacy/DiplomacyConstants.cs`) so a balance pass
  edits one file. An un-tuned constant says so in its own doc comment.
- All player-facing text is English. The design docs are English.
- **Reply to the user in Vietnamese.** The lead writes in Vietnamese; the codebase is not.
- Any Vietnamese meant to be read (the Vietnamese handbook, player text, write-ups for the lead)
  goes through the `vietnamese-writing` skill (`.claude/skills/vietnamese-writing/`), with its
  glossary for the mod's terms.

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
| [docs/STATUS.md](docs/STATUS.md) | **where the work stands, what to do next** - the current part only |
| [TODO.md](TODO.md) | the one list of open decisions for the lead and pending work |
| [docs/STATUS-history.md](docs/STATUS-history.md) | every earlier handoff, checkpoint and verification table, verbatim |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | layering, the runtime facts in full, save rules, how the game is hooked |
| [docs/ROADMAP.md](docs/ROADMAP.md) | all four phases, what is done, what is verified, acceptance criteria |
| [docs/design/01-diplomacy.md](docs/design/01-diplomacy.md) | Phase 1 spec, formulas, and the lead's decisions |
| [docs/design/02-intrigue.md](docs/design/02-intrigue.md) | Phase 2 spec — grievances, loyalty, blocs, legitimacy, civil war |
| [docs/design/03-espionage.md](docs/design/03-espionage.md) | Phase 3 spec — networks, missions, exposure as diplomacy |
| [docs/design/04-hegemony.md](docs/design/04-hegemony.md) | Phase 1.9/1.10 spec — hegemon derived from vassalage, Hold, defiance, the rise |
| [docs/design/05-vanilla-override.md](docs/design/05-vanilla-override.md) | Phase 1.11 — every vanilla diplomacy surface and the lever that takes it |
| [docs/design/08-statecraft.md](docs/design/08-statecraft.md) | Phase 2.8 — the six political skills, who holds each office, the terms, and what trains them |
| Game install | `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` (auto-detected; override with `BANNERLORD_GAME_DIR`) |
| Mod logs | `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\` |
| Mod reports | `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Reports\` |

Two files outside the repo were modified to make testing work, both with `.bak-*` backups
beside them: the launcher load order (`Documents\...\Configs\LauncherData.xml`, enabling
ButterLib/UIExtenderEx/MCM which were off) and the GABS launch script
(`Desktop\Agent_Bannerlord\Bannerlord.GABS\launch-bannerlord.ps1`, whose `_MODULES_` list is
hardcoded and does not read LauncherData).

## 7. Roles, and delegating to opencode

- **Product owner**: the user. Makes design and priority calls.
- **Claude Code (you)**: BA / tech lead. Breaks work down, decides what to delegate to
  opencode versus do directly, reviews what opencode produces before reporting to the owner.
- **opencode CLI**: dev / tester. Implements and tests whatever Claude Code delegates to it.

Claude and opencode work in **two separate clones** of the same GitHub repo
(`namphan19/Bannerlord-Diplomacy-Intrigue`) — this one (`bannerlord.mod`, branch `development`)
and opencode's own `bannerlord.mod.opencode` (one `feature/*` branch per brief) — not one shared
folder. opencode's actual output therefore travels through three channels: the `opencode-bridge`
MCP server for synchronous delegation, **git** for what it actually built (its commits sit
in its own clone against the shared origin; review them there, e.g. `git fetch` + diff, or a
PR), and `claude-bridge` for opencode to page Claude mid-task with a question.

Delegate through the `opencode-bridge` MCP server (`tools/opencode-bridge/`, registered in
`.mcp.json` with `OPENCODE_PROJECT_DIR` pointed at opencode's checkout, not this one):
`opencode_delegate(task, session_id?, agent?, title?, timeout_seconds?)` — pass
`session_id` from a prior call to continue the same conversation (follow-ups, fix requests on
the same piece of work). `opencode_list_sessions` / `opencode_delete_session` for housekeeping.
Details and setup: [tools/opencode-bridge/README.md](tools/opencode-bridge/README.md).

The reverse direction is `tools/claude-bridge/` (registered in opencode's own
`opencode.jsonc`, not here): `ask_claude(question, session_id?, timeout_seconds?)` lets
opencode consult Claude without a human relaying. It runs read-only (`claude -p
--allowedTools "Read Grep Glob"`, no permission-bypass flag) — deliberately, since a
headless Claude with write access could collide with an interactive session editing the
same tree, and answering a question is not the same job as acting on one. Not live-verified
end to end: this harness blocks a Claude Code session from spawning a nested `claude -p`
itself ("Create Unsafe Agents"), so only opencode's side of the handshake (`opencode mcp
list` showing `claude-bridge` as `connected`) has been confirmed — the first real
`ask_claude` call is the first live test of the `claude -p` invocation. Setup and the full
safety reasoning: [tools/claude-bridge/README.md](tools/claude-bridge/README.md).

opencode runs with `--auto` — it has no TTY through this bridge, so a permission prompt would
hang forever with nothing able to answer it — and the owner granted it full permissions on
2026-09-22. A delegated task can therefore run any shell command in this repo unsupervised.
Review what comes back before passing it on; the rules in §3 (save ids, Harmony as last
resort, one resolver per concept, no throw across the engine boundary) still apply to code
opencode wrote — delegating a task doesn't relax them.

opencode's working rules live in [AGENTS.md](AGENTS.md), which opencode reads on its own: a
branch per brief off `origin/development`, a PR into `development`, **never merging its own
PR** (you review, then merge or send fixes back through the same `session_id`), and a fixed
PR body (what / how verified / not verified / save data / Harmony). The lead enabled GABS
for opencode on 2026-09-23, so it deploys and drives the game itself. There is **one game
and one deploy target for both clones**: do not touch the game while a delegated task that
tests in game is running, and after one finishes, check which branch it left deployed
before you test anything of your own.
