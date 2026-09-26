# Status — 2026-09-26

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
Branch `development`. `main` sits 85 commits behind on purpose: cutting a release is Phase 4's job.

## Start here — 2026-09-26

**Phase 2.8, Statecraft, is built and run live** ([design/08](design/08-statecraft.md) §16-§17).
The lead delegated D1-D10 on 2026-09-26 and asked for every open item to be handled and then
one test pass over everything. Each decision was taken as §15 recommended; the reasons are §16.

- **S0-S2 built** (commit `669a431`): the six political skills in ten terms, the XP grants,
  the Realm tab's Statecraft strip and the breakdown lines, `EnableStatecraft` (default on).
  **A-1 fixed a real fairness bug:** the player's Declare war now charges the AI's price (72 on a
  Conquest claim) instead of vanilla's 200. Firebrand and Silver Tongue reach the mod's own acts.
  No save data. No Harmony; one `GameModel` override.
- **Verified live** with every term predicted by hand first: design/08 §17 has the table. What is
  not verified is listed there too - most importantly, the regression proof was off against on,
  not against the previous build.
- **Balance run 08** is the S3 measurement: 10 years with the layer on, 10 with it off, from
  `di_fresh_1084`, 0 errors in both. The war economy is unchanged (chosen wars 71 vs 73 days,
  white peace 28 vs 29), and XP does not inflate skills (Envoy Charm median 232 to 235 over ten
  years). The one large difference is pacts (47 AI pacts with the layer on, 31 off), which one
  pair cannot separate from world divergence. It also answers design/04 §13.7. No civil war
  happened in either run. [balance/run-08.md](balance/run-08.md).

**The open items of 2026-09-26, handled:**

| Item | What was done |
|---|---|
| Unpushed commit `8e41561` (Phase 3.7) | Pushed with this work |
| 2.6c price lines named the player | "you"/"your" throughout the price column, for the buyer, the claimant and the player's own house |
| Diplomacy row read "Independent" after the player's own tribute demand | The row now rebuilds after any action that changes the pair on the spot (pact, tribute, renounce, war). Not seen live: no tribute demand was available on the test saves; a pact through the same wrapper was not completed either (the bridge clicked a vanilla "Propose" first; the button now has `Id="DiPactPropose"`) |
| A rebel player sees vanilla's Kingdom tabs as the rising (design question) | **Kept, on purpose**: those tabs are what a rebel commands (the rising's clans, fiefs and armies), and showing the realm would need Harmony on four vanilla VMs with no case under CLAUDE.md §3. The Court line now tells the player so |
| "Raise your banner?" never reached | **Reached by the real path** on `di_fresh_1084`: the player made a pretender at a contested succession (81%), the crown at legitimacy 25, `tick_days 1` - the prompt, answered "Raise the banner", and the war began ("you raised your banner") |
| The side-choice prompt at a war's start | **Reached** (Battania, Ergeon's rising): "Civil war in Battania … Join the rebellion / Stay loyal", answered Stay loyal |
| The 30-day captivity ending | **Holds**: the ruler held by a rebel party, 30 daily ticks, RebelsWon "held by the rebels for 30 days"; a claimant held by a *foreign* power correctly counts nothing |
| A cadet branch starting an internal war | **Reached the claimant stage, not the war.** A cadet founder stands at the next succession (it did, "Mengus 0% (1 clan)") but a new cadet branch has almost no influence, so it cannot reach the 30% a pretender needs. Finding: this path is structurally near-impossible soon after a split. A design question for the lead, below |
| Esc over the peace table | **Not testable here**: the machine had no display attached, so no key reaches the game (CLAUDE.md §2). Still open |
| `AiTributeCourtRefusalShare` untuned; how often the weekly demand fires | Run 08: the weekly demand was accepted twice in 20 in-game years (both in run B). Refusals are not logged, so the share could not be measured - a `tribute_refused` telemetry event is the next step |
| Long-run balance of side changes and concession at 75 | **Not measured.** Run 08 had no internal war in either half: a fresh 1084 world does not strain a court within ten years. Needs a run from a save with a low-legitimacy realm |

