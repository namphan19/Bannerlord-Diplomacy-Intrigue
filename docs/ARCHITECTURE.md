# Diplomacy & Intrigue — Architecture

## 1. Target and hard constraints

| Item | Value | Why it matters |
|---|---|---|
| Game | Mount & Blade II: Bannerlord **v1.4.8** | TaleWorlds breaks internal APIs between minor versions. Every patch we write is version-pinned. |
| Runtime | **.NET 6** (game ships its own CoreCLR in `bin/Win64_Shipping_Client`) | Mod must target `net6.0`. Never ship a newer BCL assembly — the game's runtime wins and you get `TypeLoadException`. |
| Loader | **BLSE** (already installed) | Gives us assembly resolution and crash reports. |
| Frameworks | Harmony 2.4.2, ButterLib 2.10.4, UIExtenderEx 2.13.2, MCM 5.11.4 | All declared as hard dependencies in `SubModule.xml`. |
| Scope | Singleplayer campaign only | Multiplayer has no campaign layer; `OnGameStart` bails out unless `game.GameType is Campaign`. |

Two constraints drive most design decisions below:

1. **A save must keep loading.** Players run 200-hour campaigns. Once a data shape ships, it is frozen. See §4.
2. **A throw inside an engine hook kills the game.** Not our mod — the whole game. So every engine-facing entry point is wrapped. See §6.

## 2. Layering

```
┌──────────────────────────────────────────────────────────────┐
│  UI            Gauntlet screens, ViewModels, UIExtenderEx    │
│                (reads state, issues commands — no rules)     │
├──────────────────────────────────────────────────────────────┤
│  Behaviors     CampaignBehaviorBase: event wiring, ticks,    │
│                save/load. The only layer the engine calls.   │
├──────────────────────────────────────────────────────────────┤
│  Systems       Diplomacy / Intrigue / Espionage              │
│                Pure-ish rules: evaluate, decide, mutate.     │
├──────────────────────────────────────────────────────────────┤
│  Models        Treaty, WarRecord, Grievance, SpyMission…     │
│                Savable data + invariants. No engine calls.   │
├──────────────────────────────────────────────────────────────┤
│  Core          State root, logging, settings, save definer    │
└──────────────────────────────────────────────────────────────┘
        ▲                                        ▲
        │ Harmony patches (Patches/)             │ GameModel overrides
        │ — last resort, version-pinned          │ — preferred override path
```

Dependency rule: a layer may reference anything **below** it, never above. `Models` must stay free of `Campaign.Current` so it can be reasoned about and tested.

### Directory map

| Path | Holds |
|---|---|
| `src/DiplomacyIntrigue/Core/` | `ModState` (save root), `Log`, `ModSettings`, `ModSaveDefiner`, `DebugCommands` |
| `src/DiplomacyIntrigue/Models/` | Savable data types and enums |
| `src/DiplomacyIntrigue/Diplomacy/` | Treaty engine, casus belli, war exhaustion, peace-table valuation, AI diplomacy |
| `src/DiplomacyIntrigue/Intrigue/` | Grievances, court blocs, loyalty, succession, civil war |
| `src/DiplomacyIntrigue/Espionage/` | Networks, missions, counter-intelligence |
| `src/DiplomacyIntrigue/Behaviors/` | `CampaignBehaviorBase` implementations — the engine boundary |
| `src/DiplomacyIntrigue/Patches/` | Harmony patches, one file per patched method |
| `src/DiplomacyIntrigue/UI/` | ViewModels and screen code |
| `module/DiplomacyIntrigue/` | Ships to the player: `SubModule.xml`, `ModuleData/`, `GUI/`, built `bin/` |

## 3. Three ways to change game behavior — in order of preference

**1. Campaign behaviors and events (always prefer this).**
`CampaignBehaviorBase` + `CampaignEvents` is public, stable across patches, and composes with other mods. All of our own bookkeeping lives here.

**2. `GameModel` overrides.**
TaleWorlds routes most tunable numbers through swappable models — `DiplomacyModel`, `DefaultDiplomacyModel`, `ClanPoliticsModel`. We subclass and register in `OnGameStart`:

```csharp
gameStarter.AddModel(new ModDiplomacyModel());   // replaces the vanilla model
```

This is the supported way to change costs, AI valuations, and thresholds. Downside: **two mods overriding the same model fight**, last registration wins. Documented in the compatibility notes per model we take over.

