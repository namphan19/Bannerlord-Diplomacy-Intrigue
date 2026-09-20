# Why no kingdom became a vassal — 2026-09-19

Two live runs on the run-06 review build (`di_review_0919`, then `di_review_0919_b`; together
Winter 1156 → Winter 1162, ~6 in-game years) produced **zero vassal links**. This note explains
why, from code and from every log this project has kept. Nothing in it has been changed yet.

## The four doors into vassalage

| Route | Precondition | Could it fire here? |
|---|---|---|
| Voluntary submission (`AiDiplomacy.TrySubmit`) | `SubmissionValue` ≥ 55 against a patron not at war with the candidate | Yes, in principle - never reached 55 |
| Imposed at the peace table (`ConcessionLadder` → `ImposeSubmission`) | War score ≥ 95 (vassalage 90 + prisoners 5), winner stronger, winner not greedy | **Has never fired in any run of this project** |
| Poaching (`Hegemony.TryPoach`) | The suitor must already be a hegemon | No - needs an existing link |
| Defection (F3) | The vassal must already have a patron | No - needs an existing link |

So with no link on the map, everything rests on the first two - and one of them is dead.

## Evidence from all runs

Every vassal link this project has ever recorded came from voluntary submission:

| Log | Voluntary | Imposed at the table |
|---|---|---|
| run-04 | 9 | 0 |
| run-05 | 3 | 0 |
| run-06 parts 1-3 | 5 | 0 |
| live 2026-09-19 (both runs) | 0 | 0 |

(Counted from `submitted to` / `route=voluntary` lines and the `as a vassal at hold` line that
`ImposeSubmission` writes.)

## 1. The peace table cannot reach vassalage (structural, pre-existing)

Run 06 part 3 ended **7 wars at war score 90 or above**. Six went through the peace table; five
paid a tributary pact and one ceded a town. Not one took vassalage. Two rules make that the only
possible outcome:

- **Losers buy peace cheapest-first, and tribute always satisfies the winner.** When the loser
  sues (`TryBuyPeace` - 18 of the 22 settlements with terms in run 06 part 3), it walks the ladder
  upward: prisoners → indemnity → **tributary (65)** → vassalage (95) → land. The winner accepts
  any package worth at least half the war score (`PeaceWinnerMinimumShare` 0.5). The score is capped
  at 100, so a tribute at 65 is always enough, and the ladder never reaches vassalage.
