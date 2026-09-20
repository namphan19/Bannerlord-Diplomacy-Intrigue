# Live check — 2026-09-19 (evening), the run-06 review fixes

Not a balance run: a GABS session driving the deployed working tree of `feature/run-06-fixes`
(the review fixes, uncommitted at the time) on `di_run06_resume`. Campaign advanced from
**Winter 10, 1156 to Spring 1, 1159** — about 180 days, just over two in-game years — then
paused for targeted tests. Log: `Documents\...\Logs\diplomacy-intrigue-20260919-181537.log`.

How it ran: hero parked in Marunath with "wait here", time speed 4, **no `BirthAndDeath`**.
Campaign speed multiplier 1 until Autumn 19, 1157, then **15** (`campaign.set_campaign_speed_multiplier`,
the lead's request, as run 06 part 1). Campaign-day logic is speed-independent; map simulation
may not be, so battles and casualties either side of Autumn 1157 are not strictly comparable.
The natural end state is saved as **`di_review_0919`**; the targeted tests below were run after
that save and were **not** saved.

## Verified in game

| What | Evidence |
|---|---|
| Session launch with the new code | Loads clean, `Session launched`, 0 errors / 0 warnings in the whole log. `DissolveChains` found nothing to cut (this save has no chain) |
| Goodwill decay 0.6/day | Winter 10 → 19 (10 daily ticks): `Vlandia -> Sturgia` 77.8 → 71.8 (−6.0) |
| Grudge decay 0.15/day | Same window: `Northern Empire -> Battania` −38.2 → −36.7 (+1.5); `Khuzait -> Southern Empire` −15.9 → −14.4 |
| Clamp at zero | `Northern Empire -> Southern Empire` 2.2 → 0.0, not below |
| War bleed 0.6 + 0.01·day | Khuzait–Aserai, war days 29-38: 95.5 → 86.1 (−9.4; expected 9.35). Sturgia–Southern Empire, days 3-12: 99.8 → 93.0 (−6.8; expected 6.75). Western–Southern Empire, days 28-37: −9.3 (expected 9.25) |
| Grace window | Pairs with a recent good act held still: `Khuzait -> Western Empire` 100, `Southern Empire <-> Aserai` 100; `Vlandia -> Aserai` resumed decaying two ticks before the reading (−1.2) |
| Saturation is gone | Mean `trustIn` across kingdoms, weekly telemetry: **79.6** (Winter 1156) → 71.0 → 59.2 → 43.9 → 31.2 → 19.1 → **13.7** (Winter 1158), flattening there - the level where decay and the world's good acts balance |
| No vassal of a vassal | With a test vassalage Khuzait → Vlandia: `sign_treaty Vlandia | Aserai | Vassalage` refused, *"Vlandia answers to Khuzait and cannot take vassals of its own."* The other half still holds: *"Khuzait holds vassals of its own and cannot itself submit."* |
| `ai_week` runs the full upkeep | One `ai_week`: that link's Hold 40.0 → 33.0 (it never moved under `ai_week` before), `Vlandia -> Khuzait` 12.5 → 8.3 and `Vlandia -> Western Empire` 17.1 → 12.9 (7 × 0.6) |
| War-ending terms skip the trust floor | Vlandia–Southern Empire at −100 both ways: `offer_peace Vlandia | Southern Empire | tribute=500` refused on **budget** (*"Demanded 60 against a war score of 0"*), i.e. `CanSign(settlesWar)` let it past the floor |
| Voluntary treaties still face it | Control: `Vlandia -> Khuzait` at −26.7, `sign_treaty Vlandia | Khuzait | Alliance` refused, *"Vlandia does not trust Khuzait enough to sign anything but a truce."* |

## What the world did (natural run, Winter 1156 → Winter 1158)

From `tools/analyse-log.py`:

- **4 wars ended, all at the peace table, 2 of them with terms**: Western Empire ceded Oristocorys
  Castle to Southern Empire (score −34.2), and **Khuzait was made to pay Aserai tribute** (score
  −91.0). The earlier live session saw 7 endings, all white; run 06 itself had imposed 15
  tributary pacts and ceded 5 fortifications at the table (parts 1 and 3), so the ladder does pay -
  that session was a quiet stretch, not the rule. The trust exemption did not decide this one:
  the pair was still at about +10 when the war ended.
- Mean war 95.8 days (all four 93-98). Every kingdom at war in 26.9% of weeks (run 06: 28.5%).
- 6 AI pacts, 5 carrying a balancing pull against Vlandia - the coalition still forms with trust
  at ~14 rather than ~100.
- No vassal links formed at any point, so F3 defection and the player-offer cooldown had nothing
  to act on.
- Vlandia's smoothed dominance reached 1.78 and greed 0.53 in 1157: **past
  `GreedRefusesVassals` (0.5)** for the first time. With no vassal to turn on, annexation still
  has not run.

## Findings

**1. A long war pins both sides at −100, and at the grudge rate that lasts years.** Vlandia's
Conquest war on Southern Empire (declared Spring 8, 1157, legitimacy 0.2) started from about 0
and reaches −100 both ways by day ~94 at these rates; it was at 161 days when the run paused.
At 0.15/day, −100 needs ~533 days (~6.3 years) to climb back over the −20 floor, so the two
cannot sign a pact of any kind for that long after the war. Arithmetically what the lead chose
(war bleed 0.6 + 0.01·day, grudges at 0.15), but it priced a long war like six treacheries.

*Resolved the same day:* the lead floored the war bleed at −35 (`TrustWarFloor`, what one broken
treaty costs its victim). A war can no longer take a pair below it; only a breach can. Records already at
−100 in `di_review_0919` are not lifted - they fade at the grudge rate like any other.

Checked in game on `di_review_0919` (log `diplomacy-intrigue-20260919-190538.log`, 0 errors,
not saved afterwards), `tick_days 100`: Western–Southern Empire 12.0 → **−35.0** and
Khuzait–Southern Empire 18.1 → **−35.0** both ways, stopping exactly at the floor;
Vlandia–Southern Empire stayed at **−100**, not lifted. Sturgia–Southern Empire (12.0) and
Aserai–Southern Empire (52.2) did not move at all: each had just been paid +12 for a treaty
honoured to expiry (a truce, and the non-aggression pact whose lapse Aserai's declaration
followed), and under `tick_days` the clock cannot leave the 30-day grace window - the
frozen-clock caveat, not a fault. The uncapped part of the bleed was verified on the real
clock earlier in this document.

**2. Southern Empire is being eaten, one declaration at a time.** At the pause it was at war with
all five other kingdoms: Vlandia (161 days), Western Empire (26), Aserai (9), Khuzait (6), Sturgia
(1). Every declaration was the AI's own, each with `fromRatio=40` (the cap - Southern Empire is the
weakest at 8.6%) and `theirSupport=0` (no allies). Aserai declared **one day after** its
non-aggression pact with Southern Empire expired. Trust is not in the war valuation, so F2 does
not cause this directly; but trust *is* in `PactValue` (`PactWeightTrust`), so a world at ~14 trust
renews fewer protective pacts than one at ~100. Not proven either way from one run. The pattern
(the weakest isolated kingdom attacked by everyone) matches the earlier live session's "Southern
Empire attacked four times in two years".

