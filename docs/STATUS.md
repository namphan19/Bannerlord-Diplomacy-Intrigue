# Status — 2026-09-16

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**.
Treaty save ids now run to **17** (`Hold`, defiance marks, the revolt clock).
Last measured: **balance run 04**, 5.1 in-game years, zero errors —
[docs/balance/run-04.md](balance/run-04.md).

**Phase 1 is code complete, 1.1 through 1.11, and run 04 met every acceptance criterion.**
Wars end (median 66 days, 12 of 15 through our peace table, nothing outside our systems),
the world came off total war (73 % of weeks → 3.3 %), alliances hold, nobody was eliminated,
zero errors over 5.1 in-game years.

**And the same run found the pillar's real problem: hegemony forms far too easily.** Four
kingdoms knelt in the first ninety seconds and one ended the run holding all seven others.
Three defects behind it are listed under "What to do next"; none is a crash, all are Phase 1
correctness and balance. A design review afterwards found the cause was mostly structural —
a patron's duty to protect existed only as a penalty, never as something code could do — and
the fixes are in "What to do next" §3, verified piecewise in game and **not yet in a run**.

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
| **1 — Diplomacy core (1.1–1.11)** | ✅ **code complete**, including submission and hegemony (1.9/1.10) and the vanilla takeover (1.11). Verified piecewise in live campaigns; the whole-pillar run is run 04 |
| **2 — Court intrigue** | ⬜ spec written and reviewed, no code |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | 🔄 runs 01-03 done and archived, run 04 is the Phase 1 acceptance run |

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

## What to do next

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

**What run 05 should watch**, beyond §2's list: `answered <vassal> and declared war` and
`leaves its vassal ... to fight alone` lines, whether `avgHold` still settles in the 15–36
band, `tributePaid` against `tributeWithheld`, and whether a sphere ever loses more than one
vassal at a time (`other vassal(s) rose with it`). The balancing weight and the join threshold
are both un-tuned.

**Deliberately left for after run 05**, so it can be measured against these changes rather
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

### 4. Phase 2 — court intrigue

Specced in `docs/design/02-intrigue.md`; order is 2.1 grievances → 2.2 loyalty → 2.3 blocs →
2.4 legitimacy → 2.5 succession → 2.6 civil war → 2.7 UI. Phase 1 leaves hooks waiting for
it: `ExhaustionCourtPressure` (40) for doves, a note in `CallToArms.WouldAnswer` where vassal
defiance should read grievances, and the `Hold` formula wants a legitimacy term
([design/04](design/04-hegemony.md) §1.2).

Two Phase 1 pieces are deliberately parked until Phase 2 makes them mean something:
**vassal-party summons** (design 04 §8 — the most intrusive and least load-bearing part of
hegemony) and **titles** (Emperor, Khagan), which sit on top of legitimacy at 2.4.

## Decisions already made. Do not re-litigate.

| Decision | Detail |
|---|---|
| Three pillars | Diplomacy, court intrigue, espionage. **Not** economy/trade |
| Standalone | No dependency on the BUTR Diplomacy mod. Mutually incompatible with it by design |
| English UI only | Localization keys for future translation, English shipped |
| Minor factions out of scope | Treaties, claims and exhaustion are kingdom-only |
| AI plays by the same rules | Enforced in code — no "is this the player" argument anywhere |
| Enemy exhaustion shown as a band | Five bands whose edges are the behavioural thresholds. Phase 3 `ReadCourt` buys the exact figure |
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
- **Menu navigation past the root** was never clicked through — GABS cannot click inside a
  `MultiSelectionInquiry`. The root renders correctly (screenshot) and the vassal/ruler
  distinction works. Ask the lead to walk the submenus.
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
| `diplomacy.submission_value A \| B` | what submitting to B is worth to A, term by term |
| `diplomacy.ai_week N` | N weeks of AI evaluation plus matching upkeep. Prints its own limitations past 4 weeks |
| `diplomacy.report` | Telemetry snapshot to the log plus a full world report to file |
| `tools/analyse-log.py` | Parses a run log into the acceptance numbers: war durations, alliance formation, permanent-war check, casus belli mix. `python tools/analyse-log.py <log>` |
