# TODO

Open items. Evidence and arithmetic live in the linked docs; this file is only the list.
Tick an item off, or delete it, when it is done.

For where the work stands overall, read [docs/STATUS.md](docs/STATUS.md).

## Decided and built on 2026-09-20 — nothing to do

The vassalage drought that filled this file is over. Kept as one line each so nobody re-opens
them; the reasoning is in [design/04 §12](docs/design/04-hegemony.md) and
[§13](docs/design/04-hegemony.md#13-one-subjugation-rung-and-a-cliff-2026-09-20-after-run-07),
the evidence in [balance/run-07.md](docs/balance/run-07.md).

- ~~No vassal link forms~~ — **run 07 produced 7 links through all 4 routes in 20.8 years.**
  Cause 1 (the ladder could not reach vassalage) was the war-score clamp and is fixed. Cause 2
  (F2 removed the trust term) was real but the threshold was reachable anyway. Cause 3 (`cover`
  near zero) turned out to be **a disease of an old world, not a flaw in the formula** — a fresh
  map gave `relief 1.00` to an outsider. The proposal to rewrite `cover` to weigh a patron's
  willingness is **withdrawn** pending late-game evidence.
- ~~F3 defection never verified~~ — **ran end to end**, Autumn 1096, Hold 38.6, old bond broken
  as the patron's breach.
- ~~`settlesWar` signing after the peace below −20~~ — exercised by all three routes that settle
  a war with an oath.
- ~~12b, the ladder ordering question~~ — closed by §13.2. The demand is now a cliff, so both
  directions of the ladder stop on the same rung and the initiator no longer decides the outcome.

## Decisions for the lead

### 1. The strength margin — deliberately deferred on 2026-09-20

`Hegemony.IsStrongEnoughToHold` is a strict inequality with no margin, so a patron **1.2%**
stronger passes. Run 07 §7.1: Northern Empire (9700) took Aserai (9584) and the link's Hold
target was **0 on the day it was signed**; it was collapsing within weeks.

The lead chose to leave it. It matters more now than it did: §13.2's cliff multiplies the rate
of imposed links, so each one is another chance to create a doomed one.

- [ ] Decide after run 08 shows how often it happens. Option: require 1.25×, matching
      `GreedStartsAtDominance`, which would give `fear ≥ +8` instead of +0.44.

### 2. Should money bite?

The indemnity rung was dead — **0 of 100 settlements in run 07 used it** — and §13.4 fixed the
*sizing* so it is demandable. The *price* is untouched: at 8 points per 1,000 denars a 60-point
indemnity is 7,500 denars, real on the ladder and trivial to a ruler holding several hundred
thousand.

- [ ] Decide whether `PeaceCostPerThousandIndemnity` should change. This is balance, not a fix.

### 3. Grace shields the first month of a war

The 30-day trust grace is checked **before** the war bleed in `TrustRegistry.DailyTick`. Three
times in the 2026-09-19 max-speed run a kingdom attacked its former defensive-pact partner ~6
days after the pact lapsed; the expiry's +12 "honoured" and its grace meant the first ~30 days
of each war bled nothing. See [balance/live-2026-09-19-review.md](docs/balance/live-2026-09-19-review.md).

- [ ] **Recommended:** war bleed ignores grace (check for war before grace — a one-line reorder).
- [ ] Design call: a cooling-off period after a pact lapses, or claw back the +12 dividend if the
      payee attacks within some window.

### 4. The weakest isolated kingdom gets eaten

Observed on the old evolved save: Southern Empire went 10 fortifications → 2 while paying
tribute to **three** kingdoms at once (stacking is allowed — only Vassalage subordinates foreign
policy). **Run 07 did not reproduce the death spiral** on a fresh map; Western Empire was
eliminated but by an ordinary dogpile.

- [ ] Decide whether tribute stacking on one payer should be capped.
- [ ] Decide whether the AI should weigh a target already at war with several others.

### 5. F3 "legal neglect"

`Hegemony.Protection` does not count a patron treaty-bound to its vassal's attacker as neglect,
so Hold stays healthy and defection never opens on that path. Run 07's defection fired through
the ordinary neglect route instead, so this is no longer blocking anything.

- [ ] Options: (a) fractional neglect, (b) a Hold/trust hit for treating with the attacker,
      (c) accept it.

### 6. Tribute re-imposition on the expiry day

Happens on the exact expiry day, every cycle.

- [ ] Add a cooldown, or let the payer refuse?

## Pending work

- [ ] **Run 08** from `di_fresh_1084`, to answer
      [design/04 §13.7](docs/design/04-hegemony.md#137-what-the-next-run-must-answer). §13 has
      only been smoke-tested (2 years, 0 errors), not measured.

## Not verified in game

- [ ] **`ReconcileWithSiblings` (§12.4.5)** — has never executed its real branch. It runs on
      every `Hegemony.Submit` and did so 7 times in run 07 without throwing, but no kingdom knelt
      to a hegemon while at war with one of that hegemon's other vassals. More links per hegemon
      makes the coincidence likelier.
- [ ] **The dissolution rung, chosen by the AI.** Verified end to end by console command
      (design/04 §12.8) but no war in run 07 had a hegemon as its loser inside the affordable
      band.
- [ ] `Hegemony.DissolveChains` — needs a save that already holds a chain; none of ours does.
- [ ] Player-offer cooldown (42 days) and the inquiry callbacks — the test hero is not a ruler.

## Saves

| Save | State |
|---|---|
| `di_fresh_1084` | **Summer 1, 1084, pristine, hero parked in Myzea.** The run-08 baseline |
| `di_run07_1104` | Winter 1104, end of run 07 — 7 kingdoms, 2 hegemons |
| `di_hegemony_1166` | Vlandia with 2 vassals; the only save holding a sphere built at the peace table |
| `di_review_0919_b` | Winter 15, 1162 — the old evolved world, pre-§12 |
| `di_review_0919` | Spring 1, 1159 |
| `di_run06_resume` | Winter 10, 1156 |

Never save over `di_phase1_full`.
