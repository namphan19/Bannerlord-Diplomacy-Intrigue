# Run 09 notes (scratch)

## 09C observations while running
- Rising at the first tick again; ended Winter 20 1084 as Stalemate (rebels 40.1 / crown 46.1), 0 sieges across the line. Side changes: Derngil -> crown 32,000; Penraic -> rising 9,300; Giall -> rising 11,200.
- AI->player peace offers seen: NE Winter 1 1084 indemnity 165,000 = 29% of 562,291, 59 pts (refused, logged); Aserai white peace (refused); NE Summer 15 1085 indemnity 66,000 = 13% of 514,362, 26 pts, "You are Exhausted at 99 days".
- The Summer-15-1085 NE offer (66,000) closed with no answer logged, and Aserai's Spring-20-1086 white peace was logged "The player accepted" without a click from the bridge. **The lead confirmed clicking in the game window during that time** - manual interaction, not a mod defect. So in 09C Khuzait made white peace with Aserai on Summer 1 1086 by the lead's hand. (The NE close with no answer line is consistent with Esc/close on the table, but which key was used is not known - not counted as evidence for the Esc check.)
- Second rising ended Summer ~1 1086: "Stalemate ... Aradwyr (3 clans), exhaustion rebels 75.2 / crown 40.2". Edge case: the rebels were past the 75 concession line but the crown was at 40.2, not below 40, so AiWouldConcede(risingSide) did not fire and the stalemate rule (both >= 40) did. The concession rule's "other side below 40" and the stalemate's ">= 40" meet exactly at 40; a side that has nothing left (75.2) ends in a draw because the winner is also at 40.2.
- Second rising: Battania, 21:55:41 real (~Spring 1 1086), right after the 84-day cooldown: "Legitimacy 27.0, bloc 55%, 6 clans below 25. Rebels (4)".
- Peace-table text: Aserai's white-peace offer (Spring 20 1086) header "of a war they are losing at score -16" while every row reads "Demanded N against a war score of 0". The rows read the offer's budget (0) where the header reads the war score. Minor, confusing.
- Header reads "winning at score +0" for a score of exactly 0 (Aserai, earlier) - the sign test is `< 0`.
- Third rising ~Spring 1087 (right after the next 84-day cooldown): "Legitimacy 3.0, bloc 80%, 6 clans below 25. Rebels (5)". Legitimacy 25 -> 27 -> 3 across two stalemates: each draw leaves the crown weaker and the next rising bigger. A loop: rise, stalemate, 84 days, rise.
- Fourth rising Winter 13 1088: "Legitimacy 0.0, bloc 59% ... Rebels (3)". War durations: #1 Summer 20 -> Winter 18 1084, #2 Winter 18 1085 -> Summer 17 1086, #3 Summer 18 -> Winter 12 1087 (each ~36-42 days), all Stalemate, all 0 captures.
- **§3.9 steps 1-2 PASS on rising #4 (a war begun on this build):** "(InternalWar) Battania: losing Uthelaim Castle costs the crown exhaustion -> rebels 18.4 / crown 32.0" (~Spring 1089). `internal_wars Battania`: "fiefs taken across the line: 1 / Uthelaim Castle: first taken from fen Gruffendoc; held now by fen Eingal / held across the line: the crown has lost 1 (+0.02/day before resolve), the rising 0 (+0.00/day) / if the crown won today, 1 fief(s) would be restored: Uthelaim Castle: fen Eingal -> fen Gruffendoc (held it when the war began)".
- Legitimacy elsewhere at the same moment: Southern Empire 0.0 (disloyal 5), Northern Empire 7.2, Western Empire 32.0, Aserai 37.0 - no claimant, so no rising. Several crowns sit at or near the floor in a 5-year run; worth the lead's look (what drags legitimacy down this far? not investigated).
- Kingdom decisions (player rules Khuzait): Road Tolls, Owner of Syratos Castle, Owner of Epinosa Castle, Owner of Amprela - each answered Ok -> Abstain -> Done -> Done. They slow a monitored run to ~12 real minutes per game year.

## §5 09A / 09B headline numbers
- 09A: di_fresh_1084, statecraft on, Summer 3 1084 -> Summer 8 1104 (242 snapshots, 20.2 y), log diplomacy-intrigue-20260927-195705.log, 0 errors. 120 wars ended; chosen mean 83.5 d / median 65; all 77.6 d PASS. PeaceTable 65.8%, FollowerRelease 15.8%, Defection 10.0% (12), Dormant 7.5%.
- 09B: same save, test_statecraft off right after load, Summer 3 1084 -> Summer 1094 (10 y), log ...-204800.log, 0 errors, 0 skill_xp events (proves off). 67 wars; chosen mean 74.6 d; PeaceTable 67.2%, Defection 11.9% (8).
- Q9 pacts first decade: 09A 37, 09B 36 (run 08: 47 vs 31). The run-08 gap did not reproduce -> most likely noise.
- Player parking: Myzea besieged in both runs (09A Winter 1085, 09B ~1089); player moved to Lageta then Pravend (09A; Lageta besieged Summer 1092), Pravend (09B); bandits paid 701 + 3,000 (09A), 3x3,000 (09B); campaign.add_gold_to_hero 50,000 in each. Player only; no AI effect.

Commit tested: 06f2acc (development). Build + deploy OK, LoadProbe clean, save-id check OK (18 classes, 137 members, 9 enums, 17 containers).
Host: BLSE Standalone via games_start. Harmony patched 6 methods.

