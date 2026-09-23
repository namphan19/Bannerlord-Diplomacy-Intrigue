# Status — 2026-09-23

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**.
Save ids in use: `Treaty` 1-17, `TrustRecord` 1-6, `ModState` 1-10, definer class ids to 9
(`KingdomPower`), enums 20-25. Free for Phase 2: class ids **from 10**, `ModState`
properties **from 11**.
Last completed measurement: **balance run 07** — [docs/balance/run-07.md](balance/run-07.md).

## Start here — handoff, 2026-09-23

**Phase 1 is accepted and closed. Phase 2 — court intrigue — is the work now.**

Branch `development`, clean and level with `origin/development`. Everything that was in
flight has landed: PR #3 (run-06 fixes, §12, §13) and PR #4 (Kingdom screen UI), and no
remote branch is unmerged. `main` sits **24 commits behind** `development` and has
deliberately not been moved — cutting a release is Phase 4's job, not a side effect of
closing a pillar.

### What acceptance did and did not mean

The lead accepted Phase 1 on 2026-09-23, on priority grounds. The measured part is real:
run 01 cleared the acceptance bar over 28 in-game years, and **run 07** — 20.8 in-game
years, 100 wars, 0 errors, and the first vassal links this project ever produced — is the
reference measurement.

**The §13 vassalage rework sitting under that acceptance is smoke-tested only**: it builds
clean, passes LoadProbe, and ran 2 in-game years with 0 errors. None of the five questions
in [design/04 §13.7](design/04-hegemony.md#137-what-the-next-run-must-answer) is answered.
Anyone reading "Phase 1 ✅" should read that sentence with it.

The full carried-debt table is in
[ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23). The short version:

- **Balance run 08 is deferred, not cancelled.** It is what closes §13, and it unblocks two
  undecided constants: the strength margin on `IsStrongEnoughToHold` (§13.6) and whether the
  indemnity price should bite (§13.4).
- **Two peace-table surfaces have never been seen working**: the multi-selection checklist
  against a real budget, and the AI→player incoming offer. `save007`'s wars are all war
  score ~0, so only the white-peace short path has rendered on screen.
- The risk run 07 flagged is still unmeasured: **13 of its 18 tributary pacts settled at war
  score ≥ 75**, and the §13 cliff is expected to convert most of those into subjugations.
  That multiplies the imposed-vassalage rate by an unknown factor.

### Phase 2 — where it starts

Spec: [design/02-intrigue.md](design/02-intrigue.md). The order is fixed by that spec's §8
and is a real dependency chain, not a preference: **2.1 grievances → 2.2 loyalty →
2.3 blocs → 2.4 legitimacy → 2.5 succession → 2.6 civil war → 2.7 court UI**.

The shape of the pillar, restated so the first commit does not have to re-derive it:

- A **grievance is event-sourced** — a thing that happened, attached to a (clan → liege)
  pair, with a type, a weight and a date. It is saved. Nothing about it is recomputed from
  world state.
- **Loyalty is derived and not saved** — a function of saved grievances plus live world
  state. That keeps the save small and makes a balance change take effect on existing
  campaigns rather than only on new ones. Same discipline as `Hegemony.IsHegemon`: one
  source of truth, derived where it is read.
- **Crown legitimacy is a saved pool**, 0–100, starting at 60. It is the one genuinely new
  per-kingdom number, and the fabrication hook in `ClaimRegistry` is already computing a
  penalty for it and only logging it.
- **Civil war and succession are outcomes, not systems.** They route through Phase 1
  machinery — a civil war is a war with a war record, exhaustion and a peace table.

What Phase 1 already left waiting, verified present in the code on 2026-09-23:

| Hook | Where |
|---|---|
| `ExhaustionCourtPressure = 40`, the doves threshold | `Diplomacy/DiplomacyConstants.cs:83` |
| A caught fabrication computes its legitimacy penalty and only logs it, "pending the Phase 2 legitimacy pool" | `Diplomacy/ClaimRegistry.cs:283` |
| Policy votes, annexation, clan expulsion and king selection **left alone on purpose** for Phase 2 to extend | `GameModels/ModKingdomDecisionPermissionModel.cs:29` |
| `CourtAgenda`, `SpyMissionType`, `MissionOutcome` enums, registered at definer ids 23–25 since Phase 0 | `Models/Enums.cs`, `Core/ModSaveDefiner.cs` |
| `EnableIntrigue` settings toggle, shipped and defaulting on | `Core/ModSettings.cs:31` |

A claim this handoff removed rather than repeated: the previous version said
`CallToArms.WouldAnswer` carried a note about vassal defiance reading grievances. **It does
not** — there is no such note in that file. The idea is still right and belongs at 2.2; it
was simply never written into the code.

Parked until Phase 2 gives them weight: **vassal-party summons**
([design/04 §8](design/04-hegemony.md)) and **titles** (Emperor, Khagan), which sit on top of
legitimacy at 2.4.

### 2.1 is built and verified live, 2026-09-23

Verified on `save007` (Khuzait, player-led, Okhon of clan Airit is the ruler), through the
GABS bridge, **0 errors and 0 warnings** in the mod log throughout:

| Check | Result |
|---|---|
| New definer entry loads on an existing save | `Loaded: ... 0 grievances, schema v4` on `save007` - no schema bump needed, as designed |
| `UnjustWar` source fires | Khuzait declared war on Aserai at legitimacy 0.00: **all 9 non-ruling Khuzait clans** recorded weight **8.0**, the full ceiling |
| The weight is `(1 - legitimacy) x ceiling` | 8.0 at legitimacy 0.00 matches exactly |
| One resolver, two readers agree | the grievance handler and `CoreBehavior` both logged legitimacy 0.00 for the same war |
| Decay arithmetic | `tick_days 10` took every record 8.0 -> **7.8**, exactly 10 x 0.02 |
| **Save does not crash** | saved as `di_grievance_test` - the test that catches a missing container definition |
| **Round trip across a process restart** | game stopped, restarted, save reloaded: **9 grievances** back with weight **7.8**, type, holder and target all intact |
| Renew, not stack | a second unjust war (on Vlandia) pushed 7.8 back to **8.0** and created **no tenth record** |
| The player's clan is under the same rules | every grievance names **Airit**, the player's own clan, as the target. Design 02 §9.2 holds in practice, not just in the spec |

Not covered by this session: `FiefToRival`, `FiefLostToEnemy`, `HumiliatingTribute` and
`RelativeInCaptivity` are wired but **were not triggered** - they need a fief grant, a siege,
an active tribute and a year-long captivity respectively. `PolicyAgainstAgenda`,
`PeaceWhileWinning` and `RequestRefused` are not wired at all and wait on 2.3.

A correction to CLAUDE.md §2 while testing: the bridge **does** have `core/skip_video`, so the
note that the intro video needs a key sent from outside is out of date.

### 2.2 is built and verified live, 2026-09-23

Verified on `di_grievance_test` (the `save007` world carried forward). **0 errors, 0
warnings.** Every figure below was predicted by hand first and then read off the game:

| Check | Result |
|---|---|
| Grievance term | grievance weight 7.8 x 1.5 = **-11.7**, exact |
| War term | worst exhaustion 3.0 x 0.2 = **-0.6**, exact |
| Whole sum | Harfit: 50 - 0.5 - 11.7 - 6.7 - 0.6 = **30.5**, exact |
| The chain moves together | `tick_days 100`: grievance decayed to 5.8 (**-8.7**), exhaustion rose to 33.0 (**-6.6**), predicted total **27.5** and the game printed 27.5 |
| All four bands reachable | Sturgia, which holds no grievances, spans **16.4 (defection risk) to 75.4 (reliable)** |
| The ruling clan is excluded | Airit, the player's clan, does not appear in its own court's list |

**Two balance signals, recorded not acted on** - every constant is still marked UN-TUNED and
tuning needs a real run:

- **One maximum-weight unjust war moves an entire court a band.** Khuzait's nine clans sat
  around 44 (transactional) before; one war at legitimacy 0.00 put **all nine** at 30-38,
  disaffected. `LoyaltyGrievanceFactor` 1.5 is the most load-bearing number in the pillar.
- **War exhaustion outruns grievance decay.** Over 100 days the grievance term recovered 3.0
  points while the war term lost 6.0, so loyalty fell *despite* the court forgetting. At
  exhaustion 100 the war term alone is -20 across every clan in the realm. Whether that is
  right is a design question for the lead; it is certainly potent.
- `Kuloving` of Sturgia is a **defection risk at campaign start** on vanilla relation alone
  (-50 relation, short of land). Not caused by this mod, but design 07 should know that a
  day-one defection candidate already exists.

A correction made while testing: mid-session I predicted a loyalty figure using
`ExhaustionPerDayAtWar = 0.08`. The real rate is **0.3/day**; 0.08 is a stale Phase 1 figure
that survives in ROADMAP's early narrative. The prediction was re-derived and then matched.

### What to do next

1. ~~**2.1 - the grievance ledger.**~~ **Done and verified above.** Originally: A new savable type at class id **10**, with its container
   definition added to `ModSaveDefiner` in the same commit (a missing container definition
   crashes on save), a `ModState` list at property **11**, the eight sources in
   [design/02 §1](design/02-intrigue.md), the −0.02/day decay, and a `diplomacy.grievances`
   diagnostic. No schema bump: a new list that defaults empty does not change the meaning of
   existing data.
2. **2.2 — loyalty**, derived on the weekly tick, plus the `AiDiplomacy.TryDemandTribute`
   revisit — it accepts on strength ratio and trust alone, with no sense of the target
   court's willingness.
3. **2.3–2.4** in order. 2.4 is where the `ClaimRegistry` hook stops logging and starts
   paying, and where the `Hold` formula gets its legitimacy term
   ([design/04 §1.2](design/04-hegemony.md)).
4. **Fold run 08 in** once Phase 2 work produces a campaign long enough to carry it. Same
   deployment, same telemetry; what it needs is in-game years, which Phase 2 testing
   generates anyway.

### Design decisions taken before any Phase 2 code, [design/02 §9](design/02-intrigue.md)

| # | Question | Decision, 2026-09-23 |
|---|---|---|
| 1 | Does the player see *rival* kingdoms' internal politics without espionage? | **A band for rivals, the full ledger for your own court.** Exact rival figures are what Phase 3 `ReadCourt` sells. Same fork the lead already took for enemy war exhaustion |
| 2 | Is the player's own clan subject to this when serving another king? | **Yes, on the same terms as any AI clan.** No "is this the player" argument in the ledger or the loyalty calculation; where the experience must differ, that lives in the UI layer. Costs extra work at 2.2 and 2.7, taken deliberately |
| 3 | Kingdom decisions: extend `KingdomDecision` or replace it? | **Extend.** The spec recommends it and `ModKingdomDecisionPermissionModel` was already written on that assumption |
| 4 | Civil-war trigger thresholds ([design/02 §6](design/02-intrigue.md)) | **Still open** — guesses by admission, deferred to a long AI-only run. Nothing in 2.1–2.5 is blocked by it |

### Saves

| Save | State |
|---|---|
| `di_fresh_1084` | **Summer 1, 1084, pristine start, hero parked in Myzea.** The run-08 baseline |
| `save007` | Khuzait, player-led — the save the Kingdom screen UI was verified on |
| `di_run07_1104` | Winter 1104, end of run 07: 7 kingdoms, 2 hegemons |
| `di_hegemony_1166` | Vlandia with 2 vassals. The only state holding a sphere built at the peace table |
| `di_review_0919_b` | Winter 15, 1162 — the old evolved world, pre-§12 |
| `di_run06_resume`, `di_review_0919` | run 06 checkpoints |

Never save over `di_phase1_full`.

### Traps from the 2026-09-20 session that are not in CLAUDE.md §1

- **Parking the hero is not optional.** Crossing the map to a town, the party was stopped by
  bandits **twice**; each halts the clock until something clears it.
- Two diagnostics were lying and are fixed: `diplomacy.submission_value` called `CanSign`
  without `settlesWar` (so it reported the entire attacker route as impossible), and
  `diplomacy.offer_peace` had no term for the dissolution rung.

The §12/§13 design detail that used to fill this section lives in
[design/04 §12–§13](design/04-hegemony.md); the run-07 measurement is in
[balance/run-07.md](balance/run-07.md). The corrections made on 2026-09-20 — the
`SpeedUpMultiplier` lever, the war-score bleed figure — were folded into CLAUDE.md §1 and
are not repeated here.
---

---

## Intermittent: the game sometimes dies on startup from the official launcher

Reported by the lead as "game không thể chạy được crash ngay khi mở", with the engine's
"we need to collect necessary files" dialog. **Not root-caused yet.** What is established:

| Launch path | Result on 2026-09-15 |
|---|---|
| Official launcher → Play | **5 died** before the main menu, 14-21s in — **and 2 later launches worked**, one of them balance run 03: 25 minutes, 6.2 in-game years, zero errors |
| `Bannerlord.exe` directly, launcher's own mod list | reached the main menu |
| `Bannerlord.BLSE.Standalone.exe` (GABS, `scripts/play.ps1`) | reached the menu, loaded `di_phase1_full`, 0 errors |

So it is **intermittent on that path, not deterministic**, which also means an earlier note
here claiming the launcher path was simply broken was too strong.

Evidence for the fault itself, from the Windows `CLR20r3` record (P4/P7/P8 resolved with
Cecil): an unhandled managed exception in `TaleWorlds.MountAndBlade`, method
`ActionIndexCache..ctor` → `MBAnimation.GetActionCodeWithName`, IL offset 8 — the instruction
that reads the static `MBAPI.IMBAnimation`. The only caller of that method in the whole game
is `ActionIndexCache..ctor`, so something constructed an action-index cache while the native
animation API was still null. Nothing in this mod touches animations, and it ships no XML and
no assets.

Every failing run's mod log stops at `OnSubModuleLoad complete` with no `Notify` line, so the
module loaded and the game died before the main-menu screen.

**Not yet answered: is this mod implicated at all?** The decisive test is one launcher run
with the mod unticked, which needs a human to press Play. Since the same path has now carried
a 25-minute session with zero errors, the honest statement is that it fails intermittently and
nothing yet points at the module.

Two things were added because of this, independent of the cause:

- `SubModule.InstallCrashLogging` — an `AppDomain.UnhandledException` handler that writes the
  exception and its stack into the mod log. This crash produced an 86 MB minidump and a
  method token and nothing else; the next one will produce a stack trace.
- `OnSubModuleLoad` logs `host=<process> modules=[…]`, because the official launcher runs the
  game inside its own process and the logs could not tell the two paths apart.

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.12)** | ✅ **accepted by the lead, 2026-09-23**. Code complete including submission and hegemony (1.9/1.10), the vanilla takeover (1.11) and power (1.12). Measured over runs 01–07; the §13 rework under it is smoke-tested only, and the carried debt is listed in [ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23) |
| **2 — Court intrigue** | 🔄 **started 2026-09-23**. Spec written and reviewed, no code yet; 2.1 grievances is the first deliverable |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | 🔄 runs 01-07 archived. **Run 07** (2026-09-20) is the current reference — [balance/run-07.md](balance/run-07.md). **Run 08 is owed** and closes the §13 questions |

