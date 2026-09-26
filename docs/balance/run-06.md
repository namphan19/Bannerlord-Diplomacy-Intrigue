# Balance run 06 — interim analysis, 2026-09-17

**Status: interim.** The run was still going when this was written. It covers **Winter 1136 to
Winter 9, 1153 (~17 in-game years)** across three logs. Every number below is measured from those
logs; anything inferred is marked as such. Nothing here has been decided by the lead yet —
findings carry *candidate* fixes, not applied ones.

Written for whoever picks up the balance work next. §8 is the checklist.

---

## 1. What this run measures

The first campaign with all of branch `feature/hegemony-structural-fixes` in play
([STATUS-history.md](../STATUS-history.md), "History — how each run changed the design" §3, §3b, §3c):

- the patron's duty to defend its vassal, submission reading the patron's protection, withheld
  tribute as defiance, joint revolt, safer poaching;
- strength read on a symmetric scale, the revolt line moving with strength, the peace table
  refusing vassalage to a weaker winner;
- Power ([design/06](../design/06-power.md)): ambition, coalitions that deter and hold, greed and
  annexation through war, elimination handling.

## 2. Setup — read before trusting a number

| Part | Campaign dates | Log | Analysis | How it ran |
|---|---|---|---|---|
| 1 | Winter 14, 1136 → Summer 8, 1139 | [run-06-part1.log](run-06-part1.log) | [run-06-part1-analysis.txt](run-06-part1-analysis.txt) | GABS session from `di_phase1_full` (the run-04 end state), campaign speed multiplier 15, no `BirthAndDeath` |
| 2 | Summer 8, 1139 → Summer 1, 1140 | [run-06-part2.log](run-06-part2.log) | — (11 weeks) | GABS, from `di_run06_mid`; the game saved over that file on exit |
| 3 | Summer 1, 1140 → Winter 9, 1153 (**still running**) | [run-06-part3-interim.log](run-06-part3-interim.log) | [run-06-part3-analysis.txt](run-06-part3-analysis.txt) | the lead's unattended session from `di_run06_resume`, **official launcher**, no GABS, no `BirthAndDeath`, default speed |

Confounds and gaps:

- **Parts 1-2 predate the structured telemetry.** They have `[SNAPSHOT]` and `[WAR-ENDED]` but no
  `[KINGDOM]`, `[LINK]`, `[WAR]` or `[EVENT]`. Everything in §4-§6 about power, coalitions and
  hegemony events comes from **part 3 only**; parts 1-2 are described from the live monitor.
- **The test hero was altered twice** with `diplomacy.test_set_player_age` (aged 75 and dying of
  an old-age illness; set to 35 and cured). The player is an independent clan with no kingdom, so
  this should not touch diplomacy — but it is a change to the world.
- **Speed differed** (multiplier 15 in parts 1-2, default in part 3). Campaign-day logic is
  speed-independent; map simulation (battle frequency, casualties) may not be.
- **Part 3's save was overwritten mid-run**: `di_run06_resume` now holds the lead's checkpoint
  from about Spring 1146, not Summer 1140.
- The starting world was **not a fresh campaign** but the run-04 end state: one hegemon holding
  seven kingdoms. Parts 1-2 are a stress test of how that comes apart.

To regenerate: `python tools/analyse-log.py docs/balance/run-06-part3-interim.log` (add the final
log when the run stops; pass logs oldest first).

## 3. Headline

1. **The saturated hegemony came apart exactly as predicted.** STATUS §3b predicted Khuzait would
   revolt first and Southern Empire, Aserai, Vlandia and Sturgia would rise with it. That is what
   happened (Summer 1137), to the kingdom. Western Empire left the same day through defiance;
   Battania revolted alone ten months later. Northern Empire went from seven vassals to none.
2. **Two kingdoms were eliminated by conquest** — the first eliminations in any run — and the war
   ledger handled both with zero errors. **Both were vassals of Khuzait.** For Battania the log shows its
   patron's defence blocked on every attack it could have answered (§5 F3); for Northern Empire
   the war predates the telemetry and cannot be checked.
3. **Hegemony became small and short-lived.** After the collapse, at most two vassal links existed
   at once; every link formed was voluntary and **every one lapsed at its five-year term**. No
   poach, no revolt, no annexation in part 3.
