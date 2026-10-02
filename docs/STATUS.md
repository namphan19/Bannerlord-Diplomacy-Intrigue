# Status — 2026-10-02

Point-in-time state, and only the current part of it. [CLAUDE.md](../CLAUDE.md) holds what is
always true. [TODO.md](../TODO.md) is the one list of open decisions and pending work.
[STATUS-history.md](STATUS-history.md) keeps every earlier handoff, checkpoint and verification
table, verbatim. When a section here stops being current, move it there: by 2026-09-26 this file
had grown to 1,266 lines, most of it history, and the "What to do next" in its middle still called
Statecraft "not yet started" on the day it was built.

Module version 0.2.0 (released 2026-09-28; 0.1.0 went to Nexus on 2026-09-26 from `4adba72`). Save schema **v4**, definer base id **2749100**. The save ids in use and
the next free ones are kept in CLAUDE.md §3 and nowhere else; `scripts/check-save-ids.ps1`
checks the declarations before every build and deploy.
Last completed measurement: **balance run 08** (statecraft on/off) — [balance/run-08.md](balance/run-08.md).
Branch `development`. `main` sits well behind on purpose: cutting a release is Phase 4's job.
opencode was removed from the project on 2026-09-27 (CLAUDE.md §7).

## Start here — 2026-10-02: Phase 3 completion - all dev work done, run 11 next

**Every story of Phase 3's completion is built and on `development`; nothing since the merges has run in
a game.** The next step is **run 11**, one run that validates all of it ([run-11-runbook.md](balance/run-11-runbook.md)),
and **the lead reviews the runbook and answers its §0 first.**

- **3.11, the odds shown are today's** (built by the tech lead, dev B's track): the plan overlay says
  the roll uses the day's odds, an operation under way reads "N% to succeed now", and every result
  notice ends "Success was N% on the day". No save data. Compile only.
- **3.12 ST-2, espionage telemetry:** `[NETWORK]` weekly, `mission_launched`, `mission_resolved`,
  `espionage_exposed`, and `[KINGDOM]` fields for counter-intelligence, the ruling house's free members
  and its war parties. `analyse-log.py` gained ESPIONAGE and LOG HEALTH sections. No save data. Compile
  only on the mod side; the analyser tested on an old log and a synthetic one.
- **3.12 ST-1, the runbook**, which also gathers every AC 3.8-3.11 left open.
- Design 03 §9 records the lead's 2026-10-01 calls as decisions 14-18.

### The three merged stories

Stories 3.8, 3.9 and 3.10 were built by two devs on their own branches and reviewed and merged into
`development` by the tech lead (`a0ddb6d`, `3e75885`). The 3.10 branch carried 3.9's commit, so one
merge brought both; `feature/3.9-ai-reads-courts-as-bands` holds nothing more. Build and
`compile-check.sh` (v1.4.8 refs) pass, `check-save-ids.ps1` unchanged (19 classes, 146 members), no
Harmony patch added (6). **No test was run after the merge** - the lead's instruction; every live
result below is the devs' own, from before it.

| Story | What it does | Verified |
|---|---|---|
| [3.8](stories/3.8-handler-stays-posted.md) | A posted handler is refused a party (event veto), cannot be posted while in transit, and is fetched back to the station daily; each loss names its cause (`handler_lost`) | Live on one save for about a month: party veto holds on the common path; **2 `forced-party`** losses of one house (the residual, roughly 5 a year for that house - 3.12 measures the real rate); the move veto is not asked on the path that sends a lord home, so the daily return holds the station alone; a day-one `governor` loss in every session, likely carried in the save. AC1 and AC5's party count not seen. Full table in the story's §10 |
| [3.9](stories/3.9-ai-reads-courts-as-bands.md) | The AI picks bribe and forgery marks from the bands the player sees, tie-broken by public signs | AC1-AC3, AC5 live on `di_pretender_test`; AC4's three-save print not run |
| [3.10](stories/3.10-player-house-exemptions.md) | The player's house loses its two exemptions: it can be assassinated, and forged letters become a believe/dismiss offer | AC1 only. AC2/AC3 were blocked: no AI ruling house had a free lord to found a network |

**Added by the tech lead the same day, decided by the lead:** `diplomacy.test_found_network <hero> |
<kingdom> [| strength]`, a cheat-gated lever that founds a network without the handler rules, so 3.10's
AC2/AC3 can be staged (CLAUDE.md §2). Not run. Also a wording fix in the forged-letters offer (no
pronoun for the ruler).

**Next:** run 11 (above).

## Start here — 2026-10-01: the vassal summons (story 1.10c)

**ST-2 to ST-6 are built. Nothing has run in a game.** The mod compiles clean against the **v1.4.8
reference assemblies** (BUTR `1.4.8.119303`, laid out as a stand-in game folder — the only failure
is the documented `MapEvent.BattleTypes.SiegeAmbush` one, which `compile-check.sh` also drops), and
against the local v1.5.3 install. `scripts/check-save-ids.ps1` passes: 19 classes, 146 members,
18 containers. **No Harmony patch, no new enum, no new save type beyond the two the story named.**

