# Diplomacy & Intrigue — Roadmap

Three pillars, chosen by the project lead: **inter-kingdom diplomacy**, **court intrigue**, **espionage**. Standalone (no BUTR Diplomacy dependency), English UI.

The ordering below is not arbitrary. Each phase produces something playable on its own, and each later pillar consumes data the earlier one produces — intrigue needs war exhaustion to argue about, espionage needs treaties and grievances to sabotage.

---

## Phase 0 — Foundation ✅ done

Buildable, loadable, save-safe skeleton.

| Delivered | Where |
|---|---|
| `net6.0` project pinned to game v1.4.8, auto-detecting the install | `Directory.Build.props`, `src/DiplomacyIntrigue/DiplomacyIntrigue.csproj` |
| Module manifest with all four framework dependencies | `module/DiplomacyIntrigue/SubModule.xml` |
| Entry point with failure isolation and health flag | `src/DiplomacyIntrigue/SubModule.cs` |
| File logger, log rotation, in-game notify | `Core/Log.cs` |
| Save root + schema versioning + migration + orphan cleanup | `Core/ModState.cs`, `Core/ModSaveDefiner.cs` |
| War ledger tracking vanilla war/peace, with mid-campaign backfill | `Behaviors/CoreBehavior.cs` |
| MCM settings with safe fallback | `Core/ModSettings.cs` |
| Console diagnostics | `Core/DebugCommands.cs` |
| Build / deploy scripts | `scripts/` |

| Load pre-flight diagnostic (catches failures that happen before any module code runs) | `tools/LoadProbe` |

**Acceptance: verified in a live campaign on 2026-09-15.**

| Check | Evidence |
|---|---|
| Module loads | `OnSubModuleLoad complete. Harmony patches applied.` |
| Startup notice shown | `Diplomacy & Intrigue v0.1.0 loaded.` |
| Behaviors registered | `Campaign behaviors registered.` |
| Console commands live | `diplomacy.status` → `healthy: True`, schema v1 |
| War ledger populated | `Backfilled 4 pre-existing war(s)` — the four 1084 starting wars |
| Save → load round-trip | after reload: `Loaded: 0 treaties, 4 war records, schema v1` **and no backfill line**, proving the records came from the save rather than being re-created |

One real bug was found and fixed during this: the module was targeting `net6.0`, which the
game cannot load. See ARCHITECTURE §1.1.

---

## Phase 1 — Diplomacy core

The pillar everything else hangs off. Playable target: *wars end for reasons, and peace can be shaped.*

**1.1 War exhaustion** — accumulates from casualties, lost fiefs, sieges, raided villages, and plain elapsed time; decays in peace. Scaled by `WarExhaustionRate`. Surfaced in UI as a bar per war. This is the engine that stops Calradia's permanent-war problem.

**1.2 Casus belli** — a war is declared *for* something. Legitimacy (0–1, already scaffolded in `Diplomacy/CasusBelli.cs`) scales influence cost, third-party relation damage, and internal opposition. Claims expire; fabricating one costs influence and can be exposed.

**1.3 Treaty engine** — the six types in `Models/TreatyType` become live: non-aggression, truce, defensive pact, alliance, tributary pact, vassalage. Signing, expiry, renewal, breach. Breach carries a lasting trust penalty.

**1.4 Diplomatic trust** — per kingdom-pair memory of honoured and broken agreements. Low trust makes a kingdom unable to find allies, which is the real punishment for treachery.

**1.5 Peace table** — peace stops being a binary. War score plus exhaustion sets what the winner may demand: fief transfer, tribute, prisoner release, white peace. Both AI and player negotiate against the same valuation.

**1.6 Call to arms** — alliances and defensive pacts pull signatories into wars. Refusing is allowed and costs trust.

**1.7 AI diplomacy** — weekly per-kingdom evaluation: seek peace, offer pact, demand tribute, pick a war target. Must feel deliberate, not twitchy.

**1.8 Diplomacy UI** — kingdom screen tab: relations matrix, active treaties, ongoing wars with exhaustion and war score, proposal flow.

**Acceptance:** in a 10-year AI-only campaign, wars average under ~3 years, at least one alliance forms and holds, and no kingdom sits at permanent total war. Save/load stable across the phase.

---

## Phase 2 — Court intrigue

