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

Developer console (Alt+`~`):

```
diplomacy.status
diplomacy.wars
diplomacy.treaties
```

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — layering, save-data rules, how we hook the
  game, reliability conventions
- [docs/ROADMAP.md](docs/ROADMAP.md) — phases, acceptance criteria, cross-pillar dependencies