- **What a hegemon's ruler can now do:** order up to ⌈N/2⌉ of a serving vassal's war parties under
  their own command — but **only inside the obligation war that vassal is already fighting for
  them** (D5), at most half its eligible parties nearest the summoner first, for 20 days or until
  that war ends, refused below Hold 40 through the **existing** defiance path, and excused at no
  charge when the vassal is spent, at war with itself, besieged, or left alone by its patron (D7).
  A summoned party that would be marched at a kingdom its own realm is not fighting comes home
  before the army gets there (R3).
- **Save data:** `Treaty` 19 `LastSummonedOn`, class 19 `SummonsRecord` + container, `ModState` 19.
  **The block of class ids below the enum ids is now empty** — the next class takes 29 or above.
- **Commands:** `diplomacy.summons [kingdom]`, `summons_value A | B`, `ai_summons [kingdom]`,
  cheat-gated `test_summon A | B`. `tools/analyse-log.py` reads the `[SUMMONS]` lines and the five
  `summons_*` telemetry kinds.
- **ST-1's IL answer, written up** in [design/04 §5.2a](design/04-hegemony.md#52a-the-summons-may-be-a-real-army-after-all-st-1-2026-09-30):
  a real `Army` is safe, and — a finding that matters for D2 and D8 — **the leader's clan pays
  vanilla's own per-party cost and cohesion upkeep on top of our price.**
- **Two things were decided without the lead and should be confirmed:** a summons needs an army
  the summoner already commands (the button says so when they have none, rather than raising one
  for a king who has not chosen to), and the player-vassal inquiry has **no timeout** — silence
  cannot earn a mark of defiance, at the cost of the clock stopping, which R9 already accepts.
- **Next:** ST-7 live verification on `di_phase1_full` and `di_naval_test`, then ST-8's balance run.

**Tech-lead review, the same day - four faults fixed before anything ran:**
- **No summons could ever be served.** `Issue` stamps the cooldown, then `Serve` re-asked
  `QuoteFor`, which found "summoned 0 days ago" and marched nobody - after charging the full price.
  The answer now re-asks only the world's gates, not R7's (`QuoteFor(..., answering: true)`).
- **D7's excuse could never fire.** It asked `Hegemony.AnswerTo` about the *service* war, which the
  vassal declared and the patron is in; it now asks about each of the vassal's other wars.
- **R9: a player who is a lord of an AI-ruled vassal could have their party taken** without being
  asked. The player's house is now excluded unless the player rules the vassal.
- An order to an excused vassal now logs `[SUMMONS] excused` with its reason (AC5); the cooldown
  constant's comment says what the code does (from the order, not the return); the `Move` enum's
  indentation.
- **Still open for ST-7:** R3 is checked once a day against the settlement the army targets. A
  field battle with a third realm's party is not covered, and whether vanilla pulls an attached
  foreign party into such a battle is unverified.

**ST-7, first live pass, 2026-10-01 (`di_hegemony_1166`, v1.5.3, local build, 0 `ERROR` / 0 `WARN`).**
Staged with a new cheat-gated lever, `diplomacy.test_set_hold <patron> | <vassal> | <value>`: a link
made by `sign_treaty` has no starting Hold and drifts below the 40 line within a day, so nothing
downstream of "serves" could otherwise be reached.
- **Served (AC3):** Vlandia (AI) called up Southern Empire in the obligation war against Aserai:
  `[SUMMONS] issued … n=19 price=623inf/114500gold`, then `served n=19`. Influence factor 0.68,
  gold factor 1.00, Hold 84. The patron's ruler held 65k influence and 4.6M gold, so the AI's
  budget share never bound; the vanilla per-party charge showed as the rest of that week's
  influence drop (about 1,300 against our 623).
- **Save round trip (AC8):** saved with the summons live, restarted the game, reloaded: the record
  was still there (`1 live summons`) and **all 19 parties were still in the army** - membership
  needs no re-attach code. A save made before the field loads with the list empty.
- **Ended:** the army lasted 19 of the 20 days, then the engine disbanded it and the parties went
  home; `released reason=army-gone` fired the same day (`expired n=0` before that fix, one day
  later). **Why that army ended on day 19 is not known** - whether its cohesion ran out under 19
  extra parties is a question for the next run, not an answer.
- **Gates read live:** the war named, "commands no army", "serving in no war right now - it is
  fighting one for <its old patron>", "already marching". `summons_value` no longer prints a price
  of 0 or an empty "would refuse" for a quote refused early; the Realm tab's button no longer says
  "Summon 0 parties - 0 influence, 0 denars".
- **NOT verified, and the first things to run next:** the refusal and its defiance mark (AC4), D7's
  excuse (AC5), the player-vassal inquiry (AC9), R3's release before a third realm's settlement
  (AC6), ending on a broken link (AC7), the Realm tab's two-click order under War Sails (AC12),
  and the 20-year balance run (ST-8). No army or Hold can be set from a tool; an army of the
  patron's ruler was only ever reached by waiting for the AI to raise one.