### Kingdom screen UI — built and verified live, 2026-09-21

The mockup pass over the native Kingdom screen shipped and was verified on `save007`
(Khuzait, player-led) — screenshots, not just build success:

- **Realm tab** (6th header tab): standing strip, wars with exhaustion/score and a
  per-war *Peace table* button, vassals, spheres, claims, agreements, *Write a report*.
  Switching to and from vanilla tabs verified — the panel coordinates visibility with
  the five `Show` flags instead of the tab control, which only knows five pairs.
- **Diplomacy rows**: war rows carry exhaustion band + score (`Fresh +0`); truce rows
  carry the relationship summary (`independent`, `our vassal`, `answers to X`, tribute).
- **Headline** under the banners: casus belli + legitimacy + enemy band meaning for
  wars, standing agreements for peace. Wars recorded without a claim show
  *"no claim on record"*, not `over None`.
- **"What their court would sign"** chooser on truce items: the court's real
  `PactValue` against all three rungs with verdicts, influence costs, and per-rung
  Propose buttons — the same numbers and path the AI uses.
- **Bottom action strip**: vanilla's proposal row no longer overlaps ours. The
  `{Actions}` ListPanel could not be hidden by an `IsVisible` binding — bindings on it
  resolve against the Actions list, not the panel VM — so its `DataSource` is repointed
  at `DiVanillaActions`, which serves the real list when the mod is off and an empty
  list when it runs. Pact buttons were removed from the strip; the chooser owns them.
