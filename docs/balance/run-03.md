# Balance run 03 — 2026-09-15/16

6.2 in-game years (Autumn 16, 1125 → Winter 2, 1131), 74 weekly snapshots, 25 minutes of real
play from `di_phase1_full`, launched from the official launcher. **Zero errors in the log.**

This is the first run after taking peace, alliances, trade agreements and vanilla's call-to-war
([design 05](../design/05-vanilla-override.md)). Raw log: `run-03.log`. Analyser output:
`run-03-analysis.txt`.

---

## The takeover worked, without qualification

| Vanilla attempt | Refused over 6.2 years |
|---|---|
| Peace between kingdoms | **3,141** |
| Alliances | **823** |
| Trade agreements | **804** |
| Call-to-war agreements | 140 |
| War declarations (the patch) | 2 |

3,141 refusals is about 500 a year. Vanilla's peace AI is relentless, and that single number
explains run 02's six-day median wars entirely — it was undoing our wars roughly twice a day.

The call-to-war counter is the interesting one: it climbed to 140 by 1128 and then **stopped**.
Vanilla's call-to-war needs an alliance to hang off, and by then the alliance web had collapsed
(below), so there was nothing left to offer.

## What ended wars, corrected

The analyser reports 2 of 10 wars as `endedBy=External`. **Both are an instrumentation bug, not
a vanilla path**, and the log proves it three lines later in each case:

```
[WAR-ENDED] endedBy=External … aggressor=Battania defender=Northern_Empire finalScore=100.0
(Peace) Battania and Northern Empire made peace: tributary pact at 500 per period; release prisoners.
(AI)    Northern Empire bought peace from Battania at exhaustion 68.5: tributary pact …
```

Both were settled by our own peace table, with real terms. The cause was lost because peaces
nest: our war ledger handles the engine's `MakePeace` event, and while handling it releases
allies who had only been called in — a second `MakePeaceAction` inside the first. The inner call
*cleared* the pending cause instead of restoring it, so the outer war read as ended by nobody.

Fixed the same evening (`Telemetry.NotePeaceCause` now returns the previous value and
`RestorePeaceCause` puts it back). The corrected tally:

| Ended by | Count | Run 02 |
|---|---|---|
| Our peace table | **4** | 13 of 167 (7.8 %) |
| Dormant (new rule) | 4 | — |
| Follower released | 2 | 9 |
| **Anything outside our systems** | **0** | **145 of 167 (86.8 %)** |

86.8 % → **0 %**. That was the run's headline question.

## The concession ladder is alive

Run 02: 13 settlements, 13 white peaces, not one concession in 13 in-game years. Run 03:

| Settlement | Terms |
|---|---|
| Southern Empire ← Northern Empire | **cede Mecalovea Castle, release prisoners** (score 33.3, package 30) |
| Battania ← Northern Empire | **tributary pact at 500/period, release prisoners** (score 100) |
| Khuzait ← Sturgia | **tributary pact at 500/period, release prisoners** (score 100) |
| Aserai / Western Empire | white peace at 77 days, score −0.3 — a genuine stalemate, correctly white |

Three of four had real terms, and the castle case lands exactly inside the intended window:
war score 33.3, so the winner wanted ≥ 16.7 and could take ≤ 33.3; a castle plus prisoners is
30.

## War length: fixed for wars that end, broken for wars that do not

| | Run 02 | Run 03 |
|---|---|---|
| Median length of **ended** wars | 6 days | **60 days** |
| Wars ending in the first week | 51 % | **0** |
| Chosen wars declared per year | 11.3 | **1.6** |
| Wars with any fief change | 39.5 % | 50 % |

Ten declarations in 6.2 years, all with a stated casus belli and paid for at 52–80 influence.
The one-war-at-a-time cap plus the influence cost cut the rate by seven times.

**But the wars still running at the end had been running for an average of 407 days, the
longest 485.** At 485 days, 38.8 of that war's 43.6 exhaustion is the 0.08/day that elapsed
time alone contributes — meaning roughly five points in sixteen months came from actual
fighting. Those wars were dead on their feet, and nothing could close them: they had crept
past the 300-casualty dormancy cap years earlier, and no kingdom in a war can sign a treaty.

That is the mechanism behind the rest of the world state:

| | Autumn 1125 | Winter 1131 |
|---|---|---|
| Kingdoms at war | 4 of 8 | **8 of 8** |
| Alliances | 10 | **1** |
| Truces | 10 | **0** |
| Tributary pacts | 8 | 2 |
| Live claims | 101 | 59 |

Weeks with every kingdom at war: **54 of 74 (73 %)**. The map went from locked in pacts to
locked in war, by the same mechanism in reverse — wars that cannot end crowd out everything
diplomacy would otherwise do.

**Fix applied:** dormancy is now judged on casualties **per day** as well as in total. Under
3/day after 42 days counts as not being fought. Measured rates: the four wars the old rule
caught ran at 1.9, 5.8, 3.0 and 1.9 per day; the 400-day survivors at roughly 1. The threshold
sits inside that spread rather than cleanly between the two groups, so it is **provisional** and
run 04 should be read with that in mind.

## Smaller observations

- **Vlandia is an outlier.** Four of its five declarations were `Conquest` at legitimacy 0.20,
  and three of the four dormant wars were its — it declares wars it then does not fight. One
  declaration scored **value 199** against a threshold of 18, which is far outside the 18–72
  range of every other declaration and suggests `LandHunger` can blow up for a kingdom with a
  large strength share and few fiefs. Worth a look before run 04.
- **Tribute demands repeat.** `Khuzait imposed a tributary pact on Northern Empire` appears
  three times, `Vlandia … on Western Empire` three times. Pacts lapse and are re-imposed on the
  same pair, which reads as mechanical rather than political.
- **Vassalage is still 0**, as expected: it has no route into play until 1.9.
- Every ended war carried a casus belli: Conquest 3, ReclaimAncestralLand 3, DefendAlly 3,
  BrokenTreaty 1. No naked aggression at all.
- Nobody was eliminated; 8 kingdoms throughout.

## What run 04 has to answer

1. Do the 400-day wars disappear, and does the world come off 73 % total war?
2. Does `endedBy=External` stay at zero now that the instrument is honest?
3. Do alliances and truces come back once kingdoms can leave wars?
4. Is `Dormant` now *too* eager — are real wars being closed as dormant? Watch for
   `endedBy=Dormant` on wars with meaningful fief changes or war scores.
5. Where does the war rate settle, with the cap and the shorter wars interacting?

Two constants are still deliberately unapplied, awaiting this measurement:
`ExhaustionSeekPeace` 60 → 70, and the pact thresholds `AiAllianceThreshold` 70 → 82 /
`AiDefensivePactThreshold` 55 → 65. The second pair looks much less necessary than it did after
run 02 — the alliance web collapsed on its own.
