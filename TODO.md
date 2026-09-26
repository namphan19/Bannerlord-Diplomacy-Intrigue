# TODO

The one list of open decisions and pending work. Evidence and arithmetic live in the linked
docs; this file is only the list. Tick an item off, or delete it, when it is done.

For where the work stands overall, read [docs/STATUS.md](docs/STATUS.md). The saves are listed
there too, and only there.

## Answered — nothing to do

Kept as one line each so nobody re-opens them.

- ~~No vassal link forms~~ — **run 07 produced 7 links through all 4 routes in 20.8 years**, run
  08 five per run in ten years. The reasoning is in [design/04 §12](docs/design/04-hegemony.md)
  and [§13](docs/design/04-hegemony.md#13-one-subjugation-rung-and-a-cliff-2026-09-20-after-run-07),
  the evidence in [balance/run-07.md](docs/balance/run-07.md). The proposal to rewrite `cover` to
  weigh a patron's willingness stays **withdrawn** pending late-game evidence.
- ~~F3 defection never verified~~ — ran end to end, Autumn 1096, Hold 38.6.
- ~~`settlesWar` signing after the peace below −20~~ — exercised by all three routes that settle
  a war with an oath.
- ~~12b, the ladder ordering question~~ — closed by §13.2's cliff.
- ~~The strength margin (`Hegemony.IsStrongEnoughToHold`, no margin)~~ — **run 08 §5.4: no link
  started doomed.** The lowest starting Hold target was 24.1, the closest margin 1.10× (that link
  survived). The links that reached a target of 0 got there after signing, through protection
  and revolt, which a margin at signing would not have stopped. The margin stays where the lead
  left it. This was also R-8 in the 2026-09-24 review.
- ~~Run 08~~ — run 2026-09-26, [balance/run-08.md](docs/balance/run-08.md). It answers
  [design/04 §13.7](docs/design/04-hegemony.md#137-what-the-next-run-must-answer).

## Decisions for the lead

### 1. Phase 2's acceptance line cannot be met yet

ROADMAP's line for Phase 2 ends *"The player can survive it by managing grievances."* The player
has no act that reduces a grievance: the Court tab shows and selects, and nothing else. This was
the 2026-09-24 review's headline (R-2, court verbs), and it is still open. Nobody has formally
checked Phase 2 against its acceptance line.

- [ ] Build the court verbs (R-2: appease, patronage, per-vassal tribute were the review's
      recommendation), or change the acceptance line.

### 2. The court does not reach the AI's foreign policy

`AiDiplomacy`'s war and peace valuations read no bloc, loyalty, crown legitimacy or pretender.
The only place the diplomacy layer reads a court is the tribute demand (`Intrigue/TributeCourt`).
Review item R-1.

- [ ] Wire it (a Doves share lowering the peace threshold, a divided or illegitimate neighbour as
      a war-valuation term), or leave the pillars apart on purpose.

### 3. Espionage is on by default while the AI cannot use it

`EnableEspionage` defaults to true. The player's networks grow; an AI network never does,
because vanilla makes its handler a governor or a party leader within days (the 3.6 blocker,
[design/03 §10](docs/design/03-espionage.md)). The code keeps "the AI plays by the same rules";
the outcome does not.

- [ ] Default it off until the handler blocker is solved, or keep it on knowingly.

### 4. Civil war, tribute at the table and cadet branches almost never happen

Run 08 (20 in-game years across both halves): **zero** internal wars, contested successions,
divided houses or side changes; **0 of 123** peace settlements conceded a tributary pact (it
needs a war score of 65-75; the mean final score was 21-27). A new cadet branch has almost no
influence, so soon after a split it cannot reach the 30% a pretender needs (STATUS, 2026-09-26).
A player can play a whole campaign and see none of it.

- [ ] Civil war: a run from a strained court first (`test_set_legitimacy`), or revisit the
      trigger thresholds, which design/02 §6 calls guesses and which were deferred on 2026-09-23.
- [ ] Tribute: move the §13 bands or not (design/04 §13.7).
- [ ] Cadet branches: should a cadet founder inherit some of the parent house's influence or
      backers, so that 2.6b can lead to 2.6?

### 5. Pacts under statecraft

Run 08 signed 47 AI pacts with statecraft on against 31 off. One pair cannot separate that from
world divergence.

- [ ] A second A/B pair from another seed before any constant moves — say if it should be run.

### 6. Should money bite?

Run 07 used the indemnity rung in **0 of 100** settlements; after §13.4's sizing fix, run 08
used it in **35 of 123** (24 with statecraft on, 11 off). The *price* is untouched: at 8 points
per 1,000 denars a 60-point indemnity is 7,500 denars, trivial to a ruler holding several hundred
thousand.

- [ ] Decide whether `PeaceCostPerThousandIndemnity` should change. Balance, not a fix.

### 7. Grace shields the first month of a war

The 30-day trust grace is checked **before** the war bleed in `TrustRegistry.DailyTick`. Three
times in the 2026-09-19 max-speed run a kingdom attacked its former defensive-pact partner ~6
days after the pact lapsed; the expiry's +12 "honoured" and its grace meant the first ~30 days
of each war bled nothing. See [balance/live-2026-09-19-review.md](docs/balance/live-2026-09-19-review.md).

- [ ] **Recommended:** war bleed ignores grace (check for war before grace — a one-line reorder).
- [ ] Design call: a cooling-off period after a pact lapses, or claw back the +12 dividend if the
      payee attacks within some window.

### 8. The weakest isolated kingdom gets eaten

Observed on the old evolved save: Southern Empire went 10 fortifications → 2 while paying
tribute to **three** kingdoms at once (stacking is allowed — only Vassalage subordinates foreign
policy). **Run 07 did not reproduce the death spiral** on a fresh map; Western Empire was
eliminated but by an ordinary dogpile.

- [ ] Decide whether tribute stacking on one payer should be capped.
- [ ] Decide whether the AI should weigh a target already at war with several others.

### 9. F3 "legal neglect"

`Hegemony.Protection` does not count a patron treaty-bound to its vassal's attacker as neglect,
so Hold stays healthy and defection never opens on that path. Run 07's defection fired through
the ordinary neglect route instead, so this is no longer blocking anything.

- [ ] Options: (a) fractional neglect, (b) a Hold/trust hit for treating with the attacker,
      (c) accept it.

### 10. Tribute re-imposition on the expiry day

Happens on the exact expiry day, every cycle.

- [ ] Add a cooldown, or let the payer refuse?

### 11. Before a release (Phase 4)

- [ ] The 75 `diplomacy.*` console commands, the `test_*` levers among them, need no cheat mode.
      The retail game has no console, but a console mod reaches them. Gate the levers, or strip
      them from a release build.
- [ ] `EnableTelemetry` defaults to true and writes weekly reports into Documents. Right for a
      test build; decide for a release.

### 12. The 2026-09-24 mechanics review is not on `development`

Its decision register (R-1 to R-10, U-1 to U-9, S-1 to S-3) lives only on the branch
`origin/review/game-mechanics`, which was never merged. Several of its questions have been
answered since, so the register reads out of date:

- S-1 to S-3 are built as Phase 2.8 ([design/08](docs/design/08-statecraft.md)).
- Decided on 2026-09-25 in design/03 §9: U-4 and R-10, assassination (decisions 1 and 11); U-5, a
  handler's fate (3); U-6, networks in a war (4); U-7, the player as a target (2). U-6 went against
  the review's recommendation: networks survive a war at half growth.
- U-3 (four missions or eight) was not asked in that form: Phase 3 was specified and built with
  all eight.
- U-1 (court verbs before Phase 3) was overtaken: the lead started Phase 3 on 2026-09-25, and 2.8
  became Statecraft. The court verbs are still open, as item 1 above.
- R-8 is answered above.

The branch also carries a vendored `vietnamese-tech-writing` skill that overlaps the project's
own `vietnamese-writing` (CLAUDE.md §4).

- [ ] Merge it (without the vendored skill) and bring the register up to date, or close it.

## Pending work

- [ ] **A civil-war balance run** from a save with a strained court — item 4.
- [ ] **A `tribute_refused` telemetry event.** Acceptances of a tribute demand are logged,
      refusals are not, so `AiTributeCourtRefusalShare` cannot be tuned (run 08 §6).
- [ ] **Espionage, when the lead unparks it:** the 3.6 handler blocker (find from vanilla's IL
      what assigns governors and parties to an AI clan's heroes, then choose the lever with that
      evidence), then 3.6's long AI-only run, then the two 3.5 wording faults and the note on a
      reloaded bribe offer ([design/03 §10](docs/design/03-espionage.md)).

## Not verified in game

- [ ] **`ReconcileWithSiblings` (§12.4.5)** has never executed its real branch: not in run 07,
      not in run 08 (§5.5). It needs a hegemon whose new vassal is at war with one of its older
      vassals.
- [ ] **The dissolution rung, chosen by the AI.** Verified end to end by console command
      (design/04 §12.8) but no war in run 07 had a hegemon as its loser inside the affordable
      band.
- [ ] `Hegemony.DissolveChains` — needs a save that already holds a chain; none of ours does.
- [ ] Player-offer cooldown (42 days) and the inquiry callbacks — the test hero is not a ruler.
- [ ] **The `DeclareWarAction.ApplyByKingdomDecision` prefix** since it was split into its own
      file (2026-09-26): shown applied by Harmony at startup, not run. Its only vanilla caller is a
      passed war vote.
- [ ] The rest of the carried list — the peace table's two unseen surfaces, the civil-war line,
      the Court tab's gaps — is in [STATUS.md](docs/STATUS.md), "Not verified — carried".