## §2.2 old save load — PASS
- di_pretender_test loaded 18:31:10: "Loaded: 4 treaties, 5 war records, ... 0 internal wars, 0 spy networks, 0 spy missions ... schema v4", 0 ERROR/WARN.
- Saved run09_roundtrip, games_stop (confirmed no process), games_start, loaded run09_roundtrip 18:32:04: same counts, 0 ERROR/WARN.
- Caveat: the save holds no InternalWar and no SpyMission, so the new fields (InternalWar 15, InternalWarMember 2, SpyMission 12) were exercised only at their defaults. The civil-war round trip (§3.9) is the real test for InternalWar 15 / Member 2.

## §2.3 cheat gate — PASS (with a finding)
- The game launched already in cheat mode: engine_config.txt holds `cheat_mode = 1` persistently (left by earlier GABS set_cheat_mode calls). So step 1 as written ran tick_days instead of refusing - not a code fault.
- set_cheat_mode false -> `diplomacy.tick_days 1` -> "Refused: diplomacy.tick_days changes the campaign for testing and needs cheat mode (Cheat mode is disabled)." Exact expected text.
- diplomacy.legitimacy with cheats off: normal answer.
- set_cheat_mode true -> tick_days runs.
- Finding for the runbook: "before set_cheat_mode" is not a clean state on this machine; turn it off explicitly to test the refusal.

## §3.1 (partial, in run09_roundtrip after Battania rose on tick 1)
- step 4 PASS: war_value Battania|Vlandia -> "internal war: at war with its own rising for 0 days - it defends, it chooses no new war BLOCKED". Vlandia|Battania -> "value from their court: 6.0 (at war with itself +4, crown Failing +3, a claimant stands +2; capped at 6)".
- step 2 (bands): court_bands Battania -> "Crown's standing: Failing", "Aradwyr presses a claim". Matches the term.
- FINDING (minor, display): with the rising under way, court_bands still says "Four houses are spoken of as ready to break with him. Whether they would act on it, your men could not say." and lists all seven as owing fealty. The AI term reads "at war with itself +4"; the bands page never says the realm is at war with itself. A civil war is public; the page should say so.

## Seen on the way (run09_roundtrip, real clock, speed 30)
- **AI->player incoming peace offer SEEN WORKING for the first time** (Phase 1 carried debt). Winter 1 1084: "Northern Empire asks for peace", 43 days, their score -114; their offer = submission as Khuzait's vassal paying 500/period + release prisoners, "What Northern Empire's offer costs them 75", "Worth taking". Screenshot docs/balance/run-09-shots/ai-to-player-peace-offer-submission.jpg. Refused via the Refuse button: log "(AI) The player refused Northern Empire's peace offer (submit as a vassal paying 500 per period; release prisoners)." + Notify.
- D2 evidence: that score -114 = battles -42.8 + prisoners (15 held vs 85 lost = -70). The prisoner term alone put Northern Empire past the subjugation cliff at day 43 - the same pattern design/10 §9a saw.
- The same table listed "An indemnity of 170000 denars" (not offered). NE ruler gold 562,172: 30% = 168,651; the runbook says rounded DOWN to thousands (168,000). 170,000 suggests rounding to 10,000 or up. Check in §3.5.
- **check_blockers reports "clear" while the mod's peace table (layer DiPeaceTableLayer) is open and the clock is stopped.** A monitored run must also screenshot / look for that layer. Recorded for the runbook.
- **A vanilla Kingdom Decision (player rules Khuzait, "Owner of Syratos Castle") also stops the clock**: check_blockers reports inquiry_active, ui/get_inquiry FAILS on v1.4.8 (Method not found: Incident.GetOptionHint). Cleared with answer_inquiry affirmative (Ok) + click "Abstain" + "Done"; took two rounds.
- vanillaPeaceRefused climbs fast: 14 (Autumn 1) -> 53 -> 520 (Autumn 15) -> 563 (Winter 1).
- Battania rising: Derngil went over to the crown for 32,900, Uvain for 32,000 (paid by Muinser) within the first 10 days; later Penraic and Giall joined the rebels (4 again by Winter 1). Rebel exhaustion 36.7 / crown 25.3 at Winter 1, no fief taken across the line yet.

## §3.5 indemnity — seen on the real clock, AI-AI (before the staged check)
- Winter 10 1084: WAR-ENDED endedBy=PeaceTable terms=indemnity_of_104000,_release_prisoners, W.Empire (aggressor, score -47.6) vs Battania. `kind=indemnity_paid payer=Western_Empire payee=Battania gold=104000 treasury=536182 points=38.79`; prose "(Peace) Western Empire paid Battania an indemnity of 104000 denars, 19% of the 536182 its ruler held (38.8 points)."
- Check: 0.5% x 536,182 = 2,680.9/pt; 104,000 / 2,680.9 = 38.79 pt. Ceiling 40% = 214,473, not binding. Rounded to thousands. Matches the formula.
- Note: W.Empire had just taken Rhemtoil Castle by siege (same day) and kept it; it paid for a war it lost on score.