- **Peace table** opens a native inquiry from both surfaces. `save007`'s wars are all
  score ~0, so only the white-peace short path is verified on screen; the
  multi-selection checklist with a real budget and the AI→player incoming-offer
  inquiry remain **unverified in a live game** — they need a war that earned terms.

Still loose: the Realm tab widens the centered tab strip enough to touch the leader
portrait's caption — cosmetic only. Unverified above.

### Phase 1, feature by feature

All of this exists, builds, and was exercised in a live campaign. `docs/ROADMAP.md` carries
the per-feature evidence tables.

| | Feature | Code |
|---|---|---|
| 1.1 | War exhaustion, war score, weariness | `Diplomacy/WarExhaustion.cs` |
| 1.2 | Casus belli, fief ledger, claims, fabrication | `Diplomacy/ClaimRegistry.cs`, `FiefHistory.cs`, `CasusBelli.cs` |
| 1.3 | Treaty engine, six types, tribute, enforcement | `Diplomacy/TreatyRegistry.cs`, `TreatyEnforcement.cs`, `Patches/` |
| 1.4 | Diplomatic trust, directional and non-decaying | `Diplomacy/TrustRegistry.cs` |
| 1.5 | Peace table on a war-score budget | `Diplomacy/PeaceTable.cs` |
| 1.6 | Call to arms, refusal, follower release | `Diplomacy/CallToArms.cs` |
| 1.7 | Weekly AI evaluation, one action per kingdom | `Diplomacy/AiDiplomacy.cs` |
| 1.8 | Ctrl+D diplomacy menu, exhaustion bands | `UI/DiplomacyMenu.cs`, `Diplomacy/ExhaustionBands.cs` |
| 1.9 | Submission, Hold, defiance, revolt, collapse | `Diplomacy/Hegemony.cs` |
| 1.10 | Rival poaching, cascade cap, hegemony UI | `Diplomacy/Hegemony.cs`, `Diplomacy/CallToArms.cs`, `UI/DiplomacyMenu.cs` |
| 1.11 | Inter-kingdom diplomacy taken from vanilla | `GameModels/` (four models), `Diplomacy/VanillaDiplomacy.cs` |
| 1.12 | Power: ambition, coalitions, greed, annexation, elimination | `Diplomacy/Power.cs`, `CallToArms.ExpectedSupport`, `Hegemony` — [design/06](design/06-power.md) |

---

## Phase 1 acceptance: met, measured over 28 in-game years

Balance run 01 is in: **340 weekly snapshots, 247 wars, 0 errors**, Summer 1084 to Autumn
1112. Full analysis in [docs/balance/run-01.md](balance/run-01.md).

| Criterion | Result |
|---|---|
| Wars average under ~3 years | 18.4 days mean — passes hugely |
| An alliance forms and holds | up to 8 at once, present 85% of weeks |
| No permanent total war | every kingdom at war in 0.9% of weeks |
| Stability | 28 years, zero errors, zero tribute defaults |

**The criteria are met and the run still found a real problem: wars are ~10× shorter than
the design intends.** A chosen war reaches exhaustion 51.6 in 23.6 days, i.e. 2.19 per day
of which elapsed time is 0.08 — casualties were doing ~96% of the work, about 30× what the
design assumed.

Two constants were tuned from that data and **both need a second run to confirm**:

- `ExhaustionCasualtyStrengthDivisor` 100 → **20** (casualty exhaustion 5× weaker)
- `AncestralClaimMemoryYears` 20 → **12** (live claims had settled at 83–93, so everyone
  held a claim on everyone and `Conquest` was never needed)

## War initiation taken over from vanilla

Decided by the lead after run 01 showed our evaluation declaring 25 wars against vanilla's
~220: everything the mod knows about a war — exhaustion, weariness, claims, trust, the
influence a casus belli costs — had no say in whether wars happened. Casus belli was a label
applied afterwards rather than a gate.

**What changed**

- `DeclareWarDecision.IsAllowed` now refuses **AI-proposed** wars between kingdoms outright.
  A decision proposed by the **player's own clan** still goes through, so the Kingdom
  screen's declare-war option is untouched. A treaty forbidding the war still stops
  everyone, player included.
- `DeclareWarAction.ApplyByKingdomDecision` refuses anything unsanctioned as a backstop.
  Our evaluation wraps its own call in `TreatyEnforcement.BeginSanctionedWar()`.
- Engine paths are deliberately untouched: rebellion, claim on throne, player hostility,
  crime rating, kingdom creation, call to arms.

**And a design fault it exposed: weariness saturated.**

