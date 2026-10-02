# Run 11 — Phase 3's validation and acceptance

Story 3.12, run on 2026-10-02 (20:03-21:46 real) through GABS, monitored throughout, by the tech lead.
The procedure is [run-11-runbook.md](run-11-runbook.md); the lead approved its §0 the same day. It
validates the checks stories 3.8-3.11 left open (§3), plays the acceptance scenario (§4, Part A) and
measures AI espionage over ten years (§5, Part B).

| | |
|---|---|
| Commit | `0554b20`, then two cosmetic fixes found by this run (§6) |
| Game | v1.5.3 (Steam beta), BLSE Standalone through `games_start`, NavalDLC not loaded |
| DLL | §3: the local build (`deploy.ps1`). Parts A and B: the v1.4.8-reference build from `scripts/compile-check.sh`, SHA-256 `4A86C79304D7BB1F63FAFEC8924190298B12DCC36E61EF5429EB3FE596EE0792` (`[RUN] built=2026-10-02_20:37:17`) |
| Part B | `di_fresh_1084`, Summer 3, 1084 → Summer 15, 1094: **10.2 years**, 123 weekly snapshots, 69 wars ended, `test_set_speed 50`, the player (no kingdom) parked in Myzea, then Saneopa after a siege |
| Errors | **0 `ERROR`, 0 `WARN`** in every session; Harmony patched 6 methods in every session |
| Files | `run-11.log` (Part B), `run-11-analysis.txt`, `run-11-shots/`; end state `run11_end` |

Screenshots: GABS keeps only its last ten, so most of §3's were rotated out before they were copied.
Each was read when it was taken and is described below; the ones kept are in `run-11-shots/`.

## 1. Headline

1. **The acceptance scenario happened between two AI realms, with no lever.** Northern Empire's
   ruling house was caught spreading dissent in the Western Empire (Winter 19, 1087); 61 days later
   the Western Empire declared war on the Northern Empire **citing `EspionageExposed`**, through a
   vanilla kingdom decision. 1 exposure in ten years, 1 war from it.
2. **3.8 holds the handler; the economics then starve the network.** Every year 6-7 of 6-7 eligible
   AI ruling houses held a handler, all at their post, with `party` and `governor` losses at **0**.
   But the median AI network was **1.4 at year 2** (bar: above 30) and only reached ~28 in years 7-8.
   A network aimed at a realm at peace grows 1-2 a week; one aimed at a realm at war shrinks, because
   war halves the gold's effect and counter-intelligence, attrition and daily decay do not halve - and
   the AI's target score ranks "at war" first. **27 AI operations in ten years**, only SpreadDissent
   and StealTreasury; no bribe, forgery, sabotage or assassination was ever launched.
3. **`forced-party` ran at 3.0 a year** (30 in 10.2 years), exactly on 3.8 R5's line. It goes to the
   lead as R5 asks.
4. **Every story check that could be staged passed** (§3), and two cosmetic defects were found and
   fixed (§6). The player's death by assassination runs vanilla's own heir and Game Over paths with no
   error.
5. **Part A played A1-A4 and A3**, with the border lord flipped on the real clock and the bought house
   rising with the rebels; **A5 could not run its 168 days** (§4) and A6 was seen only in data.
6. Two balance observations outside espionage (§7): **legitimacy fell to 0-24 in every realm** over
   the ten years, and **no internal war started** in them.

## 2. Phase 3 acceptance, item by item