- **Winners collecting walk the ladder backwards, and land is at the end.** `TryCollectPeace`
  reverses the list "largest package first", but the list is not sorted by cost: single fiefs
  (castle 25, town 45) come *after* vassalage (90). The reverse walk therefore asks for a town or
  a castle first, and with a territorial casus belli - ReclaimAncestralLand or Conquest, 17 of the
  18 wars that ended in the two live runs - one is always demandable. The comment on the vassalage rung ("offered ahead of
  land") is true only in the loser's direction.

Vassalage is reached only when tribute *and* every fief are undemandable at a score of 95+ - which
has not happened in any run.

## 2. Voluntary submission lost its trust term (caused by F2)

`SubmissionValue` = threat (≤ 50) + reach (≤ 40) + weariness (≤ 30) + **trust (≤ 20)** − pride
(≤ 50) − culture (20 if different) − dread (≤ 25). Threshold 55.

All three submissions in run 06 part 3 carried an almost maximal trust term, because trust was
saturated near +100:

| Vassal → patron | Value | Trust term | Same value at today's trust (~14 → +2.8) |
|---|---|---|---|
| Western Empire → Southern Empire | 59.3 | +20.0 | **42.1** - would not submit |
| Sturgia → Khuzait | 56.4 | +19.6 | **39.6** - would not submit |
| Southern Empire → Western Empire | 73.4 | +20.0 | 56.2 - would still submit, barely |

(Counterfactual arithmetic on logged terms, not a measurement.) The submissions of run 06 were
riding on the saturated trust that F2 was designed to remove. With mean trust settling near 14,
that ~17 points is gone, and many pairs now sit below the −20 floor, which blocks signing
altogether (measured below).

## 3. The threat term rarely pays in this world

Threat is the big positive term, but it is multiplied by **cover**: how much of the candidate's
danger the patron could actually fight. The patron must be strong relative to the *whole* threat,
and free of treaties with the attackers. In this world both conditions fail:

- The attackers are the strong kingdoms. The would-be patrons are bound to them by tributary
  pacts, defensive pacts and non-aggression pacts - all of which forbid war, so that share of the
  threat is not coverable.
- `TrySubmit` skips any patron at war with the candidate. Southern Empire at the peak of its crisis
  was at war with **all five** other kingdoms, so it had no patron to kneel to at all.

Measured in game on `di_review_0919_b` (Winter 15, 1162), `diplomacy.submission_value`:

| Candidate → patron | Threat (cover) | Reach | Trust | Pride | Culture | Dread | Value |
|---|---|---|---|---|---|---|---|
| Western Empire → Aserai | +6.4 (0.13) | +31.6 | −1.9 | −16.0 | −20.0 | 0 | **4.0** |
| Western Empire → Vlandia | +8.6 (0.17) | +31.4 | −5.7 | −16.0 | −20.0 | −2.8 | −0.6, and blocked by the trust floor |
| Western Empire → Khuzait | 0 | +25.8 | −1.3 | −16.0 | −20.0 | −7.8 | −15.6, and at war |
| Southern Empire → Western Empire | 0 (at peace) | +33.7 | −0.7 | −11.2 | 0 | 0 | 29.8 |
| Southern Empire → Khuzait | 0 | +30.0 | −6.3 | −11.2 | −20.0 | −7.8 | −7.3, and blocked by the trust floor |

Western Empire was losing to Sturgia at +92 when measured - the most desperate kingdom on the
map - and its best offer was worth 4 against a threshold of 55.

## 4. Smaller contributors

- **Greed (F5).** Vlandia, the strongest kingdom and the obvious patron, sat at greed 0.52-0.58 from
  1157 to 1159, past `GreedRefusesVassals` (0.5): it would take no vassals in exactly the years it
  was most worth kneeling to.
- **Culture** costs 20 against every patron of another culture - most pairs on this map.

## Summary

| Cause | Kind | Effect |
|---|---|---|
| Peace-table ladder order + winner's 50% rule | Structural, pre-existing | Imposed vassalage unreachable - 0 in project history |
| F2 trust decay | New, from run-06 fixes | Removes ~17 points from every submission value; run 06's links would mostly not have formed |
| Cover rarely available (treaty web, dogpiles) | World state + formula | Threat term, the main positive, stays near zero |
| Greed ≥ 0.5 for the strongest kingdom | New, from F5 | Top patron refuses vassals |

## Options for the lead (not implemented)

1. **Make the peace table able to reach vassalage.** Two independent levers, one per path:
   - *The loser's path* (18 of 22 settlements): let the winner refuse a tribute when the score
     entitles it to more - e.g. at score ≥ 95 the winner wants at least the vassalage cost, not
     half the score. Only the top of the scale changes.
   - *The winner's path* (4 of 22): move the vassalage rung after the fiefs in
     `ConcessionLadder`, so the reverse walk asks for submission before land. On its own this
     does not touch the loser's path - the loser still offers tribute first.
   The first lever does most of the work; the second matters only when the winner sues first.
2. **Re-balance submission for the post-F2 world**: lower `AiSubmissionThreshold` (55) by about the
   trust that disappeared (~15-17), or raise the other weights. A tuning decision after F2, not a
   return to saturated trust.
3. **Let cover count the patron's willingness, not only its legal freedom** - out of scope for a
   balance pass; recorded because it is why the most endangered kingdoms get the least from
   submitting.

Unverified: the counterfactual table in §2 is arithmetic on logged values; the causes in §3 are
read from one world state. A run after any change would be the measurement.
