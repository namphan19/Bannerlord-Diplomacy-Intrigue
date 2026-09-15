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

**1.1 War exhaustion** ✅ **implemented** — accrues from elapsed time, battle casualties
(divided by kingdom strength, so it is relative to size), lost towns and castles, raided
villages, sieges endured and enemy-occupied fiefs. War score tracked separately, with a
daily drift toward zero so stalemates trend to a white peace. On peace, half the exhaustion
carries into a per-kingdom weariness pool that decays at 0.15/day.
Code: `Diplomacy/WarExhaustion.cs`, `Diplomacy/DiplomacyConstants.cs`,
`Behaviors/WarExhaustionBehavior.cs`, `Models/KingdomWeariness.cs`.

**1.2 Casus belli** ✅ **implemented** — every war now carries a justification, and
legitimacy (0–1) is the single number the rest of the mod reads. Fief ownership history is
recorded so ancestral claims can exist at all; claims are granted by conquest and by raids,
renewed rather than stacked, and expire. Claim fabrication is implemented end to end
(cost, 30-day timer, 20% exposure with relation damage and a counter-claim); until the 1.8
UI exists its only entry point is `diplomacy.fabricate_claim`.
Code: `Diplomacy/FiefHistory.cs`, `Diplomacy/ClaimRegistry.cs`, `Diplomacy/CasusBelli.cs`,
`Behaviors/ClaimsBehavior.cs`, `Models/Claim.cs`, `Models/FiefOwnershipRecord.cs`,
`Models/FabricationAttempt.cs`.

Verified in a live campaign: war declaration opens a record with correct aggressor/defender
attribution; a fief transfer closes the old ledger row and opens a new one; a fief *gifted*
grants no claim (only `BySiege` does), which is the intended distinction between a grievance
and a transaction; schema migrated v1 → v3 on a pre-existing save without data loss.

**Verified in a live campaign** (1084 sandbox save, reloaded across restarts):

| Check | Result |
|---|---|
| War declaration opens a record | `Vlandia vs Battania` with correct aggressor/defender |
| Daily exhaustion accrual | 30 days → **2.40** on every war, exactly `30 × 0.08` |
| Continued accrual | +5 days → **2.80**, matching `2.40 + 5 × 0.08` |
| Peace closes the war | dropped from the ongoing list; record kept |
| Weariness carry-over | `2.40 × 0.5` → **1.20** each side, logged on the peace |
| Weariness decay | 5 days → **0.45**, matching `1.20 − 5 × 0.15` |
| Fief ledger | transfer closed the old row and opened a new one |
| Gift ≠ grievance | a fief given by kingdom decision granted **no** claim; only `BySiege` does |
| Fabrication guards | refused cleanly when the player holds no kingdom |
| Save round-trip, new schema | 4 war records, 2 weariness entries, 120 fief records, all values identical after a process restart |
| Migration | a schema v1 save migrated to v3 with its war records intact |

Accrual was driven through `diplomacy.tick_days`, which calls the same
`WarExhaustion.DailyTick` the campaign's daily tick calls — a campaign day takes minutes of
real time to pass, which makes rate verification and the Phase 4 balance pass impractical
otherwise.

Still unverified, and honestly so: **battle casualties** and **siege capture** feed
exhaustion and war score through `MapEventEnded` and `OnSettlementOwnerChangedEvent`, and
neither can be triggered from the console — they need a real battle and a real siege. Both
are covered by the Phase 4 long-run task.

**1.3 Treaty engine** ✅ **implemented** — all six types live: signing with per-type
duration and cost, expiry paying a trust dividend, breach, mutual dissolution, tribute
transfers every 7 days with default handling, and an automatic truce recorded whenever a
peace is made. Enforcement is the interesting half: a treaty that forbids war actually
prevents it.
Code: `Diplomacy/TreatyRegistry.cs`, `Diplomacy/TreatyEnforcement.cs`,
`Behaviors/TreatyBehavior.cs`, `Patches/`.

Two decisions worth recording:

- **Vassalage carries military service.** It did not at first, which left it nearly
  identical to a tributary pact - pay and be left alone. A tributary buys peace; a vassal
  buys protection and owes troops in every one of its patron's wars.
- **Blocked on the routine path, defiance on the deliberate one.** A kingdom never wanders
  into a forbidden war because the vanilla decision AI rolled it - `DeclareWarDecision.IsAllowed`
  is narrowed so the proposal never reaches a vote. But breaking a treaty on purpose is
  always available and never blocked; it just costs reputation. Accidents are noise,
  defiance is a story beat.

**1.4 Diplomatic trust** ✅ **implemented** — one value per **ordered** kingdom pair, because
"Vlandia trusts Battania" and the reverse are different facts and diverge sharply after a
betrayal. Trust does not decay: relation already covers feeling that fades, so trust is
reputation that follows a kingdom for the rest of the campaign. Below −20 nobody will sign
anything but a truce, and a truce is never refused - stopping a war has to stay possible
however badly the parties behaved.
Code: `Diplomacy/TrustRegistry.cs`, `Models/TrustRecord.cs`.

**Verified in a live campaign:**

| Check | Result |
|---|---|
| Sign a non-aggression pact | active, expiry two years out |
| War refused while it stands | `campaign.declare_war` printed its own success, but `ApplyByDefault` was refused and logged; no war stance, treaty still active |
| Break it deliberately | allowed; victim trust **−35**, all six other courts **−12**, and the victim gained a `BrokenTreaty` claim at legitimacy 0.95 |
| Trust is directional | only `X → Sturgia` records were created; Sturgia's own view of others untouched |
| War allowed afterwards | yes - defiance has a price, not a lock |
| Trust floor | a new alliance was refused: "Vlandia does not trust Sturgia enough to sign anything but a truce" |
| Vassalage | subordinate recorded explicitly, tribute scheduled; the vassal cannot declare war on anyone, nor on its patron |
| Double subordination | refused - "Battania is already subordinate to another kingdom" |
| Save round-trip | 2 treaties, 7 trust records, 1 claim, 120 fief records after a process restart; the patron/client link still resolved |

Not verified, and honestly so: anything gated on the campaign **clock** - treaty expiry and
its trust dividend, tribute actually changing hands on day 7, the two-year peace dividend.
`diplomacy.tick_days` drives the daily upkeep but cannot move `CampaignTime.Now`, so these
need real campaign days, like the battle and siege paths. All of it is covered by the
Phase 4 long-run task.

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
