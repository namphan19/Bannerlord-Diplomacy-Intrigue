# Balance run 09 — the delegated pass of 2026-09-27, measured

Runbook: [run-09-runbook.md](run-09-runbook.md). Run on 2026-09-27 (checks 18:30-23:16, balance runs
19:57-22:08), written up on 2026-10-01 from the logs, the three analysis files and the working notes
([run-09-working-notes.md](run-09-working-notes.md)), which hold every command and output quoted here.

| | |
|---|---|
| Commit tested | `06f2acc` (development); the logs read "mod 0.1.0 built 2026-09-27 18:30" |
| Game | v1.5.3 (Steam beta branch), BLSE Standalone through `games_start`, **NavalDLC not loaded** |
| Build | local build against the installed game (v1.5.3), not the v1.4.8-reference release DLL - see §7 |
| Harmony | 6 methods patched, as expected |
| Errors | **0 `ERROR`, 0 `WARN`** in all four logs (09A, 09B, 09C, checks) |

Read every number from a single run as one sample. Two runs that disagree with each other are the
normal case, as this report shows twice (§5.9, §5.10).

---

## 1. Headline

1. **Everything the pass built works on its main path.** Of §3's 19 rows, 13 pass, one is done
   with its hypothesis refuted (3.10.2), and **none fails**. Five could not be run or were only partly
   run; none of those is a failure. §2's three setup steps and §4's checks also pass.
2. **Run 08's two statecraft differences did not reproduce.** Pacts 37 against 36 (run 08: 47 against
   31). Wars in the first decade: 57 with statecraft on, 67 off - the *opposite* direction from run
   08's +20%. Both look like noise between two runs.
3. **Vassal defection is now common: 12 in 20 years (09A), 8 in 10 years (09B), against 1 and 2 in
   run 08.** Northern Empire changed patron eight times in 09A. The likely cause is legal neglect
   (2026-09-27) pulling Hold under F3's line of 40 - an inference, not shown.
4. **The prisoner term is about half of every war's score** (mean 45.6% in 09A, 48.7% in 09B). D2
   was kept knowing it dominated the first live check; the runs confirm it at scale.
5. **A civil war is a month-long draw on a loop** (09C): four risings in five years, three ended in
   stalemate after 37-42 days, crown legitimacy 25 → 27 → 3 → 0, no concession, no crown win, no
   restitution. One fief taken across the line in five years.
6. **Phase 3 is the same rules, uneven outcomes** (§4): the rule layer is shared; the AI's networks
   aimed at a realm at war barely grow, and vanilla takes an AI handler about every five weeks. The
   handler-to-governor case now has a cause and a fix that needs no Harmony.

---

## 2. Setup

| Step | Result | Evidence |
|---|---|---|
| 2.1 Build and deploy | PASS | LoadProbe clean; save-id check OK (18 classes, 137 members, 9 enums, 17 containers) |
| 2.2 New save data on an old save | PASS | `di_pretender_test`: "Loaded: 4 treaties, 5 war records … schema v4", 0 errors; saved `run09_roundtrip`, restarted, reloaded: same counts, 0 errors. The save holds no internal war or spy mission, so the new fields were loaded only at their defaults |
| 2.3 Cheat-mode gate | PASS, with a finding | The refusal text is exact. But `engine_config.txt` keeps `cheat_mode = 1` between sessions, so "before `set_cheat_mode`" is not a clean state on this machine; turn it off explicitly to test the refusal |

---

## 3. Targeted checks