4. **Greed never triggered.** The highest smoothed dominance any kingdom reached was 1.76; greed
   starts at 2. Vlandia ends at 28% of the world's strength and rising.
5. **Total war came back in bursts**: every kingdom at war in 28.5% of weeks (run 04: 3.3%).
6. **Trust saturated**: by the end nearly every kingdom is trusted at 100 by all others.

## 4. Acceptance criteria

| Criterion | Run 04 | Run 05 | **Run 06 part 1** | **Run 06 part 3** |
|---|---|---|---|---|
| Years covered | 5.1 | 4.3 | 2.6 | **13.8** |
| Wars ended | 15 | 13 | 17 | **53** |
| War length, median / mean | 66 / 204 d | 76 / 106 d | 49 / 63 d | **76 / 87 d** — PASS (< 252) |
| Ended by our peace table | 80% | 85% | 59% | **70%** |
| Ended by elimination | 0 | 0 | 0 | **9.4%** (5 wars) |
| Dormant | 7% | 8% | 24% | **9.4%** |
| Alliances present | 56/61 weeks | 51/52 | — | **165/165 weeks** |
| Weeks with every kingdom at war | 3.3% | 0% | 35.5% | **28.5%** ⚠️ |
| Kingdoms alive | 8 → 8 | 8 → 8 | 8 → 8 | **8 → 6** |
| Errors | 0 | 0 | 0 | **0** (3 warnings, §5 F4) |

Wars fought over `ReclaimAncestralLand` 41.5%, `Conquest` 30.2%, `DefendAlly` 28.3%. 62% of wars
moved at least one fief; 180 fortifications changed hands.

Total war by year (part 3):

| Year | 1140 | 1141 | 1142 | 1143 | 1144 | 1145 | 1146 | 1147 | 1148 | 1149 | 1150 | 1151 | 1152 | 1153 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Weeks all at war | 7/10 | 4/12 | 5/12 | 6/12 | 5/12 | 0/12 | 0/12 | 7/12 | 0/12 | 0/12 | 5/12 | 5/12 | 2/12 | 1/11 |
| Mean ongoing wars | 7.8 | 3.9 | 4.2 | 4.1 | 3.9 | 3.0 | 3.0 | 3.4 | 3.2 | 2.5 | 3.5 | 4.8 | 3.7 | 1.9 |

It is bursty, not permanent: the map repeatedly recovers to zero and falls back in.

## 5. Findings, ranked

### F1 — Total war returns in bursts (28.5% of weeks)

**Evidence.** Table above. Run 04 had 3.3%, run 05 0%.

**Suspect (inferred, not proven): alliances make attacking cheaper, not harder.** The war
valuation now reads sides (`CallToArms.ExpectedSupport`). Of 36 AI war declarations, **25 had the
aggressor's own allies counted in** (`ourSupport > 0`), and only **3 targets had any expected
support**. Median side ratio 2.20 against a median own ratio of 1.56. An alliance counts toward its
member's offensive odds (an `Alliance` owes offensive service), so every allied kingdom sees
favourable odds — and every such war then pulls the partner in (`DefendAlly` is 28% of wars). The
side-ratio change was meant to make alliances *deter*; the selection of isolated targets suggests
it does that, but it also *emboldens*.

**Candidate fixes** (for the lead): count only defensive support for the defender and not
offensive support for the aggressor; or discount an ally's strength by its distance to the target;
or keep the gate on sides but compute the strength *term* on own strength. Test one at a time.

**Lead's call (2026-09-18): leave it, keep observing.** No change made. The mechanism does both
things the finding says - it deters attacks on the networked and emboldens the networked - and
whether that nets out as a problem needs another run's evidence more than it needs a guess.

### F2 — Trust saturates at 100

**Evidence.** Mean `trustIn` across kingdoms, by year: 71 (1140) → 86 (1142) → 93 (1145) → 97
(1147) → **99 (1152)**; the minimum rises from 28 to 92. 105 treaties expired in part 3.

**Cause (from code).** `TrustTreatyHonoured` +12 to both parties on every expiry — including the
48 truces that follow every peace — `TrustCallToArmsAnswered` +20, `TrustPeaceHeld` +8, and trust
never decays (`TrustRegistry`, `DiplomacyConstants`). Losses need a breach, and breaches were rare.