**3. Equilibrium trust is ~14.** It is where the new rates settle this world, not a tuned target.
Everything that reads trust now sees small numbers: Hold's trust term, `SubmissionValue`, the pact
valuation, and the call-to-arms floor at 0 - which a pair at peace can reach but not cross, since
peace drift stops at zero.

## Not verified

- **F3 defection** (`TryDefectToAttacker`, `CanDefectTo`, `Hegemony.Defect` via `Renounce`): no
  vassal link existed, and none could be built that met every gate (the only free kingdoms were
  either the one at war with everyone or the attackers).
- **`DissolveChains`**: needs a save that already holds a chain; none of ours does, and `CanSign`
  now refuses to build one - which the chain test above confirms.
- **The player-offer cooldown and the inquiry callbacks**: the player is an independent clan, so
  no offer can reach them.
- **The trust exemption after the peace** (`ImposeTribute`/`ImposeSubmission` signing with
  `settlesWar`): exercised only above the floor (Khuzait–Aserai at ~+10). The −100 case was
  checked at `IsDemandable` only, and refused on budget.

---

## Max-speed run with the −35 war floor (Spring 1, 1159 → Winter 15, 1162)

A second natural run from `di_review_0919`, the build with `TrustWarFloor = −35`. Campaign speed
multiplier **15 from the start** - the game caps it there (`set_campaign_speed_multiplier 30` answers
*"Campaign speed is set to 15. which is the maximum value"*). 29 real minutes gave **~329 days, ~3.9
in-game years**, about 11 days a minute. Log `diplomacy-intrigue-20260919-190836.log`: **0 errors,
0 warnings**. End state saved as **`di_review_0919_b`**.