Measured in the mature world at the end of run 01, every kingdom sat at **69–96 weariness**
against a cap of 45, which alone inflicted up to −40 on every war valuation. The arithmetic
was never going to work: each war end injected up to +30 while decay removed a flat
0.15/day, so across 247 wars the pool pinned itself near the ceiling. Weariness was meant to
say "you just fought a long war, wait" and instead said "you have ever fought a war".

Three fixes:

| Fault | Fix |
|---|---|
| A flat drain cannot bound an accumulating pool | Decay is now **proportional**, 2% of the remaining pool per day. Self-limiting: 1.6/day at 80, 0.4/day at 20 |
| Obligation wars injected weariness for wars nobody chose | They carry none at all |
| Weariness both gated **and** penalised at −0.5/point | Gate kept; the value penalty drops to 0.15 |

Verified immediately: weariness drained **91 → 8.1 over 120 days**, and the next four weeks
of evaluation produced **3 war declarations** across eight kingdoms with values of 34–48
against a threshold of 18, at 55–75 influence each. Under the old numbers the same world
produced 64 consecutive "do nothing" decisions.

**Aggression retuned to carry the whole load** — our evaluation is now the only source of
wars between kingdoms, so it has to do what vanilla was doing:

| Constant | Was | Now | Why |
|---|---|---|---|
| `WarDeclarationBaseInfluence` | 100 | **40** | A war cost 180–240 while ruling clans held 170–230 — about one war ever, against vanilla's free ones |
| `AiWarThreshold` | 25 | **18** | Carrying 8× the load needs a far lower bar |
| `AiWarStrengthRatio` | 1.2 | **1.0** | Eight kingdoms sit within 6,000–7,100 strength; the best ratio anyone could find was 1.08. Attacking an equal is now allowed, and the valuation still punishes attacking upward |
| `AiMaxWearinessToExpand` | 30 | **45** | A war ending at exhaustion 60 carries 30, so the old cap blocked a kingdom after *every* war |
| `AiNonAggressionThreshold` | 20 | **35** | At 20 a pact was worth signing with anyone not actively disliked, and eight kingdoms pacted themselves into a locked map |
| `AiDefensivePactThreshold` | 45 | **55** | Same reason; alliance stays at 70 |

Priority order also changed: **peace → war → tribute → pact**, where it used to put pacts
before war. With vanilla no longer starting wars, an evaluation that prefers a cheap pact
whenever one is available signs the map into permanent peace — which is exactly what run 01
produced.

## What balance run 02 has to answer

1. **Did the takeover take effect?** `[SNAPSHOT]` now carries `vanillaWarsRefused=`,
   cumulative for the session. It should climb steadily; if it stays at zero, the
   decision-level patch is not being reached and the whole change is inert.
2. **Is the war rate sane?** Four simulated weeks gave 3 declarations, which extrapolates
   absurdly — the clock was frozen, so treaties never expired and the burst followed
   weariness clearing all at once. The real rate is unknown. Run 01's ~8.8 wars/year is the
   reference point.
3. **Do chosen wars now last 100–200 days?** `ExhaustionCasualtyStrengthDivisor` 100 → 20.
4. **Does the concession ladder ever fire?** `terms=` on `[WAR-ENDED]` answers it directly
   now. It fired zero times in 28 years.
5. **Do live claims settle nearer 30–40?** `AncestralClaimMemoryYears` 20 → 12.
6. **Does anyone get eliminated?** Run 01 kept all 8 kingdoms. With more war and land
   actually changing hands, that may no longer hold — and a kingdom being destroyed is fine,
   the map collapsing to two is not.

## History — how each run changed the design

Kept because the reasoning is load-bearing: several constants only make sense next to the run
that produced them. **For what to do now, read the handoff at the top of this file.**

### Before run 04: the unbounded term in the war valuation, capped

Run 03 recorded one war declaration at **value 199** against a threshold of 18 and put it down
to `LandHunger` blowing up. That was wrong, and the arithmetic says so: `LandHunger` is clamped
to 1 and so contributes at most 35, and with `Conquest` legitimacy 0.20 every bounded term
together reaches at most 61. At least 138 of the 199 came from `(ratio − 1) × 40` — the one
term in the valuation with no ceiling — which puts Vlandia at 4.45× Northern Empire.

What that costs is not war frequency, since the threshold is only a floor. It is **which target
gets picked**: an unbounded term makes "whoever is weakest" outrank claims, borders and land
hunger together, which is the shape run 03 saw — four of Vlandia's five declarations were
`Conquest` at legitimacy 0.20, wars it then did not fight.

| Change | Where |
|---|---|
| `WarValueMaxStrengthAdvantage = 1` — the term caps at twice our own strength, 40 points | `DiplomacyConstants.cs` |
| One resolver: `AiDiplomacy.EvaluateWar` returns every term, and `TryDeclareWar` and `ExplainWarValue` both read it. They were two hand-written copies of the same formula | `AiDiplomacy.cs` |
| A comment claiming the valuation "punishes attacking upward, the strength term goes negative below parity" — it cannot, the `ratio >= 1.0` gate skips those targets before the valuation runs | `DiplomacyConstants.cs` |

**Verified live** on `di_phase1_full` (Winter 3, 1131), zero errors in the log:

```
Vlandia considering war on Battania
  strength ratio: 2.44 (must be >= 1.00)
  value from strength advantage: 40.0   (capped at ratio 2.00)
  ...
  total: 62.9 x aggressiveness 1.00 = 62.9 (needs 18)
```

Uncapped that term would have been 57.6 and the total 80.5. A pair below the cap
(Vlandia → Aserai, ratio 1.69) prints 27.4 with no cap note, and a pair below parity
(Vlandia → Khuzait, 0.98) is still stopped by the gate rather than by its value.

**The cap itself is un-tuned** — chosen so a decisive advantage weighs about as much as a good
claim across a shared border, not measured. Run 04 is the first data on it.

### 1. What run 04 found, in the order it matters

Full report: [docs/balance/run-04.md](balance/run-04.md). Everything below is measured, not
inferred; nothing below has been decided or changed yet.

| # | Finding | Evidence |
|---|---|---|
| 1 | **Submission is automatic, not political.** 9 submissions, values **55.5–166.9 against a threshold of 55**, `threat` at its +140 ceiling in three of them. Four kingdoms knelt on the first weekly tick after load | `submitted to` lines in `run-04.log` |
| 2 | **The map collapsed to one sphere.** Northern Empire ends holding **all seven** other kingdoms. All 8 alive, none eliminated — but the run's peace is subjugation, not diplomacy | `hegemons=1 vassalLinks=7` |
| 3 | **Poaching is a metronome.** Battania changed patron 4× in 2.5 years at values 151/151/159/158. Below Hold 40 a vassal is always on the market, and average Hold was 30 | 5 poaches |
| 4 | **A revolt can be silently refused.** `Hegemony.Revolt` breaks only the vassalage, so a `DefensivePact` with the same patron vetoed the war of independence — and `Revolt` logged the war anyway. 1 of this run's 2 revolts is recorded wrongly | 11:12:52 in `run-04.log` |
| 5 | **Nothing ends a hegemony.** Both revolts were back under the same patron inside the run, one after 20 seconds. Hold settles at 15–36 and drifts *up* | `avgHold=` series |
| 6 | **Tribute barely arrives** — 193 withheld payments, and a successful payment is not logged at all, so the ratio is unknown | add the missing log line |
| 7 | **Wars are shorter than intended.** Wars begun and ended inside the run: median **63 days** against the design target of 150–200. The acceptance bar passes either way | 47–202 day spread |

