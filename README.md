# Bannerlord: Diplomacy & Intrigue

A large gameplay mod for **Mount & Blade II: Bannerlord v1.4.8** that replaces Calradia's
permanent-war stalemate with three connected political systems:

- **Diplomacy** — treaties, casus belli, war exhaustion, a real peace table
- **Court intrigue** — vassal grievances, court blocs, legitimacy, succession, civil war
- **Espionage** — spy networks, covert missions, counter-intelligence, diplomatic incidents

Standalone: no dependency on other diplomacy mods.

> Status: **Phase 0 (foundation) complete.** Builds and loads; gameplay systems are being
> implemented pillar by pillar. See [docs/ROADMAP.md](docs/ROADMAP.md).

## Requirements

| | Version |
|---|---|
| Bannerlord | v1.4.8 (singleplayer campaign) |
| BLSE | any current |
| Bannerlord.Harmony | 2.4.2+ |
| Bannerlord.ButterLib | 2.10.4+ |
| Bannerlord.UIExtenderEx | 2.13.2+ |
| Bannerlord.MBOptionScreen (MCM) | 5.11.4+ |

## Building

Needs the .NET SDK (8 or newer). The module itself targets **net472**, because the Win64
shipping client is a .NET Framework 4.7.2 host — see
[docs/ARCHITECTURE.md §1.1](docs/ARCHITECTURE.md). No Visual Studio required: the net472
reference assemblies come from a NuGet package.

```powershell
pwsh ./scripts/build.ps1                 # compile only — does not touch the game folder
pwsh ./scripts/deploy.ps1                # pre-flight check, then install into the game
```

`deploy.ps1` runs `tools/LoadProbe` before copying anything, and refuses to install a
module the game could not load.

The game install is located automatically. To point at a different one:

```powershell
$env:BANNERLORD_GAME_DIR = "D:\SteamLibrary\steamapps\common\Mount and Blade II Bannerlord"
# or
pwsh ./scripts/deploy.ps1 -GameFolder "D:\...\Mount and Blade II Bannerlord"
```

Deploy never happens as a side effect of a normal build, and refuses to run while the game
is open.

## Layout

```
src/DiplomacyIntrigue/        mod source (see docs/ARCHITECTURE.md for the layering rules)
module/DiplomacyIntrigue/     what ships to the player: SubModule.xml, ModuleData, GUI, built bin
docs/                  architecture, roadmap, per-system design specs
scripts/               build and deploy
```

## Diagnostics

Logs: `Documents/Mount and Blade II Bannerlord/DiplomacyIntrigue/Logs/` — attach the newest file to
any bug report.

If the game shows *"submodule could not be loaded correctly due to a dependency conflict"*,
there will be **no log at all** — the failure happens before any module code runs. Run the
pre-flight check instead, which names the actual cause:

```powershell
dotnet run --project tools/LoadProbe
```

In game, press **Ctrl+D** on the campaign map to open the diplomacy menu.

Developer console (Alt+`~`):

```
diplomacy.status          module health, schema version, record counts
diplomacy.wars            ongoing wars with exhaustion and war score
diplomacy.treaties        active agreements, subordination and tribute
diplomacy.claims          live casus belli and what each allows
diplomacy.trust           the reputation ledger, both directions
diplomacy.weariness       what past wars have left behind
diplomacy.bands           how a rival's exhaustion is shown to the player
diplomacy.menu            open the diplomacy menu

diplomacy.can_war A | B           whether a war is allowed, and why not
diplomacy.war_value A | B         the AI war valuation, term by term
diplomacy.pact_value A | B        what each side thinks an agreement is worth
diplomacy.peace_allowance A | B   what a war has earned
diplomacy.sign_treaty A | B | Alliance
diplomacy.break_treaty A | B | Alliance
diplomacy.offer_peace A | B | fief=Pravend, prisoners
diplomacy.fief_history Pravend
diplomacy.fabricate_claim Pravend
```

Balance and diagnosis tools:

```
diplomacy.tick_days 30    run N days of upkeep without moving the clock
diplomacy.ai_week 4       run N weeks of AI diplomacy
```

Both drive the same functions the campaign tick calls. Neither can advance
`CampaignTime.Now`, so treaty expiry, claim ageing and influence income do not happen
inside them - see [docs/ROADMAP.md](docs/ROADMAP.md) before drawing conclusions from a
long run.

## Running a balance pass

The acceptance test for the diplomacy pillar is a statement about a decade of campaign -
average war length, whether alliances form and hold, whether anyone sits at permanent war.
The mod measures that on its own, so a balance run needs no console and no supervision.

1. Start or load a campaign with the mod enabled.
2. Set the campaign speed high and **leave the game window focused** - Bannerlord throttles
   itself heavily in the background, to roughly two in-game hours per real minute, which
   makes an unfocused run useless.
3. Let it run. A few hours of real time is a few in-game years.
4. Collect two things:

**The log** — one file, this is the dataset:

```
Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\
```

Take the newest `diplomacy-intrigue-*.log`. It carries two machine-readable line types:

```
[SNAPSHOT]   date=... kingdoms=8 atWar=6 wars=3 avgExhaustion=34.2 alliance=1 ...
[WAR-ENDED]  aggressor=Vlandia defender=Sturgia days=624 casusBelli=ReclaimAncestralLand ...
```

One snapshot per in-game week, one war-ended line per war. `days=` on the war-ended lines is
what answers "do wars average under three years" - 3 years is 252 days, since a Bannerlord
year is four 21-day seasons.

**A report** — optional, a readable snapshot of the whole world: press **Ctrl+D** on the map
and choose *Write a report to file*. It lands in:

```
Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Reports\
```

The in-game developer console is **not** available in a retail install unless a separate
console mod is installed, so nothing above depends on it. The `diplomacy.*` commands listed
earlier exist for development and for anyone who does have a console.

## Documentation

- [docs/STATUS.md](docs/STATUS.md) — where the work stands right now and what to do next
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — layering, save-data rules, how we hook the
  game, reliability conventions
- [docs/ROADMAP.md](docs/ROADMAP.md) — phases, acceptance criteria, cross-pillar dependencies
- [docs/design/](docs/design/) — the specs for each pillar, with formulas and the decisions taken
- [CLAUDE.md](CLAUDE.md) — conventions and hard-won environment facts, for anyone (or any
  agent) picking the project up
