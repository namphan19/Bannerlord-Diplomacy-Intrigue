# Balance run 06 — interim analysis, 2026-09-17

**Status: interim.** The run was still going when this was written. It covers **Winter 1136 to
Winter 9, 1153 (~17 in-game years)** across three logs. Every number below is measured from those
logs; anything inferred is marked as such. Nothing here has been decided by the lead yet —
findings carry *candidate* fixes, not applied ones.

Written for whoever picks up the balance work next. §8 is the checklist.

---

## 1. What this run measures

The first campaign with all of branch `feature/hegemony-structural-fixes` in play
([STATUS.md](../STATUS.md) "What to do next" §3, §3b, §3c):

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

### F4 — Bug: the peace table accepts tribute a vassal cannot pay

**Evidence.** Three warnings, e.g. `Could not impose the tributary pact: Battania answers to Khuzait
and cannot sign with outsiders on its own account` (17:46:29); also Sturgia, and
`Southern Empire is already subordinate to another kingdom`.

**Cause.** `PeaceTable.IsDemandable` checks vassalage legality but not whether a `TributaryPact`
can be signed. `Apply` makes the peace first, then `ImposeTribute` fails at
`TreatyRegistry.CanSign` — **the winner signs a peace and receives nothing it was promised**.

**Fix (not controversial, not yet applied).** In `IsDemandable`, refuse `ImposeTributaryPact`
when `TreatyRegistry.CanSign(winner, loser, TributaryPact)` would refuse — one resolver.

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
2. Fix F4 (small, uncontroversial) and the two analyser caveats in §7.
3. Put F1, F2 and F3 to the lead with the candidate fixes; they are design decisions, and F2
   belongs with the deferred trust/grim-trigger item in STATUS §3.
4. Change one thing per run. Run 06 changed four layers at once; its numbers describe the
   combination, not any single fix.
5. For a clean read on formation (not collapse), start run 07 from a fresh 1084 campaign rather
   than a run-04 descendant.