**Consequence.** Trust no longer separates partners: the pact floor (−20) and the call-to-arms
floor (0) never bind, and Hold's trust term sits at its +15 maximum. This is the mirror image of
the review's "grim trigger" concern (STATUS §3 deferred list) — trust only moves in the direction
nobody is doing anything to prevent.

**Candidate fixes.** Pay nothing for an expiring truce; decay trust toward 0 slowly; make the
honour dividend shrink as trust rises. Decide together with the grim-trigger item.

**Lead's call (2026-09-18): decay it, three rules.** Implemented on `feature/run-06-fixes`
(builds clean; unverified in game - the frozen clock can run `TrustRegistry.DailyTick` but
cannot age a timestamp, so the grace window and war ramp need a real campaign):

- `TrustRegistry.DailyTick` runs in the daily upkeep. At peace a record drifts toward zero
  at 0.05/day, from either side - reputation and grudges both fade if nobody tends them.
  The toward-zero direction also answers the grim-trigger item: the ledger now has a route
  back from the bottom.
- A positive change suspends decay for 30 days, read off a new
  `TrustRecord.LastPositiveChange` (save id 5; pre-F2 saves load it as campaign-start,
  which simply means their records decay normally). `LastChanged` could not serve - it
  moves on the decay itself.
- At war the record moves *down* instead, 0.05 + 0.005 per day the war has run - roughly
  18 trust over a median run-06 war, most of a century of goodwill over a year-long one.

All constants un-tuned. `diplomacy.tick_days` runs the decay too, but cannot move
`CampaignTime.Now`, so within it nothing ages into or out of the grace window - same
frozen-clock caveat as treaties.

**Live check (2026-09-19, `di_run06_resume` via GABS, `tick_days 10`).** Peacetime drift
exact (-0.5 on every record) and the war ramp exact (Vlandia-Southern Empire -1.9 at 28
days elapsed, Sturgia-Western Empire -0.9 at 8, the two 19-day wars -1.45). It also caught
a real bug: `Northern Empire -> Battania` sat at -51.0 and never moved - the +0.05 decay
step on a negative record went through `TrustRecord.Add`, which stamped
`LastPositiveChange` and froze the record inside its own grace window. Decay now runs
through `TrustRecord.Decay`, which never feeds the grace clock; the record then moved
-51.0 -> -50.5 as designed. Still unverified: the grace window doing its job (needs a real
clock), decay over months, and whether 0.05/day is the right rate.

**Review follow-up (2026-09-19): the rates above were wrong by the length of a year.** The
constant comments converted with 365-day years; a Bannerlord year is 84 days, so 0.05/day
took ~24 years to empty a saturated record, not ~5.5, and the live session's "~74-95 after
two years" was that slowness, not plausibility. The lead re-set the target and asked for
the rest to be scaled from it:

- Goodwill drifts at **0.6/day**: +100 to 0 in ~167 days, just under two years.
- Grudges drift at **0.15/day**, a quarter as fast (the lead's choice over symmetric decay,
  which would have lifted a -35 breach back over the pact floor in ~25 days). -35 is back
  over -20 in ~100 days, fully forgiven in ~2.8 years; -100 takes ~8.
- War bleeds **0.6 + 0.01 per day of war** - no lower than peacetime drift, or a friendly
  pair would keep more goodwill by fighting. A median war (76 days) costs ~74; a full year
  of war ~85. Chosen over a straight x12 of the old ramp, which cost ~216 per median war.
  **Floored at -35** (`TrustWarFloor`), added after the evening live check found Vlandia and
  Southern Empire pinned at -100 by day ~94 - at the grudge rate ~6 years under the pact floor,
  a long war priced like six betrayals. -35 is one broken treaty's cost to its victim: a war,
  however long, leaves the sides ~100 days under the pact floor after the peace and ~2.8 years
  from zero, and only a breach goes deeper. (-20, the pact floor itself, was the lead's first
  pick and was dropped because one grudge tick after the peace would have cleared it.)
- Grace stays at 30 days: it measures how often a tended pair does each other a good turn,
  not how fast trust drains.