Playable target: *being a king is a political problem, not just a military one.*

**2.1 Grievances** — event-sourced records with decay: fief given to a rival, war the clan opposed, humiliating tribute, a relative left in captivity, forced levies.

**2.2 Vassal loyalty** — derived from grievances, relation, fief wealth, and war exhaustion. Drives defection risk and vote behaviour.

**2.3 Court blocs** — clans coalesce into the agendas in `Models/CourtAgenda` (doves, hawks, autonomists, centralists, pretenders) around a leading clan. Blocs vote as units, which makes kingdom decisions predictable enough to play against.

**2.4 Crown legitimacy** — a per-kingdom pool: illegitimate wars, lost fiefs, broken treaties drain it; victories and just wars restore it. Low legitimacy unlocks pretender bids.

**2.5 Succession crises** — on a ruler's death, competing claims resolve through bloc support rather than a silent assignment.

**2.6 Civil war** — a strong pretender bloc can secede into a rival kingdom, taking its fiefs. The end state of unmanaged internal pressure.

**2.7 Intrigue UI** — court screen: blocs, loyalty, grievance ledger, legitimacy.

**Acceptance:** an AI kingdom that loses a long illegitimate war visibly fractures — blocs shift, then either sues for peace or splits. The player can survive it by managing grievances.

---

## Phase 3 — Espionage

Playable target: *information and subversion are a third way to fight.*

**3.1 Spy networks** — per kingdom-pair strength, built up over time, decayed by enemy counter-intelligence.

**3.2 Missions** — the types in `Models/SpyMissionType`: scout armies, read court, sabotage garrison, spread dissent, bribe a lord, forge letters, steal treasury, assassinate. Resolution from network strength + agent skill (Roguery/Charm) vs target counter-intelligence.

**3.3 Counter-intelligence** — passive defence a kingdom invests in; determines exposure chance.

**3.4 Exposure as diplomacy** — a burned network is a *diplomatic incident*: the victim gains the `EspionageExposed` casus belli and trust drops. This is the cross-pillar hinge that keeps espionage from being a free action.

**3.5 Cross-pillar payoffs** — `BribeLord` feeds Phase 2 defection; `ForgeLetters` manufactures grievances; `ReadCourt` reveals pending kingdom decisions.

**3.6 Espionage UI** — network map, mission board, running operations.

**Acceptance:** a player can flip a border lord through bribery, and a caught operation drags them into a war they did not choose.

---

## Phase 4 — Integration, balance, release

- **Cross-pillar wiring pass** — every hinge above actually connected and logged.
- **Balance pass** — long AI-only campaign runs with telemetry from the log; tune exhaustion, costs, decay rates.
- **Compatibility** — detect BUTR Diplomacy and warn (the two are mutually exclusive by design); document every overridden `GameModel`.
- **Localization** — all strings behind `TextObject` keys in `ModuleData/Languages/EN`; community translations become drop-in.
- **Performance** — daily/weekly tick budget measured; no per-frame work.
- **Release** — Nexus page, changelog, bug-report instructions pointing at the log directory.

---

## Cross-pillar dependency map

```
Casus belli ──────────────┬─► war legitimacy ──► crown legitimacy ──► pretender bids
                          │
War exhaustion ───────────┼─► AI peace-seeking
                          └─► vassal loyalty ──► defection / civil war
Broken treaty ────────────┬─► diplomatic trust ──► who will ally with you
                          └─► BrokenTreaty casus belli
Exposed spy network ──────┬─► EspionageExposed casus belli
                          └─► trust loss
Bribed lord ──────────────► defection during civil war
Forged letters ───────────► manufactured grievance
```

---

## Standing risks

| Risk | Handling |
|---|---|
| Game patch breaks our Harmony patches | Keep patch count minimal; prefer models and events; version-stamp each patch (§3 of ARCHITECTURE) |
| Save-data corruption across mod versions | Frozen ids, `SchemaVersion` + `Migrate()`, orphan sweep on load |
| AI feedback loops (endless war or instant world peace) | Slow evaluation cadence, clamped values, balance pass with log telemetry |
| Scope creep across three pillars | Each phase ships playable on its own; no pillar starts before the previous one meets its acceptance bar |
| Conflicts with other diplomacy mods | Declared incompatible with BUTR Diplomacy; overridden models documented |