| Check | Result | One line of evidence |
|---|---|---|
| 3.1.1-3 Court reaches foreign policy | PASS | `war_value Vlandia \| Battania`: "value from their court: 5.0 (crown Failing +3, a claimant stands +2)"; after `test_set_legitimacy 45`: 3.0 |
| 3.1.4 A realm at war with itself | PASS | "internal war … it chooses no new war BLOCKED"; the other side reads 6.0, "capped at 6" |
| 3.1.5 Court moves the peace bars | PASS | Hawks 76% effective → "every bar x1.114; seeks peace at 66.8 (base 60)" |
| 3.1.6 Intrigue off | **PARTLY** | "their court" reads 0 with the switch off. "Our court" and the peace bars also read 0 / ×1.000 - but they read the same with it on, on this save, so the step does not discriminate them |
| 3.2 Threat relaxes the defensive-pact floor | PASS | `pact_value`: "defensive pact: -30.0 (-20 - pull 1.00 x 10, against Khuzait)"; DefensivePact signed, Alliance refused |
| 3.3.1-4 Two tributes at most, 84-day cooldown | PASS | third pact refused with the exact text; after expiry "demandable again after 84 … BLOCKED" |
| 3.3.5 `tribute_refused` telemetry | **NOT RUN** | `test_demand_tribute` stops at "holds no territorial claim" on every pair; the widest strength ratio on the save is 1.58, short of the 2.0 the demand needs |
| 3.4 Trust bleeds from a war's first day | PASS | after a truce expiry (+12), one day moved trust by 0.86 = 0.6 + 0.01 × 26 days |
| 3.5 Indemnity against the loser's treasury | PASS | staged: 14,470 a point × 60 = 868,000, 30% of 2,894,043; on the real clock an AI-AI indemnity of 104,000 = 38.79 points, matches |
| 3.6.1-3 Legal neglect | PASS | "protection +0.0 legal neglect -10.0" with the NAP named; a fellow vassal as attacker adds none |
| 3.6.4 Realm tab chip | PASS, **UI defect** | the chip shows, and the chip row overflows into "Our claims" (§6, D-3). Screenshot `run-09-shots/realm-tab-legal-neglect-chip-overflow.jpg` |
| 3.7 Cadet branch influence | PASS | "Kostoroving 432 -> 324, Kostoroving of Chastimir 108" = 432 × min(1/4, 50%) |
| 3.8 Peace dividend | **PARTLY** | the reading is right ("next dividend +3.1 in 82 days"; 3.1 by the steward's factor). No payment was seen: it needs a year of peace on the real clock |
| 3.9.1-2 Civil war captures | PASS (on the 4th rising) | "losing Uthelaim Castle costs the crown exhaustion"; `internal_wars` lists the capture, the daily cost and "1 fief(s) would be restored" |
| 3.9.3-5 Round trip, restitution, no grievance | **NOT RUN** | the only capture came late in 09C; the steps were not done. A retry on 2026-10-01 hit §7's defect before any rising started |
| 3.10.1 `test_set_skill` sticks | PASS | "Charm 217 -> 232", no DISAGREE; still 232 after an amends |
| 3.10.2 Zero-manpower siege | DONE - **hypothesis refuted** | 4 sieges: the owner changes after `MapEventEnded` and the garrison is already counted for the defender; `yes(walls)` never appeared. STATUS.md's "cause found in code and fixed" is wrong (§6, D-11) |
| 3.10.3 Occupied fiefs | PASS (format) | `fiefs=a/b (held now c/d)` printed; no retake seen |
| 3.10.4 Agreements red in their last 21 days | **NOT RUN** | no agreement of the player's realm near expiry on any save, and no lever sets an expiry |

---

## 4. Phase 3 and the same-rules principle

**Rules: shared.** 4.1 PASS - the Intelligence tab, `mission_odds` and the launch line agree
(52 / 47 / 42 / 44% at strength 35; 49% at 30). One wrinkle: odds are recomputed at resolution with
the network's strength then, so a launch shown at 68% resolved "success was 71%" (D-8).

**Outcome: the gap is real, but narrower than the runbook expected and partly self-inflicted.**
`di_run07_1104`, two campaign years:

| Network | Final strength | Note |
|---|---|---|
| Player (Calastides) in Southern Empire | 29.6 | +2.2 a week, handler never lost |
| Argoros (N. Empire) in Khuzait | 51.2 | target at peace: +3.8 a week |
| Urkhunait (Khuzait) in Southern Empire | 44.2 | 4 handler changes |
| dey Meroc (Vlandia) in Southern Empire | 36.4 | flat for 5 weeks at war, then +5 a week |
| AI networks aimed at a realm at war (run09_roundtrip) | ~0 | +0.14 a week net: ×0.5 at war, minus decay and attrition |

Handler losses: 21 in two years (party 16, governor 3, recalled 1, left clan 1); the AI re-posts the
next week. AI operations: 3 (SpreadDissent ×3: 1 success, 1 lost when vanilla took the handler
mid-operation, 1 pending). The runbook's "AI networks stay near 0, no operation in two years" holds
only for networks aimed at a war enemy - and the AI aims at war enemies first.

**Information: the AI uses exact figures the player cannot see.** 3 plans read; 2 chose a house by
an exact loyalty among houses the bands show as equal (four "Ready to break" in Battania). None of
those plans passed its gates, so no outcome changed in these runs.

**Exemptions: four player-specific lines, two of them the known asymmetries.** `AiEspionage.cs:102`
(no planning for a player-ruled realm - not an asymmetry), `:250` (no forgery against the player's
house, #4), `:373` (orphaned networks wound down except the player's - follows decision 5), `:510`
(no assassination of the player's house, #3). No case arose to stage.

**The lever for #1 - found, with IL (v1.5.3) and two live cases.** Vanilla *does* ask
`CanHeroBeGovernor`; design 03 §10's "it is not asked" is wrong (D-11). The hole is a hero **already
travelling** to take up a governorship: `SpyNetworks.CanHandle` checks `GovernorOf` (still null in
transit) but not `IsTraveling`, the mod teleports the hero abroad, and the queued governor teleport
completes seconds later (Hajara, Simir: 3-5 real seconds). Separately, `StillHandles` never checks
the handler is still in the target realm, and vanilla's relocation moves AI handlers home (7 of 7
AI handlers seen outside their target on day 15) while the network keeps growing.

Levers without Harmony: `CanHandle` rejects `IsTraveling` (and `IsFugitive`, `IsReleased`);
`CampaignEvents.CanHeroLeadPartyEvent` and `CanMoveToSettlementEvent` vetoes for a handler. Residual:
vanilla's second pass for a clan with no other free lord ignores the party veto.

**Three questions for the lead**
1. The handler lever: **recommend yes** to `IsTraveling` in `CanHandle` and the two event vetoes;
   no Harmony.
2. Should AI espionage read bands, like R-1 does for war? **Recommend yes** - 2 of 3 plans were
   decided by figures the player is shown only as a band.
3. Keep or drop the player's-house exemptions (#3, #4): the lead's call; this run has no case either way.

---

## 5. Balance runs 09A / 09B / 09C

| | 09A | 09B | 09C |
|---|---|---|---|
| Save | `di_fresh_1084` | `di_fresh_1084` | `di_pretender_test` |
| Setting | statecraft on | `test_statecraft off` after load (0 `skill_xp` events confirms it) | Battania rises on the first tick |
| Length | 20.2 years (242 snapshots) | 10.1 years (121) | 4.9 years (59) |
| Wars ended | 120 | 67 | 23 |
| Mean war | 77.6 days - PASS (< 252) | 67.2 - PASS | - |
| Ended at the table / dormant / defection | 65.8% / 7.5% / 10.0% | 67.2% / 10.4% / 11.9% | 69.6% / 21.7% / 0 |
| Weeks with every kingdom at war | 36.8% (run 08: 31%) | 42.1% (run 08: 46%) | - |
| Weeks with nobody at war | 0 | 0 | - |

**09C was not hands-off:** the lead clicked in the game window during it, and Khuzait made white peace
with Aserai on Summer 1, 1086 by hand. Nothing from that peace is counted as evidence.

### The ten questions

1. **Prisoner share of a war's closing score (design 10, D2).** 09A mean 45.6%, median 48.7%;
   prisoners outweigh battles in 51 of 120 wars and pull the opposite way in 18. 09B: 48.7%, 32 of
   67. Peace-table wars only: 45.8%. 30 of 120 wars in 09A closed at |score| ≥ 75, the subjugation
   cliff. **The term is half the score, as the first live check warned.**
2. **Tributary pacts and subjugations per decade.** 09A: tribute at the table 1 then 0; "submit as a
   vassal" 9 then 7; kneeling (submission or defection) 3 then 9; vassalage by all routes 12 then 19.
   09B, first decade: tribute 1, subjugation 9, kneel 8, vassalage 17. Wars stay far under 252 days.
3. **Is the §13 tribute band still unreachable?** Yes, practically: **2 tributes at the table in 187
   settlements** across 09A and 09B (run 08: 0 of 123). Design 10's new score did not open it.
4. **Indemnities.** 09A: 24 paid, 4.08M denars, mean 169,875, mean 20.7% of the treasury, max 30%,
   **0 at the 40% cap**. 09B: 13, mean 23.1%. Whether any realm fell under `AiGoldReserve` (50,000)
   afterwards is **not measured** - the analysis does not read rulers' gold after a payment.
5. **Tribute demands.** 09A: 5 pairs accepted, 3 pairs refused by their court, the cap bound once,
   the cooldown once. 09B: 1 pair, refused by its court. `courtShare` separates cleanly at
   `refusesAt` 0.34: every acceptance at ≤ 0.11, every refusal at ≥ 0.40. Demands are rare (6 pairs
   in 20 years) - consistent with 3.3.5: few pairs reach the 2.0 strength ratio.
6. **Court terms on AI war declarations (R-1).** 09A: all 84 declarations carry them; the own-court
   term is always hawkish (mean +3.26, never dovish - Doves need exhaustion above 40, and a realm that
   tired does not declare); target weakness mean +2.48. **3 of 84 would have fallen under the bar
   without both terms.** The bigger effect is on peace: 65 of 79 AI peaces came at a lower bar
   (sooner), 14 at a higher. 09B: 0 of 45 depended on the terms.
7. **Legal neglect and F3 defections.** Legal neglect in 26.5% of link-weeks (09A) and 27.2% (09B),
   mean −8 Hold when present. **Defections: 12 in 09A, 8 in 09B, against 1 and 2 in run 08.**
   Northern Empire alone defected seven times in 09A, changing patron eight times in 18 years -
   each time to the kingdom attacking it (Khuzait ↔ Aserai ↔ Southern Empire ↔ Western Empire).
   Western Empire did the same seven times in 09B. Hold sits low on most links: patrons answered 4
   calls and refused 9 in 09A, and most links show `prot 0`.
8. **Internal wars, side changes, restitution, cadet splits.** 09A: one internal war in 20 years
   (Western Empire, in its last month), 1 side change, 0 captures; 3 cadet splits (13-33% of the
   house's influence). 09B: none, no split. Run 08 had none in 20 years. A civil war is still rare in
   an unstaged world.
9. **Pacts, first decade, statecraft on against off.** 37 against 36. **Run 08's 47 against 31 did
   not reproduce.**
10. **Errors and warnings.** None, in any of the four logs.

### 09C - the civil war

| Rising | Began | Ended | Days | Outcome | Rebels | Captures |
|---|---|---|---|---|---|---|
| 1 | Summer 20, 1084 | Winter 18, 1084 | 40 | Stalemate | 4 | 0 |
| 2 | Winter 17, 1085 | Summer 17, 1086 | 42 | Stalemate | 4 | 0 |
| 3 | Summer 17, 1087 | Winter 12, 1087 | 37 | Stalemate | 5 | 0 |
| 4 | Winter 13, 1088 | - | - | ongoing at the end | 3 | 1 (Uthelaim Castle, from the crown) |

- **Each rising follows the previous one's 84-day cooldown almost to the day.** Legitimacy 25, 27, 3,
  then 0. A draw leaves the crown weaker and the next rising bigger.
- **Side changes:** 8, all paid (5 to the crown, 3 to the rising), 210,500 denars in all; single
  prices from 9,300 to 32,900. Rulers' purses at those moments were not recorded.
- **Conceding at 75 never ended a war.** Rising 2 ended with the rebels at 75.2 and the crown at 40.2:
  the concession rule needs the other side **below** 40, the stalemate rule fires at **≥ 40** on both,
  so a spent rising was scored a draw (D-10).
- **No crown win, so no restitution**; no cadet split.
- **Elsewhere at the same moment:** Southern Empire legitimacy 0.0, Northern Empire 7.2, Western
  Empire 32.0, Aserai 37.0. Why crowns sink this far in five years was not investigated.

---

## 6. Defects and corrections

| # | What | Evidence | Proposed fix |
|---|---|---|---|
| D-1 | `court_bands` never says the realm is at war with itself, while the AI's term reads "+4 at war with itself" | 3.1 partial, run09_roundtrip | add the internal war to the bands page; a civil war is public |
| D-2 | With intrigue off, `war_value`'s parenthesis still lists "+3 / +2" behind a 0 | 3.1.6 | say the pillar is off instead |
| D-3 | Realm tab: the Hold chip row overflows into "Our claims" with nine or ten chips | 3.6.4, screenshot | wrap the row or fold the small terms |
| D-4 | Peace table header "losing at score -16" while each row says "against a war score of 0"; "+0" printed for exactly 0 | 09C prose | rows read the offer's budget, the header the score - label them apart; fix the sign test |
| D-5 | `check_blockers` reports "clear" while the mod's peace table is open and the clock stopped | run09_roundtrip | runbook: also look for `DiPeaceTableLayer` |
| D-6 | `ui/get_inquiry` fails on a vanilla Kingdom Decision (`Incident.GetOptionHint` not found) | run09_roundtrip, game v1.5.3 | GABS-side; note in CLAUDE.md §2 |
| D-7 | Espionage: `CanHandle` accepts a hero travelling to a governorship; `StillHandles` ignores where the handler is | §4 | reject `IsTraveling`; decide whether the station is enforced or dropped from the fiction |
| D-8 | Mission odds shown at launch are not the odds rolled | §4.3, 68% → 71% | roll against the launch odds, or show that they move |
| D-9 | A peace table listed an indemnity of 170,000 where the rule gives 168,000 | run09_roundtrip, NE gold 562,172 | later staged checks round down correctly (868,000; 132,000); the 170,000 is unexplained - recheck the treasury at that moment |
| D-10 | Internal war: a spent rising (75.2) against a crown at 40.2 ends in stalemate, not a concession | 09C rising 2 | make the two rules meet without a gap, e.g. concession when one side ≥ 75 and the other is lower |
| D-11 | Two documents state something this run disproved: STATUS.md says the zero-manpower siege cause was "found in code and fixed"; design 03 §10 says vanilla does not ask `CanHeroBeGovernor` | 3.10.2, §4 | correct both (CLAUDE.md §5) |
| D-12 | **Design question:** two vassals of the same patron went to war with each other because one answered an outside ally's call; `CallToArms.Applies` does not consider a shared patron | 3.6 step 3 | **Decided 2026-10-01 (lead): stays allowed.** Seen again live the same day |

Observations, not defects:
- **2,385 of 6,034 battles (09A) score a side with 0 men fielded** - almost all field battles where the
  defender is villagers, a caravan or a patrol (`no(army)`). By design 10 they count for neither side,
  but the attacker's own losses still count, so a lord who beats a caravan loses a fraction of a
  point (−0.05 to −0.15). Small, and in the direction design 10 chose; worth a sentence in design 10.
- **Khuzait ends 09A at dominance 1.92, greed 0.45**, +12 fortifications, top kingdom for 9 of the
  last 10 years. 09B's leader changes every few years. One run; worth watching in the next.
- Militia die in sieges and count for nobody (95, 57 and 159 deaths in three sieges).

---

## 7. A retry on 2026-10-01, and what it found

To finish 3.9.3-5, `di_pretender_test` was loaded again on 2026-10-01 with the **installed release
DLL (0.2.0, built against the v1.4.8 reference assemblies)** on this machine's v1.5.3, NavalDLC off.
The rising failed on every daily tick:

`MissingMethodException: Kingdom.InitializeKingdom(… 10 parameters …)`

- v1.4.8 declares `InitializeKingdom` with 10 parameters; v1.5.3 with 12 (two optional
  `Nullable<uint>` banner colours). Read from metadata on both sides.
- The source compiles against either, because the new parameters have defaults; the compiled call
  binds to the exact signature it was built against. So the v1.4.8 build cannot start a rising on
  v1.5.3. The exception was caught and logged; the game did not crash.
- A scan of all 681 references from the release DLL into `TaleWorlds.*` against v1.5.3 found **this
  one only** (names and signatures; not enum values or behaviour).
- **Players on v1.4.8 are not affected.** 0.2.0 will lose civil wars - only those - on any player
  who moves to 1.5.x. CLAUDE.md §1's "the v1.4.8-ref build is verified to load and run correctly on
  v1.5.3" is too strong: it loads; this path does not run.
- **Fixed the same day, not yet run in a game:** `InitializeKingdom` is now called through
  reflection, choosing the overload whose first ten parameters are the known ones and whose others
  are optional, so one DLL serves both versions. The rebuilt DLL compiles against v1.5.3 and the
  v1.4.8 references, and the scan finds 0 unresolved references against either. Still proposed:
  adding the scan to `release.ps1`.

---

## 8. Not run, and why

| What | Why |
|---|---|
| 3.1.6, our-court and peace bars | the save gives 0 / ×1.000 with intrigue on too; needs a court with Hawks or Doves |
| 3.3.5 | no pair reaches the demand's claim and 2.0-ratio gates on `di_tribute_test` |
| 3.8 payment | needs a year of peace on the real clock |
| 3.9.3-5 | the only capture came late in 09C; the 2026-10-01 retry hit §7 |
| 3.10.4 | no agreement near expiry, no lever to set one |
| 4.5 staging | no case arose; the runbook says not to stage it |
| §6 Esc over the peace table; the intermittent launcher crash | need a person at the machine |
| Q4's gold reserve after an indemnity | the analysis does not read rulers' gold |

## 9. Decisions the run calls for

1. **The civil war's shape.** A month-long draw that repeats every 84 days while legitimacy sinks to
   0 - is that the intended end state of a strained court? It also leaves R-6 (captures and
   restitution) almost never exercised. Levers: the stalemate threshold, what a draw does to
   legitimacy, the cooldown.
2. **Defections.** 4-8× run 08's rate, with one kingdom changing patron every two years. Accept as the
   cost of legal neglect, or slow it (a cooldown on defecting, or a Hold floor right after one).
3. **D2's prisoner weights.** Half of every war's score.
4. **The §13 tribute band.** 2 of 187 settlements: still unreachable.
5. **Statecraft's acceptance (design 08 §12).** Run 08's two differences did not reproduce; the case
   that statecraft changes war volume or pacts now rests on nothing.
6. The three §4 questions.
7. The fix in §7.

## 10. Files

`run-09a.log`, `run-09b.log`, `run-09c.log`, `run-09-checks.log`; `run-09a-analysis.txt`,
`run-09b-analysis.txt`, `run-09c-analysis.txt`; `run-09-working-notes.md`; screenshots in
`run-09-shots/` (AI peace offer to the player, the indemnity row, the chip overflow).