The two fixes carried into this run both did their job, verified against the mechanism:
`ExhaustionPerDayAtWar` 0.30 closed all five of run 03's 400-day wars at exhaustion 58–62, and
the war-value cap pulled declarations into 18–81 from run 03's 18–199, with the three highest
now carrying a claim at legitimacy 0.70 rather than naked opportunism.

Two constants remain deliberately unapplied and both look less urgent after this run:
`ExhaustionSeekPeace` 60 → 70, and `AiAllianceThreshold` 70 → 82 / `AiDefensivePactThreshold`
55 → 65 — the alliance web has behaved for two runs running.

### 2. Applied after run 04, on the lead's decision — all three unverified in a campaign

Committed and deployed the same day the run finished. None of this has been measured yet;
run 05 is the measurement.

**The revolt bug, fixed.** `Hegemony.TryRevolt` now repudiates *every* live war-forbidding
treaty the vassal holds with its patron, not just the vassalage, through a new
`TreatyRegistry.RepudiateAlongside` — which closes them as `Broken` so the save record stays
honest, but charges nothing, because the headline breach has already been paid for in trust
and in the casus belli it hands over. And the revolt now reads the war back off the world
before it claims one: if the declaration is refused anyway, the renunciation still stands and
the log says so instead of asserting a war that does not exist.

**The submission threat term, capped.** `SubmissionThreatWeight` 70 → **25**, so the term tops
out at 50 against a threshold of 55 and being surrounded is never by itself enough — the same
rule the war valuation's strength term was capped to: *no single term may clear the threshold
alone*.

*Verified live* on the run-04 world: Battania's threat ratio of 1.376 now contributes **34.4**
where it would have contributed 96.3, and the verdict flips from a comfortable submission at
~107 to **45.1, would not submit**. That is the same input scored both ways, not a rerun.

**Poaching now means war.** The lead's call: taking a rival patron's vassal is a serious act,
so it costs relation as well as trust and, by default, puts the two hegemons at war — and
their spheres follow them in through the ordinary call to arms. `PoachingRelationLoss` = 15
between the two rulers, on top of the existing −30 trust and the casus belli. The suitor now
has to clear the same restraint any other war does (`AiDiplomacy.CanTakeOnAnotherWar`, one
resolver shared with `TryDeclareWar`), and a treaty forbidding war with the patron forbids
taking its vassal too — otherwise poaching would be the back door around it.

**Not verified in game, and it cannot be from a tool call.** A poach needs two hegemons and a
neglected vassal whose submission value clears the bar; a revolt needs Hold under 15 sustained
for 30 days. Both are states the frozen campaign clock cannot produce, and a fabricated
scenario that fails to fire would say nothing. Run 05 will show them: watch for
`took ... as a vassal from ... and went to war with it` and for the two-branch revolt line.

**The `deploy.ps1` guard, widened.** It matched `Bannerlord*` only, which does not match a
game hosted by the official launcher (`TaleWorlds.MountAndBlade.Launcher`). It saw nothing at
all during run 04 and would have overwritten the DLL underneath a 30-minute session.

**`di_phase1_full` is no longer run 03's end state.** Run 04 saved over it at 11:15, so the
save now holds the run-04 world: one hegemon, seven vassals, Hold 15.7–44.9. Starting run 05
there measures whether a *saturated* hegemony comes apart, which is a fair question but not
the same one as whether it forms too easily — that needs an earlier save.

**Measured since, in run 05** — the lead's own session from the launcher, archived in commit
`29f8abc` as [run-05.log](balance/run-05.log) but not written up here until now. 52 snapshots,
~4.3 in-game years, zero errors, starting from a save of the lead's own (Spring 1137, one
vassal link) rather than `di_phase1_full`. It measured the §2 changes:

- **The submission cap held.** Three submissions against run 04's nine, at 64.5, 61.9 and 63.8
  against the bar of 55, with `threat` at +50.0, +50.0 and +41.0 — never above its new ceiling.
- **`avgHold` ended at 52.3**, from 26.7 at the start, but the set of links changed underneath
  it (1 → 4 → 3) and it sat at 23.1 halfway, so this is not the same links recovering.
  Tribute was withheld **125** times.
- **No poach and no revolt happened**, so neither of the paths §2 changed was exercised. The
  "watch for" lines above are still unanswered.
- Wars: 13 ended, 11 through the peace table, median 76 days, no week with every kingdom at
  war.

### 3. The design review of run 04: the hegemony's structure, fixed — unverified in a campaign

Branch `feature/hegemony-structural-fixes`, 2026-09-16. The three changes in §2 each capped a
number. A game-theory review of run 04 concluded that the collapse to one sphere was mostly
**structural** rather than numerical: a bargain in which one side's duty did not exist in code,
and no counterweight anywhere above the level of a single link. What changed, in the order the
review ranked it:

| # | Defect found by reading the code | Change | Where |
|---|---|---|---|
| 1 | **Protection was measured but never provided.** `CallToArms.Applies` refused every call from a vassal to its patron, and no AI code ever joined a vassal's war — so the Hold term `protection` could only ever read 0 or −20. The bargain had no enforceable patron side, and a sullen vassal was the rational equilibrium | A patron is called when its vassal is **attacked** (never into a war the vassal started), judged by the ally rules including the trust floor, and is called at signing into the wars its new vassal was already defending. A patron's refusal costs trust and Hold, never a mark | `CallToArms.cs` (`Applies`, `DefendNewVassal`), `Hegemony.Submit` |
| 1b | `Protection` counted wars the patron could never be called into | Counts only wars where the vassal is the defender **and** no treaty stops the patron joining — the same rule as `Applies`. Found live: Battania showed −20 for a war with Aserai, a fellow vassal of the same patron | `Hegemony.Protection` |
| 3 | **Submission never read the patron's strength.** Threat and pride are identical for every candidate patron, so the choice came down to reach, trust and culture — a cornered kingdom knelt to its nearest same-culture neighbour even when weaker | A patron no stronger than the candidate scores 0. The threat term is scaled by *cover*: the share of the attackers the patron may fight × how much of them it could match | `Hegemony.SubmissionValue` |
| 5 | **Withholding tribute cost nothing** — no mark, no trust, no lever for the patron. Run 04: 193 withheld | Withholding earns a defiance mark, at most one per 28 days (`TributeWithheldMarkIntervalDays`). A vassal that keeps it up reaches two marks in about a month: no renewal, and its next refused summons breaks the link | `TreatyRegistry.PayDueTribute` |
| 2a | **Nothing balanced against a rising sphere.** Every pact term reads the present | `PactValue` gains a balancing term: how far the strongest sphere neither party belongs to outweighs the two of them, weight 40 | `AiDiplomacy.BalancingPull`, `PactWeightBalancing` |
| 2b | **Revolt was a lone act** against the patron plus half its other vassals, which is why run 04's rebels knelt again | When one vassal revolts, siblings under Hold 25 after the contagion rise with it. All rebels renounce before anyone declares, so the patron's call reaches only the loyal | `Hegemony.TryRevolt`, `RevoltJoinBelowHold` |
| 8 | **`TryPoach` broke the old link before knowing the new one could be signed** — and the break's own −12 observer trust could be what made the signing fail, leaving the vassal free and nobody's | Checks `CanSign` first, with the old link set aside (`replacing:`). The old link now closes without charging the client: the poacher pays (trust, relation, casus belli, war), not both parties | `Hegemony.TryPoach`, `TreatyRegistry.CanSign` |
| — | `HoldAfterFailedRevolt` was a constant nothing read | A vassalage imposed on a kingdom that walked out on the same winner inside two years starts at 20 | `Hegemony.StartingHoldWhenImposed`, `PeaceTable.ImposeSubmission` |
| — | Run 04 could not say whether tribute ever arrived | `[SNAPSHOT]` carries `tributePaid=` and `tributeWithheld=`, cumulative per session | `Telemetry.cs` |