**3. Harmony patches (last resort).**
Used only where there is no event and no model — mainly AI decision-making inside `KingdomDecision` subclasses and a few UI hooks. Rules for `Patches/`:

- One patched method per file, named `<Type>_<Method>_Patch.cs`.
- Prefer `Prefix` returning `bool` for veto, `Postfix` for adjustment. **Avoid `Transpiler`** — it breaks on every game patch.
- Every patch declares in a header comment: *what* it changes, *why no event exists*, and the *game version verified against*.
- Every patch body is `try/catch`; a failing patch must degrade to vanilla behavior, never throw into the game.

## 4. Save data strategy

All Diplomacy & Intrigue data hangs off one root object, `Core.ModState`, owned by `CoreBehavior` and synced in `SyncData`. Registration lives in `ModSaveDefiner`, which reserves the id block **`2749100`–`2749199`**.

Non-negotiable rules:

1. **Never renumber or reuse a `SaveableProperty` id.** Retire it with a comment and take the next free number.
2. **Never reuse a save-definer local id** for a different type.
3. **Never change the definer base id** once a build has shipped — it invalidates every existing save.
4. **Assume every collection can come back `null`** (older save, field added later). `ModState.AfterLoad()` re-checks all of them.
5. **Assume references can dangle.** A destroyed kingdom leaves `null` behind. `AfterLoad()` drops orphaned records rather than letting a `NullReferenceException` surface three hours later.
6. **`SchemaVersion` + `Migrate()`** carry saves forward when the *meaning* of existing data changes.

Mid-campaign install is a supported scenario: `BackfillOngoingWars()` opens records for wars that predate the mod, so no system ever meets a war it has no record of.

## 5. Where the AI plugs in

Diplomacy & Intrigue is mostly an AI mod — the player-facing UI is a thin shell over decisions the AI also makes. Three insertion points:

- **Kingdom decisions.** Vanilla AI votes through `KingdomDecision`. We add our own decision types (treaty ratification, war declaration with a named casus belli) and adjust support calculations so grievances and court blocs actually move votes.
- **Model numbers.** War exhaustion, treaty value, and tribute amounts flow through our `DiplomacyModel` subclass, so vanilla AI code paths pick them up for free.
- **Periodic evaluation.** A weekly tick per kingdom scores available diplomatic actions (seek peace / offer pact / demand tribute / launch scheme) and enqueues the best one. Deliberately *slow* — AI diplomacy churning daily feels random to the player.

## 6. Reliability conventions

- **No throw crosses the engine boundary.** `SubModule` hooks, behavior event handlers, and Harmony patches all catch, log, and continue.
- **`SubModule.Healthy`** is false when startup failed; systems check it and stay inert rather than half-running.
- **Logging goes to a file**, not the game console: `Documents/Mount and Blade II Bannerlord/DiplomacyIntrigue/Logs/`. Ten most recent runs are kept. Bug reports = this file.
- **`Log.Notify`** for anything the player must see; it also writes to the log.
- **Settings are read through `Core.Settings.Current`**, never `ModSettings.Instance`. MCM can be absent or throw during early startup, and the accessor falls back to defaults.

## 7. Compatibility posture

Standalone by decision — no dependency on the BUTR *Diplomacy* mod. Consequences to manage:

- We own the whole peace/war/treaty surface, so **running Diplomacy & Intrigue alongside Diplomacy will conflict** (both override diplomacy models and patch the same decisions). Documented as incompatible; a detection-and-warn check is Phase 4 work.
- Mods that only *add* content (troops, items, map) are unaffected.
- Any mod overriding `DiplomacyModel` or `ClanPoliticsModel` conflicts by nature. We keep the list of models we take over short and documented.

## 8. Build and deploy

```
pwsh ./scripts/build.ps1                  # compile only, nothing touches the game
pwsh ./scripts/deploy.ps1                 # compile + copy module tree into the game
```

Game path resolution order: `-p:GameFolder=…` → `%BANNERLORD_GAME_DIR%` → known Steam paths. Output is built straight into `module/DiplomacyIntrigue/bin/Win64_Shipping_Client/` so the repo layout always mirrors what a player installs.

Deploy never runs as a build side effect — it requires `-p:DeployToGame=true`, and `deploy.ps1` refuses while the game is running.

## 9. In-game diagnostics

Developer console (Alt+`~`):

```
diplomacy.status      module health, schema version, record counts, log path
diplomacy.wars        ongoing wars with exhaustion and war score
diplomacy.treaties    active treaties with expiry dates
```
