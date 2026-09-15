# Status — 2026-09-15

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**.
Last measured: balance run 01, 28 in-game years — see below.

---

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.8)** | ✅ code complete, **acceptance met** over a measured 28-year run. Two constants tuned from it and awaiting a confirming run |
| **2 — Court intrigue** | ⬜ spec written and reviewed, no code |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | 🔄 balance run 01 done, run 02 wanted |

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

## Choose what to do next

**A. A second balance run.** Confirms the two tuned constants and shows whether longer wars
bring the peace table's concession ladder to life. Cheapest high-value step: the lead just
plays, the mod reports.

**B. Start Phase 2 — court intrigue.** Fully specced in `docs/design/02-intrigue.md` and
independent of the tuning. Implementation order is 2.1 grievances → 2.2 loyalty → 2.3 blocs
and voting → 2.4 legitimacy → 2.5 succession → 2.6 civil war → 2.7 UI. Phase 1 already
leaves hooks: `ExhaustionCourtPressure` (40) is where doves are meant to gain support, and
`CallToArms.WouldAnswer` has an explicit note where vassal defiance should read grievances.

**C. Hegemony (emperor / khagan).** The lead proposed a supra-kingdom tier — one title over
several vassalage treaties, with election and defection cascades. Assessed as feasible and
cheap, because `TreatyType.Vassalage` already does the hard part. **Agreed to sit at 2.8,
after grievances and legitimacy exist**, because without those counter-pressures a hegemony
is a one-way ratchet that decides the map. Not yet specced; `docs/design/04-hegemony.md`
would be the place, and four questions are still open — how the title is founded, whether
membership gives anything or is pure coercion, how to cap call-to-arms cascades across it,
and what happens when the overlord is destroyed.

---

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
  the client's own wars, so a patron can inherit a war it did not choose. Deliberate for now;
  decide when hegemony is specced.
- **`ConcessionLadder` yields castles before towns** via a two-pass flag that reads awkwardly
  (`townsFirst: false`). It works; it would read better as two explicit loops.

## Tools written for this project

| | |
|---|---|
| `tools/LoadProbe` | Pre-flight: target framework vs game host, reference resolution, `SubModuleClassType`. Catches the class of failure that produces no log at all |
| `tools/ApiDump` | Dumps the real public surface of game types to `artifacts/api/`. Use before writing against any unfamiliar API |
| `diplomacy.war_value A \| B` | The AI war valuation term by term, naming the gate that blocks. Written after guessing wrong twice |
| `diplomacy.tick_days N` | N days of upkeep, real functions, clock unmoved |
| `diplomacy.ai_week N` | N weeks of AI evaluation plus matching upkeep. Prints its own limitations past 4 weeks |
| `diplomacy.report` | Telemetry snapshot to the log plus a full world report to file |
| `tools/analyse-log.py` | Parses a run log into the acceptance numbers: war durations, alliance formation, permanent-war check, casus belli mix. `python tools/analyse-log.py <log>` |