No save data changed: every new behaviour reads existing fields.

**Verified live on `di_phase1_full`, zero errors in the log.** Driven from the run-04 world
through `diplomacy.break_treaty` and `campaign.declare_war`, never saved:

```
Southern Empire freed, declares on Khuzait (NE trusts Khuzait 97):
  (CallToArms) Northern Empire answered Khuzait and declared war on Southern Empire.
  (Core) War opened: Northern Empire -> Southern Empire (CausedByCallToWarAgreement => DefendAlly, legitimacy 1.00)
  Khuzait   protection +20.0 ... => 49.8        (was +0.0 => 28.9)

Western Empire freed, declares on Battania (NE trusts Battania -9):
  (CallToArms) Northern Empire refused Battania - does not trust Battania (-9.0).
  Battania  protection -20.0  trust +12.8 ... => 17.5

Battania on load, at war only with Aserai (a fellow vassal):
  protection +0.0 => 39.7                        (was -20.0 => 19.7 before fix 1b)

diplomacy.submission_value Battania | Northern Empire
  threat +0.0 (cover 0.00) ...                   (its attacker is NE's own vassal)
diplomacy.submission_value Southern Empire | Northern Empire
  Northern Empire is no stronger than Southern Empire and has no protection to offer => 0.0
diplomacy.pact_value Western Empire | Southern Empire
  Balancing pull, included above: 40.0 against Northern Empire's sphere (106389 strength)
```

A one-week `diplomacy.ai_week` on that world ran clean: Western Empire signed an alliance with
Aserai and a non-aggression pact with Khuzait, both defiant vassals of NE. **That is not
evidence for the balancing term** — `pact_value` shows it at 0 for both pairs, because the
partner belongs to NE's sphere; shared threat carried them.

**Not verified, and a tool call cannot verify it:** the joint revolt (needs Hold under 15 for
30 days of real clock), the tribute marks (no payment fell due on the frozen date), the lower
Hold on a re-imposed vassalage, a poach going through `CanSign(replacing:)`, and
`DefendNewVassal` at the moment of submission — the run-04 world has no free kingdom that
would kneel. The player's patron prompt text was built, not seen.

**What run 06 should watch**, beyond §2's list: `answered <vassal> and declared war` and
`leaves its vassal ... to fight alone` lines, whether `avgHold` still settles in the 15–36
band, `tributePaid` against `tributeWithheld`, and whether a sphere ever loses more than one
vassal at a time (`other vassal(s) rose with it`). The balancing weight and the join threshold
are both un-tuned.

**Deliberately left for after run 06**, so it can be measured against these changes rather
than confounded with them — the other findings of the same review:

- **Trust behaves as a grim trigger.** Non-decaying, broadcast to every observer on a breach,
  repaid only bilaterally — and several breaches are forced by the system (revolt at Hold 15,
  two automatic refusals below 40, tribute default). Two breaches put a kingdom below the pact
  floor with every court, with no route back.
- **Alliances do not deter.** `EvaluateWar` never reads the target's allies or patron.
- **The weariness gate almost never binds.** A war ending at exhaustion 60–70 carries 30–35,
  under the gate of 45.
