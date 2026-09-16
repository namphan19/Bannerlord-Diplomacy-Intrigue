# Balance run 04 — 2026-09-16

5.1 in-game years (Winter 8, 1131 → Winter 8, 1136), 61 weekly snapshots, ~30 minutes of real
play from `di_phase1_full`, launched from the official launcher. **Zero errors in the log.**

This is the Phase 1 acceptance run: the first with all of 1.1–1.11 in play at once, and the
first campaign in the project's history in which a hegemony formed on its own. Raw log:
`run-04.log`. Analyser output: `run-04-analysis.txt`.

Two things changed between run 03 and this one: `ExhaustionPerDayAtWar` 0.08 → 0.30, and the
cap on the war valuation's strength term (`WarValueMaxStrengthAdvantage`, committed the same
morning).

**The starting state matters for every number below.** This run began where run 03 ended: all
eight kingdoms at war, five of those wars already 400–500 days old and unable to close. It is
a stress test of the fixes, not a fresh campaign, and the early figures are dominated by that
backlog clearing.

---

## The two fixes both worked

**Every long war ended.** All five of the wars run 03 could not close died in the first
in-game year of this run, each at the moment the calendar pushed it past the peace threshold:

| War | Days | Exhaustion at peace | Settlement |
|---|---|---|---|
| Sturgia / Western Empire | 498 | 62.1 / 69.5 | white peace |
| Southern Empire / Aserai | 484 | 58.5 / 60.1 | tributary pact, prisoners |
| Khuzait / Battania | 470 | 60.4 / 60.2 | tributary pact, prisoners |
| Battania / Aserai | 443 | 47.2 / 61.2 | **cede Qasira**, prisoners |
| Vlandia / Northern Empire | 414 | 61.4 / 60.9 | tributary pact, prisoners |

That is `ExhaustionPerDayAtWar = 0.30` doing exactly what it was raised to do, confirmed
against the mechanism rather than inferred from an average.

**The war-value cap holds.** Ten declarations, values **18–81**, against run 03's 18–199. The
three highest-valued wars of the run all carried `ReclaimAncestralLand` at legitimacy 0.70
(81, 73, 64) — a claim now outranks opportunism, which is the entire point of capping the
strength term. No declaration in the run reached the old runaway shape.

## Acceptance criteria: met

| Criterion | Run 03 | Run 04 |
|---|---|---|
| Wars average under ~3 years | 60d median, five open at 407d | **204d mean, 66d median**, PASS |
| An alliance forms and holds | web collapsed to 1 | alliances present **56 of 61 weeks**, max 3 |
| No permanent total war | **73 %** of weeks with every kingdom at war | **3.3 %** (2 of 61 weeks) |
| Stability | 0 errors | **0 errors** over 5.1 years |
| Nothing outside our systems ends wars | 0 | **0** — `endedBy=External` stayed gone |

| Ended by | Count |
|---|---|
| Our peace table | **12** of 15 |
| Follower released | 2 |
| Dormant | **1** |

`Dormant` closing 1 war of 15 answers run 03's fourth question: it is **not** too eager now.
The one it caught was an obligation war at exhaustion 14.1, war score 0.0, zero casualties
between the parties over 47 days — precisely the case the rule exists for.

Terms conceded across 15 settlements: 6 tributary pacts, 2 fiefs ceded (Qasira, Rhemtoil
Castle), 5 white peaces, 2 follower releases. The concession ladder is fully alive; run 02's
13-of-13 white peaces are three runs behind us.

## Wars are now shorter than the design wants

Excluding the five carried-over wars, the wars that both started and ended inside this run ran
**47, 52, 52, 59, 63, 63, 66, 91, 202 days — median 63**. The design target written into
STATUS before the run was a median of **150–200**.

At 0.30/day the calendar alone needs 200 days to reach the threshold of 60, so roughly two
thirds of the exhaustion in a 63-day war is still coming from casualties. Run 01's finding has
not gone away, it has only been rebalanced: raising the calendar rate made long wars end, but
did not stop hard fighting from ending a war in two months. Whether that is wrong is a design
call — the acceptance bar passes comfortably either way.

## Hegemony: it forms, and it forms far too easily

Every part of the machine ran, for the first time in a real campaign:

| Event | Count |
|---|---|
| Submissions | **9** |
| Poaches (a rival taking a vassal off its patron) | 5 |
| Defiance marks | 10 (four vassals reached 2 of 2) |
| Tribute withheld | 193 |
| **Revolts** — a vassal renouncing its patron at breaking point | **2** |
| Renewals at end of term | 0 |
| Collapses (patron destroyed) | 0 |

And it produced the outcome STATUS named as the failure mode:

| | Winter 1131 | Winter 1136 |
|---|---|---|
| Hegemons | 2 | **1** |
| Vassal links | 4 | **7** |
| Average Hold | 56.0 | **30.4** |
| Standing defiance marks | 0 | 6 |
| Kingdoms at war | 8 of 8 | 2 of 8 |

