# Status — 2026-09-27

Point-in-time state, and only the current part of it. [CLAUDE.md](../CLAUDE.md) holds what is
always true. [TODO.md](../TODO.md) is the one list of open decisions and pending work.
[STATUS-history.md](STATUS-history.md) keeps every earlier handoff, checkpoint and verification
table, verbatim. When a section here stops being current, move it there: by 2026-09-26 this file
had grown to 1,266 lines, most of it history, and the "What to do next" in its middle still called
Statecraft "not yet started" on the day it was built.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**. The save ids in use and
the next free ones are kept in CLAUDE.md §3 and nowhere else; `scripts/check-save-ids.ps1`
checks the declarations before every build and deploy.
Last completed measurement: **balance run 08** (statecraft on/off) — [balance/run-08.md](balance/run-08.md).
Branch `development`. `main` sits well behind on purpose: cutting a release is Phase 4's job.
opencode was removed from the project on 2026-09-27 (CLAUDE.md §7).

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
| **3 — Espionage** | ⏸ **parked by the lead, 2026-09-26.** 3.1–3.7 built and run live ([design/03 §10](design/03-espionage.md)). The AI's handlers are still taken by vanilla, so an AI network never grows (TODO, "Still open"). Fixed 2026-09-27 while parked: the bribe window in campaign years, the 3.5 wording, the reloaded bribe offer |
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