- A pair at war with no record, or with a record peace had drifted to exactly 0, used to
  be skipped; it now bleeds like any other (a missing record is read as 0 everywhere else).

The faster war bleed exposed a latent rule: `CanSign`'s trust floor also applied to the
terms that *end* a war, so a neutral pair ~27 days into a war could no longer be made to
pay tribute or kneel at the peace table. The lead exempted those terms, as the truce always
was - see F4's follow-up. Nothing in this paragraph has been run in game.

### F3 — A patron's protection is blocked exactly when a vassal is dying

**Evidence.** Battania (Khuzait's vassal) was attacked four times before its elimination on
Spring 1, 1142. The log has **no `call_to_arms` event with Battania as the caller**:

| Attack | Why Khuzait was not called |
|---|---|
| Sturgia, Winter 7, 1140 | Khuzait was already at war with Sturgia — correct, it counts as protection |
| Western Empire, Autumn 10, 1141 | **Truce** Khuzait–Western Empire signed Spring 3, 1141 (one year) |
| Southern Empire, Autumn 10, 1141 | Southern Empire joined as **Western Empire's ally** (`CausedByCallToWarAgreement`); that declaration happens inside a call to arms, and the one-step cascade guard (`CallToArms._issuing`) means nobody is called in response |
| Vlandia, Winter 8, 1141 | **Truce** Vlandia–Khuzait signed Winter 2, 1141 |

Both rules are deliberate — "an obligation cannot override a standing agreement with the target",
and "obligations reach one step" — and `Hegemony.Protection` correctly does not count these as
neglect. But together they mean a patron that has just fought its vassal's enemy is barred from
defending it for a year, and an aggressor that brings an ally gets the ally's war for free.
Northern Empire, also Khuzait's vassal, fell to Southern Empire (Diathma, by siege, Winter 5, 1140)
while a Khuzait–Southern Empire truce signed Summer 12, 1140 was in force; the war itself began
before part 3's telemetry, so whether Khuzait was ever called cannot be checked from the log.

**Candidate fixes.** Let a vassal's defence override a truce with the attacker (the attacker chose
to attack a protected kingdom); let the defender's obligations answer an obligation joiner, with
the cascade guard keyed per war rather than global.

**Lead's call (2026-09-18): neither.** The truce and the cascade guard stay - they are what stop
the cascade failures the mod exists to remove. Instead the *vassal* gets the exit, and the
*patron* gets the bill. Implemented on `feature/run-06-fixes` (builds clean; unverified in game):

- `AiDiplomacy.TryDefectToAttacker`, weekly, after the ordinary peace routes: a vassal whose
  Hold is under 40, defending in a war it is losing by 20+ war score, whose patron is **not at
  war with the aggressor** - whatever the reason - may submit to that aggressor. The submission
  is the peace (`MakePeaceAction`, cause `Defection`), the old bond is broken **by the patron**
  through `TreatyRegistry.Break` (its -35 with the vassal, -12 in every court, the BrokenTreaty
  casus belli) and the patron's other vassals take the secession-contagion Hold hit.
- The new bond is signed through the same `Hegemony.Submit` every route uses, at a new
  `HoldOnDesperateSubmission` (45, between coerced 35 and voluntary 60) - so the new patron is
  called into the vassal's *other* defensive wars the same day, which is the protection the old
  one never gave.