**Northern Empire ends the run holding all seven other kingdoms.** The map did not collapse to
two kingdoms — all eight are alive and none was ever eliminated — but it collapsed to one
sphere, and the peace in that last column is peace by subjugation, not by diplomacy. Any
reading of the war numbers above has to carry that qualification.

**Four kingdoms knelt in the first ninety seconds of play**, on the first weekly evaluation
after load. The submission values say why:

```
Battania  → Northern Empire   threat +140.0 ... => 150.5  (submits at 55)
Aserai    → Northern Empire   threat +140.0 ... => 137.6  (submits at 55)
Sturgia   → Northern Empire   threat  +56.5 ... =>  63.6  (submits at 55)
```

Of the nine submissions, values ran **55.5 to 166.9 against a threshold of 55**, and `threat`
sat at its ceiling of +140 in three of them. This is the same shape as the war-valuation bug
fixed that morning: one term large enough to make every other term decorative. Flagged as
provisional in STATUS before the run; it is no longer provisional.

**Poaching has no cost and no cooldown.** Battania changed patron four times in about two and
a half in-game years, each time breaking its vassalage — which hands the abandoned patron a
casus belli — and signing with the rival in the same second:

```
Winter 1131  Northern Empire takes Battania
Winter 1131  Southern Empire takes Battania from Northern Empire (value 151)
Spring 1132  Northern Empire takes Battania from Southern Empire (value 151)
Summer 1132  Southern Empire takes Battania from Northern Empire (value 159)
Spring 1134  Northern Empire takes Battania from Southern Empire (value 158)
```

A vassal is poachable below Hold 40 and the average Hold across the run was 30, so nearly
every link was on the market nearly all the time. The valuation does not move between flips
(151 → 151 → 159 → 158) because nothing about the act changes it: the vassal pays no price for
breaking, and the new patron pays nothing for taking.

**Tribute effectively never arrives.** Passive resistance triggers below Hold 40, and with the
average sitting at 30 the log records **193 withheld payments**. A payment that succeeds is
not logged at all, so this run cannot say what the paid-to-withheld ratio was — but a hegemon
whose vassals are nearly all below the threshold is collecting very little, which makes the
income side of the bargain close to inert.

**Nothing ends a hegemony.** Two revolts fired and neither reduced the sphere: both kingdoms
were back under Northern Empire within the run, one of them twenty seconds later. Hold settled
into a band of roughly 15–36 — deep in sullen territory, never long enough below 15 to start a
revolt clock, and drifting slightly *upward* at the end (26.2 → 30.4 over the last quarter).
The equilibrium is a permanent sullen empire, which is more static than a collapse would be.

## A real bug: a revolt can be silently refused

```
11:12:52  (Treaty)   Sturgia broke Vassalage with Northern Empire - Northern Empire now has a casus belli.
11:12:52  (Enforce)  Refused war via kingdom decision: Sturgia -> Northern Empire because the
                     DefensivePact with Northern Empire forbids it.
11:12:52  (Hegemony) Sturgia renounced Northern Empire and declared war for its independence
                     after 30 days at breaking point; 6 other vassal(s) took note.
```

No war opened. Twenty seconds later Sturgia submitted to Northern Empire again.

Cause, traced in the code: `Hegemony.Revolt` breaks the **vassalage** and then declares war
inside `TreatyEnforcement.BeginSanctionedWar()`, but `DeclareWarAction_Veto_Patch.Allow` does
not consult that flag — it consults `WhyWarBlocked`, which still saw a live **DefensivePact**
between the same two kingdoms. The vassalage was the only treaty the revolt cleared.

Two defects, one visible and one not:

1. A deliberate act of defiance — the thing `TreatyEnforcement`'s own class comment promises
   can never be blocked — is blocked by a second treaty with the same party.
2. `Revolt` logs and announces the war of independence without checking that it happened, so
   the log asserts a war that does not exist. Half of this run's revolts are recorded wrongly.

Neither is a crash and neither affected the acceptance numbers. Both are Phase 1 correctness.

## What run 05 has to answer

Nothing here is a balance decision the lead has taken yet; these are the questions the run
raised, in the order they matter.

1. **Does submission stop being automatic?** The `threat` term needs the same treatment the
   war valuation's strength term just had. Until then a hegemony forms on the first weekly
   tick of any campaign that starts from an uneven map.
2. **Does a poached vassal cost anyone anything?** A break with no price and no cooldown turns
   a political event into a metronome.
3. **Can a hegemony end?** Revolt at Hold below 15 for 30 days was reached twice and neither
   instance changed the map. Either the trigger or the aftermath needs to bite.
4. **Do wars want to be longer?** Median 63 days against a design target of 150–200. Passing
   the acceptance bar is not the same as matching the intent.
5. **Does tribute ever arrive?** Add a log line for a payment that succeeds, so the next run
   can answer the question this one could not.

Two constants remain deliberately unapplied, and after this run both look less urgent than
they did: `ExhaustionSeekPeace` 60 → 70 would make wars longer, which cuts against nothing
here, and the pact thresholds `AiAllianceThreshold` 70 → 82 / `AiDefensivePactThreshold`
55 → 65 address an alliance web that has now behaved for two runs running.