**Story 1.10d (the bound patron chooses), tech-lead review 2026-10-01 - built, compiled against
v1.5.3 and the v1.4.8 references, not run in a game.** Fixed in review:
- **The player was quoted a price the breach did not charge.** The inquiry printed the AI's
  valuation terms (trust × the treaty's worth, legitimacy at a quarter) as if they were the charge:
  "-18 trust" for a truce that costs 35, "5 legitimacy" for a breach that costs 20, and nothing at
  all for the −12 every other court takes. It now prints what `TreatyRegistry.Break` charges.
- **An exhausted patron was still asked.** The bound path skipped `WillingToAnswer`, so a patron
  past `CallToArmsRefuseAboveExhaustion` was offered the choice that R1 says it never gets.
- **Tribute lost counted tribute the attacker paid anyone**, not only the patron.
- The legitimacy term is weighed only while intrigue is on (the only time it is charged); three
  `§` signs had been re-encoded as `Ã‚Â§`; a doc comment the new section was spliced into; a typo.

**1.10d, first live pass, 2026-10-01 (`di_hegemony_1166`, v1.5.3, 0 `ERROR` / 0 `WARN`).**
- **The AI always honoured the vassal, because the breach cost came out negative.** Run live:
  `the breach: treaties -21 ... => costs -6`. `TrustTreatyBrokenVictim` is -35 and was added as it
  stood, and `Observers` (already a positive cost) was *subtracted*. Both signs fixed in
  `Hegemony.BoundChoiceTermsOf`; the same truce now reads `treaties 21 ... costs 36`. **Nothing
  but a live run showed it** - the code read correctly line by line.
- **AI, honour the vassal (AC1, AC2):** NAP with Aserai, Aserai attacks Southern Empire:
  `[PROTECT] bound-choice ... treaties=NonAggressionPact`, `Vlandia broke NonAggressionPact with
  Aserai - Aserai now has a casus belli`, `answer=honour-vassal ... joined its vassal's war`. The
  link's protection term went to +20.0 and legal neglect to 0.
- **AI, honour the treaty (AC3):** NAP + DefensivePact (cost 86) against a link at Hold 10 (worth 75):
  `answer=honour-treaty ... the link is worth 75 against 86 for the breach`, matching
  `diplomacy.bound_choice` run beforehand, and `legal neglect -10.0` on the link.
- **Player, break and defend (AC6, partly):** the inquiry opened (`inquiry_active`) and the answer
  `Vlandia broke DefensivePact with Khuzait ... joined`, `source=player`. **The inquiry's text was
  never read** - see the next point. Not run: its timeout, AC4 (the forced failure), the exhausted
  patron, a patron that is itself a vassal of the attacker.
- **Driving it: a scene notification can sit over the mod's inquiry, and `answer_inquiry` answers
  the inquiry beneath it.** `test_player_rule` raises "fen Calrain joined the Kingdom of Vlandia"
  and the bound choice queued behind it; `ui/answer_inquiry affirmative` was meant to dismiss
  the notification and instead chose "Break it and defend". Read the screen first; on a test save
  nothing is lost, and it is how this check passed by accident.
- Cosmetic: `diplomacy.bound_choice` with no treaty standing still opens "bound by treaty to ..."
  and "would break 0 treaty(ies)" after saying there is no choice to put.
- `diplomacy.sign_treaty ... Truce` is refused unless the pair is at war ("A truce needs a war to
  end"); use `NonAggressionPact` to stage a binding.
- Finding again (run 09 D-12): Western Empire, a vassal of Vlandia, answered its ally Aserai and
  declared war on Southern Empire, Vlandia's other vassal.

**Second live pass, 2026-10-01 evening - the outstanding 1.10c and 1.10d branches.** Staged with a
new cheat-gated lever, `diplomacy.test_raise_army <kingdom> [| <target>]` (the engine's own
`Kingdom.CreateArmy`, the ruler's party alone): an AI ruler without a party, like Vlandia's Perin
(a governor), otherwise left every branch past "commands no army" unreachable. Saves
`run10_stage` (Southern Empire serving Vlandia, Hold 95) and `run10_player_patron` (the same, the
player ruling Vlandia). 0 `ERROR` / 0 `WARN` in every session.

| Check | Result |
|---|---|
| 1.10c R3 (AC6): marching at a realm the vassal is at peace with | PASS after a fix - `released reason=would-fight-a-peace`. **The first run released 8 of 16**: `Release` walked `army.Parties` while `party.Army = null` removed from it. Now a copy; re-run released 16 of 16. For a player-led army the test reads where the player marches, not the army's AI target - correct, and why the first attempt from inside a town released nothing |
| 1.10c, a shared enemy | PASS - a summons aimed at Aserai stayed through the tick |
| 1.10c AC7: the link broken mid-summons | PASS - `released reason=link-ended n=16` the tick after `break_treaty` |
| 1.10c AC4: refusal | PASS - price charged (174,100 gold), `[SUMMONS] refused hold=35`, mark 6 → 7, **vassalage renounced on the spot**. Trust fell **50.1**, not 15: the renunciation is a broken treaty on top. The docs and both player prompts now say so (`CallToArms.RenounceClause`) |
| 1.10c AC5 / D7: excused | PASS - `excused ... fighting Khuzait alone: Vlandia is bound by treaty to stand out of it`; no charge, no mark. Also seen: `its own territory is besieged` |
| 1.10c AC9: the player as vassal | PASS - Khuzait (AI) summoned Sturgia (player-ruled); the prompt named 9 parties, the refusal's cost and the price already paid, with **no timer**; "Send" served 9, and the player's own party stayed out of the army |
| 1.10d AC6: the player's prompt | PASS - the text shows what the breach charges (−35 with the attacker, −12 with every court, a claim, −20 legitimacy). Treaty names now read "defensive pact", not `DefensivePact` |
| 1.10d AC4: treaties broken, war refused | PASS - `honour-vassal FAILED ... broke DefensivePact and the war was refused`; Vlandia was not at war with Khuzait |
| 1.10d: the prompt's timeout | **24.0 real seconds** (opened 17:45:04.869, closed 17:45:28.860, unanswered) - see the open question below. The log called it "declined by the ruler"; it now says "no answer before the prompt expired" |
| Call-to-arms refusal on a link with marks ≥ 1 | seen by accident: Southern Empire at Hold 38 refused Vlandia's call and was renounced at once |
| Not run | the exhausted-patron gate (no exhaustion lever); a patron that is itself the attacker's vassal; AC12 under War Sails (NavalDLC is off in the launch script at the lead's request); ST-8 |

**Decided by the lead, 2026-10-01: 60 real seconds** (`CallToArmsPlayerResponseSeconds`, renamed
from `...Hours`). D-12 (a vassal called against a fellow client of its own patron) **stays allowed**.
What was found: `CallToArmsPlayerResponseHours = 24` was passed as `InquiryData.ExpireTime`, which
`SingleQueryPopUpVM.OnTick` counts in real seconds of UI time (read by IL, v1.5.3), and the game is
paused while the prompt is up, so no in-game hour ever passes. A player vassal who looks away for
24 seconds is marked as defiant - since Phase 1. The summons prompt has no timer, by the same
reasoning.

`answer_inquiry` answers a mod inquiry first and dismisses a scene notification only when no
inquiry is queued (`dismissed_JoinKingdomSceneNotificationItem`); a notification left up blocks
`save_game` silently.

**Balance run 10, 2026-10-01 - [balance/run-10.md](balance/run-10.md).** 10.3 years, 79 wars, 0 errors,
mean war 56 days. **1.10d (AC7): met** - 13 bound choices, 6 defended the vassal, 7 honoured the
treaty, 7 treaties torn up, no patron lost them all. **1.10c (AC10): not met - the AI issued no
summons in 10 years.** Only 9.7% of link-weeks had Hold at or above 40 (the line a vassal serves
at), and vassals answered a patron's call twice. The mechanism is unreachable for the AI, not
broken; whether that is what is wanted is a question for the lead (run-10.md §4, recommendation: leave
it). `DefendAlly` was 31.6% of wars, under the 35% bar, up from run 09's 26-27%.

**1.10c, the lead's reachability update (2026-10-01, after run 10) - built and run live on v1.5.3.**
Three changes to `Summons.cs`: a vassal serves a summons from **Hold 25** (`SummonsServeThreshold`,
not the call to arms' 40); the summons' war may also be the one the vassal is *defending* and its
patron has joined; and the army may be any army of the patron's realm, not only the ruler's own.
Live on `di_hegemony_1166` (Aserai attacks Southern Empire, Vlandia comes to its defence): the war
was found, a realm army was found for a ruler who leads no party, and the vassal at **Hold 38.2**
(refused before) was ready to serve - 14 of 28 parties, `served n=14`, **0 errors**.
- **A defect in the new army choice, found and fixed live:** `FindArmy` took any realm army "at war
  with the enemy"; Vlandia, at war with two realms, had one army besieging the other, so the 14
  parties went in and the daily release (R3) sent all 14 home on the **first tick** - the price
  (498 influence, 58,400 gold) paid for no day of service. The chooser and the release now share
  one test, `ArmyMarchesOnAPeace`; an army the release would empty is skipped, and if every army is
  such a one the order is refused with that reason before anything is charged. Re-run: with the
  realm's armies all marching at the other war, "not possible: Vlandia's armies are marching at a
  realm Southern Empire is at peace with" (no charge); with an army aimed at Aserai, `served n=14`
  and all 14 still in the army two ticks later.
- **Not run:** whether the lower bar makes the AI summon in a long run (a second ST-8); the Hold-25
  refusal edge; the player-facing text and Nexus page still say a vassal below 40 "refuses summons"
  (the constant's own comment records that these are to be corrected, ST-9).

**Trust on a refused call, corrected in four documents.** Story 1.10c, design 04 §6.1 and a code
comment said a refusal costs −10 trust; `TrustCallToArmsRefused` has been −15 throughout. They
also said two marks make the vassalage "lapse at its next expiry", and the player guide that "the
next refused summons breaks it": in the code, **the refusal that brings the marks to two renounces
the vassalage at once**, and two marks earned otherwise only stop renewal. The player-facing
inquiries were already right - they read the constants.

**Also 2026-10-01: civil wars on v1.5.3.** The release DLL (built against v1.4.8) could not start a
rising on this machine's v1.5.3 - `Kingdom.InitializeKingdom` changed signature. Fixed by calling it
through reflection (`InternalWars.FindInitializeKingdom`), resolved before the kingdom is created, so
a failure no longer leaves a half-built kingdom behind. Compiles against both v1.5.3 and the v1.4.8
references; a reference scan finds 0 unresolved against either. **Not run in a game yet.**
Details: [balance/run-09.md §7](balance/run-09.md).

## Start here — 2026-09-27, evening: the delegated pass

**The lead handed every open decision to the tech lead** ("toàn quyền quyết định", 2026-09-27),
except the Kingdom-UI loose ends, and asked for it to be built with sub-agents and run later on
the lead's machine. It was: TODO.md's table "Decided 2026-09-27" has each decision in one line,
and the design docs carry the rules. **Nothing of it has run in a game.** Everything was
compile-checked here against the v1.4.8 reference assemblies (`scripts/compile-check.sh`), and the
save-id check passes. **The next step is [run 09](balance/run-09-runbook.md)**, on the lead's machine.

- **What changed for the player and the AI:** the court now moves a realm's peace bars and war
  value, and a rival's visible weakness is a reason for war; a threat relaxes the defensive-pact
  floor; trust bleeds from a war's first day; at most two tributes at once, and none re-demanded
  for a year after one ends; an indemnity priced against the loser's treasury; legal neglect at
  half weight; a cadet branch takes part of its house's influence; an internal war's captured
  fiefs wear the loser down and go back after a crown win; the legitimacy dividend needs a real
  year of peace; a retaken fief stops costing its owner.
- **Found and fixed on the way:** the bribe window (730 calendar days, meant two campaign years:
  now 168); the trust dividend paid for a peace that had been broken; an open bribe offer was
  re-rolled after a reload; a siege garrison that could count on neither side; stale figures in
  six design docs.
- **New save data:** `SpyMission` 12, `InternalWar` 15, `InternalWarMember` 2 (CLAUDE.md §3). Run
  09's first step is loading an old save.
- **The test levers need cheat mode now** (CLAUDE.md §2, step 3).
- **Build note for a cloud session:** the NuGet reference assemblies for v1.4.8
  (`1.4.8.119303`) lack `MapEvent.BattleTypes.SiegeAmbush`, which the lead's game has and
  `WarExhaustion.ScoresAsBattle` uses. `scripts/compile-check.sh` now builds a copy of `src/` with
  that one line dropped, and says why; the source is right for the game and the lead's build is
  unaffected.
- **Merged branches** (`feature/*`, `review/game-mechanics`) stay on origin for now, the lead's
  call; all of them hold nothing `development` lacks except the review branch's vendored skill.

## Start here — 2026-09-27

**Phase 2 is accepted by the lead (2026-09-27).** Like Phase 1's, the acceptance is a decision,
not a claim that everything under it is measured; the carried debt is one table in
[ROADMAP.md](ROADMAP.md#phase-2--accepted-by-the-project-lead-2026-09-27).

**Housekeeping, the same day.** The 2026-09-24 mechanics review is merged into `development` at
[reviews/2026-09-24-mechanics/](reviews/2026-09-24-mechanics/README.md), its register brought up
to date (TODO 12). Every `feature/*` branch on origin was checked (full history, not a shallow
clone) and holds nothing `development` lacks, as does `main`. They are **kept for now** (the
lead's call). `review/game-mechanics` still holds its three original commits and the vendored
`vietnamese-tech-writing` skill, which `development` did not take.

**Design 10, war score measured in the fighting, is built (W1+W2) and had its first live check**
([design/10](design/10-war-score.md)). It replaces the old fief/raid-based war score with one
earned from battles (each side's manpower lost, as a share of what it fielded, sieges included)
and the lords each side holds prisoner — the same rework that also touched war exhaustion,
`WarRecord`, `CoreBehavior`, `PeaceTable` and the diagnostics. Committed as `44a12a9`.

- **Live check on `testmod_claude_1`** (player rules Khuzait, four wars already running), Summer
  9 to Summer 17, 1084 at speed-up 30, 0 errors. Manpower snapshots and battle scoring matched a
  by-hand check for 3 of 4 sampled battles; the fourth (a siege where the defender's manpower read
  0) is unexplained and flagged for the next run rather than papered over (design/10 §9a).
- **D2 (prisoner weights) is reopened the same day it was decided.** Vanilla takes lords in almost
  every battle and holds many for weeks; at the agreed 20/10/5 uncapped, the prisoner term alone
  swung a war's score by 30-75 points within two weeks and pushed one kingdom past the
  subjugation cliff on prisoners alone. §7 of the design named this risk before the check; the
  check shows it is larger than written there.
- **The lead closed the decisions the same day** (design/10 §10): **D1 as recommended** (W 120,
  cap 30, +3 for a win over 100+ men), **D2 kept** at 20/10/5 uncapped despite the check, and
  **D6 dropped**. The code already held these values, so nothing in the build changed; the
  constants' doc comments now record the decisions. Still UN-TUNED: no balance run has measured
  them.
- **Two unrelated constants were corrected the same day, decided by the lead:**
  `GrievanceDecayPerDay` (0.02 → 0.0635, so an 8-weight grievance now fades in ~126 days, "a year
  and a half" as design/02 §1 always said — at the old value it took 4.8 years) and
  `InternalWarCooldownDays` (365 → 84, one real campaign year — 365 calendar days is 4.3 game
  years, likely part of why civil war has been rare in every run). Both were miscopied against
  the 84-day campaign year, the same class of error as the net6.0/net472 mistake. Design 07's
  cooldown line and design 10's own momentum half-life (was "70 days, about a season" — 70 days is
  nearly a year; now 42, two seasons) are corrected to match. No other constant has been checked
  against the 84-day year yet.
- **Not yet done:** the balance run design/10 §9 asks for (20 years, tributes and subjugations per
  decade, which component closed each war) — blocked on D1/D2 being settled, since the numbers
  would be re-measured under whatever weights the lead picks.

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.12)** | ✅ **accepted by the lead, 2026-09-23**. Code complete including submission and hegemony (1.9/1.10), the vanilla takeover (1.11) and power (1.12). Measured over runs 01–08; run 08 answered the §13.7 questions the §13 rework had left open. **War score is being reworked under it since 2026-09-27** ([design/10](design/10-war-score.md), built, live-checked, D1/D2 decided and D6 dropped 2026-09-27; its balance run is owed) — acceptance stands, but the numbers behind it are mid-change. Carried debt: [ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23) and "Not verified — carried" below |
| **2 — Court intrigue** | ✅ **accepted by the lead, 2026-09-27.** 2.1–2.7 built and verified live on their main paths; **2.8 Statecraft** built and run live 2026-09-26 (S0–S2; S3 is run 08; S4 became design/09's C2; S5 traits waits); **2.9 Court verbs** C1, C2 and C3 built and run live 2026-09-26. The acceptance line is met by C1 under the lead's reading (prevention, design/09 D16). Carried debt — no balance run with the court verbs, civil war's rate unmeasured, 2.8's own §12 unmet — is listed in [ROADMAP.md](ROADMAP.md#phase-2--accepted-by-the-project-lead-2026-09-27) |
| **3 — Espionage** | 🔄 **completion built 2026-10-02, acceptance is run 11.** 3.1–3.7 built and run live ([design/03 §10](design/03-espionage.md)). Resumed by the lead 2026-10-01 with stories 3.8–3.12 (design/03 §9, decisions 14–18): the handler stays posted, the AI reads courts as bands, the player's house is a mark, the odds say they move, and espionage telemetry. All merged; run 11 ([runbook](balance/run-11-runbook.md)) validates them and plays the acceptance scenario |
| **4 — Integration, balance, release** | 🔄 runs 01–08 archived; **run 08** is the current reference. **Run 09 is planned** ([balance/run-09-runbook.md](balance/run-09-runbook.md)): design 10's 20-year run, the second statecraft pair, the civil-war run, and a check of everything built on 2026-09-27 |

## What to do next

1. **Run 09, on the lead's machine** - [balance/run-09-runbook.md](balance/run-09-runbook.md): first the
   old-save load and the cheat-mode gate (§0), then the targeted checks (A), then the balance runs
   (B, C). `tools/analyse-log.py` reads the new telemetry.
2. **Then decide from its numbers:** the §13 tribute band (run 09 question 3), whether D2's
   prisoner weights stand (question 1), whether the indemnity bites too hard (question 4).
3. **Phase 3 and the same-rules principle** (the lead's question, 2026-09-27): the rules are shared,
   but four asymmetries sit outside them - AI networks never grow (vanilla takes the handlers), AI
   bribe targets are picked from exact rival figures the player sees as bands, and the AI never
   assassinates or forges against the player's house. Run 09 §4 gathers the evidence and the IL;
   the lead decides from it (TODO, "Still open").
4. **Still open, not blocking:** whether the peace hint should show a rival's bar as a band.

## Not verified — carried

Short on purpose; each line points to where the detail is.

- **Everything built on 2026-09-27** (the delegated pass): compile-checked only. Each item's check
  is in [balance/run-09-runbook.md](balance/run-09-runbook.md) §A.
- **Phase 1:** two peace-table surfaces have never been seen working, the multi-selection
  checklist against a real budget and the AI→player incoming offer (history, "What acceptance did
  and did not mean"). `ReconcileWithSiblings`, the AI choosing the dissolution rung, and
  `DissolveChains` ([TODO.md](../TODO.md)). What Esc does over the peace table: needs a display
  attached (UI-INTEGRATION.md §0c.7).
- **Phase 2, the civil war:** the long-run balance of side changes and of conceding at 75; how
  prices compare with purses in more than one kingdom; a cadet branch starting a war
  ([design/07 §6](design/07-internal-politics.md); the 2026-09-24 checkpoint in the history).
- **Phase 2, the rest:** a decisive win or loss moving legitimacy, the peace dividend, a fief lost
  and a caught fabrication, which need a real war score or the clock (history, "2.4"); the Court
  tab past ~13 sworn clans, and a physical click on its rows; the Diplomacy row after the
  player's own tribute demand (a pact through the same path was not completed either).
- **Phase 2.8:** the regression proof was statecraft off against on, not against the build before
  it, and design/08 §12's acceptance is not yet met ([design/08 §17](design/08-statecraft.md)).
- **Phase 3:** the AI handler blocker; the 3.5 wording and the reloaded bribe offer were fixed
  2026-09-27, not run ([design/03 §10](design/03-espionage.md)).
- **2026-09-26:** the `DeclareWarAction.ApplyByKingdomDecision` prefix since its split: applied,
  not run.
- **2.9 C3:** the AI raising a link to Heavy; a vassal player told of a new tribute; the Diplomacy tab's action grid overlapping a vanilla label on a ten-button row (design/09 §10).
- **2.9 C2:** the seat offered to a vassal player; a captured or dead holder; the AI world's balance with seats in it (design/09 §9).
- **2.9 C1:** the AI skipping an answer that moves nothing, in a case where that changes its pick; a
  player serving an AI king being told the king answered their house; the Encyclopedia ledger,
  which now leaves answered records out ([design/09 §8](design/09-court-verbs.md)).
- **Design 10, war score:** the 20-year balance run (run 09 B); a siege where the defender's
  manpower read 0 in the §9a check - a likely cause found in code and fixed 2026-09-27, with a
  decisive log line, not yet seen; carrying an old-save `WarScore` over
  as `BattleScore` under the new decay, never exercised on a real reload ([design/10 §9a](design/10-war-score.md)).

## Saves

| Save | State |
|---|---|
| `di_fresh_1084` | **Summer 1, 1084, pristine start, hero parked in Myzea.** The run-08 baseline |
| `testmod_claude_1` | Khuzait, player-ruled, four wars already running. Left at Summer 17, 1084 — the design/10 §9a war-score live check, stopped by an inquiry addressed to the player |
| `di_tribute_test` | `di_grievance_test` + a Khuzait <- Sturgia vassalage (by `sign_treaty`) set to Heavy on 2026-09-26, 16 days into its lock. The save for checking `Treaty.TributeSetOn` after a reload |
| `di_offices_test` | `di_grievance_test` after C2's checks (2026-09-26): Khada of Arkit holds Khuzait's Spymaster seat, Koltit took a seat back as a grievance. The save for checking `CourtOffice` after a reload |
| `di_amends_test` | `di_grievance_test` after one amends (2026-09-26): Urkhunait answered and remembered, then wronged again at 12.0, its next amends at x2. The save for checking C1 after a reload |
| `di_pretender_test` | **Phase 2's richest court:** Battania at legitimacy 25 with a standing pretender (Aradwyr) and a Pretenders bloc, rising on the first daily tick; Khuzait player-ruled with four would-be claimants |
| `di_civilwar_test` | **Overwritten 2026-09-24** by a "Save and Exit": Battania mid-war with the rebels at 11 fiefs, plus two cadet houses (Oburit of Sevin, Pethros of Patyr) |
| `di_civilwar_2_6c` | `di_civilwar_test` with the player's house as Battania's ruler and fen Caernacht bought back by the crown. The save for checking `SideChanges` after a reload |
| `di_grievance_test` | Khuzait, player-ruled, with a grievance ledger of 21 - the Court tab's ruler view was verified here |
| `di_espionage_test` | `di_run07_1104` + two Urkhunait (Khuzait) networks, 2026-09-25 — the 3.1 save-round-trip check |
| `di_espionage_missions` | same world, one `ReadCourt` mission left pending — the 3.2 save-round-trip check; also where the real clock was seen resolving it and vanilla taking a second AI handler as governor |
| `save007` | Khuzait, player-led — the save the Kingdom screen UI was verified on |
| `di_run07_1104` | Winter 1104, end of run 07: 7 kingdoms, 2 hegemons |
| `di_hegemony_1166` | Vlandia with 2 vassals. The only state holding a sphere built at the peace table |
| `di_review_0919_b` | Winter 15, 1162 — the old evolved world, pre-§12 |
| `di_review_0919`, `di_run06_resume` | run 06 checkpoints: Spring 1, 1159 and Winter 10, 1156 |
| `di_phase1_full`, `di_treaty_test`, `di_phase0_test` | the Phase 0/1 saves; `di_phase1_full` is the richest Phase 1 state |

Never save over `di_phase1_full`. Load any other expecting that whoever played it last may have
saved over it (CLAUDE.md §2).

## Decisions already made. Do not re-litigate.

| Decision | Detail |
|---|---|
| Three pillars | Diplomacy, court intrigue, espionage. **Not** economy/trade |
| Standalone | No dependency on the BUTR Diplomacy mod. Mutually incompatible with it by design |
| English UI only | Localization keys for future translation, English shipped |
| Minor factions out of scope | Treaties, claims and exhaustion are kingdom-only |
| AI plays by the same rules | Enforced in code — no "is this the player" argument anywhere |
| Enemy exhaustion shown as a band | Five bands whose edges are the behavioural thresholds. Phase 3 `ReadCourt` buys the exact figure |
| Rival courts shown as a band too | 2026-09-23. Own court fully legible, rivals qualitative only, exact figures sold by Phase 3. The same fork as enemy exhaustion, for the same reason |
| The player's clan is subject to intrigue | 2026-09-23. Serving a king is a political position, not a waiting room. Extends "the AI plays by the same rules" to Phase 2 |
| Kingdom decisions extended, not replaced | 2026-09-23. Revisit only when extending visibly constrains us |
| Vassalage stays in Phase 1 | And it carries military service; a tributary pays, a vassal pays and fights |
| Blocked routine path, deliberate defiance | The AI never wanders into a forbidden war; breaking a treaty on purpose is always possible and always expensive |
| Native dialogs, not a Gauntlet screen | A custom screen is the eventual goal and the most fragile thing a mod can own |
| `net472` | See CLAUDE.md §1 |


## Known gaps and loose ends

- **Battle and siege paths unverified.** Casualties reach exhaustion through `MapEventEnded`
  and fief capture through `OnSettlementOwnerChangedEvent`. Neither can be triggered from a
  console, so both need a real battle and a real siege. They are wired and reviewed, not
  observed.
- **Clock-dependent behaviour unverified** for the same reason: treaty expiry and its trust
  dividend, tribute changing hands on day 7, the two-year peace dividend.
- **Menu navigation past the root** has not been clicked through in a `MultiSelectionInquiry`.
  The root renders correctly (screenshot) and the vassal/ruler distinction works. The blanket
  claim that this needs a human is **too strong** and was corrected on 2026-09-20:
  `ui/click_widget` drove the whole of character creation, so GABS is not limited to the map
  layer. Whether it reaches a `MultiSelectionInquiry` specifically is **untested** — worth ten
  minutes before asking the lead to walk the submenus by hand.
- ~~`AiDiplomacy.TryDemandTribute` accepts on a strength ratio and a trust floor only~~ - the
  target's court answers since 2026-09-25 (design/02 §7.1).
- **A vassal's existing wars are untouched when it submits.** Signing vassalage does not end
  the client's own wars. Since the run-04 review the patron is called into the ones the vassal
  is *defending* (`CallToArms.DefendNewVassal`) and may refuse at the usual price; wars the
  vassal started stay its own.
- **`ConcessionLadder` yields castles before towns** via a two-pass flag that reads awkwardly
  (`townsFirst: false`). It works; it would read better as two explicit loops.
- **The game sometimes dies on startup from the official launcher.** Intermittent since
  2026-09-15, not root-caused, and nothing yet points at this module. The evidence, and the crash
  logging added because of it, are in [STATUS-history.md](STATUS-history.md), "Intermittent".

## Traps that are not in CLAUDE.md §1


- **Parking the hero is not optional.** Crossing the map to a town, the party was stopped by
  bandits **twice**; each halts the clock until something clears it.
- Two diagnostics were lying and are fixed: `diplomacy.submission_value` called `CanSign`
  without `settlesWar` (so it reported the entire attacker route as impossible), and
  `diplomacy.offer_peace` had no term for the dissolution rung.

The §12/§13 design detail that used to fill this section lives in
[design/04 §12–§13](design/04-hegemony.md); the run-07 measurement is in
[balance/run-07.md](balance/run-07.md). The corrections made on 2026-09-20 — the
`SpeedUpMultiplier` lever, the war-score bleed figure — were folded into CLAUDE.md §1 and
are not repeated here.

## Tools written for this project

| | |
|---|---|
| `tools/LoadProbe` | Pre-flight: target framework vs game host, reference resolution, `SubModuleClassType`. Catches the class of failure that produces no log at all |
| `tools/ApiDump` | Dumps the real public surface of game types to `artifacts/api/`. Use before writing against any unfamiliar API |
| `diplomacy.war_value A \| B` | The AI war valuation term by term, naming the gate that blocks. Written after guessing wrong twice |
| `diplomacy.tick_days N` | N days of the **full** daily upkeep, real functions, clock unmoved |
| `diplomacy.hegemony` | every sphere, each link's hold, and the terms pulling it |
| `diplomacy.strength` | every kingdom ranked by the strength the formulas read, its share, fiefs, sphere, and balance against its patron |
| `diplomacy.submission_value A \| B` | what submitting to B is worth to A, term by term |
| `diplomacy.ai_week N` | N weeks of AI evaluation plus matching upkeep. Prints its own limitations past 4 weeks |
| `diplomacy.report` | Telemetry snapshot to the log plus a full world report to file |
| `tools/analyse-log.py` | Parses one run, across any number of logs, into the acceptance numbers and the power, hegemony, fief, coalition and engine timelines. `python tools/analyse-log.py <log> [<log> ...]` |
| `diplomacy.test_set_player_age N` | Test saves only: sets the player hero's age and cures an old-age illness, so a long run does not end on the Game Over screen |
| `scripts/check-save-ids.ps1` | The save-data rules of CLAUDE.md §3, read from source: duplicate ids, and classes, enums or containers missing from the definer. `build.ps1` and `deploy.ps1` run it first |
| `tools/CallSites` | Every call site of a game method, with its IL: `--callers Type::Method`. How the bloc-voting patch found its funnel, and how a lever for a test path is found |