- Attacker-side gates are the same ones every route into vassalage passes:
  `IsStrongEnoughToHold`, `WouldTakeVassals`, `CanSign` with the old link set aside (the
  poaching route's `replacing` mechanism). Nothing is bypassed - a well-held vassal stays,
  and the patron's banked Hold is exactly how much time it has to join late.
- A player-led attacker is asked, not told, mirroring voluntary submission.

Not verified in game: needs a live war against a neglected vassal; the analyser now prints
`defection` events in the hegemony timeline.

**Review follow-up (2026-09-19).** Four corrections to the route above, none run in game:

- The old bond now goes through `Hegemony.Renounce`, not `TreatyRegistry.Break` alone: any
  pact between vassal and old patron is repudiated with it - the run-04 revolt bug, which
  would also have vetoed the casus belli the breach hands the vassal.
- **No vassal of a vassal, anywhere.** `CanSign` refused a hegemon submitting but not a
  vassal *taking* one, so this route could hand a vassal to an attacker that was itself a
  vassal (and the peace table could do the same for a defiant vassal that won). `CanSign`
  now refuses both halves. Chains already in a save are cut at session launch - the lower
  link, dissolved with no penalty (`Hegemony.DissolveChains`, event
  `vassal_chain_dissolved`), the lead's call.
- The gates live in one place, `AiDiplomacy.CanDefectTo`, and the player's Accept asks them
  again: the inquiry stays open while the campaign moves, and the first version made peace
  even when the war or the old bond was already gone. Both inquiry callbacks now catch.
- The losing-war gate reads its own `DefectionLosingScore` (20, unchanged) rather than the
  peace table's `PeaceWhitePeaceOnlyBelow`, so tuning one no longer moves the other.
- A refused offer - defection or voluntary submission - is not repeated for 42 days
  (`PlayerOfferRefusalCooldownDays`, stamped on `TrustRecord.LastOfferRefused`, save id 6);
  before this the weekly evaluation re-asked every week at -5 trust a time.

### F4 — Bug: the peace table accepts tribute a vassal cannot pay

**Evidence.** Three warnings, e.g. `Could not impose the tributary pact: Battania answers to Khuzait
and cannot sign with outsiders on its own account` (17:46:29); also Sturgia, and
`Southern Empire is already subordinate to another kingdom`.

**Cause.** `PeaceTable.IsDemandable` checks vassalage legality but not whether a `TributaryPact`
can be signed. `Apply` makes the peace first, then `ImposeTribute` fails at
`TreatyRegistry.CanSign` — **the winner signs a peace and receives nothing it was promised**.

**Fix applied** on `feature/run-06-fixes` (2026-09-18, builds clean; not verified in game —
the frozen clock cannot produce a war ending at a peace table). `IsDemandable` now refuses
`ImposeTributaryPact` when `CanSign` would refuse, asked with a new `atPeace` parameter that
skips only the "Make peace first" step — the war is still formally open when the table asks.
The same check covers `ImposeVassalage`, which had the same gap for the conditions its own
checks did not name (a loser already holding vassals, the trust floor, either side being a
vassal forbidden to treat with outsiders); the patron and strength checks keep their better
messages.

**Review follow-up (2026-09-19).** The parameter is now `settlesWar`, and it also lifts the
trust floor: a term that ends a war is exempt from it, as the truce always was (the lead's
call). The floor had always voided imposed terms silently after the peace; with F2's war
bleed it would have bound after about a month of any war, leaving the table unable to impose
a treaty at all. Tribute and vassalage imposed at the table, and a defection's vassalage, all
sign with it. Voluntary treaties still face the floor.

### F5 — Greed was never reached

**Evidence.** Highest smoothed dominance per kingdom over part 3: Khuzait 1.76, Vlandia 1.64,
Southern Empire 1.26, Sturgia 1.09, Aserai 1.02. Greed starts at 2.0. `greedy=0` in every snapshot.

**Reading (inferred).** Coalitions formed against the leader (11 AI pacts carried a balancing
pull, **all against Khuzait**), and the lead changed hands between Vlandia and Khuzait four times.
Two eliminations lowered the number of kingdoms, which raises the share needed for dominance 2 from
25% to 33%. Vlandia ends at 28.3% share, 34 fortifications, smoothed 1.62 and rising.

**Open question.** Is `GreedStartsAtDominance = 2` reachable in normal play, or is balancing
keeping everyone below it by design? Letting this run continue answers it. If not reachable, the
annexation branch is dead content.

**Lead's call (2026-09-18): lower it.** `GreedStartsAtDominance` 2.0 -> **1.25**, implemented on
`feature/run-06-fixes` (builds clean; unverified). Chosen so this run's peak - Khuzait's
smoothed 1.76 - yields greed ~0.5, exactly the `GreedRefusesVassals` line: the annexation
branch becomes reachable only at the extreme the old value was meant to mark, and stays
unreachable in an even eight-kingdom world where dominance sits near 1. Un-tuned beyond that
arithmetic; whether balancing still caps everyone below it is a run-07 question. Live check
(2026-09-19, `di_run06_resume`, `diplomacy.strength`): Vlandia smoothed dominance 1.63 reads
greed 0.38 - the branch is live, where at 2.0 the column would have been all zeros.

### F6 — Hegemony is small, voluntary, and ends by lapsing

**Evidence.** Part 3 vassalages:

| Formed | Vassal → patron | Value | Ended |
|---|---|---|---|
| Spring 17, 1142 | Western Empire → Southern Empire | 59.3 | expired Spring 1147, Hold 51 |
| Summer 14, 1143 | Sturgia → Khuzait | 56.4 | expired Summer 1148, Hold 39 |
| Summer 12, 1151 | Southern Empire → Western Empire | 73.4 | active |

None renewed (`HoldRenewThreshold` = 70; observed Hold means 50-65). Seven defiance marks, all
from vassals hovering just under 40. The last row is a role reversal: Southern Empire, the run's
biggest loser (−9 fortifications), knelt to its own former vassal.

**Reading.** This is the "not every hegemony ends in war" branch of design 04, working. Whether it
is *too* quiet is a design call: no poach, revolt or annexation happened in 13.8 years.

### F7 — Wars are still shorter than the design intends

Median 76 days against the 150-200 target from STATUS. Acceptance passes. Unchanged since run 05;
not caused by this branch.

## 6. Mechanism checklist

| Mechanism | In part 3 | Verdict |
|---|---|---|
| Patron called to defend a vassal | 1 answered (Sturgia vs Vlandia, 1143) | works when not blocked; see F3 |
| Vassal service, excused when spent | 1 answered, 1 excused (exhaustion 63.1) | works |
| Submission reads cover / patron stronger | 3 voluntary, patrons 1.2-2.2× the vassal | works |
| Withheld tribute → defiance mark | 7 marks | works |
| Joint revolt | part 1 only (monitor) | worked, prediction held |
| Poach | 0 | not exercised |
| Annexation (greed) | 0 | not exercised (F5) |
| Ambition | contributed to 25 of 36 declarations, max +7.6 | active, small |
| Balancing pull | 11 of 36 AI pacts | active |
| Side-based war valuation | isolated targets 33/36 | active; see F1 |
| Elimination handling | 2 kingdoms, 5 wars closed `Eliminated`, 0 errors | works |
| Breach settlement on submission / on load | 1 settled on load (part 1) | works |
| Yearly reports | 13 written | works |

## 7. Operational notes for the next run

- **Do not load the GABS module for unattended runs.** `Lib.GAB` crashed the game on a cancelled
  connection (CLAUDE.md §1). Launch with `pwsh ./scripts/play.ps1 -Without BirthAndDeath`.
- **Test saves need a young, healthy hero**: `diplomacy.test_set_player_age 35` cures old-age
  illness; a hero that dies with no heir ends the campaign.
- **Keep the game window focused**; background throttling slows it several-fold.
- **Analyser caveats**: the COALITIONS line "where the target's allies lowered the odds" compares
  side ratio to own ratio and is misleading (the aggressor's support raises the side ratio) — count
  `theirSupport > 0` instead. ENGINE lists `clan_changed_kingdom` without separating mercenaries:
  in part 3, 175 of 235 were `JoinAsMercenary` and 28 `LeaveByKingdomDestruction`; only 4 were
  clans joining a kingdom.

## 8. Checklist for whoever continues

1. When the lead stops the run, copy the final log over `run-06-part3-interim.log` (or add it),
   rerun the analyser, and update §3-§6 with the final numbers. Answer F5 (did Vlandia reach greed?).
2. ~~Fix F4 (small, uncontroversial) and the two analyser caveats in §7.~~ Done - `188af40`.
3. ~~Put F1, F2 and F3 to the lead with the candidate fixes.~~ Done (2026-09-18): F1 left as is,
   F2 trust decay implemented, F3 vassal-defection implemented, F5 threshold lowered to 1.25 -
   all on `feature/run-06-fixes`, all unverified in game.
4. Change one thing per run. Run 06 changed four layers at once; its numbers describe the
   combination, not any single fix. Run 07 carries four more (F2 decay, F3 defection, F5 greed
   threshold, F4 demand legality) - the same caveat applies, though three of the four only bind
   at edges the last run already reached.
5. For a clean read on formation (not collapse), start run 07 from a fresh 1084 campaign rather
   than a run-04 descendant.
