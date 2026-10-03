# Balance run 12 - the run 11 follow-up, measured

2026-10-03. Save `di_fresh_1084` (the run 08-11 baseline), the player parked in a town out of every throne,
`test_set_speed 50`, through GABS and watched the whole time. **12.1 campaign years** (Summer 3, 1084 to
Spring 1096), **0 `ERROR` and 0 `WARN`**. Commit `22f8768`. Log: [run-12.log](run-12.log); analyser output:
[run-12-analysis.txt](run-12-analysis.txt).

**The DLL was the local build, not the v1.4.8-reference one** run 11 used for Part B: the copy that installs it
over the deployed one was refused by the permission classifier and was not worked around. Same source; the
local build differs only by the `SiegeAmbush` line `compile-check.sh` drops. The startup line read
`Harmony patched 7 methods`.

**A first attempt crashed** at game day 34 (Autumn 16, 1084): `Lib.GAB`, `InvalidOperationException:
Connection is not active` in `GabpServer.SendErrorResponseAsync` (dump read with `tools/DumpProbe`), the GABS
crash of CLAUDE.md §1. The mod log stopped clean, no error. Cause on my side: a `games_connect` over a
`started_bridge_pending` start, the one thing §1 says not to do. The run was restarted from the save; the
second start connected at once and no bridge call was made while it ran.

## 1. What was asked: the three espionage changes

| Change | Run 11 | Run 12 | Reads as |
|---|---|---|---|
| AI networks, median strength at year 2 (bar > 30) | 1.4 | **3.5** - and 43.1 at year 3, 44.0, 40.6, **80.8**, 51.0, 50.6, 45.7, 88.8, 99.8, 84.6 | **Bar still FAILS at year 2; the networks now do grow, a year later.** From year 3 the median never falls under 40 |
| AI operations in the run | 27 in 10.2 years | **131 in 12.1** (124 rolled) | 4.1 times as many per year (10.8 against 2.6) |
| Operation types the AI launched | SpreadDissent, StealTreasury | + **BribeLord 13, SabotageGarrison 1** | Never: ForgeLetters, Assassinate |
| Holders with a handler at each year's end | 100% | 83-100%, every year PASS | held |
| Exposures / wars from them | 1 / 1 | **6 / 1** (Gundaroving caught in Southern Empire, Winter 11, 1093) | one war in six, citing `EspionageExposed` |
| `forced-party` | **3.0 a year** | **0** | the patch holds, over 12 years |
| `party` (bar 0) | 0 | **1** (Bolat, Urkhunait, Summer 7, 1085, "took command of a party") | not the path the patch covers - see 2 |
| `governor` (bar 0) | 0 | 0 | held |
| Errors | 0 | 0 | |

What the AI did with it: StealTreasury 100 times (the cheapest return, 3.6% exposure overall in run 11 and
2.7% here), SpreadDissent 17, BribeLord 13 (overall exposure 2.4%), one SabotageGarrison at 8%. Exposure odds
at launch stayed under the 10% ceiling of decision 13. An exposure on a SpreadDissent or a StealTreasury
cost the owner its handler (captured) and a casus belli to the victim.

## 2. Findings

1. **The year-2 bar is not met, and cannot be at these numbers.** A new network reaches the 30 that
   SpreadDissent needs in about the third year, not the second. Either the bar moves to year 3, or the AI
   starts a year earlier (the first operations of this run were at year 2; none in year 1). It is the lead's
   call; nothing is wrong with the mechanism.
2. **One `party` loss, and it is the path 3.8's patch says it does not cover.** `took_command_of_a_party`
   is not `forced-party`: the veto was never asked. `EmptyClanPartiesCampaignBehavior` respawning a lord's
   cached empty party is the candidate (story 3.8 §12, the patch header's "not covered"). One in 12 years;
   not chased.
3. **StealTreasury is 76% of every AI operation.** Seventy-nine successes took gold from rulers all run. It
   is the best value-per-cost option for the AI (value up to 5 against SpreadDissent's 1.5) and so it
   crowds the others out. Whether that is the AI espionage the lead wants is the lead's call (§3).
4. **`test_player_army` was not exercised by this run** (no player army in an AI-only run). Still only
   shown to raise an army with the player ruling Khuzait.

## 3. Seen, not asked for: the legitimacy changes (design 02 §4)

The three changes landed in the same build, so this run measures them, and one result needs the lead.

| | Run 11 | Run 12 |
|---|---|---|
| Legitimacy at the end | 0-24 in 7 of 8 realms | Northern Empire 1.1, Southern Empire 0.0, Battania 12.7, Sturgia 15.9, Aserai 21.6, Western Empire 24.0, **Khuzait 47.7, Vlandia 69.0** |
| Sources | 22 treaty breaks (-325), 74 fiefs lost | wartime step **113** times (+1 each), oath **8** times (-8), 6 ordinary breaks (-20), 5 peace years |
| Claimants | none | **8 arose** (Northern Empire x2, Aserai x2, Battania, Western Empire, Southern Empire, Sturgia) |
| Civil wars | **0** | **14**: Aserai 7 (the same claimant, Adram, every time), Northern Empire 3, Battania 2, Sturgia 2 |
| How they ended | - | **12 stalemates and 1 dissolved**, 0 won by the crown or the rising |

**Every civil war ends in a stalemate, the claim stands, and the war starts again after its cooldown**, so
Aserai fought one civil war after another for years over one man. A crown at 0-30 with a claimant now spends
most of its time at war with itself: this is the other side of "no civil war in ten years", and it is too
far the other way. Options, none built: retire the claim when a war ends in a stalemate; a longer cooldown
after a stalemate; or a claimant who loses (stalemate counts) cannot be minted again for a year. The
legitimacy numbers themselves moved the way the lead asked (Khuzait and Vlandia are healthy, the wartime
trickle ran 113 times).

## 3a. The civil-war loop, answered 2026-10-03

The lead took all three remedies: a stalemate retires the claim, the cooldown after a stalemate is 252 days
instead of 84, and the stalemated claimant cannot be raised again for 84 days (design 02 §4). **Seen live** on
`di_phase1_full`: Battania at legitimacy 0 raised Tegan's claim; `test_start_internal_war`, then
`test_end_internal_war Battania | stalemate`, then one daily tick: no claimant stands, nobody is minted again,
and `internal_wars` reads "an internal war ended recently (252 days to go)". **Not yet seen over a long run**:
that needs another Part B-length run to show the number of civil wars falling to a few.

## 4. For the lead

1. Year 2 or year 3 for the AI network bar (finding 1).
2. Is StealTreasury being three quarters of the AI's operations what you want (finding 3)?
3. **The civil-war loop (§3):** which of the three remedies, or another. It is the one result here that is a
   defect of the changes I made on 2026-10-02, not a measurement.
4. Not measured here, still open: the vassal-oath price in a player-run state, AC2a.