## §3.1 steps 1, 3, 5 — PASS (fresh di_pretender_test, intrigue ON, before any tick)
- step 1: `war_value Vlandia | Battania` -> "value from their court: 5.0 (crown Failing +3, a claimant stands +2)"; "value from our court: 0.0 (Hawks 0% ... of 2838 court influence)".
- step 2: court_bands Battania -> "Crown's standing: Failing", "Aradwyr presses a claim". Same inputs.
- step 3: `test_set_legitimacy Battania | 45` -> war_value -> "3.0 (crown Questioned +1, a claimant stands +2)".
- step 5 (done on di_pretender_test's Khuzait, player-ruled, rather than di_grievance_test): `blocs Khuzait` -> Hawks 79% of the blocs' power, **76% of the court's influence effective**; Autonomists 21%/21%. `peace_allowance Khuzait | Northern Empire` -> "Hawks 76% x +15% = +11.4% -> every bar x1.114; seeks peace at 66.8 (base 60); settles as the winner 78.0 (base 70)". 60 x 1.114 = 66.8. `war_value Khuzait | Western Empire` -> "value from our court: 4.5 (Hawks 76% of the court x 6 ...)".
- step 6 revisited: in the intrigue-OFF session the same `peace_allowance Khuzait | Northern Empire` read "Hawks 0% x +15% = +0.0% -> every bar x1.000; seeks peace at 60.0". So OFF zeroes the peace-bar pull: PASS. "Our court" with OFF was not run for Khuzait; it reads the same effective shares, so it is inferred, not seen.

## §3.2 threat relaxes the defensive-pact floor — PASS
(di_pretender_test session; the lead played ~7 days in it in between and NE became Khuzait's vassal, so the threat that applies is Khuzait's sphere, not NE alone.)
- `set_smoothed_strength Northern Empire | 36000`; A = Western Empire, B = Sturgia; breaches: WE-Vlandia NAP (signed/broken; a second signing was refused, Vlandia trust -35), then WE-Southern Empire NAP (signed/broken). Sturgia -> WE trust -23.
- `pact_value Western Empire | Sturgia`: "Balancing pull, included above: 40.0 against Khuzait's sphere (40251 smoothed strength)"; "defensive pact: -30.0 (-20 - pull 1.00 x 10, against Khuzait)"; "NonAggressionPact: BLOCKED ... (trust -23, needs above -20), but would stand with them in a defensive pact against Khuzait"; "DefensivePact: trust allows it"; Alliance BLOCKED.
- `sign_treaty ... Alliance` -> Refused (same text). `sign_treaty ... DefensivePact` -> "Signed: DefensivePact(Western Empire / Sturgia, Active)".

## §3.5 staged (di_hegemony_1166) — PASS
- Vlandia vs Sturgia; `set_war_score Vlandia | Sturgia | 70`. `peace_allowance`: "indemnity 14470 denars a point against a treasury of 2894043 (0.5% a point, never under 125); no peace takes more than 1157617 / at most 868000 denars, 30% of the 2894043 Sturgia's ruler holds, 60 points here". 14,470 x 60 = 868,200 -> 868,000; 40% = 1,157,617.
- `offer_peace Vlandia | Sturgia | indemnity, prisoners` -> "Refused: Vlandia is winning and will not settle for that: the package is worth 65 against a war score of 77, so they want at least 75 ..." followed by the indemnity line (same figures). Not a fail per the runbook. The signed-and-paid path was seen on the real clock instead (the 104,000 AI-AI indemnity above).
- `test_open_peace Vlandia | Sturgia`: row "An indemnity of 868000 denars - Sized by what the war earned you, against their treasury: 868000 denars, 30% of the 2894043 Sturgia's ruler holds, 60 points", price 60. Screenshot docs/balance/run-09-shots/peace-table-indemnity-row.jpg.

## §3.6 legal neglect (di_hegemony_1166) — PASS, with a UI defect and a finding
- step 1: `diplomacy.hegemony` -> both links "protection +0.0  legal neglect +0.0" in that order.
- step 2 (staged): `sign_treaty Vlandia | Aserai | NonAggressionPact`, then vanilla `campaign.declare_war Aserai | Southern Empire`. Vlandia was not called (the NAP forbids it). `hegemony` -> Southern Empire "protection +0.0  legal neglect -10.0 ... wars -5.0" + "legal neglect: Vlandia is bound by treaty to stay out of Southern Empire's war with Aserai (NonAggressionPact) - each counts as 50% of an ignored war".
- step 3 (arose by itself): Aserai's ally Western Empire - **Vlandia's other vassal** - answered Aserai's call and declared war on Southern Empire ("(CallToArms) Western Empire answered Aserai and declared war on Southern Empire"). Southern Empire's link still shows one legal-neglect line only (Aserai). PASS: a fellow vassal as attacker adds no legal neglect.
- **FINDING (design question):** two vassals of the same patron went to war with each other because one holds an Alliance with an outsider. CallToArms.Applies only checks a treaty between the ally and the enemy (HasTreatyForbiddingWar(WE, SE)), and WE and SE have none; the shared patron is not considered. Is a vassal meant to be callable against a fellow vassal of its own patron?
- step 4: Realm tab (player joined Vlandia via test_player_join): Southern Empire row shows the chip "legal neglect -10.0". **UI DEFECT: the chip row overflows** - on both rows the last chips (rival, culture, dread, authority) spill into the "Our claims" column and print over its text ("dread -0.8 authority +4.4" over "ReclaimAncestralLand ... ages out"). Screenshot docs/balance/run-09-shots/realm-tab-legal-neglect-chip-overflow.jpg. The runbook predicted "a tenth chip may overflow": it does, with nine/ten.

## §3.10.4 agreements red in their last 21 days — NOT RUN
- No agreement of the player's realm within 21 days of expiry on any loaded save; no lever sets an expiry date.

## §3.3 tribute cap and cooldown — steps 1-4 PASS, step 5 NOT RUN (di_tribute_test)
- X = Vlandia (at peace with NE, Southern Empire, Aserai; Sturgia is Khuzait's vassal on this save).
- step 1: `sign_treaty Northern Empire | Vlandia | TributaryPact | 500` Signed; `Southern Empire | Vlandia` Signed; `Aserai | Vlandia` -> "Refused: Vlandia already pays tribute to Northern Empire and Southern Empire, and no realm is made to pay more than 2 at once."
- step 2: `tribute_value Aserai | Vlandia` -> "tributes paid: 2 - to Northern Empire (TributaryPact, 500), Southern Empire (TributaryPact, 500) (at most 2) BLOCKED".
- step 3: `peace_allowance Sturgia | Vlandia` (at war) -> "tributary pact 60 (blocked: Vlandia already pays tribute to Northern Empire and Southern Empire, and no realm is made to pay more than 2 at once.)".
- step 4: `test_expire_treaty Northern Empire | Vlandia | TributaryPact` then `tribute_value Northern Empire | Vlandia` -> "tributes paid: 1 ... last tribute: theirs to us ended 0.0 days ago (demandable again after 84) BLOCKED". The runbook's "cannot be demanded again for 84 more days" wording is the button's (CooldownReason), not the diagnostic's. "pact signable: yes" on the same readout is by design: the cooldown binds demands only, the peace table may still award tribute in a new war (AiDiplomacy.cs:1461-1463).
- step 5 NOT RUN: `test_demand_tribute` stops at "holds no territorial claim" for both pairs, so no tribute_refused line. The cap/cooldown gates sit after the claim and the x2.0 strength ratio. On this save the widest live ratio is Khuzait 6063 / Battania 3834 = 1.58: no pair can reach the cap/cooldown gates at all. Relevant to run question 5: with eight near-equal realms, AI demands that could meet the cap or cooldown should be rare.

## §3.4 trust bleeds from a war's first day — PASS
- Sturgia vs Vlandia at war 26 days. Before the check, trust had already moved -19.9 (load, day 19) -> -25.6 (day 26): 7 days x (0.6 + 0.01 x ~22) = -5.7. Matches.
- `sign_treaty Sturgia | Vlandia | Truce`, `test_expire_treaty ... Truce` -> "Expired, honoured in full ... Trust now -13.6" (+12, grace starts). The war stays on (the lever writes a treaty row).
- `tick_days 1` -> -14.5. Expected 0.6 + 0.01 x 26 = 0.86. PASS: the grace no longer shields a pair at war.

## §3.1 step 6 (Court intrigue OFF) — term PASS, two things not discriminated
- No console lever for EnableIntrigue. Wrote a temporary MCM file Configs\ModSettings\Global\DiplomacyIntrigue\DiplomacyIntrigue_v1.json (EnableIntrigue false, others default), launched, loaded di_pretender_test, then deleted the file (the lead's machine had no such file before: MCM defaults).
- `war_value Vlandia | Battania` -> "value from their court: 0.0 (crown Failing +3, a claimant stands +2)". The term is 0. **Display defect (minor):** the parenthesis still lists +3/+2 components with the switch off; it should say the intrigue pillar is off.
- "value from our court: 0.0 (Hawks 0% ... Doves 0%)" and `peace_allowance Khuzait | Northern Empire` court pull "x1.000" - but both are also 0/1.000 with intrigue ON on this save (no hawks, no doves before exhaustion 40), so step 6 does not discriminate them. NOT PROVEN for our-court and peace bars.
- On the way, §3.5 step 2 on an unstaged war (Khuzait winning at 70.8): "indemnity 2214 denars a point against a treasury of 442732 (0.5% a point, never under 125); no peace takes more than 177092 / at most 132000 denars, 30% of the 442732 Northern Empire's ruler holds, 60 points here". 0.5% x 442,732 = 2,213.7; x60 = 132,819 -> 132,000 (thousands, down); 40% = 177,092. PASS.

## §3.9 civil war captures/restitution — NOT RUN (no capture happened); a finding instead
- A rising started ON THIS BUILD: Battania rose on the first tick_days after load (Summer 20 1084), 4 rebels, the rising holding 6 fiefs and 10 war parties. Real clock at speed 30.
- Side changes: Derngil -> crown for 32,900 and Uvain -> crown for 32,000 (paid by Muinser) in the first 10 days; then Penraic (10,100), Giall (12,200), Morcar (9,900) -> the rising, paid by Aradwyr. Rebels 4 -> 2 -> 5.
- Battles inside the war: 74 FieldBattle, 36 Raid, 1 SallyOut, **0 sieges**. `fiefs taken across the line: 0` throughout.
- Ended Winter 13 1084 = **36 days** after it began: "Stalemate (both sides are worn out)", rebels 55.8 / crown 43.3 (InternalWarStalemateExhaustion 40 both sides). No concession at 75.
- So the capture -> daily cost -> restitution path (R-6) could not be exercised. With a 36-day war and exhaustion rising ~1.5/day (rebels) / ~1.2/day (crown), a rising ends before any siege. Question for the lead: R-6 may be moot at these tunings - is a civil war meant to last a month?
- Save round trip of Captures therefore also NOT RUN.

## §3.7 cadet influence — PASS (run09_roundtrip, Winter 13 1084)
- `diplomacy.heirs`: "Sturgia / Kostoroving ... WOULD DIVIDE ... a cadet branch would take 108 of 432 influence (1 of 4 adults, share 25%)". Others: Tigrit 66/328 (1/5, 20%), Baltait 335/838 (2/5, 40%), Oburit 114/228 (2/4, 50% = the cap).
- `test_divide_clan Kostoroving` -> "Influence: Kostoroving 432 -> 324, Kostoroving of Chastimir 108". 432-324 = 108 = 432 x min(1/4, 50%). Log: "(ClanSuccession) Kostoroving divides: Chastimir founds Kostoroving of Chastimir ... takes 108 of Kostoroving's 432 influence (1 of 4 adults, share 25%)." Same numbers.
- Note: the lever makes the best-scoring heir (Chastimir) the founder because the head stays alive - documented in the lever, not a defect.

## §3.10.1 test_set_skill sticks — PASS
- `test_set_skill Reingarda | charm | 232` -> "Charm 217 -> 232. Skill XP 2,356,076, what 232 requires". No DISAGREE.
- `test_amends dey Fortes` (Vlandia, Reingarda is Envoy) -> "49 influence, 5,400 denars. Loyalty 27.0 -> 29.2 (predicted 29.2)"; log `kind=skill_xp hero=Reingarda ... xp=378.56 act=amends skillNow=232`. `statecraft Vlandia` Envoy Charm 232.

## §3.8 peace dividend — read PASS, payment pending
- `diplomacy.legitimacy`: "Western Empire 50.0 last: lost a war / at peace 2 days (since Winter 10, 1084); next dividend +3.1 in 82 days". Battania (internal war running, foreign war ended) reads "at war". Every realm still at war reads "at war".
- +3.1 rather than +3.0: the steward's S-6 factor on the dividend (design 08).

## §3.10.3 occupied fiefs
- `diplomacy.wars` prints `fiefs=a/b (held now c/d)`: e.g. "Northern Empire vs Khuzait ... fiefs=1/1 (held now 1/1)", "Southern Empire vs Aserai fiefs=2/0 (held now 2/0)". Format present. No retake seen yet.

## §3.10.2 zero-manpower siege — the code's hypothesis is REFUTED (the fix is harmless)
Four `battle_scored type=Siege winner=attacker` lines in run09_roundtrip (real clock):
| Siege | settlementNow | garrison entry | defenderFielded | militia |
|---|---|---|---|---|
| Syratos Castle, Autumn 12, Khuzait took it from NE | Northern_Empire (defender) | Garrison_of_Syratos_Castle:garrison:Northern_Empire:**yes**:96:63 | 96 | 152 men, 95 died, no(army) |
| Nevyansk Castle, Autumn 15, Vlandia vs Sturgia | Sturgia (defender) | garrison **yes**:107:66 | 107 | 78/57 no(army) |
| Razih, Winter 6, S.Empire vs Aserai | Aserai (defender) | garrison **yes**:231:139 | 231 | 185/159 no(army) |
| Rhemtoil Castle, Winter 9, W.Empire vs Battania | Battania (defender), then fief_changed BySiege to Western_Empire at the same ms | garrison yes | 122 | - |
- design/10 §9a table row 2 applies: **the owner changes AFTER MapEventEnded**; garrisons are counted as the defender's already. No `yes(walls)` ever appeared, so `WarExhaustion.HeldItsOwnWalls` never fired. §9a's "NE fielded 0" castle most likely held militia only (row 3). STATUS "a likely cause found in code and fixed" should be corrected to "cause ruled out; the castle likely had no garrison".
- Observation for the lead (design, not a bug): militia are uncounted (`no(army)`), and they die in numbers: 95, 57, 159 militia deaths in three sieges went to nobody's score. A defender loses its walls and the score reads only the garrison.
- The settlement's own party entry reads 0:0 each time, so the "folded into the settlement party" row does not apply.

## §4.1 numbers shown = numbers used — PASS (di_espionage_missions, player house Airit, network in Vlandia)
- Network 35: `mission_odds Airit | Vlandia` Scout 52% / exposed-if-fails 25% / overall 12%, Read 47/25/13, Sabotage 42/25/14, Dissent 44/25/14. Intelligence tab (`test_intel open`): "Scout their armies 52% · 12%", "Read their court 47% · 13%", "Sabotage 42% · 14%", "Spread dissent 44% · 14%". Same.
- Resolved the pending ReadCourt (network spent to 30), launched ScoutArmies: log "(Espionage) Airit launched ScoutArmies ... success 49%, exposure if it fails 26%." `mission_odds` at 30: 49% / 26% / 13%. Tab: "Scout their armies 49% · 13%". All three agree. (The tab's second figure is overall exposure, the log's is exposure-if-it-fails; both printed by the console, same function.)

## §4.3 outcome gap — RESULT (di_run07_1104, Spring 1 1105 -> Spring 5 1107, 2 campaign years, speed 30, 0 errors; saved run09_espionage_2y)
Player: Calastides (independent), companion Thais the Falcon (roguery 50, ceiling ~82) in Southern Empire at 6,000/week, at peace with SE.
| Network | weeks | final strength | note |
|---|---|---|---|
| **Calastides (player) in Southern Empire** | 27 | **29.6** | +~2.2/week steady, handler never lost |
| Argoros (N.Empire) in Khuzait | 26 | 51.2 | not at war: ~+3.8/week |
| Urkhunait (Khuzait) in Southern Empire | 26 | 44.2 | 4 handler changes |
| dey Meroc (Vlandia) in Southern Empire | 27 | 36.4 | flat at 0.6 for 5 weeks (at war), then +5/week |
| Banu Hulyan (Aserai) in Battania | 27 | 34.0 | 3 handler changes |
| Pethros (S.Empire) in Khuzait | 27 | 27.3 | flat while at war, then grew |
| fen Gruffendoc (Battania) in N.Empire | 21 | 17.4 | |
| fen Gruffendoc in Sturgia / Argoros in SE / Gundaroving x3 | 2-15 | 0.0 | abandoned or never staffed long |
- Handler losses by cause: **party 16, governor 3, recalled 1, left clan 1 (21 in total)**. The AI re-posts a new handler the next week, so a network survives the churn.
- AI operations: **3 launched** (all SpreadDissent on Southern Empire castles): 1 failed for want of a handler (vanilla took him mid-operation, 4,000 lost), 1 Success (Sestadaim loyalty 56 -> 41), 1 pending at the end. No Exposed.
- **Verdict: the runbook's expectation ("AI networks stay near 0; no AI operation in two years") does not hold.** AI networks aimed at a realm at peace grow as fast as or faster than the player's (their handlers have 110-236 roguery vs Thais's 50). The gap is real but narrower: (a) war targets x0.5 flat-line, and the AI prefers war targets; (b) vanilla takes a handler every ~5 weeks per network (party mostly), costing a week and, once, a paid operation; (c) handlers relocated home are not noticed.
- New finding: the launch showed "success 68%" but the resolution printed "success was 71%": odds are recomputed at resolution with the network's current strength, so the figure the owner saw at launch is not the one rolled. Minor, but it is the "number shown = number used" rule.

## §4.3 notes while running
- First AI operation: Autumn ~6 1106 (day ~90), "(Espionage) Khuzait launches SpreadDissent by Urkhunait in Southern Empire at Jogurys Castle ... loyalty 70, exposure 1%" / "success 95%, exposure if it fails 28%". Khuzait was NOT at war with Southern Empire, so its network grew at the full rate. The runbook's "no AI operation in two years" is wrong for networks aimed at a realm at peace; right for those aimed at a war enemy (x0.5).

- That operation never rolled: "Alijin no longer runs Urkhunait's network in Southern Empire (took command of a party)" 19:44:33 (Winter ~3 1106), then "(Espionage) Failed for want of a handler: SpreadDissent by Urkhunait in Southern Empire at Jogurys Castle, Failure." 19:44:39. 4,000 paid, lost to vanilla taking the handler.
- Third governor case: "Urkhunait put Alagur in charge ... stationed at Vostrum" 19:44:47.157, "Alagur no longer runs ... (became governor of Epinosa Castle)" 19:44:49.824.

## §4.6 owed bribe offer asked again, not re-rolled — PASS (di_hegemony_1166, player Culharn of fen Calrain joined Aserai via test_player_join)
- Neither di_36_espionage_test nor di_35_bribe_test works for this as the runbook suggests: on both the player rules Khuzait and test_player_join refuses ("cannot leave that realm without a crown").
- Two Khuzait houses (Khergit, Tigrit) given networks in Aserai (test_assign_handler + test_set_network 60), BribeLord on Culharn from each.
- First attempt was defeated by the weekly AI upkeep: "Gwennein no longer runs Khergit's network in Aserai (recalled - Khergit no longer rules)" - non-ruling AI houses are wound down (design 03 §10 warns). And a forced-success second offer that finds the guard up waits for its ORIGINAL ResolvesOn (14 days), because Missions.DailyTick only picks up missions whose ResolvesOn is past. The code comment "a second offer that finds the guard up is asked the day after" holds on the natural path (a mission resolves on its date, so the next day it is past) but not with the test lever, which resolves early. Not a gameplay defect; the runbook step needs changing.
- Second attempt, within one week: forced Khergit success -> "Foreign gold" (Khergit) opened; forced Tigrit success while it was open (marked owed, not shown); answered Khergit (Take) -> "Resolved: BribeLord by Khergit ... Success (success was 65% ... outcome forced by a test lever), taken by the player."; then `test_resolve_mission Tigrit | Aserai` with no outcome -> "Foreign gold" (Tigrit) opened (screenshot seen); Turn them away -> "Resolved: BribeLord by Tigrit in Aserai on Culharn, Failure (the roll made before this offer was first shown), refused by the player." No fresh "success was N%" for the second. 0 errors.

## §4.4 information gap — 3 plans read, 2 decided by exact figures inside one band
1. run09_roundtrip (Autumn 1084), Western Empire -> Battania: "option BribeLord value 4.00 (fen Derngil at loyalty 0.0)". court_bands Battania: four houses "Ready to break" (Eingal, Uvain, Derngil, Caernacht). The exact figure picked one of four the player sees as equal. **Decided by exact figures.**
2. Same plan: "option ForgeLetters value 1.50 (fen Giall at loyalty 27.0)". Bands: Morcar and Giall both "Sullen". **Decided by exact figures** (Morcar's figure not read at that moment; it sits in the same band).
3. di_run07_1104 (Autumn 1105), Khuzait -> Southern Empire and Vlandia -> Southern Empire: BribeLord and ForgeLetters both "(Avlonos at loyalty 32.7)". court_bands Southern Empire: Avlonos is the only "Sullen" house (all others Self-interested or Steadfast). Bands would pick the same house.
- None of these plans passed its gates (networks 0.4-9.2 against 45/50), so in these runs the gap changed nobody's fate - it only shows the AI would pick among equals by numbers the player cannot see.
- Also: the plan's "at loyalty 32.7" and `diplomacy.loyalty` a few in-game days later (26.2) differ; time passed between the two reads, not investigated further.

## §4.5 player-specific lines in AiEspionage.cs @06f2acc — FOUR, not two
- :102 `p.Ruler == Hero.MainHero` -> skip planning for a player-ruled realm. Not an asymmetry (the player plans their own).
- :250 `clan != Clan.PlayerClan` in the forgery pick -> #4.
- :373 WindDownOrphans skips `owner == Clan.PlayerClan` -> an AI house that stops ruling loses its network; the player's house keeps networks whatever it rules. Consequence of decision 5 (AI networks only from ruling houses); the rule for the player is "any house". Record, not a new asymmetry in Missions/SpyNetworks.
- :510 FieldCommander skips `leader.Clan == Clan.PlayerClan` -> #3.
- Bribes: no player exemption (a bribe on the player's house asks the player, decision 12).

## §4.2 vanilla IL, v1.4.8 (tools/CallSites, real assemblies)
### Governor
- Only AI path: ClanVariablesCampaignBehavior.UpdateGovernorsOfClan -> ChangeGovernorAction.Apply. Candidate filter per lord in clan.AliveLords: `ClanPoliticsModel.CanHeroBeGovernor(hero)` (callvirt, IL_01c9) AND `PartyBelongedTo == null` AND `Clan != PlayerClan` AND not already chosen. Score = DiplomacyModel.GetHeroGoverningStrengthForClan x (1 if current governor else 0.75).
- DefaultClanPoliticsModel.CanHeroBeGovernor: IsActive, !IsChild, !IsHumanPlayerCharacter, !IsPartyLeader, !IsFugitive, !IsReleased, !IsTraveling, !IsPrisoner, hero.CanBeGovernorOrHavePartyRole() (-> CampaignEvents.CanBeGovernorOrHavePartyRoleEvent, ref bool), !IsSpecial, !IsTemplate.
- **CONTRADICTS design 03 §10 / ModClanPoliticsModel header** ("not known whether vanilla asks it"; the 2026-09-25 run concluded "CanHeroBeGovernor is not what vanilla asks"). It IS asked. ModClanPoliticsModel existed at be2e7ea (2026-09-25 16:27 UTC) before the run write-up (8e41561). No other loaded module references ClanPoliticsModel (only NavalDLC.dll, not loaded). So Lilizha -> Mazhadan governor is UNEXPLAINED by IL. Candidates: HandledBy() null at that moment; EnableEspionage read false; the model registered but not the one in Campaign.Models; the run's DLL predating the model. Needs a live observation (§4.3) + a read of Campaign.Current.Models.ClanPoliticsModel.GetType() (no bridge tool reads it; a diagnostic line would).
- Other Town.set_Governor writers: Town.AfterLoad, GovernorCampaignBehavior.OnGameLoadFinished, TeleportationCampaignBehavior.ApplyImmediateTeleport (only with TeleportationData.IsGovernor, i.e. downstream of ChangeGovernorAction), advanced-start options. The mod teleports handlers with TeleportHeroAction.ApplyImmediateTeleportToSettlement, which does not set a governor.
### Party leader
- HeroSpawnCampaignBehavior.OnNonBanditClanDailyTick -> ConsiderSpawningLordParties(clan): while WarPartyComponents < ClanTierModel.GetPartyLimitForTier, GetBestAvailableCommander(clan), spawn if GetHeroPartyCommandScore + CalculateScoreToCreateParty > 100.
- GetBestAvailableCommander pass 1: IsActive, IsAlive, PartyBelongedTo null, PartyBelongedToAsPrisoner null, **hero.CanLeadParty()** (-> CampaignEvents.CanHeroLeadPartyEvent, ref bool), Age > HeroComesOfAge, Occupation == Lord(3); best GetHeroPartyCommandScore (3 Tactics + 2 Leadership + Scouting + Steward + melee + Riding, +1000 clan leader, +500 if not governor, -5000 noncombatant).
- **Pass 2, only if pass 1 found nobody and clan != PlayerClan: the same filter WITHOUT CanLeadParty.** So an event veto keeps a handler out only while the clan has another free lord.
- EmptyClanPartiesCampaignBehavior.TryCreateNewClanMobilePartyFromCachedEmptyParties: IsActive, clan != PlayerClan, !IsHumanPlayerCharacter, PartyBelongedTo null, a cached empty party for this hero -> SpawnLordParty. No CanLeadParty; no veto reachable.
- Why the player's handler stays: it is a companion (Occupation Wanderer), never Lord, so neither path picks it; and both player-clan exclusions above.
### Moved away (not caught by the mod)
- HeroSpawnCampaignBehavior.OnHeroDailyTick: for an active hero with CanHeroMoveToAnotherSettlement (clan != PlayerClan, not template/notable/human/party leader/prisoner, HeroState != 6, no GovernorOf, no party, not wanderer, not Occupation 31, grown, current town not holding a tournament nor under siege, **hero.CanMoveToSettlement()** -> CampaignEvents.CanMoveToSettlementEvent, ref bool) -> FindASuitableSettlementToTeleportForHero(min score 10) -> TeleportHeroAction.ApplyImmediateTeleportToSettlement.
- SpyNetworks.StillHandles checks dead / left clan / prisoner / clan head / party leader / governor. **It never checks the handler is still in the target realm.** An AI handler teleported home stays "in post" and the network keeps growing. Possible silent defect - to observe in §4.3 (handler's CurrentSettlement vs station).
### The governor case EXPLAINED (live + IL, 2026-09-27 evening)
- Live, di_run07_1104: "(Espionage) fen Gruffendoc put Hajara in charge of its network in Sturgia, stationed at Dunglanys" at 19:37:56.511; "Hajara no longer runs ... (became governor of Hertogea Castle)" at 19:37:59 - ~3 real seconds, a few in-game hours.
- Module DLLs scanned too (a scratch game folder with SandBox*, StoryMode*, BirthAndDeath, CustomBattle added; tools/CallSites only reads bin\ by default): still the only AI caller of ChangeGovernorAction.Apply is UpdateGovernorsOfClan, which asks CanHeroBeGovernor.
- ChangeGovernorAction.ApplyInternal IL: a hero not already in the town is sent with **TeleportHeroAction.ApplyDelayedTeleportToSettlementAsGovernor**; TeleportationCampaignBehavior.ApplyImmediateTeleport later sets Town.Governor when TeleportationData.IsGovernor. DefaultClanPoliticsModel.CanHeroBeGovernor itself excludes IsTraveling.
- **Cause (high confidence, not yet proven by a decisive log line):** the AI picked a hero already travelling to take up a governorship. SpyNetworks.CanHandle checks GovernorOf (still null while travelling) but not IsTraveling; the mod teleports the hero abroad; the queued governor teleport then completes and makes them governor. Fits Hajara (seconds) and Lilizha 2026-09-25 ("within the first week").
- Second live case, same run: "Gundaroving put Simir in charge of its network in Northern Empire, stationed at Diathma" 19:41:59.155 (Spring 7, 1106), then "Simir no longer runs ... (became governor of Dunglanys)" 19:42:04.483 - five real seconds, about a day in game. Same signature.
- **Fix proposal (no Harmony, small):** CanHandle rejects hero.IsTraveling (and IsFugitive/IsReleased, as the governor model does); a decisive check is to log HeroState in the "put X in charge" line.
- So design 03 §10's "CanHeroBeGovernor is not what vanilla asks" is WRONG and should be corrected: it is asked; the model works; the hole is a hero already en route.

### Handlers moved home (LIVE, confirmed)
- `diplomacy.networks` at Spring 16 1105 (day 15): Eutropios (Pethros's network in Khuzait) "at Syronea"; Muinser (fen Gruffendoc in Sturgia) "at Seonon"; Lilizha (Gundaroving in Battania) "at Balgard"; Megenhelda (dey Meroc in SE) "at Mecalovea Castle"; Thelea (Argoros in Khuzait) "at Amitatys"; Alijin (Urkhunait in SE) "at Makeb"; Ghuzid (Banu Hulyan in Battania) "at Razih". None of these is in its target realm. The player's Thais stays at Vostrum (SE).
- The networks keep growing as if staffed (StillHandles does not check location). The fiction ("a handler is stationed in the target realm") is not held; mechanically the network does not care. Needs the lead's call: enforce (CanMoveToSettlementEvent veto, lever 2 below) or drop the station from the fiction.

### Levers without Harmony (proposal, not built)
1. Listen to CampaignEvents.CanHeroLeadPartyEvent: result=false for a hero SpyNetworks.HandledBy. Covers pass 1 of GetBestAvailableCommander, caravans, armies, the clan screen. Does NOT cover pass 2 (clan with no other free lord) or the cached-empty-party path.
2. Listen to CampaignEvents.CanMoveToSettlementEvent: false for a handler -> vanilla stops relocating them.
3. CanBeGovernorOrHavePartyRoleEvent: same veto as the model, and also DefaultClanMemberPartyRoleModel (quartermaster etc.) and FactionHelper relocation. Could replace ModClanPoliticsModel's override with one event (one resolver).
### LIVE, run09_roundtrip, Autumn 20 1084 (~30 days after load): AI networks grow ~0 even with the handler in post
- `diplomacy.networks`: 6 AI networks founded this session (5 with a handler in post in the target's richest town, Battania's has none: "nobody in the house is free").
- Every AI target is a realm it is AT WAR with (PlanTarget scores war highest). Weekly terms, e.g. Comnos in Battania: gold 6000 -> +5.37 x 0.5 at war = +2.69, CI 14.3 -> -1.15, attrition -0.70, daily decay -0.70 => +0.14/week. Others: +0.06, +0.12, -0.07, -0.66, -0.79.
- At peace the same network would make ~+2.8/week. At war the AI never reaches SpreadDissent's 30 (~200+ weeks), and pays 6,000/week = ~312k a campaign year per realm for it.
- design 03 §10 "by hand ~1.34 a week ... a year-two event" did not include the x0.5 wartime rate (decision 4) and the -0.7/week daily decay together with a war target. The outcome gap in #1 is therefore ALSO a formula/targeting issue, not only vanilla taking handlers. A player spying on a war enemy meets the same arithmetic (same rules) - it is the AI's choice of target (war first) that makes it bite the AI.
4. Residual gap (pass 2 + cached party) is only closable by Harmony or by the AI choosing handlers that vanilla would not pick first (e.g. prefer a house with >= 2 free lords, or a non-Lord occupation - none exist for AI).