### What the world did

| | This run (3.9 y) | Previous segment (2.2 y) | Run 06 part 3 |
|---|---|---|---|
| Wars ended | 14 (13 peace table, 1 follower release) | 4 | 53 |
| Mean / median war | 92.9 / 84.5 days | 95.8 / 96 | 87 / 76 |
| Ended with terms | **9 of 14**: 5 castles ceded, 1 tribute, 1 prisoners only | 2 of 4 | - |
| Weeks with every kingdom at war | **38.3%** | 26.9% | 28.5% |
| AI wars declared / pacts signed | 12 / 10 | 6 / 6 | - |
| Vassal links at any point | **0** | 0 | - |

- **The coalition brought Vlandia down.** It entered at dominance 1.80 and greed 0.58 - past the
  0.5 line - and was attacked in turn by Aserai, Western Empire and Sturgia: 35 → 28 fortifications,
  greed 0.1 by 1162. Khuzait rose to the top (1.70). The balance of power cycles.
- **Southern Empire is in a death spiral**: 10 fortifications in Winter 1156, 2 now, 6.5% of the
  world's strength, and paying tribute to **three** kingdoms at once (Aserai, Western Empire,
  Sturgia - 1,500 a period). Stacking is allowed by design: only Vassalage subordinates foreign
  policy (`Treaty.SubordinatesForeignPolicy`), so a tributary pact never blocks another. Across both
  runs, **none of the 18 AI war declarations targeted a kingdom with expected support** - the AI
  only ever attacks the isolated.
- **Hegemony is still dormant** after ~6 in-game years across both runs: no voluntary submission
  reached its threshold and no war reached the score 90 a peace-table vassalage needs - until the
  pause, when **Sturgia stood at +92.2 against Western Empire**. `di_review_0919_b` is the save to
  continue from to see the first imposed vassalage (and with it the first chance for F3).

### The war floor

No record was pushed below −35 by a war. The only deeper records are the legacy ones from before
the floor (`Vlandia <-> Southern Empire`, −100 at the start, **−52.0** now, recovering at the grudge
rate plus a truce honoured on the way) and pairs where an unjust-war penalty from the observers
landed on top of a war (`Southern Empire -> Khuzait` −31.6, `Vlandia -> Aserai` −31.0 - each 10 below
its mirror, which is exactly `TrustUnjustWarObserver`).

### Finding: grace shields the first month of a war, and wars follow lapsed pacts

Three times in this run a kingdom declared war on its **own former defensive-pact partner about
six days after the pact expired**:

| Pact expired | War declared |
|---|---|
| Khuzait / Western Empire, Spring 17, 1162 | Khuzait → Western Empire, Summer 2, 1162 |
| Sturgia / Western Empire, Summer 15, 1162 | Sturgia → Western Empire, Summer 21, 1162 |
| Aserai / Sturgia, Winter 7, 1162 | Aserai → Sturgia, Winter 13, 1162 |

(The earlier segment had a fourth: Aserai on Southern Empire one day after their non-aggression
pact lapsed.) Six days is about one weekly evaluation: the pact was the only thing holding the war
back.

The trust ledger rewards this. The expiry pays both sides +12 for honouring the pact and opens the
30-day grace window - and `DailyTick` checks grace **before** it checks for war, so the first
~30 days of the war bleed nothing. Khuzait–Western Empire had been at war 55 days at the pause and
stood at only −6.7 (without the grace window, 55 days of bleed from about +12 would have reached
the −35 floor); Sturgia–Western Empire, 36 days in, at +1.3.

Two separate questions for the lead:

1. **Should grace shield a war at all?** Grace exists so that a relationship still being tended
   does not fade; two kingdoms at war are not tending anything. Checking for war before grace is a
   one-line reorder in `TrustRegistry.DailyTick`. Recommended.
2. **Should a pact's lapse be followed by a cooling-off before war** - or should the +12 honour
   dividend be clawed back when the payee attacks within some window? Today the reputation system
   pays a kingdom for the pact it is about to discard. A design call, not a fix.