| Item | Result |
|---|---|
| A player can flip a border lord through bribery | **Met** - Part A A2: Impestores of Northern Empire (Amprela, next to Khuzait's Makeb) bought through the Intelligence tab, rolled on its own due day at 65%, band Self-interested → Sullen, `foreign gold −20` |
| A caught operation drags them into a war they did not choose | **Met between AI realms** (Part B, headline 1). **Not seen with the player as the target**: A5 stopped at day 48 of 168 (§4) |

## 3. The checks 3.8-3.11 left open

### 3.1 Story 3.8

| Check | Result |
|---|---|
| AC1, a traveller refused | **Pass** - `test_assign_handler` on Sora (state `Traveling`) → "Refused: Sora is on the way to a post."; Apolytea (`Released`) → "has just been let out of captivity." |
| AC2, the veto over time | **Pass in Part B** (ten years, not the three weeks on `di_run07_1104` the runbook named - that session was folded into Part B): `party` 0, `governor` 0 |
| AC3, the station | **Pass in Part B**: every handler at its post at every year's end (6/6 … 7/7), held by the daily return - **3,690 re-stations in ten years** (363 a year), because vanilla's path home never asks the move veto (story 3.8 §10) |
| The player's handler and parties | **Pass**, A/B on `di_36_espionage_test`: Morynon posted → the Clan screen's `CanCreateNewParty` False; recalled, same town → True |
| Lilizha's day-one governorship | **Explained, not proven**: at load she governed nothing and was `Active` at Ocs Hall; on the first daily tick she was governor of Mazhadan Castle, standing in it - a teleport across the map in under a day. That is vanilla's queued governor teleport, saved on 2026-09-25 before R1 existed; the queue cannot be read from the bridge. Part B on a fresh save had 0 governor losses |
| AC4, `forced-party` rate | **3.0 a year** (30) - on the line; for the lead |
| AC5, nothing else moved | `warParties` per AI realm rose 21.0 → 28.6 over the ten years; no earlier run recorded it, so it is a baseline, and it shows no fall |

### 3.2 Story 3.9, AC4

The old shaky-crown rule (legitimacy under 50, or a standing pretender) applied by hand from
`diplomacy.legitimacy` and `pretenders`, against `ai_espionage`'s "worth subverting" and `court_bands`:
**8 of 8 realms agree on `di_36_espionage_test`** (Battania at 25 with Aradwyr the only shaky one) and
**8 of 8 on `di_phase1_full`** (all at 60, none shaky - a weak test, said so). `di_pretender_test`'s
boundary was the dev's own (41 → Questioned). In Part B the AI's marks read as bands: "dey Fortes of
Vlandia is ready to break - among the great houses of the realm ... chosen because it weighs more in
that court than any other".

### 3.3 Story 3.10

On `di_phase1_full` with the player (Culharn) made a vassal of Battania, and an Aserai network on
Battania through the normal `test_assign_handler` (that ruling house had a free member).

| Check | Result |
|---|---|
| AC2a, the AI names the player as the mark | **Not run.** No tool or lever makes the player lead an army, and `FieldCommander` reads only army leaders |
| AC2b, a companion killed | **Pass** - Griff Ironbelly dead; red "Griff Ironbelly is dead. Nobody has been punished for it." |
| AC2c, the player killed, with an heir | **Pass** - married to Mariwyn first; vanilla's "Assign As Clan & Faction Leader" offered her, confirm, she became the main hero; `player_died detail=Murdered`; 0 errors, no native error |
| AC2d, the player killed, no heir | **Pass** - vanilla's "Clan Destroyed ... Your journey ends here", then "Your clan is no more!" statistics; 0 errors. The log has no `player_died` on this path: vanilla never calls `MakeDead` there (§6) |
| AC3, the offer | **Pass** - "Letters in the crown's hand", no forger named, no "forged". *Believe*: `ForgedLetters 8.0` against the crown; the Court tab row reads "Letters in the crown's hand", no "- forged". *Dismiss*: no grievance, Failure, no exposure, the yellow notice |
| AC3, reload | **A save cannot be made while the inquiry is up**: `save_game` waits and runs after the answer. Staged instead through the guard: two offers at once, the second held, saved, games stopped, reloaded in a fresh process, re-asked without a new roll ("Failure (the roll made before this offer was first shown)"). **Pass** |
| `test_found_network` | **Pass** - Alary (refused by `test_assign_handler`: "leads a party") put on a network without moving, strength capped at his ceiling, `[OUTSIDE the target realm]` |

### 3.4 Story 3.11

| Check | Result |
|---|---|
| AC1 | **Pass** - the plan overlay's second line under the odds; the under-way row "69% to succeed now" (and D-1, §6) |
| AC2 | **Pass** - launched at 69%, the log's `Resolved:` and the notice both "68% on the day". The `test_counter_budget` order was reset by the AI realm's own weekly plan before it was paid (an AI realm owns its budget), so the change came from the network, not the defence |

## 4. Part A — the scenario (`di_pretender_test`, the player ruling Khuzait)

Staging levers: two companions hired, 400,000 gold, `test_set_network` standing in for weeks of growth.

| Step | Result |
|---|---|
| A1 | **Pass** - Morynon posted to Northern Empire through the tab; tab, `networks` and `mission_odds` agree (60.0; bribe 66% · 6%) |
| A2 | **Pass, unforced, real clock** - bribe of Encurion (Impestores) sent from the tab, rolled on its due day at 65%: success, `foreign gold −20.0`, `bribes` binds 167 days, Encyclopedia band Self-interested → Sullen. The crown answered the same tick by giving Impestores the Watch seat (+8) |
| A3 | **Pass on Battania, not Northern Empire.** Northern Empire never had a standing claimant: relation and legitimacy were lowered and its ruler killed to force a succession, which went "orderly" (62% to 38%). A claim exists only from a contested succession. From a fresh load, Aeron of fen Giall (on the crown's side in the unbribed run) was bought - **forced roll** - before Battania's first daily tick: "fen Giall, bought with our gold, stands with Aradwyr against Muinser", the crown's share 46% → 37% |
| A4 | **Pass, unforced roll** - ScoutArmies in Sturgia (at peace) at 43% / 31%, exposed on the first try: red notice ending "Success was 43% on the day", `EspionageExposed` held by Sturgia for 168 days, trust −30.5, handler a prisoner, network 0 |
| A5 | **Not completed.** The clock ran 48 of the claim's 168 days, then a vanilla kingdom decision for the player-ruler stopped it and the bridge could not finish the decision popup. No war by then. `war_value Sturgia \| Khuzait` was 40.8, 60.1, 46.0 against a bar of 18, **blocked throughout** by the one-chosen-war rule (Sturgia was fighting Vlandia) and, from day ~37, by Khuzait's defensive pact with Southern Empire |
| A6 | **Partial** - an AI exposure in Khuzait (forced) gave Khuzait `EspionageExposed` against Aserai, shown on the Realm tab. The "agents caught in our realm" list is at that tab's foot (the runbook said the Intelligence tab; corrected), below a fold the bridge cannot scroll |

Seen on the way, and a Phase 1 item never seen working before: **the AI→player peace offer** renders
and works - "Aserai asks for peace" (white) and "Northern Empire asks for peace" (indemnity 94,000 = 16%
of the ruler's 577,400, decision 6), both refused (`run-11-shots/peace-offer-*`).

## 5. Part B — ten years of AI espionage

| Measure (3.12 §5) | Bar | Result |
|---|---|---|
| AI ruling houses with a handler at year's end | ≥ half of those with a free member | **Pass every year**: 6/6, 6/6, 5/5, … 7/7 |
| Median AI network strength at year 2 | > 30 | **Fail: 1.4.** Years 1-11: 1.8, 1.4, 11.4, 10.0, 2.6, 9.7, 28.1, 28.7, 20.6, 17.3, 19.0 |
| AI operations per year | > 0 from year 2; every type in 5 years | **Fail**: 0 in years 1-2, then 1-5 a year, 27 in all; SpreadDissent 20, StealTreasury 7; never SabotageGarrison, BribeLord, ForgeLetters, Assassinate |
| Their rolls | - | 23 rolled: 19 success, 3 failure, **1 exposed** |
| Exposures, and wars from them | reported | 1 exposure, **1 war** by the victim within the claim, citing `EspionageExposed` |
| `handler_lost` per cause | party 0, governor 0; forced-party > 3/yr to the lead | party 0, governor 0, **forced-party 3.0/yr**, prisoner 23, recalled 13, exposed 1 |
| The player's house as a mark | 0 expected (out of reach) | 0 |
| Counter-intelligence | against 3.3's 6.4% / 11.7% | 4-8 realms ordering a budget each year, mean defence 15.2-16.9; AI operations launched at 7.1% overall exposure (SpreadDissent) and 3.6% (StealTreasury) |
| War share and length vs run 10 | within run 10's range | 89.1% of kingdoms at war on average (run 10: 89.5%); mean war 65.7 days (56.3); 69 wars (79); DefendAlly 27.5% (31.6%). Espionage moved diplomacy little, but the means are not the same run twice |
| Errors | 0 | **0** |

**Why the networks stay small.** At year 2's end the at-peace networks were growing (Osticos in the
Western Empire +1.92 a week, 36.2; Gundaroving in Vlandia +1.16, 14.7) and the at-war ones shrinking
(Pethros in Aserai −0.68, 4.3; Banu Hulyan in Battania 0 with a ceiling of 40; dey Meroc with its purse
able to pay only 1,200 a week). The AI's 6,000-a-week cap, halved at war, does not beat 15
counter-intelligence, 0.7 attrition and 0.7 decay a week. **Decided in 3.6 by the tech lead, never by
the lead:** the cap, the war halving and the target ranking. Changing them is the lead's call (§8).

## 6. Defects

| # | Defect | Status |
|---|---|---|
| D-1 | The Intelligence tab's under-way row: the "Call off" button covered the end of the text (fixed 300 wide in a ~440 column) | **Fixed**, re-run on `di_36_espionage_test` with the local build: the line wraps clear of the button (`run-11-shots/D1-fixed-under-way-row.jpg`) |
| D-2 | A result notice ran on: "...no army in the field Success was 68% on the day" | **Fixed** (`TellOwner` adds the full stop), re-run: "...no army in the field. The outcome was forced by a test lever." |
| D-3 | The runbook placed the "caught in our realm" list on the Intelligence tab | **Corrected** in the runbook |
| D-4 | No `player_died` telemetry when the player dies with no heir | Not fixed: vanilla's no-heir path never kills the hero; listed in TODO |

## 7. Observed, not caused by espionage

- **Legitimacy collapsed everywhere.** At the end: Northern Empire, Khuzait, Aserai and Sturgia at
  0.0, Western Empire 3.2, Battania 17, Southern Empire and Vlandia 24. A crown regains legitimacy only
  from a year of unbroken peace, and 89% of kingdoms were at war on average. Not in telemetry, so the
  path down is not measurable from this log.
- **No internal war in ten years**, with every crown Failing: the trigger needs a standing claimant,
  and a claim is made only by a contested succession. Two clan successions and two cadet splits
  happened; no contested royal one.
- 239 amends, 24 offices, 30 tribute-level changes: the court verbs run.

## 8. For the lead

1. **AI espionage economics** (headline 2): the AI's networks cannot grow where it aims them. Options,
   none built: raise the AI's weekly cap; drop the war halving for a realm already at war with the
   target; rank a target at peace above one at war; or accept that AI espionage is rare.
2. **`forced-party` at 3.0 a year** - on 3.8 R5's line. Accept, or open the Harmony question D2 closed.
3. **A5 with the player as the victim** was not reached; the AI-to-AI case was. Whether that is enough
   for "drags them into a war they did not choose" is the lead's call. If a player run is wanted, it
   needs the player as a vassal (no kingdom decisions to block the clock) or a human at the keyboard.
4. **Legitimacy drains to zero and civil war never comes** (§7) - a Phase 2 balance question.
5. **AC2a** (the AI naming the player as an assassination mark) needs a player-led army to stage.
