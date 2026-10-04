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

**The game's language belongs to the official launcher, and `BannerlordConfig.txt` does not set it.**
`Documents\Mount and Blade II Bannerlord\Configs\BannerlordConfig.txt` line 1 reads `Language=<id>`,
and it looks like the setting - it is an **output**. The game rewrites it to English at every
startup (checked 2026-10-04: the file's timestamp is the launch second). There is no Language entry
in the in-game Options screen, no console command that sets one (`list_commands` on `lang` and
`locale`: none), no registry key under `HKCU\Software`, and no attribute containing "lang" in
`LauncherData.xml`. The language belongs to the **official launcher's UI**, and because the launcher
hosts the game in its own process (§1), a `games_start` / BLSE Standalone launch bypasses that UI
and comes up English.

**A language change takes effect without restarting the game** (the lead's session, 2026-10-04), and
**the game's own log is where you prove any of this**: `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`
names **every language file it opens, per module**. That log is how AC2 was answered - it shows
`Native/.../Languages/DE/de_functions.xml` at 11:15:45 and then
`DiplomacyIntrigue/.../Languages/DE/di_strings.xml` at 11:15:46, so the mod's folder is found and
read. It also shows a `Languages/VI/` load, because the Vietnamese community patch is installed in
`Native` on this machine (story 4.2).

**An empty language folder proves nothing.** The 12 non-English folders ship empty on purpose (lead,
2026-10-03), so with an empty `DE` folder every key falls back to the English the code carries -
which is R1 working - and a screen full of English is indistinguishable from the mod ignoring the
language. That is exactly what the lead's first test showed.
`scripts/localization-fixture.ps1 -Action Write` writes fourteen German keys into the **deployed**
folder for a look, `-Action Remove` puts the repo's empty file back, and it refuses to write a key
that is not in `EN` or whose `{VARIABLES}` differ. `deploy.ps1` overwrites the deployed folder, so
re-run it after a deploy. It is a fixture, not a translation: machine-written and unreviewed, which
is the same argument the lead accepted for shipping the folders empty.

**`bannerlord.diplomacy.declare_war` reports success when the war was refused.** It answers
`"Khuzait declared war on Battania"` and nothing happens - the mod refuses a vassal declaring a war
on its own account, and the refusal is not surfaced. Check with `diplomacy.wars`, which reads our
own state, not with `kingdom.list_wars`. `campaign.declare_war` behaves the same way, and it says
`"Faction 2 is eliminated"` for a kingdom that is not in the save.

**Vanilla's Encyclopedia layer crashes v1.5.3 when `diplomacy.test_open_encyclopedia` pushes it.**
A `NullReferenceException` in `GauntletLayer.IsFocusedOnInput` from
`SandBox.EncyclopediaData.OnTick`, with the mod log clean and no frames of ours - read live with
`tools/DumpProbe -- --pid`. The Encyclopedia court page is the one converted screen never seen for
that reason. Close the dialog with **No** (§1) and the process exits and writes its dump.

**A golden file generated from the code cannot see what the conversion changed.** Story 4.1's
`artifacts/localization/golden-EN.json` records the English *after* the conversion, so it agrees
with whatever the conversion produced. What found three damaged strings was reading every literal
that moved out of a prefab back out of the previous commit (`git show <base>:<prefab>`), matching it
to the property that replaced it by line number, and comparing that property's English with it:
88 moved, 4 left as punctuation on purpose, 3 different - a U+00A7 where a U+00B7 had been, and a
heading that had lost its first word. **Compare against the base commit when a pass rewrites text
in bulk, not against a file the same pass generated.**

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

**Players own War Sails, and it swaps the Clan and Kingdom screens' view models.** With NavalDLC
loaded the screens are `NavalGauntletClanScreen` / `NavalGauntletKingdomScreen` over
`NavalClanManagementVM` / `NavalKingdomManagementVM`, subclasses of the vanilla ones. A
`[ViewModelMixin]` without `handleDerived: true` then never attaches while its prefab patch still
lands, so every `IsVisible="@..."` keeps its default of true: all our panels and overlays draw at
once, empty, with dead buttons. Players reported exactly that on 2026-09-28; no test had loaded
NavalDLC. The GABS launch list includes it since then. Saves made without NavalDLC crash on load
with it (an NRE in `NavalDLCManager.OnGameStart`, not ours); `di_naval_test` is a save made with it.

**This machine is not on v1.4.8.** Since 2026-09-26 18:54 the Steam install is on the `beta`
branch, v1.5.3. Every live check and every "verified by IL on v1.4.8" written after that moment
was made against v1.5.3. Players are on v1.4.8, so a release ships the DLL
`scripts/compile-check.sh` builds against the v1.4.8 reference assemblies (`scripts/release.ps1`
does this), never the local build. The lead confirms (2026-09-30) that this is also the DLL that
has been running live on this v1.5.3 machine all along - every GABS session in this file loaded
it there, not on v1.4.8 - so the v1.4.8-ref build is verified to *load* on v1.5.3. **Loading is
not running**: on 2026-10-01 that same DLL could not start a single civil war on v1.5.3
(`MissingMethodException` on `Kingdom.InitializeKingdom`, whose ten-parameter form v1.5.3 replaced
with a twelve-parameter one). .NET binds a call when the method holding it first runs, so LoadProbe
and a clean session cannot see a broken reference on a path nobody has walked. The fix calls that
one method by reflection; a scan of every reference the DLL makes into `TaleWorlds.*` (689) then
resolves against both v1.4.8 and v1.5.3. A scan checks names and signatures, not enum numbers or
behaviour. That does not retire the rule: `DependedModules` in `SubModule.xml` gates on the *installed*
game version being at least `v1.4.8`, which is a separate check from whether the compiled DLL's
baked-in enum numbers still line up with a later version's API - the risk `release.ps1`'s header
describes. Nothing has tested that a *future* version keeps the same numbering; build for a
release against the v1.4.8 refs regardless of which branch this machine happens to be on.

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
pwsh ./scripts/check-localization.ps1   # the key rules of §4, read from source; build and deploy run it too
dotnet run --project tools/Localize -- report    # every string literal, and whether a player reads it
dotnet run --project tools/Localize -- rewrite --apply   # key the approved ones; dry run without --apply
dotnet run --project tools/Localize -- emit --apply      # write Languages/EN from the DiText calls
dotnet run --project tools/Localize -- prefabs          # the prefab labels that cannot be keyed in place
scripts/compile-check.sh     # no game on this box (Linux, cloud): compile against NuGet reference assemblies
pwsh ./scripts/release.ps1   # Nexus zip from the committed tree, DLL built against v1.4.8 refs
pwsh ./scripts/workshop.ps1 -ChangeNotes <file>   # Steam Workshop update from release.ps1's folder; -Upload publishes
```

**The Steam Workshop item already exists: `3810668052`** (created 2026-09-30, public, subscribed).
`workshop.ps1` updates it; a `CreateItem` task would publish a duplicate. The "Bannerlord: Mod
Uploader" tool that created it uploads from the game's `Modules\DiplomacyIntrigue`, which holds
whatever `deploy.ps1` last installed - not the v1.4.8-reference build a release must be - so do not
publish through it. Its own log is `bin\Win64_Shipping_Client\steam_workshop_uploader.txt`.

A release build (`DI_RELEASE_BUILD=1`, set only by `release.ps1`) compiles player-facing defaults:
today, telemetry off. Every other build keeps telemetry on, so a balance run is never short of it.
`diplomacy.perf [reset]` prints the tick budget; with telemetry on a weekly `[PERF]` line logs it.

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
3. `bannerlord.core.set_cheat_mode` true — needed before any `campaign.*` command, and since
   2026-09-27 before every `diplomacy.test_*` lever and `sign_treaty`, `break_treaty`,
   `offer_peace`, `fabricate_claim`, `tick_days`, `ai_week`, `set_smoothed_strength`,
   `set_war_score` too (`CampaignCheats.CheckCheatUsage`, the check vanilla's cheats make).
   Without it they answer `Refused: ... needs cheat mode`; read-only diagnostics stay open
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
live ReadCourt on that realm), `amends <kingdom> [| clan]` (every grievance against a crown priced
term by term, and the AI's pick this week; a dry run) and the lever `test_amends <clan> [| type]`
(the real act, paid by whoever rules - `test_player_rule` cannot move a player who already rules a
realm, so this is how a crisis court's own ruler is made to act), `offices [kingdom]` (each seat,
its holder, who speaks for it, the houses in favour, and the AI's plan today), the levers
`test_appoint <kingdom> | <seat> | <hero>` and `test_dismiss <kingdom> | <seat>`, and
`test_court_seat <seat>` (selects a seat on the Court tab: "Envoy" also appears elsewhere in the
widget tree, so a click by text is not reliable), `vassal_tribute [kingdom]` (every vassalage's
tribute and what each level would do to its Hold target and income), the lever
`test_vassal_tribute <patron> | <vassal> | <None|Light|Standard|Heavy>`, and the summons
(diagnose 04 §5.2a, story 1.10c): `summons [kingdom]` (every live summons, every cooldown and
each link's eligibility, at the price the button would charge), `summons_value <patron> | <vassal>`
(that price term by term; one kingdom alone is priced against each of its vassals),
`ai_summons [kingdom]` (each AI hegemon's plan for the week, a dry run) and the lever
`test_summon <patron> | <vassal>` (the real order, through the same call the Realm tab's button
makes: it charges the full price, and a refusal earns a defiance mark - so it is a test save only), and
the bound patron's choice (story 1.10d): `bound_choice <patron> | <vassal> | <attacker>` (what the
link is worth against what breaking every treaty with the attacker costs, term by term, and the
answer; one kingdom alone is priced against each of its vassals) and the lever
`test_fail_bound_war [on|off]`, which tears the treaties up and refuses the war so the atomicity
can be checked in the log. `test_set_hold <patron> | <vassal> | <0.1-100>` sets a link's Hold: a link made by `sign_treaty`
has no starting Hold and falls under the 40 line within a day, so it is the only way to stage a
vassal that serves (Hold drifts down one a day; re-set it while waiting). `test_raise_army <kingdom> [| <target settlement>]`
has the ruler raise an army of their own party (the engine's `Kingdom.CreateArmy`; an AI ruler who is a
governor has no party and cannot) - without it nothing past "commands no army" can be staged.
`test_player_army [<target settlement>] [| <n>]` does the same for the player, with up to n of the realm's
lord parties called in, and says whether it is now the realm's largest army - story 3.10 AC2a's mark
(the player must serve a realm, and not rule it, to be one).
Espionage: `diplomacy.networks` (each handler's settlement and realm, flagged when outside the target), `mission_odds <clan> | <kingdom>`, `missions`, `bribes` (every
bribe still on the record and whether it binds anybody), `counter_intelligence [kingdom]` (every
realm's defence term by term), `ai_espionage [kingdom]` (each AI realm's espionage plan for the week,
a dry run), and the levers `test_set_network`, `test_counter_budget <kingdom> | <denars>`,
`test_launch_mission <clan> | <kingdom> | <type> [| settlement or hero]` and
`test_resolve_mission <clan> | <kingdom> [| success|failure|exposed]`, and
`test_found_network <hero> | <kingdom> [| strength]`, which founds a network **without the handler
rules** (a clan head, party leader or governor may be put on it, and nobody is moved) - the only way to
stage an AI ruling house's operation, since every such house tried had no free lord. It lasts until the
next daily tick, which releases or re-stations that hero, so launch and force-resolve before any
`tick_days`; `test_assign_handler` stays the lever for anything about who may handle.
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
`tick_days 1` - only the 84-day cooldown after a war needs the real clock. Note that `sign_treaty` with
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
a scene notification ("… joined the Kingdom of …", raised by `ChangeKingdomAction`), but only when
no inquiry is queued: it answers an inquiry first, even one hidden behind the notification (on
2026-10-01 a call meant to clear the notification chose "break the treaty" on the prompt beneath
it). A notification left up makes `save_game` fail silently. A second notification queued behind a
Kingdom Decisions popup stayed on screen with no inquiry the bridge could see; after
`test_player_rule`, dismiss the notification, save, and stage from that save.
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
(`2749100`, block `2749100`–`2749199`). `Treaty` currently uses ids **1-19** (14 `Hold`, 15
defiance marks, 16 last defiance, 17 the revolt clock, 18 `TributeSetOn`, design 09 C3, 19
`LastSummonedOn`, story 1.10c), so the next free id there is **20**. `TrustRecord` uses **1-6** (5 `LastPositiveChange`, 6
`LastOfferRefused`), next free **7**. `WarRecord` uses **1-16** (7 is the old `WarScore`, renamed
`BattleScore` by design 10 - the save system keys by id, not name; 15-16 manpower at the war's
start), next free **17**. `ModState` uses
properties **1-19** (11 `Grievances`, 12 `Legitimacy`, 13 `Pretenders`, 14 `InternalWars`,
15 `SpyNetworks`, 16 `SpyMissions`, 17 `CounterIntelligenceBudgets`, 18 `Offices`, 19 `Summons`), next free **20**. The definer's class ids run to **19**
(10 `Grievance`, 11 `KingdomLegitimacy`, 12 `Pretender`, 13 `InternalWar`, 14 `InternalWarMember`,
15 `SpyNetwork`, 16 `SpyMission`, 17 `CounterIntelligenceBudget`, 18 `CourtOffice`, 19 `SummonsRecord` - **19 is now taken, so nothing is left
below the enum block**: the definer adds its base to class and enum ids alike, and they are
believed to share one id space (not verified - the reference assemblies carry no method bodies), so
the next class takes **29** or above - not 20, not 28, which `Portfolio` took. Enums are **20-28**
(26 `GrievanceType`, 27 `InternalWarOutcome`, 28 `Portfolio`, whose values 0-5 are now frozen), next free **29**. `CourtOffice` uses
properties 1-4 (next free **5**). `Grievance` uses
properties 1-7 (6 `AnsweredOn`, 7 `Answers`, design 09; next free **8**), `KingdomLegitimacy` 1-5, `Pretender` 1-4, `InternalWar` 1-15 (13 `Faction`,
14 `SideChanges`, 15 `Captures`, next free **16**), `InternalWarMember` 1-2 (2 `Fief`, set only on a capture; next free **3**), `SpyNetwork` 1-8 (next free **9**), `SpyMission` 1-12 (12 `OfferOwed`, 2026-09-27; next free **13**),
`CounterIntelligenceBudget` 1-3 (next free **4**), `SummonsRecord` 1-7 (next free **8**). A new *value* on an enum the definer
already registers is safe (`GrievanceType.SuccessionPassedOver = 9`, `ForgedLetters = 10` and
`DismissedFromOffice = 11` were added that way; next free value there is **12**);
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
exists, and the *game version verified against*; a `try/catch` that degrades to vanilla. Seven
patch files exist today, one method each, and all follow this. Phase 1's three guard war
initiation: `DeclareWarDecision_IsAllowed_Patch` and the two `DeclareWarAction_*` backstops,
which were one file patching both methods until 2026-09-26 and now share one answer,
`TreatyEnforcement.WhyWarActionRefused`. `KingdomDecision_DetermineSupportOption_Patch`
(Phase 2.3 bloc voting), `Clan_MapFaction_Patch` and `Hero_MapFaction_Patch` (Phase 2.6
internal war) record in their headers the evidence that no event or `GameModel` could do the
job, and so does `HeroSpawn_GetBestAvailableCommander_Patch` (2026-10-02, the lead's call after
run 11): vanilla's second pass for a commander skips the `CanHeroLeadPartyEvent` veto, so a
posted spy handler who was their house's only free lord was put on a party anyway. It is the
only patch on a private method, so it finds its target in `TargetMethod` and skips itself in
`Prepare` if the method is gone, rather than failing `PatchAll` for all seven. The mod log's
`Harmony patched N methods:` line, written at startup, names every method actually patched. Do
not add an eighth without the same evidence.

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
- Player-facing text is written **through `DiText.T`**, never as a bare literal:
  `DiText.T("DI_REALM_VASSAL_OF_NAME", "Vassal of {NAME}", ("NAME", patron.Name))`. The English
  stays in the source as the fallback; `ModuleData/Languages/EN/di_strings.xml` is generated from
  those calls by `tools/Localize emit`, and **`scripts/check-localization.ps1` fails the build on a
  key with no entry, an entry nobody uses, a key used for two English texts, and a variable passed
  and unused or used and unpassed.** Two rules the call sites must keep: a sentence with a number
  or a name in it is **one call with named variables**, never a concatenation, so word order can
  change per language; and a value travels as a variable, never typed into the English, because a
  number on screen is the number the AI used (§3). **A widget's `Text` cannot be keyed**: a
  prefab's literal must move into a view-model property (`Text="@Property"`), which
  `tools/Localize prefabs` lists. Logs, telemetry and `diplomacy.*` output stay English and are
  never keyed. The design docs are English. `docs/localization.md` is the guide for a translator.
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
| [docs/stories/](docs/stories/) | one file per story: scope, rules, acceptance criteria, sub-tasks (e.g. [4.1 localization](docs/stories/4.1-localization.md)) |
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

## 7. Roles

- **Product owner**: the user. Makes design and priority calls.
- **Claude Code (you)**: BA / tech lead and developer. Breaks the work down, builds it, tests it
  in the live game, and reports to the owner.

There is no second agent. opencode was a dev / tester working from its own clone through an
`opencode-bridge` MCP server, with a `claude-bridge` server in the other direction; the lead
removed it on 2026-09-27, along with both bridges, `AGENTS.md` and `.mcp.json`. Its old
`feature/*` branches may still sit on origin: the merged ones are history, and anything unmerged
is reviewed like any other branch before it goes near `development`. Earlier docs that say
"briefed to opencode" or "opencode's merge" describe how that work was done, not how it is done
now.