- **Vassals of one patron can be at war with each other** (Aserai / Battania in this save,
  both NE's), and the patron has no way to impose peace between them.
- **The same save holds a hegemon paying tribute to its own vassal**:
  `TributaryPact(Khuzait / Northern Empire)`, 500 from NE, beside `Vassalage(NE / Khuzait)`.
  Left over from an earlier peace; nothing stops the two coexisting.

### 3b. Strength, read where it decides — unverified in a campaign

Raised by the lead, who found reading the save by hand that the hegemon of the whole map was
nearly its weakest kingdom. `diplomacy.strength` (new) on `di_phase1_full`:

```
rank  kingdom            strength   share  fiefs  sphere
   1  Khuzait               28329   19.9%    31  vassal of Northern Empire (balance vs patron +1.00)
   2  Vlandia               20486   14.4%    23  vassal of Northern Empire (balance vs patron +0.66)
   3  Southern Empire       18487   13.0%    12  vassal of Northern Empire (balance vs patron +0.51)
   4  Western Empire        17254   12.1%    13  vassal of Northern Empire (balance vs patron +0.42)
   5  Aserai                16572   11.7%    13  vassal of Northern Empire (balance vs patron +0.36)
   6  Sturgia               16016   11.3%    14  vassal of Northern Empire (balance vs patron +0.31)
   7  Northern Empire       12938    9.1%     7  hegemon, 7 vassal(s), sphere 142130
   8  Battania              12047    8.5%     7  vassal of Northern Empire (balance vs patron -0.10)
```

The strongest kingdom on the map, at 2.2× its patron's strength and 4.4× its fiefs, was a
vassal. **How it happened, from run-04.log:** every one of Northern Empire's links came by
voluntary submission or by poaching, never through the peace table, and neither route read
the patron's strength. Khuzait knelt at 55.5 with `pride -50.0` — the valuation knew it was
the strongest kingdom and let it kneel anyway, because pride compares against the strongest
kingdom overall, not against the patron. §3 already closed that route; this section is what
still let strength fail to matter once a link existed.

Strength was already read in ten places before this branch (war gate and value, pact aggression, tribute demand, land hunger, the hopeless-call check, casualty exhaustion, Hold fear, rival pull, submission threat and pride). The problems were in **how** it was read:

| Where | Defect | Change |
|---|---|---|
| Hold's `fear` term | `ratio − 1`, clamped ±1: twice the vassal's strength scored +25 but half of it only −12.5, and −25 needed a patron with no army. Northern Empire lost at most 13.6 to any vassal for being weaker than six of them | `Hegemony.PowerBalance` — log2 of the ratio, clamped ±1, so the scale is symmetric. One helper for the hegemony system's two-sided comparisons |
| Revolt | Read resentment only. A vassal a fifth of its patron's size revolted at the same Hold as one twice its size: the weak marched to certain defeat, the strong sat under a patron they could have thrown off | The revolt line moves with strength: `15 + 15 × PowerBalance(vassal, patron)`, clamped 0..30. Twice the patron: revolts below 30. Half: never alone — its link still lapses at term, and it can rise with a stronger sibling. `SecessionCapabilityWeight`, un-tuned |
| Peace table | Vassalage could be imposed by a winner weaker than the loser — the one route into vassalage that still asked nothing | `IsDemandable` refuses it; `DescribeAllowance` says why. All three routes now ask `Hegemony.IsStrongEnoughToHold` |
| `Treaty.SetHold` | A Hold stored as exactly 0 read back as the load default of 40 (`HoldOf` treats 0 as "unset"), drifted to 0 and read 40 again — a sawtooth, reachable because the target can be 0 | Set Hold floors at 0.1 |

The same world, before and after, nothing else changed:

| Vassal | fear before → after | Hold target before → after | revolts below |
|---|---|---|---|
| Khuzait (2.19× NE) | −13.6 → **−25.0** | 28.9 → **17.5** | **30.0** — at 28.9, now counting down |
| Vlandia | −9.2 → −16.6 | 33.6 → 26.2 | 24.9 |
| Southern Empire | −7.5 → −12.9 | 32.8 → 27.5 | 22.7 |
| Western Empire | −6.3 → −10.4 | 44.9 → 40.8 | 21.2 |
| Aserai | −5.5 → −8.9 | 35.7 → 32.2 | 20.4 — at 15.7 it starts counting, but its target is 32.2 and it climbs out in about five days |
| Sturgia | −4.8 → −7.7 | 36.6 → 33.7 | 19.6 |
| Battania (0.93× NE) | +1.8 → +2.6 | 39.7 → 40.4 | 13.5 |

The peace-table gate, live: with Aserai freed, `offer_peace Battania | Aserai | vassalage` is
refused with *"Battania is no stronger than Aserai and could not hold it as a vassal"*; the
reverse passes the strength check and stops at the existing one (Battania already has a
patron). A one-week `ai_week` afterwards ran with zero errors and zero warnings.

**Not verified:** a revolt actually firing on the moved line, which needs 30 days of real
clock. The arithmetic, if nothing else in the world moved: Khuzait's target (17.5) sits under
its line (30), so it counts down the full 30 days and revolts. By then every link has drifted
to its target, and after §3's contagion of −10 Southern Empire (17.5), Aserai (22.2), Vlandia
(16.2) and Sturgia (23.7) are under 25 and rise with it; Western Empire (30.8) and Battania
(30.4) stay. Five of seven in one event. The world will move in 30 days, so this is a
prediction to check in run 06, not a result.

**What strength still does not mean, and why it was left:** `CurrentTotalStrength` is the
engine's live military figure. It swings after every large battle, and it counts nothing a
kingdom owns - Khuzait's 31 fiefs and Northern Empire's 7 weigh the same in it. A smoothed or
economic measure (fiefs, prosperity) would describe power better, but a smoothed one needs saved
state and an economic one is a new concept with its own balance. Not started; worth deciding
after run 06 shows how much the swings matter.

### 3d. Run 06 in progress, and the log it writes

Run 06 started 2026-09-16 from `di_phase1_full` and has covered Winter 1136 to Summer 1140 so
far, in two sessions archived as [run-06-part1.log](balance/run-06-part1.log) and
[run-06-part2.log](balance/run-06-part2.log) (part 2 continues from the save part 1 ended on).
It is resumed unattended from **`di_run06_resume`** (Summer 1, 1140; the test hero cured and aged
35, cheat mode off), launched with `pwsh ./scripts/play.ps1 -Without BirthAndDeath` so the module
set matches the first two parts, which ran under GABS without that module.

What it showed before the telemetry was extended, from the monitor, not yet analysed:

- The prediction in §3b held exactly: Khuzait revolted after 30 days at breaking point and
  Southern Empire, Aserai, Vlandia and Sturgia rose with it; Western Empire left through defiance
  the same day. Battania revolted alone ten months later. Northern Empire's sphere went from
  seven vassals to none.
- Western Empire rose first, taking Northern Empire as a vassal (value 90.4); Khuzait followed,
  taking Battania (68.3, cover 0.97) and then poaching Northern Empire from Western Empire.
- A coalition answered Southern Empire against Khuzait: Vlandia, Sturgia and Western Empire.

**The log was rebuilt for runs nobody watches** (2026-09-17). Beside the prose it now writes:

| Record | When | What |
|---|---|---|
| `[RUN]`, `[CONFIG]` | session launch | build time, settings, and **every constant** in `DiplomacyConstants` |
| `[SNAPSHOT]` | weekly, and at launch | world totals, as before |
| `[KINGDOM]` | weekly, per kingdom | live and smoothed strength, dominance, ambition, greed, towns/castles/villages, clans, ruler, influence, gold, weariness, wars, worst exhaustion, patron, vassals, pacts, tribute, trust in and out, last AI move |
| `[LINK]` | weekly, per vassalage | hold, target and every term of it, marks, revolt line, days at breaking point |
| `[WAR]` | weekly, per war | exhaustion, score, casualties, fiefs taken, called by |
| `[EVENT]` | as it happens | `ai_war_declared` (every valuation term, sides, support), `ai_pact_signed` (balancing pull), `war_opened`, `vassalage_formed` (route, value), `poach`, `revolt`, `annexation_breach`, `vassalage_collapsed`/`renewed`, `defiance_mark`, `call_to_arms` (role, outcome, reason), `treaty_signed`/`broken`/`repudiated`/`dissolved`/`expired`, `kingdom_eliminated`, `fief_changed`, `clan_changed_kingdom`, `ruler_changed`, `ruler_died`, `player_died`, `yearly_report` |
| report file | each campaign year | the full world, with the strength table and every sphere |

Every record is `[KIND] day=<absolute day> date=<Season_D;_Year> key=value ...`, no spaces inside
a value. Logs kept: 60, up from 10. Verified live on `di_run06_resume`: the header, the weekly
records, `ai_pact_signed`, `treaty_signed`, `fief_changed` and `war_opened` wrote correctly with
zero errors, and the analyser read them. `call_to_arms` and the hegemony events were not
triggered in that check.

`python tools/analyse-log.py <log> [<log> ...]` reads several logs, oldest first, and drops what a
later log re-covers after a reload. New sections: RUN (flags constants that changed between
sessions), POWER by year, TOP KINGDOM, AI MOVES, EVENTS, HEGEMONY TIMELINE, FIEFS, COALITIONS,
ENGINE (rulers, clans, the player), VASSAL LINKS.

### 3c. Power — the lead's design, built and verified piecewise, unverified in a run

Spec: [design/06-power.md](design/06-power.md). The lead's decisions: strength breeds ambition,
the strong provoke coalitions, a ruler grown too strong turns greedy and wants provinces rather
than vassals, **annexation only through war**, and **a kingdom that loses all its land is gone**.
How strength is measured was left to the implementation: **live** strength for what a kingdom can
do now (ambition, revolt capability, allies), a **smoothed 84-day average** for what it is becoming
(greed, dread, the balancing pull). New save data: `KingdomPower`, definer id 9, `ModState` 10.

Found and fixed on the way, both real:

- **Elimination would have frozen the conqueror.** The engine already destroys an AI kingdom on its
  last settlement (verified by IL), but raises no peace event, and our ledger closed wars only on
  that event. The war would have stayed open forever, counted as a chosen war. Wars now close with
  `endedBy=Eliminated`.
- **Coalitions could not work.** Allies judged a war hopeless against the caller plus one ally, so
  against a strong enemy every ally refused in turn; and the war valuation ignored the target's
  allies, so an alliance never deterred. Both read whole sides now (`CallToArms.ExpectedSupport`).
- **An annexation was filed as a just war** — BrokenTreaty at 0.95 — on a revolt claim the patron
  had forgiven by taking the vassal back. Submission now settles breach claims between the two; a
  sweep at load settles the old ones (1 in `di_phase1_full`).

**Verified live on `di_phase1_full`, zero errors, never saved:**

| What | Evidence |
|---|---|
| Strength table | Khuzait dominance 1.59, ambition 0.40; every greed 0.00 — nobody dominant |
| Greed and dread (smoothed strength set by the new test command) | NE at greed 0.54: every link `dread -13.4`, revolt lines up 8.1 |
| Annexation by the real AI evaluation | `Northern Empire tore up its vassalage with Sturgia to annex it (greed 0.93); 6 other vassal(s) saw it happen` — war declared, vassals refusing the summons, one vassalage broken on its third mark |
| Coalition carried by the balancing pull | Sturgia / Southern Empire defensive pact at 63.9, of which the pull was 40.0 — 23.9 without it, under the bar of 35 |
| Sides in the war valuation | NE → Battania: alone 1.07, with expected support 17,254 vs 0 → 2.51 |
| Elimination | Battania's seven fiefs given away; on the last: both its wars closed `endedBy=Eliminated`, the vassalage dissolved, two further AI weeks ran clean |
| Old breach claim settled on load | `Settled 1 broken-treaty claim(s)`; `Northern Empire vs Sturgia: BrokenTreaty` gone |

**Not verified:** anything that needs the clock or a genuinely dominant kingdom — greed arising on
its own, dread-driven revolts, the second-war allowance in use, coalitions deterring over years.
Run 06 is that measurement.

### 4. Phase 2 — court intrigue

Superseded on 2026-09-23 — Phase 2 is no longer a future item, and the current version of
this is the **Phase 2 — where it starts** section in the handoff at the top of this file.
One correction this entry needs recording rather than deleting: it claimed
`CallToArms.WouldAnswer` carries a note about vassal defiance reading grievances. It does
not, and never did.

## Decisions already made. Do not re-litigate.

| Decision | Detail |
|---|---|
| Three pillars | Diplomacy, court intrigue, espionage. **Not** economy/trade |
| Standalone | No dependency on the BUTR Diplomacy mod. Mutually incompatible with it by design |
| English UI only | Localization keys for future translation, English shipped |
| Minor factions out of scope | Treaties, claims and exhaustion are kingdom-only |
| AI plays by the same rules | Enforced in code — no "is this the player" argument anywhere |
| Enemy exhaustion shown as a band | Five bands whose edges are the behavioural thresholds. Phase 3 `ReadCourt` buys the exact figure |
| Rival courts shown as a band too | 2026-09-23. Own court fully legible, rivals qualitative only, exact figures sold by Phase 3. The same fork as enemy exhaustion, for the same reason |
| The player's clan is subject to intrigue | 2026-09-23. Serving a king is a political position, not a waiting room. Extends "the AI plays by the same rules" to Phase 2 |
| Kingdom decisions extended, not replaced | 2026-09-23. Revisit only when extending visibly constrains us |
| Vassalage stays in Phase 1 | And it carries military service; a tributary pays, a vassal pays and fights |
| Blocked routine path, deliberate defiance | The AI never wanders into a forbidden war; breaking a treaty on purpose is always possible and always expensive |
| Native dialogs, not a Gauntlet screen | A custom screen is the eventual goal and the most fragile thing a mod can own |
| `net472` | See CLAUDE.md §1 |

---

## Known gaps and loose ends

- **Battle and siege paths unverified.** Casualties reach exhaustion through `MapEventEnded`
  and fief capture through `OnSettlementOwnerChangedEvent`. Neither can be triggered from a
  console, so both need a real battle and a real siege. They are wired and reviewed, not
  observed.
- **Clock-dependent behaviour unverified** for the same reason: treaty expiry and its trust
  dividend, tribute changing hands on day 7, the two-year peace dividend.
- **Menu navigation past the root** has not been clicked through in a `MultiSelectionInquiry`.
  The root renders correctly (screenshot) and the vassal/ruler distinction works. The blanket
  claim that this needs a human is **too strong** and was corrected on 2026-09-20:
  `ui/click_widget` drove the whole of character creation, so GABS is not limited to the map
  layer. Whether it reaches a `MultiSelectionInquiry` specifically is **untested** — worth ten
  minutes before asking the lead to walk the submenus by hand.
- **`AiDiplomacy.TryDemandTribute` accepts on a strength ratio and a trust floor only.** It
  has no notion of the target's willingness beyond that; a weak kingdom with high trust will
  submit readily. Worth revisiting when Phase 2 gives courts an opinion.
- **A vassal's existing wars are untouched when it submits.** Signing vassalage does not end
  the client's own wars. Since the run-04 review the patron is called into the ones the vassal
  is *defending* (`CallToArms.DefendNewVassal`) and may refuse at the usual price; wars the
  vassal started stay its own.
- **`ConcessionLadder` yields castles before towns** via a two-pass flag that reads awkwardly
  (`townsFirst: false`). It works; it would read better as two explicit loops.

## Tools written for this project

| | |
|---|---|
| `tools/LoadProbe` | Pre-flight: target framework vs game host, reference resolution, `SubModuleClassType`. Catches the class of failure that produces no log at all |
| `tools/ApiDump` | Dumps the real public surface of game types to `artifacts/api/`. Use before writing against any unfamiliar API |
| `diplomacy.war_value A \| B` | The AI war valuation term by term, naming the gate that blocks. Written after guessing wrong twice |
| `diplomacy.tick_days N` | N days of the **full** daily upkeep, real functions, clock unmoved |
| `diplomacy.hegemony` | every sphere, each link's hold, and the terms pulling it |
| `diplomacy.strength` | every kingdom ranked by the strength the formulas read, its share, fiefs, sphere, and balance against its patron |
| `diplomacy.submission_value A \| B` | what submitting to B is worth to A, term by term |
| `diplomacy.ai_week N` | N weeks of AI evaluation plus matching upkeep. Prints its own limitations past 4 weeks |
| `diplomacy.report` | Telemetry snapshot to the log plus a full world report to file |
| `tools/analyse-log.py` | Parses one run, across any number of logs, into the acceptance numbers and the power, hegemony, fief, coalition and engine timelines. `python tools/analyse-log.py <log> [<log> ...]` |
| `diplomacy.test_set_player_age N` | Test saves only: sets the player hero's age and cures an old-age illness, so a long run does not end on the Game Over screen |