**A project review, the same day** (Claude, at the lead's request). The code is healthy: a clean
build, save declarations consistent, every campaign event handler and Harmony patch behind a
try/catch, and every player branch in the core rules one that asks the player rather than
deciding for them. Commit `65913a4` holds what the review fixed: a save-data check that build and
deploy now run (`scripts/check-save-ids.ps1`); stale "next free id" comments in six model files,
InternalWar's naming an id already in use; a static cache that kept the previous campaign alive
after every load (`AiDiplomacy.LastMoves`); dead code (`GrievanceRegistry.Forgive`); and the
war-veto patch split into one file per method as CLAUDE.md §3 requires, verified live. The review
also moved this file's history out. What needs the lead is in TODO.md, items 1-4 and 12: Phase 2's
acceptance line, the court and the AI's foreign policy, espionage's default, how rarely civil war
and tribute happen, and the unmerged 2026-09-24 mechanics review.

**Then 2.9, court verbs (R-2), the same day.** The lead decided R-2 and set the pricing rule
(CLAUDE.md §3); design/09 was written, decided and mocked up, and the lead approved the mockup.
**C1, make amends, is built and run live** (design/09 §8): every price matched a hand prediction,
the Court tab's two-click button paid, the save round trip held the new `Grievance` properties 6-7,
and an AI ruler made amends through the weekly pass. Running it corrected the spec twice (standing
now counts peer houses only; the AI scores the loyalty an answer really moves). Measured on
Battania one tick from rising, two amends shrank the bloc (61% to 56%) and the rising (5 houses to
4), and the rising still came, held by crown legitimacy 25 and by relation. **The lead then decided
(design/09 D16-D17):** the acceptance line means prevention, which C1 meets; and AI rulers consider
amends daily, before the internal-war check - built and run live the same day.

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.12)** | ✅ **accepted by the lead, 2026-09-23**. Code complete including submission and hegemony (1.9/1.10), the vanilla takeover (1.11) and power (1.12). Measured over runs 01–08; run 08 answered the §13.7 questions the §13 rework had left open. Carried debt: [ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23) and "Not verified — carried" below |
| **2 — Court intrigue** | 🔄 2.1–2.7 built and verified live on their main paths; **2.8 Statecraft** built and run live 2026-09-26 (S0–S2; S3 is run 08; S4 became design/09's C2; S5 traits waits); **2.9 Court verbs**: C1 make amends built and run live 2026-09-26, C2 and C3 not built. The acceptance line is met by C1 under the lead's reading (prevention, design/09 D16); formal acceptance is the lead's |
| **3 — Espionage** | ⏸ **parked by the lead, 2026-09-26.** 3.1–3.7 built and run live ([design/03 §10](design/03-espionage.md)). The AI's handlers are still taken by vanilla, so an AI network never grows (TODO 3) |
| **4 — Integration, balance, release** | 🔄 runs 01–08 archived; **run 08** is the current reference. The civil-war balance needs its own run (TODO 4) |

## What to do next

**Phase 2's court verbs (R-2), [design/09](design/09-court-verbs.md).** C1 is built and run live.
The lead's D16-D17 are built. Next: C2 offices and patronage, then C3 tribute per vassal, and the
lead's formal acceptance of Phase 2 when they choose. Espionage's default and civil war's rarity
stay as they are, the lead's call of 2026-09-26.

Work that needs no decision:

1. **A civil-war balance run** from a save with a strained court. Run 08 saw no internal war and
   no contested succession in 20 in-game years.
2. **A `tribute_refused` telemetry event**, so `AiTributeCourtRefusalShare` can be tuned.
3. **Espionage, when the lead unparks it:** the 3.6 handler blocker first (TODO.md, pending work).
4. **The carried unverified items below**, whenever a session is in the game anyway.

## Not verified — carried

Short on purpose; each line points to where the detail is.

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
- **Phase 3:** the AI handler blocker, two 3.5 wording faults, a reloaded bribe offer
  ([design/03 §10](design/03-espionage.md)).
- **2026-09-26:** the `DeclareWarAction.ApplyByKingdomDecision` prefix since its split: applied,
  not run.
- **2.9 C1:** the AI skipping an answer that moves nothing, in a case where that changes its pick; a
  player serving an AI king being told the king answered their house; the Encyclopedia ledger,
  which now leaves answered records out ([design/09 §8](design/09-court-verbs.md)).

## Saves

| Save | State |
|---|---|
| `di_fresh_1084` | **Summer 1, 1084, pristine start, hero parked in Myzea.** The run-08 baseline |
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
