# Status — 2026-09-25

Point-in-time state. [CLAUDE.md](../CLAUDE.md) holds the things that are always true; this
file holds what changes. Update it when you finish a chunk of work.

Module version 0.1.0. Save schema **v4**, definer base id **2749100**.
Save ids in use: `Treaty` 1-17, `TrustRecord` 1-6, `ModState` 1-16, definer class ids to 16
(`SpyNetwork` 3.1, `SpyMission` 3.2), enums 20-27. Next free: class id **17**, `ModState` property **17**, enum
**28** (CLAUDE.md §3 has the per-type detail). The 2.6 ids (13, 14, 27, property 14) are on
`development` since 2026-09-24, and so is 2.6c's `InternalWar` property 14 (`SideChanges`);
next free on `InternalWar` is 15.
Last completed measurement: **balance run 07** — [docs/balance/run-07.md](balance/run-07.md).

## Start here — handoff, 2026-09-25

**Phase 2 is built. Phase 3, espionage, has started (the lead's call, 2026-09-25).**
[design/03-espionage.md](design/03-espionage.md) now records the lead's five decisions (§9) and
what was built (§10).

- **3.1, spy networks: built and verified live**, merged into `development`. A network is owned
  by a clan and sits in a target realm, run by a handler of that clan stationed in the realm's
  richest town, and grows weekly out of the owner's purse. Checked by hand against the game,
  including a save round trip (`di_espionage_test`) and the campaign's own weekly tick.
- **Found, for 3.6:** vanilla made both AI handlers governors within days. An AI handler has to
  be kept out of vanilla's appointments before the AI can run networks.
- **3.2, missions, with 3.4, exposure: built and verified live**, merged into `development`.
  Six of eight missions have their effect; BribeLord and ForgeLetters wait for 3.5. An exposure
  hands the victim a casus belli against the owner's realm, costs trust, captures the handler, and
  costs a vassal house its crown's favour. The player is told of every operation that lands on
  their realm or drags it in. §4's missing difficulty was set as (requirement - 15) / 200.
- **Next: 3.5, the cross-pillar effects** - BribeLord into loyalty and civil war, ForgeLetters into
  grievances, ReadCourt's exact figures on the Encyclopedia - then 3.3 counter-intelligence
  budgets, 3.6 the AI, 3.7 the UI.

**The side branch of 2026-09-25, `feature/side-civil-war-gaps-tribute-court`, merged into
`development`.** It was briefed for opencode, but the bridge was broken, so Claude built it.
0 errors in 11 game sessions.
- **The tribute revisit that 2.2 owed.** A demand now asks the target's court: an AI crown
  refuses when paying would leave a third of its court, by influence, a defection risk
  (design/02 §7.1). **A fault was found and fixed:** the weekly scan signed a tributary pact in the
  name of a player-ruled target. The player is now asked, with their own court shown in full.
  Verified live: the AI refusing on its court, the AI imposing, the player's button refused in
  bands and then accepted, and the player's inquiry refused, re-asked inside the cooldown, and
  accepted.
- **2.6c's two unseen places, run.** An AI leader's offer to the player's house, both answers,
  and the player as claimant: buying a house, and conceding. Three display bugs were fixed
  (design/07 §6, "The second live run").
- **New levers:** `tribute_value`, `test_demand_tribute`, `test_player_rule`,
  `test_expire_treaty` (CLAUDE.md §2).

### The handoff of 2026-09-23, kept for its Phase 2 detail

**Phase 1 is accepted and closed. Phase 2, court intrigue, is the work now.**
2.1-2.5 and 2.7 are built and verified live (sections below). **2.6, war inside a kingdom, and
2.6b, a house divided by its succession, are built and verified live** on branch
`feature/phase-2.6-civil-war`, merged into `development` on 2026-09-24.
[design/07 §3d and §5](design/07-internal-politics.md) have the result tables.

**2.6c, the civil war on screen: built, run live, merged into `development`.** Decided with the lead on
2026-09-24, and built the same day on `feature/phase-2.6c-civil-war-ui`. Either leader can
concede. A house can change sides mid-war for gold, the player's included. The war is shown on
the Court tab, with a pointer on the Realm tab, and the rising is kept off the Diplomacy tab.
Run live on 2026-09-24 on `di_civilwar_test`, from all three places a player can stand, with 0
errors. Five display bugs were found and fixed. Not yet seen: an AI leader's offer to the player's
house, and the player as claimant. One design question is open for the lead: a rebel player
sees vanilla's Kingdom-screen tabs as the rising. Details are in design/07 §6, "The first live
run".
[design/07 §6](design/07-internal-politics.md) has the rules and the price formula. The mockup
is the "Civil war" row of the court canvas (https://claude.ai/artifact/1FrpG5in328WYfNi6sP8Pf).

### Checkpoint, 2026-09-24

- **Branch:** `development` holds 2.6/2.6b, opencode's Phase 1 UI
  (`feature/phase1-ui-match-mockup`: the tabs matched to the mockup, and the peace table as its
  own popup) and 2.6c, all merged 2026-09-24. The two hand-resolved conflicts in
  `RealmVM`/`DiplomacyItemMixin` were run in game with an internal war going during the 2.6c
  test: the Realm tab rendered with no error. The `IsRealm` filter those conflicts kept was not
  really exercised, since the rising held no vassal and nobody had a claim on it. **One game for both clones**: check who is running it before deploying
  (CLAUDE.md §7).
- **Verified live:** all three outcomes (rebels win, twice naturally; crown wins; stalemate,
  with the 365-day cooldown holding), sieges and fiefs changing sides once rebels could raise
  armies, save and reload mid-war with captured fiefs, and houses dividing at a real death,
  including two ruling houses. Six sessions; five bugs found and fixed, one of them a crash.
- **Not verified - the whole civil-war line, in one place:**
  - *2.6c:* ~~an AI leader's offer to the player's house, and the player as the claimant~~ -
    **both run live 2026-09-25** (design/07 §6). Left: the price lines name the player rather
    than saying "you" (they are built in the Intrigue layer).
  - *2.6, carried:* the player prompts at the start of a war (rising as the claimant, choosing a
    side), the 30-day captivity ending, and a cadet branch going on to start an internal war.
    On 2026-09-25 the player became a claimant by the real path (a ruler deposed and kept as a
    pretender), but the war was started by lever inside the 365-day cooldown, so the
    "Raise your banner?" prompt was still not reached.
  - *Tribute, 2026-09-25:* `AiTributeCourtRefusalShare` 0.34 is untuned. On `di_pretender_test`,
    3 of 8 courts would refuse, one of them Aserai, whose crown read Secure. How often the weekly
    demand fires at all in a long run is unmeasured. It found no qualifying pair in three evolved
    test worlds without levers.
  - *Found, not fixed:* after the player's own "Demand tribute" succeeds, the Diplomacy tab row
    still reads "Independent" until the screen is reopened. That is pre-existing: the button
    path does not refresh the row.
  - *Balance, never measured:* how often houses change sides over a long AI-only run, whether
    concession at 75 ends wars too early (the one live war conceded a few days after load), and
    how prices compare with purses across more than one kingdom. Rulers held 144k-453k against
    prices of 2k-46k in the one war measured.
  - *Phase 1 UI (opencode's merge):* the peace table was verified by opencode over the Kingdom
    screen and the map. Not re-run in this session. What Esc does over it is unverified
    (UI-INTEGRATION.md §0c.7).
- **Open for the lead:** a player among the rebels sees vanilla's Kingdom-screen header and tabs
  (Clans, Fiefs, Policies, Armies) as the rising, because vanilla reads the player's map faction;
  the Realm and Court tabs show the realm. 2.6's behaviour, not 2.6c's.
- **Saves:** `di_civilwar_2_6c` (2026-09-24) is `di_civilwar_test` with the player's house as
  Battania's ruler and fen Caernacht bought back by the crown. It is the save for checking
  `SideChanges` after a reload.
- **Saves:** `di_civilwar_test` was **overwritten** on 2026-09-24 by a "Save and Exit" at the
  end of a session. It now holds Battania mid-war with the rebels at 11 fiefs, plus two cadet
  houses (Oburit of Sevin, Pethros of Patyr). `di_pretender_test` is unchanged and still the
  clean starting point: Battania rises on the first daily tick.
- **Test worlds for Phase 2:** `di_pretender_test` (Battania: legitimacy 25, standing claim by
  Aradwyr, Pretenders bloc - the richest court state) and `di_grievance_test` (Khuzait,
  player-ruled, 21 grievances). Neither is precious; `di_phase1_full` still must never be saved over.
- **UI test levers:** `diplomacy.test_open_kingdom`, `test_open_encyclopedia <kingdom>`,
  `test_court_select <clan>`; mouse-wheel scrolling via Win32 input (UI-INTEGRATION.md §0b.6).

Branch `development`, pushed. `main` sits **40-odd commits behind** `development` and has
deliberately not been moved — cutting a release is Phase 4's job, not a side effect of
closing a pillar.

### What acceptance did and did not mean

The lead accepted Phase 1 on 2026-09-23, on priority grounds. The measured part is real:
run 01 cleared the acceptance bar over 28 in-game years, and **run 07** — 20.8 in-game
years, 100 wars, 0 errors, and the first vassal links this project ever produced — is the
reference measurement.

**The §13 vassalage rework sitting under that acceptance is smoke-tested only**: it builds
clean, passes LoadProbe, and ran 2 in-game years with 0 errors. None of the five questions
in [design/04 §13.7](design/04-hegemony.md#137-what-the-next-run-must-answer) is answered.
Anyone reading "Phase 1 ✅" should read that sentence with it.

The full carried-debt table is in
[ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23). The short version:

- **Balance run 08 is deferred, not cancelled.** It is what closes §13, and it unblocks two
  undecided constants: the strength margin on `IsStrongEnoughToHold` (§13.6) and whether the
  indemnity price should bite (§13.4).
- **Two peace-table surfaces have never been seen working**: the multi-selection checklist
  against a real budget, and the AI→player incoming offer. `save007`'s wars are all war
  score ~0, so only the white-peace short path has rendered on screen.
- The risk run 07 flagged is still unmeasured: **13 of its 18 tributary pacts settled at war
  score ≥ 75**, and the §13 cliff is expected to convert most of those into subjugations.
  That multiplies the imposed-vassalage rate by an unknown factor.

### Phase 2 — where it starts

Spec: [design/02-intrigue.md](design/02-intrigue.md). The order is fixed by that spec's §8
and is a real dependency chain, not a preference: **2.1 grievances → 2.2 loyalty →
2.3 blocs → 2.4 legitimacy → 2.5 succession → 2.6 civil war → 2.7 court UI**.

The shape of the pillar, restated so the first commit does not have to re-derive it:

- A **grievance is event-sourced** — a thing that happened, attached to a (clan → liege)
  pair, with a type, a weight and a date. It is saved. Nothing about it is recomputed from
  world state.
- **Loyalty is derived and not saved** — a function of saved grievances plus live world
  state. That keeps the save small and makes a balance change take effect on existing
  campaigns rather than only on new ones. Same discipline as `Hegemony.IsHegemon`: one
  source of truth, derived where it is read.
- **Crown legitimacy is a saved pool**, 0–100, starting at 60. It is the one genuinely new
  per-kingdom number, and the fabrication hook in `ClaimRegistry` is already computing a
  penalty for it and only logging it.
- **Civil war and succession are outcomes, not systems.** They route through Phase 1
  machinery — a civil war is a war with a war record, exhaustion and a peace table.

What Phase 1 already left waiting, verified present in the code on 2026-09-23:

| Hook | Where |
|---|---|
| `ExhaustionCourtPressure = 40`, the doves threshold | `Diplomacy/DiplomacyConstants.cs:83` |
| A caught fabrication computes its legitimacy penalty and only logs it, "pending the Phase 2 legitimacy pool" | `Diplomacy/ClaimRegistry.cs:283` |
| Policy votes, annexation, clan expulsion and king selection **left alone on purpose** for Phase 2 to extend | `GameModels/ModKingdomDecisionPermissionModel.cs:29` |
| `CourtAgenda`, `SpyMissionType`, `MissionOutcome` enums, registered at definer ids 23–25 since Phase 0 | `Models/Enums.cs`, `Core/ModSaveDefiner.cs` |
| `EnableIntrigue` settings toggle, shipped and defaulting on | `Core/ModSettings.cs:31` |

A claim this handoff removed rather than repeated: the previous version said
`CallToArms.WouldAnswer` carried a note about vassal defiance reading grievances. **It does
not** — there is no such note in that file. The idea is still right and belongs at 2.2; it
was simply never written into the code.

Parked until Phase 2 gives them weight: **vassal-party summons**
([design/04 §8](design/04-hegemony.md)) and **titles** (Emperor, Khagan), which sit on top of
legitimacy at 2.4.

### 2.1 is built and verified live, 2026-09-23

Verified on `save007` (Khuzait, player-led, Okhon of clan Airit is the ruler), through the
GABS bridge, **0 errors and 0 warnings** in the mod log throughout:

| Check | Result |
|---|---|
| New definer entry loads on an existing save | `Loaded: ... 0 grievances, schema v4` on `save007` - no schema bump needed, as designed |
| `UnjustWar` source fires | Khuzait declared war on Aserai at legitimacy 0.00: **all 9 non-ruling Khuzait clans** recorded weight **8.0**, the full ceiling |
| The weight is `(1 - legitimacy) x ceiling` | 8.0 at legitimacy 0.00 matches exactly |
| One resolver, two readers agree | the grievance handler and `CoreBehavior` both logged legitimacy 0.00 for the same war |
| Decay arithmetic | `tick_days 10` took every record 8.0 -> **7.8**, exactly 10 x 0.02 |
| **Save does not crash** | saved as `di_grievance_test` - the test that catches a missing container definition |
| **Round trip across a process restart** | game stopped, restarted, save reloaded: **9 grievances** back with weight **7.8**, type, holder and target all intact |
| Renew, not stack | a second unjust war (on Vlandia) pushed 7.8 back to **8.0** and created **no tenth record** |
| The player's clan is under the same rules | every grievance names **Airit**, the player's own clan, as the target. Design 02 §9.2 holds in practice, not just in the spec |

Not covered by this session: `FiefToRival`, `FiefLostToEnemy`, `HumiliatingTribute` and
`RelativeInCaptivity` are wired but **were not triggered** - they need a fief grant, a siege,
an active tribute and a year-long captivity respectively. `PolicyAgainstAgenda`,
`PeaceWhileWinning` and `RequestRefused` are not wired at all and wait on 2.3.

A correction to CLAUDE.md §2 while testing: the bridge **does** have `core/skip_video`, so the
note that the intro video needs a key sent from outside is out of date.

### 2.2 is built and verified live, 2026-09-23

Verified on `di_grievance_test` (the `save007` world carried forward). **0 errors, 0
warnings.** Every figure below was predicted by hand first and then read off the game:

| Check | Result |
|---|---|
| Grievance term | grievance weight 7.8 x 1.5 = **-11.7**, exact |
| War term | worst exhaustion 3.0 x 0.2 = **-0.6**, exact |
| Whole sum | Harfit: 50 - 0.5 - 11.7 - 6.7 - 0.6 = **30.5**, exact |
| The chain moves together | `tick_days 100`: grievance decayed to 5.8 (**-8.7**), exhaustion rose to 33.0 (**-6.6**), predicted total **27.5** and the game printed 27.5 |
| All four bands reachable | Sturgia, which holds no grievances, spans **16.4 (defection risk) to 75.4 (reliable)** |
| The ruling clan is excluded | Airit, the player's clan, does not appear in its own court's list |

**Two balance signals, recorded not acted on** - every constant is still marked UN-TUNED and
tuning needs a real run:

- **One maximum-weight unjust war moves an entire court a band.** Khuzait's nine clans sat
  around 44 (transactional) before; one war at legitimacy 0.00 put **all nine** at 30-38,
  disaffected. `LoyaltyGrievanceFactor` 1.5 is the most load-bearing number in the pillar.
- **War exhaustion outruns grievance decay.** Over 100 days the grievance term recovered 3.0
  points while the war term lost 6.0, so loyalty fell *despite* the court forgetting. At
  exhaustion 100 the war term alone is -20 across every clan in the realm. Whether that is
  right is a design question for the lead; it is certainly potent.
- `Kuloving` of Sturgia is a **defection risk at campaign start** on vanilla relation alone
  (-50 relation, short of land). Not caused by this mod, but design 07 should know that a
  day-one defection candidate already exists.

A correction made while testing: mid-session I predicted a loyalty figure using
`ExhaustionPerDayAtWar = 0.08`. The real rate is **0.3/day**; 0.08 is a stale Phase 1 figure
that survives in ROADMAP's early narrative. The prediction was re-derived and then matched.

### 2.3 is built and verified live, 2026-09-23

Verified on `di_grievance_test`. **0 errors, 0 warnings.**

| Check | Result |
|---|---|
| Blocs form and are led correctly | Khuzait: **Hawks 7 clans / 78%** led by Arkit, **Autonomists 2 / 22%** led by Koltit |
| Strongest pressure wins, including at the margin | Koltit went Autonomist on **13.4 against Hawks 13.3** |
| Dove threshold is real | `tick_days 300` took exhaustion to 93; dove pressure **(93-40) x 1 = 53.0**, exact, and the whole court flipped to a single Doves bloc at 100% |
| Loyalty beats agenda | Southern Empire: power 1528, **effective 1177** with 2 members at loyalty 79.4 and 77.2 excluded. 1528 - (156+196) = 1176, matches. Aserai 1704 - (217+190) = **1297**, exact |
| Crown authority varies on real data | **+1.00** Khuzait and Southern Empire, **0.00** Western Empire and Sturgia, **-1.00** Northern Empire, Battania, Aserai, Vlandia |
| Pretenders stays empty | zero everywhere, as hard-wired until 2.4/2.5 |

**An emergent result worth keeping:** at exhaustion 93 the Khuzait court was simultaneously
**entirely dovish and entirely below loyalty 29** - most of it under the defection threshold
of 25. A long bloody war unites a court behind peace *and* makes it disloyal, which is
exactly the precondition design 07's armed contest needs. Nothing was written to make that
happen; it falls out of the two formulas.

**Two gaps found by running it, reported rather than silently patched:**

- **Centralists can essentially never form.** The spec's rule is "the clan is the ruling clan,
  or benefits from crown patronage", and the ruling clan is deliberately excluded from its own
  court, so only patronage remains - which on a fresh map is nobody. The bloc was zero in all
  eight kingdoms.
- **Four of eight kingdoms have no bloc at all** at campaign start. Autonomists only pull when
  crown authority is *positive*, so a kingdom whose lords already hold everything (-1.00) has
  nothing pulling on anyone. Arguably a court where the crown is weak should have centralists
  pushing back; the spec's agenda table does not say so, so it was left alone.

Both are design questions for the lead rather than bugs: the code does what design 02 §3 says.

### 2.4 is built and verified live, 2026-09-23

Verified on `di_grievance_test`. **0 errors, 0 warnings.**

| Check | Result |
|---|---|
| New definer entry (class 11) loads on an existing save | `0 legitimacy pools` - and every kingdom still read **60.0**, the starting value, not zero |
| War with no casus belli | Khuzait **60.0 -> 52.0**, exactly -8, reason recorded |
| Treaty broken | Sturgia **60.0 -> 40.0**, exactly -20, the heaviest entry in the table |
| **Legitimacy feeds loyalty** | every Sturgian clan picked up **legitimacy -2.0** = (40-50) x 0.2, and every loyalty fell by exactly 2.0 from the earlier reading - Kuloving 16.4 -> 14.4, Togaroving 33.6 -> 31.6, and so on down the list |
| White peace moves nothing | a settlement at war score 0.00 left Khuzait at 52 and Aserai with no record at all. A stalemate has no victor and no verdict |
| Boundary is right | Sturgia at exactly 40.0 is **not** flagged weak; the threshold is *below* 40 |

One breach of one pact cost the entire Sturgian court two loyalty points each. That is the
whole argument for the pool being worth defending rather than a number on a screen.

**Still unverified in 2.4**, and not verifiable from a debug command:

- **A decisive win or loss.** Both need a real war score, which comes from battles and sieges;
  no console lever fabricates one. The white-peace branch is the only one exercised.
- **The peace dividend.** Measured in dates, and `diplomacy.tick_days` cannot move
  `CampaignTime.Now`. `diplomacy.legitimacy` says so in its own output rather than leaving it
  to be discovered.
- **Fief lost** (needs a siege) and **caught fabricating** (needs a 30-day timer the frozen
  clock cannot advance, behind a 20% roll).

A bug caught by reading the output: `diplomacy.loyalty` still printed "the crown-legitimacy
term is inert until 2.4" while showing a live -2.0 in the same block. Fixed, along with the
same staleness in `diplomacy.blocs`.

### 2.3b bloc voting is built and verified live, 2026-09-23

The third Harmony patch in the project, and the first since Phase 1.
`Patches/KingdomDecision_DetermineSupportOption_Patch.cs`.

**Why the obvious target was the wrong one**, found with `tools/CallSites` before a line was
written: `KingdomDecision.DetermineSupport(Clan, DecisionOutcome)` is overridden by **every**
decision type - DeclareWar, MakePeace, KingdomPolicy, SettlementClaimant and the rest - so a
patch on the base would have missed almost every vote in the game while appearing to work.
`DetermineSupportOption` is declared once, overridden nowhere, and is the funnel all of them
pass through. One patch, whole game.

Verified on `di_grievance_test`. **0 errors, 0 warnings**, Harmony applied cleanly.

| Check | Result |
|---|---|
| A bloc actually overrides a member | Khergit wanted **No** on its own and voted **Yes**, because Arkit - the Hawks leader - wanted Yes |
| **Loyalty beats agenda** | raising Khergit's relation with the ruler took its loyalty **32.7 -> 82.2**, past the reliable band, and the override **stopped**: `alone: No, votes: No`. Same clan, same decision, same bloc, same leader; only loyalty changed |
| The bloc cache does not lie under a frozen clock | `tick_days 300` with `CampaignTime.Now` unmoved took Khuzait from Hawks 7 / Autonomists 2 to a single Doves bloc. A cache keyed on the day alone would have served the stale split - see below |

`diplomacy.test_vote` was written for this: there is no console command in v1.4.8 that opens a
kingdom decision, and the AI raises them on its own schedule, so the vote path could not be
observed at all otherwise. It builds a real `KingdomPolicyDecision`, drives the real
`DetermineSupportOption`, and applies nothing.

**The cache, and the trap it nearly reintroduced.** Bloc membership is read once per clan per
outcome during a vote, and each read walks every clan and every grievance, so it is memoised
per kingdom. The obvious key is the campaign day - and that would have been a **lie under
`diplomacy.tick_days`**, where the clock never moves: grievances decay, legitimacy shifts, and
the cache keeps serving the world as it was before the command ran. Exactly the trap CLAUDE.md
§1 records. The key is the day **and** a generation counter bumped by every write that can move
a loyalty.

**Two behaviours found by running it**, now in the patch header rather than left implicit:

- An **abstaining member is left abstaining**. A null answer from the engine means the clan
  declined to take a side; turning that into a vote would be manufacturing one.
- The bloc follows its leader's **preference**, not its leader's cast vote. Observed live:
  Southern Empire's Autonomist leader abstained while its bloc voted Yes. Following the cast
  vote would let an indifferent leader silence its whole bloc, which makes blocs weaker rather
  than more united. Worth revisiting if a balance run shows blocs carrying votes their leaders
  visibly did not want.

### 2.5 is built and verified live, 2026-09-23 (evening)

The first attempt at verifying this failed for a reason worth keeping: killing a ruler hands the
choice to vanilla's king selection, which needs the **real** campaign clock, and
`diplomacy.tick_days` does not move it. What worked: seed the throne watch with one
`tick_days 1`, kill the ruler, then `diplomacy.test_set_speed 50` plus `set_time_speed 3` and let
the real clock run. The succession resolves within a few seconds of wall time.

Set-up on `di_grievance_test`: Battania, whose fen Eingal (Aradwyr) already qualified as a
claimant on strength (influence x1.70, loyalty 23.4). Two great houses were given +100 relation
with him - "he has been courting them" - then the king, Caladog, was killed. **0 errors, 0
warnings** throughout.

| Check | Result |
|---|---|
| The throne watch catches an heir inheriting *inside* the ruling clan | yes - the case `RulingClanChanged` never fires for |
| A contested succession | `Battania divides: Muinser 49% (4 clans), Aradwyr 51% (4 clans)` |
| It costs the new crown | legitimacy **60.0 -> 45.0**, exactly -15 |
| Backers of the loser are grieved | **4** `SuccessionPassedOver` grievances at 6.0 - exactly the 4 clans the tally put behind Aradwyr |
| A strong loser stays a claimant | "Aradwyr kept 51% of the court and remains a pretender" |
| No bloc on half the condition | at legitimacy 45 with a living claimant: **no bloc** |
| The bloc forms on both halves | the new king broke a treaty, 45 -> **25**, and a **Pretenders bloc** formed: 5 clans, power 1,560, led by fen Eingal |
| Pressures check by hand | Eingal **200.0** (own claim); Caernacht **50.0** = 0.5 x (100 - 0); the two houses that do not prefer Aradwyr: **0** |
| **Save round trip** of `Pretender` (class 12) | saved `di_pretender_test`, new process, reloaded: `1 pretenders, 2 legitimacy pools`, and the bloc came back identical |

Note the division: the court wanted the loser. Aradwyr held **51%** and vanilla crowned the heir
anyway - which is the strongest pretender this system can produce, and a direct consequence of
the decision that vanilla picks the king and this pillar does the politics afterwards.

**Two bugs found by running it, both fixed and re-verified:**

- **Two definitions of "who backed whom".** The tally weighed loyalty and self-backing; the
  grievances compared bare relations. On the very first contested succession they already
  disagreed - the tally put 4 clans behind the loser and **5** were grieved. The fifth, fen
  Penraic, sat at loyalty 66, which counted it for the new king in the vote; it was then punished
  for backing his rival. It was first written up as the two "happening to agree"; they did not,
  it had simply not been checked. Now one backing map feeds both, and a `divides:` log line makes
  the invariant checkable: re-run, **4 backers and 4 grievances**.
- **Mercenary companies were treated as courtiers.** The Legion of the Betrayed and Skolderbroda
  were being given loyalty scores, sorted into blocs and counted at successions in the Northern
  Empire. A clan under mercenary service holds no fief from the crown and cannot vote in vanilla.
  `Intrigue/Court.IsMember` is now the single definition of court membership, used by all nine
  loops that had been deciding it separately. Verified: the Northern Empire court now lists 8
  clans.

**An emergent balance signal, recorded not acted on:** after the contested succession and one
broken treaty, most of Battania's court sat at loyalty **0-7.5**. A new king starts with near-zero
relation with everyone, and relation is measured against the *current* ruler, so every
succession resets the largest positive term in the loyalty sum. Realistic, and very strong.

### 2.7 Court tab is built and verified live, 2026-09-23 (night)

The lead approved the mockup (artifact "Court Intrigue Screen") and chose where it lives: a
**seventh Kingdom-screen tab, "Court"**, after Realm. A rival court goes on that kingdom's
**Encyclopedia page** as bands only (design 02 §9.1) - built the same night, next section.

Built the Realm tab's way: one prefab patch inserts both our tab buttons, one inserts the panel,
the management mixin owns `DiCourtVM` beside `DiRealmVM`, and each of our tabs hides every other
panel when it opens. All seven tabs narrowed from 0.90 to 0.70 of their art so they fit.

Every number on the panel comes from the resolver the AI uses (`LoyaltyModel.Explain`,
`BlocModel`, `LegitimacyRegistry`, `GrievanceRegistry`, `SuccessionModel`) - never recomputed
for display.

Verified by screenshot on two different worlds. **0 errors, 0 warnings.**

| Check | Result |
|---|---|
| Seven tabs fit | Clans ... Diplomacy, Realm, **Court**; clear of the leader portrait's caption |
| Ruler's view (`di_grievance_test`, Khuzait) | Hawks 78% / Autonomists 22%, 9 clans, three marked `claimant`, footer "3 houses would press a claim" |
| Vassal's view (`di_phase1_full` + `campaign.join_kingdom battania`) | "you serve Rath", **"fen Calrain (you)"** listed with its own loyalty 49.0 - design 02 §9.2 on screen |
| Mercenaries kept out | vanilla's Clans tab lists 16 Battanian clans; Court lists the **8** sworn ones |
| Numbers match the AI's | Arkit 50 - 0.5 - 11.7 - 6.0 - 0.6 + 2.0 = **33.2**; fen Uvain **53.5**; fen Giall **84.5** - each shown and each summed by hand |
| Bloc card agrees with rows | "2 of them will vote with the crown regardless" - exactly two RELIABLE rows |
| Cross-check against vanilla | vanilla shows Harfit tier 3 with 1 fief: (1-3)/3 x 10 = **-6.7**, the fief term on our panel |
| Selecting a clan redraws the right column | row highlight moves, WHY and grievances rebuild |
| Tab switching | Court <-> vanilla Clans, Court <-> Realm: never two panels at once |
| Close and reopen | the rebuilt panel is pixel-identical to the first |
| No kingdom | the screen is refused, as vanilla does |

**Not verified:**
- **A physical click on a clan row.** The GABS bridge clicks the first widget whose text matches,
  and every clan name also sits, earlier in the tree, in vanilla's hidden Clans list - so a row
  here cannot be clicked from a tool call. Selection was driven through `DiCourtVM.Select`, the
  method the click calls, via the new `diplomacy.test_court_select`. The binding itself is
  vanilla's own `ClanTuple` pattern (`Command.Click="OnSelect" IsSelected="@IsSelected"`).
- **No scrolling.** The largest court seen had 9 sworn clans and fits. A court past roughly 13
  would run into the footer.
- Only 1280x720 was captured.
- The legitimacy note showed "none yet" in both worlds; a real reason on screen was not seen.

Five Gauntlet lessons from building it went into UI-INTEGRATION.md §0b.

### 2.7 rival court on the Encyclopedia is built and verified live, 2026-09-23 (night)

The rival half of the approved design (board "A rival court"), where the lead put it: a
**"Court" section on each kingdom's Encyclopedia page**, after the ruler and before the clans.
Your own kingdom's page shows one line pointing to the Court tab instead.

**Bands, never figures.** `Intrigue/CourtBands.cs` is the one place the bands are drawn, in
the same style as Phase 1's `ExhaustionBands`: every edge is a threshold the AI acts on, read
from the constant it uses.

| Band | Edges | What acts on the edge |
|---|---|---|
| Crown: Failing / Questioned / Secure | 40, 50 | below 40 a pretender's party may gather (`LegitimacyRegistry.IsWeak`); below 50 the crown costs every clan loyalty (`LoyaltyModel`) |
| House mood: Ready to break / Sullen / Self-interested / Steadfast | 25, 40, 70 | the loyalty bands themselves (`LoyaltyModel.Band`) |
| House weight: no weight / middling / great house | 0, x1.30 the court's average | at or below 0 influence adds nothing to bloc power or succession support; x1.30 is the magnate half of a power claim (`SuccessionModel.IsMagnate`) |

The view model exposes only text and colours - there is no number property on it to bind.
Standing pretenders are named (a claim nobody hears of is not a claim); a house that *would*
press a claim is not, but "great house" + "Ready to break" says it to a player who reads both.

Two changes outside the new files, both "one resolver per concept":
- `SuccessionModel.HasPowerClaim` computed the court's average influence itself, a second copy
  of `InfluenceRatio`. It now reads `IsMagnate`, which reads `InfluenceRatio`. Verified the
  same result live: the four Khuzait clans flagged `claimant` on the Court tab are exactly the
  four `diplomacy.pretenders` marks WOULD STAND, including Khergit sitting on x1.30.
- The Court tab coloured legitimacy on **60** (`LegitimacyStart`), which is where a crown
  starts and changes no behaviour. It now reads `CourtBands.CrownOf`, so both views share the
  40/50 edges. **Visible change:** a crown at 50-59 was orange on the Court tab and is green now.

Verified on `di_pretender_test`. **0 errors, 0 warnings** across two sessions.

| Check | Result |
|---|---|
| Section renders in the page | after Leader, vanilla divider style; screenshot |
| Battania, the crisis court | Failing (legitimacy 25), "The Pretenders are the strongest party at court, and speak through Clan fen Eingal", four houses ready to break, "Aradwyr presses a claim to the throne of Battania" |
| Bands against the figures | fen Morcar 39.4 Sullen, fen Giall 43.4 Self-interested, fen Penraic 66.0 Self-interested, fen Eingal x1.91 great house, Dolentos x1.34 great house, dey Cortain x-0.08 no weight, Aserai 57 Secure - every one matched against `diplomacy.loyalty` / `pretenders` / `legitimacy` |
| All eight kingdoms composed | `diplomacy.court_bands`, which prints the very VM the page binds |
| Your own kingdom (Khuzait) | the pointer line only, Clans directly below |
| The divider collapses the section | a **real click** through the bridge: arrow turns, body hides |
| Court tab regression | unchanged apart from the colour edge above |

**Not verified:**
- A female ruler's page on screen. `court_bands` composed Southern Empire with "her"
  throughout, but only Battania's and Khuzait's pages were looked at.
- A kingdom whose court has no sworn clan besides the ruler's ("none but his own house").
- The page reached by clicking through the Encyclopedia's own lists: `diplomacy.test_open_encyclopedia`
  opens it through `EncyclopediaManager.GoToLink`, the same call a link in a message makes.
- Only 1280x720, again.

One testing trick worth keeping: **the bridge has no scroll tool**, and the section sits below
the fold. Real mouse-wheel input to the game window works (Win32 `SetCursorPos` +
`mouse_event(MOUSEEVENTF_WHEEL)` after `SetForegroundWindow`); it is recorded in
UI-INTEGRATION.md §0b.

### What to do next

1. **Phase 3, espionage** - 3.1, 3.2 and 3.4 done; 3.5 next (design/03 §8 has the order).
2. **A long AI-only run with internal wars in it**, the balance question 2.6 leaves open. How
   often do internal wars start, how often do houses divide, and does a realm recover from a
   lost civil war followed by foreign wars? Both of the first run's neighbours declared war on
   Battania the day its war ended. Fold it into run 08.
3. **Court tab gaps**: no scrolling past ~13 sworn clans; the physical row click is unverified.
4. ~~The `AiDiplomacy.TryDemandTribute` revisit~~ - **done 2026-09-25** (design/02 §7.1).
5. **Fold run 08 in** once Phase 2 work produces a campaign long enough to carry it. Same
   deployment, same telemetry; what it needs is in-game years, which Phase 2 testing
   generates anyway.

### Design decisions taken before any Phase 2 code, [design/02 §9](design/02-intrigue.md)

| # | Question | Decision, 2026-09-23 |
|---|---|---|
| 1 | Does the player see *rival* kingdoms' internal politics without espionage? | **A band for rivals, the full ledger for your own court.** Exact rival figures are what Phase 3 `ReadCourt` sells. Same fork the lead already took for enemy war exhaustion |
| 2 | Is the player's own clan subject to this when serving another king? | **Yes, on the same terms as any AI clan.** No "is this the player" argument in the ledger or the loyalty calculation; where the experience must differ, that lives in the UI layer. Costs extra work at 2.2 and 2.7, taken deliberately |
| 3 | Kingdom decisions: extend `KingdomDecision` or replace it? | **Extend.** The spec recommends it and `ModKingdomDecisionPermissionModel` was already written on that assumption |
| 4 | Civil-war trigger thresholds ([design/02 §6](design/02-intrigue.md)) | **Still open** — guesses by admission, deferred to a long AI-only run. Nothing in 2.1–2.5 is blocked by it |

### Saves

| Save | State |
|---|---|
| `di_pretender_test` | **Phase 2's richest court:** Battania at legitimacy 25 with a standing pretender (Aradwyr) and a Pretenders bloc; Khuzait player-ruled with four would-be claimants |
| `di_grievance_test` | Khuzait, player-ruled, with a grievance ledger - the Court tab's ruler view was verified here |
| `di_fresh_1084` | **Summer 1, 1084, pristine start, hero parked in Myzea.** The run-08 baseline |
| `save007` | Khuzait, player-led — the save the Kingdom screen UI was verified on |
| `di_run07_1104` | Winter 1104, end of run 07: 7 kingdoms, 2 hegemons |
| `di_hegemony_1166` | Vlandia with 2 vassals. The only state holding a sphere built at the peace table |
| `di_review_0919_b` | Winter 15, 1162 — the old evolved world, pre-§12 |
| `di_run06_resume`, `di_review_0919` | run 06 checkpoints |

Never save over `di_phase1_full`.

### Traps from the 2026-09-20 session that are not in CLAUDE.md §1

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
---

---

## Intermittent: the game sometimes dies on startup from the official launcher

Reported by the lead as "game không thể chạy được crash ngay khi mở", with the engine's
"we need to collect necessary files" dialog. **Not root-caused yet.** What is established:

| Launch path | Result on 2026-09-15 |
|---|---|
| Official launcher → Play | **5 died** before the main menu, 14-21s in — **and 2 later launches worked**, one of them balance run 03: 25 minutes, 6.2 in-game years, zero errors |
| `Bannerlord.exe` directly, launcher's own mod list | reached the main menu |
| `Bannerlord.BLSE.Standalone.exe` (GABS, `scripts/play.ps1`) | reached the menu, loaded `di_phase1_full`, 0 errors |

So it is **intermittent on that path, not deterministic**, which also means an earlier note
here claiming the launcher path was simply broken was too strong.

Evidence for the fault itself, from the Windows `CLR20r3` record (P4/P7/P8 resolved with
Cecil): an unhandled managed exception in `TaleWorlds.MountAndBlade`, method
`ActionIndexCache..ctor` → `MBAnimation.GetActionCodeWithName`, IL offset 8 — the instruction
that reads the static `MBAPI.IMBAnimation`. The only caller of that method in the whole game
is `ActionIndexCache..ctor`, so something constructed an action-index cache while the native
animation API was still null. Nothing in this mod touches animations, and it ships no XML and
no assets.

Every failing run's mod log stops at `OnSubModuleLoad complete` with no `Notify` line, so the
module loaded and the game died before the main-menu screen.

**Not yet answered: is this mod implicated at all?** The decisive test is one launcher run
with the mod unticked, which needs a human to press Play. Since the same path has now carried
a 25-minute session with zero errors, the honest statement is that it fails intermittently and
nothing yet points at the module.

Two things were added because of this, independent of the cause:

- `SubModule.InstallCrashLogging` — an `AppDomain.UnhandledException` handler that writes the
  exception and its stack into the mod log. This crash produced an 86 MB minidump and a
  method token and nothing else; the next one will produce a stack trace.
- `OnSubModuleLoad` logs `host=<process> modules=[…]`, because the official launcher runs the
  game inside its own process and the logs could not tell the two paths apart.

## Where the work stands

| Phase | State |
|---|---|
| **0 — Foundation** | ✅ done, verified in a live campaign |
| **1 — Diplomacy core (1.1–1.12)** | ✅ **accepted by the lead, 2026-09-23**. Code complete including submission and hegemony (1.9/1.10), the vanilla takeover (1.11) and power (1.12). Measured over runs 01–07; the §13 rework under it is smoke-tested only, and the carried debt is listed in [ROADMAP.md](ROADMAP.md#phase-1--accepted-by-the-project-lead-2026-09-23) |
| **2 — Court intrigue** | 🔄 2.1-2.7 built and verified live on their main paths, all on `development` (2.6/2.6b and 2.6c merged 2026-09-24). What is still unverified is listed in the checkpoint at the top |
| **3 — Espionage** | ⬜ spec written and reviewed, no code |
| **4 — Integration, balance, release** | 🔄 runs 01-07 archived. **Run 07** (2026-09-20) is the current reference — [balance/run-07.md](balance/run-07.md). **Run 08 is owed** and closes the §13 questions |

### Kingdom screen UI — built and verified live, 2026-09-21

The mockup pass over the native Kingdom screen shipped and was verified on `save007`
(Khuzait, player-led) — screenshots, not just build success:

- **Realm tab** (6th header tab): standing strip, wars with exhaustion/score and a
  per-war *Peace table* button, vassals, spheres, claims, agreements, *Write a report*.
  Switching to and from vanilla tabs verified — the panel coordinates visibility with
  the five `Show` flags instead of the tab control, which only knows five pairs.
- **Diplomacy rows**: war rows carry exhaustion band + score (`Fresh +0`); truce rows
  carry the relationship summary (`independent`, `our vassal`, `answers to X`, tribute).
- **Headline** under the banners: casus belli + legitimacy + enemy band meaning for
  wars, standing agreements for peace. Wars recorded without a claim show
  *"no claim on record"*, not `over None`.
- **"What their court would sign"** chooser on truce items: the court's real
  `PactValue` against all three rungs with verdicts, influence costs, and per-rung
  Propose buttons — the same numbers and path the AI uses.
- **Bottom action strip**: vanilla's proposal row no longer overlaps ours. The
  `{Actions}` ListPanel could not be hidden by an `IsVisible` binding — bindings on it
  resolve against the Actions list, not the panel VM — so its `DataSource` is repointed
  at `DiVanillaActions`, which serves the real list when the mod is off and an empty
  list when it runs. Pact buttons were removed from the strip; the chooser owns them.
- **Peace table** opens a native inquiry from both surfaces. `save007`'s wars are all
  score ~0, so only the white-peace short path is verified on screen; the
  multi-selection checklist with a real budget and the AI→player incoming-offer
  inquiry remain **unverified in a live game** — they need a war that earned terms.

Still loose: the Realm tab widens the centered tab strip enough to touch the leader
portrait's caption — cosmetic only. Unverified above.

### Phase 1, feature by feature

All of this exists, builds, and was exercised in a live campaign. `docs/ROADMAP.md` carries
the per-feature evidence tables.

| | Feature | Code |
|---|---|---|
| 1.1 | War exhaustion, war score, weariness | `Diplomacy/WarExhaustion.cs` |
| 1.2 | Casus belli, fief ledger, claims, fabrication | `Diplomacy/ClaimRegistry.cs`, `FiefHistory.cs`, `CasusBelli.cs` |
| 1.3 | Treaty engine, six types, tribute, enforcement | `Diplomacy/TreatyRegistry.cs`, `TreatyEnforcement.cs`, `Patches/` |
| 1.4 | Diplomatic trust, directional and non-decaying | `Diplomacy/TrustRegistry.cs` |
| 1.5 | Peace table on a war-score budget | `Diplomacy/PeaceTable.cs` |
| 1.6 | Call to arms, refusal, follower release | `Diplomacy/CallToArms.cs` |
| 1.7 | Weekly AI evaluation, one action per kingdom | `Diplomacy/AiDiplomacy.cs` |
| 1.8 | Ctrl+D diplomacy menu, exhaustion bands | `UI/DiplomacyMenu.cs`, `Diplomacy/ExhaustionBands.cs` |
| 1.9 | Submission, Hold, defiance, revolt, collapse | `Diplomacy/Hegemony.cs` |
| 1.10 | Rival poaching, cascade cap, hegemony UI | `Diplomacy/Hegemony.cs`, `Diplomacy/CallToArms.cs`, `UI/DiplomacyMenu.cs` |
| 1.11 | Inter-kingdom diplomacy taken from vanilla | `GameModels/` (four models), `Diplomacy/VanillaDiplomacy.cs` |
| 1.12 | Power: ambition, coalitions, greed, annexation, elimination | `Diplomacy/Power.cs`, `CallToArms.ExpectedSupport`, `Hegemony` — [design/06](design/06-power.md) |

---

## Phase 1 acceptance: met, measured over 28 in-game years

Balance run 01 is in: **340 weekly snapshots, 247 wars, 0 errors**, Summer 1084 to Autumn
1112. Full analysis in [docs/balance/run-01.md](balance/run-01.md).

| Criterion | Result |
|---|---|
| Wars average under ~3 years | 18.4 days mean — passes hugely |
| An alliance forms and holds | up to 8 at once, present 85% of weeks |
| No permanent total war | every kingdom at war in 0.9% of weeks |
| Stability | 28 years, zero errors, zero tribute defaults |

**The criteria are met and the run still found a real problem: wars are ~10× shorter than
the design intends.** A chosen war reaches exhaustion 51.6 in 23.6 days, i.e. 2.19 per day
of which elapsed time is 0.08 — casualties were doing ~96% of the work, about 30× what the
design assumed.

Two constants were tuned from that data and **both need a second run to confirm**:

- `ExhaustionCasualtyStrengthDivisor` 100 → **20** (casualty exhaustion 5× weaker)
- `AncestralClaimMemoryYears` 20 → **12** (live claims had settled at 83–93, so everyone
  held a claim on everyone and `Conquest` was never needed)

## War initiation taken over from vanilla

Decided by the lead after run 01 showed our evaluation declaring 25 wars against vanilla's
~220: everything the mod knows about a war — exhaustion, weariness, claims, trust, the
influence a casus belli costs — had no say in whether wars happened. Casus belli was a label
applied afterwards rather than a gate.

**What changed**

- `DeclareWarDecision.IsAllowed` now refuses **AI-proposed** wars between kingdoms outright.
  A decision proposed by the **player's own clan** still goes through, so the Kingdom
  screen's declare-war option is untouched. A treaty forbidding the war still stops
  everyone, player included.
- `DeclareWarAction.ApplyByKingdomDecision` refuses anything unsanctioned as a backstop.
  Our evaluation wraps its own call in `TreatyEnforcement.BeginSanctionedWar()`.
- Engine paths are deliberately untouched: rebellion, claim on throne, player hostility,
  crime rating, kingdom creation, call to arms.

**And a design fault it exposed: weariness saturated.**

Measured in the mature world at the end of run 01, every kingdom sat at **69–96 weariness**
against a cap of 45, which alone inflicted up to −40 on every war valuation. The arithmetic
was never going to work: each war end injected up to +30 while decay removed a flat
0.15/day, so across 247 wars the pool pinned itself near the ceiling. Weariness was meant to
say "you just fought a long war, wait" and instead said "you have ever fought a war".

Three fixes:

| Fault | Fix |
|---|---|
| A flat drain cannot bound an accumulating pool | Decay is now **proportional**, 2% of the remaining pool per day. Self-limiting: 1.6/day at 80, 0.4/day at 20 |
| Obligation wars injected weariness for wars nobody chose | They carry none at all |
| Weariness both gated **and** penalised at −0.5/point | Gate kept; the value penalty drops to 0.15 |

Verified immediately: weariness drained **91 → 8.1 over 120 days**, and the next four weeks
of evaluation produced **3 war declarations** across eight kingdoms with values of 34–48
against a threshold of 18, at 55–75 influence each. Under the old numbers the same world
produced 64 consecutive "do nothing" decisions.

**Aggression retuned to carry the whole load** — our evaluation is now the only source of
wars between kingdoms, so it has to do what vanilla was doing:

| Constant | Was | Now | Why |
|---|---|---|---|
| `WarDeclarationBaseInfluence` | 100 | **40** | A war cost 180–240 while ruling clans held 170–230 — about one war ever, against vanilla's free ones |
| `AiWarThreshold` | 25 | **18** | Carrying 8× the load needs a far lower bar |
| `AiWarStrengthRatio` | 1.2 | **1.0** | Eight kingdoms sit within 6,000–7,100 strength; the best ratio anyone could find was 1.08. Attacking an equal is now allowed, and the valuation still punishes attacking upward |
| `AiMaxWearinessToExpand` | 30 | **45** | A war ending at exhaustion 60 carries 30, so the old cap blocked a kingdom after *every* war |
| `AiNonAggressionThreshold` | 20 | **35** | At 20 a pact was worth signing with anyone not actively disliked, and eight kingdoms pacted themselves into a locked map |
| `AiDefensivePactThreshold` | 45 | **55** | Same reason; alliance stays at 70 |

Priority order also changed: **peace → war → tribute → pact**, where it used to put pacts
before war. With vanilla no longer starting wars, an evaluation that prefers a cheap pact
whenever one is available signs the map into permanent peace — which is exactly what run 01
produced.

## What balance run 02 has to answer

1. **Did the takeover take effect?** `[SNAPSHOT]` now carries `vanillaWarsRefused=`,
   cumulative for the session. It should climb steadily; if it stays at zero, the
   decision-level patch is not being reached and the whole change is inert.
2. **Is the war rate sane?** Four simulated weeks gave 3 declarations, which extrapolates
   absurdly — the clock was frozen, so treaties never expired and the burst followed
   weariness clearing all at once. The real rate is unknown. Run 01's ~8.8 wars/year is the
   reference point.
3. **Do chosen wars now last 100–200 days?** `ExhaustionCasualtyStrengthDivisor` 100 → 20.
4. **Does the concession ladder ever fire?** `terms=` on `[WAR-ENDED]` answers it directly
   now. It fired zero times in 28 years.
5. **Do live claims settle nearer 30–40?** `AncestralClaimMemoryYears` 20 → 12.
6. **Does anyone get eliminated?** Run 01 kept all 8 kingdoms. With more war and land
   actually changing hands, that may no longer hold — and a kingdom being destroyed is fine,
   the map collapsing to two is not.

## History — how each run changed the design

Kept because the reasoning is load-bearing: several constants only make sense next to the run
that produced them. **For what to do now, read the handoff at the top of this file.**

### Before run 04: the unbounded term in the war valuation, capped

Run 03 recorded one war declaration at **value 199** against a threshold of 18 and put it down
to `LandHunger` blowing up. That was wrong, and the arithmetic says so: `LandHunger` is clamped
to 1 and so contributes at most 35, and with `Conquest` legitimacy 0.20 every bounded term
together reaches at most 61. At least 138 of the 199 came from `(ratio − 1) × 40` — the one
term in the valuation with no ceiling — which puts Vlandia at 4.45× Northern Empire.

What that costs is not war frequency, since the threshold is only a floor. It is **which target
gets picked**: an unbounded term makes "whoever is weakest" outrank claims, borders and land
hunger together, which is the shape run 03 saw — four of Vlandia's five declarations were
`Conquest` at legitimacy 0.20, wars it then did not fight.

| Change | Where |
|---|---|
| `WarValueMaxStrengthAdvantage = 1` — the term caps at twice our own strength, 40 points | `DiplomacyConstants.cs` |
| One resolver: `AiDiplomacy.EvaluateWar` returns every term, and `TryDeclareWar` and `ExplainWarValue` both read it. They were two hand-written copies of the same formula | `AiDiplomacy.cs` |
| A comment claiming the valuation "punishes attacking upward, the strength term goes negative below parity" — it cannot, the `ratio >= 1.0` gate skips those targets before the valuation runs | `DiplomacyConstants.cs` |

**Verified live** on `di_phase1_full` (Winter 3, 1131), zero errors in the log:

```
Vlandia considering war on Battania
  strength ratio: 2.44 (must be >= 1.00)
  value from strength advantage: 40.0   (capped at ratio 2.00)
  ...
  total: 62.9 x aggressiveness 1.00 = 62.9 (needs 18)
```

Uncapped that term would have been 57.6 and the total 80.5. A pair below the cap
(Vlandia → Aserai, ratio 1.69) prints 27.4 with no cap note, and a pair below parity
(Vlandia → Khuzait, 0.98) is still stopped by the gate rather than by its value.

**The cap itself is un-tuned** — chosen so a decisive advantage weighs about as much as a good
claim across a shared border, not measured. Run 04 is the first data on it.

### 1. What run 04 found, in the order it matters

Full report: [docs/balance/run-04.md](balance/run-04.md). Everything below is measured, not
inferred; nothing below has been decided or changed yet.

| # | Finding | Evidence |
|---|---|---|
| 1 | **Submission is automatic, not political.** 9 submissions, values **55.5–166.9 against a threshold of 55**, `threat` at its +140 ceiling in three of them. Four kingdoms knelt on the first weekly tick after load | `submitted to` lines in `run-04.log` |
| 2 | **The map collapsed to one sphere.** Northern Empire ends holding **all seven** other kingdoms. All 8 alive, none eliminated — but the run's peace is subjugation, not diplomacy | `hegemons=1 vassalLinks=7` |
| 3 | **Poaching is a metronome.** Battania changed patron 4× in 2.5 years at values 151/151/159/158. Below Hold 40 a vassal is always on the market, and average Hold was 30 | 5 poaches |
| 4 | **A revolt can be silently refused.** `Hegemony.Revolt` breaks only the vassalage, so a `DefensivePact` with the same patron vetoed the war of independence — and `Revolt` logged the war anyway. 1 of this run's 2 revolts is recorded wrongly | 11:12:52 in `run-04.log` |
| 5 | **Nothing ends a hegemony.** Both revolts were back under the same patron inside the run, one after 20 seconds. Hold settles at 15–36 and drifts *up* | `avgHold=` series |
| 6 | **Tribute barely arrives** — 193 withheld payments, and a successful payment is not logged at all, so the ratio is unknown | add the missing log line |
| 7 | **Wars are shorter than intended.** Wars begun and ended inside the run: median **63 days** against the design target of 150–200. The acceptance bar passes either way | 47–202 day spread |

The two fixes carried into this run both did their job, verified against the mechanism:
`ExhaustionPerDayAtWar` 0.30 closed all five of run 03's 400-day wars at exhaustion 58–62, and
the war-value cap pulled declarations into 18–81 from run 03's 18–199, with the three highest
now carrying a claim at legitimacy 0.70 rather than naked opportunism.

Two constants remain deliberately unapplied and both look less urgent after this run:
`ExhaustionSeekPeace` 60 → 70, and `AiAllianceThreshold` 70 → 82 / `AiDefensivePactThreshold`
55 → 65 — the alliance web has behaved for two runs running.

### 2. Applied after run 04, on the lead's decision — all three unverified in a campaign

Committed and deployed the same day the run finished. None of this has been measured yet;
run 05 is the measurement.

**The revolt bug, fixed.** `Hegemony.TryRevolt` now repudiates *every* live war-forbidding
treaty the vassal holds with its patron, not just the vassalage, through a new
`TreatyRegistry.RepudiateAlongside` — which closes them as `Broken` so the save record stays
honest, but charges nothing, because the headline breach has already been paid for in trust
and in the casus belli it hands over. And the revolt now reads the war back off the world
before it claims one: if the declaration is refused anyway, the renunciation still stands and
the log says so instead of asserting a war that does not exist.

**The submission threat term, capped.** `SubmissionThreatWeight` 70 → **25**, so the term tops
out at 50 against a threshold of 55 and being surrounded is never by itself enough — the same
rule the war valuation's strength term was capped to: *no single term may clear the threshold
alone*.

*Verified live* on the run-04 world: Battania's threat ratio of 1.376 now contributes **34.4**
where it would have contributed 96.3, and the verdict flips from a comfortable submission at
~107 to **45.1, would not submit**. That is the same input scored both ways, not a rerun.

**Poaching now means war.** The lead's call: taking a rival patron's vassal is a serious act,
so it costs relation as well as trust and, by default, puts the two hegemons at war — and
their spheres follow them in through the ordinary call to arms. `PoachingRelationLoss` = 15
between the two rulers, on top of the existing −30 trust and the casus belli. The suitor now
has to clear the same restraint any other war does (`AiDiplomacy.CanTakeOnAnotherWar`, one
resolver shared with `TryDeclareWar`), and a treaty forbidding war with the patron forbids
taking its vassal too — otherwise poaching would be the back door around it.

**Not verified in game, and it cannot be from a tool call.** A poach needs two hegemons and a
neglected vassal whose submission value clears the bar; a revolt needs Hold under 15 sustained
for 30 days. Both are states the frozen campaign clock cannot produce, and a fabricated
scenario that fails to fire would say nothing. Run 05 will show them: watch for
`took ... as a vassal from ... and went to war with it` and for the two-branch revolt line.

**The `deploy.ps1` guard, widened.** It matched `Bannerlord*` only, which does not match a
game hosted by the official launcher (`TaleWorlds.MountAndBlade.Launcher`). It saw nothing at
all during run 04 and would have overwritten the DLL underneath a 30-minute session.

**`di_phase1_full` is no longer run 03's end state.** Run 04 saved over it at 11:15, so the
save now holds the run-04 world: one hegemon, seven vassals, Hold 15.7–44.9. Starting run 05
there measures whether a *saturated* hegemony comes apart, which is a fair question but not
the same one as whether it forms too easily — that needs an earlier save.

**Measured since, in run 05** — the lead's own session from the launcher, archived in commit
`29f8abc` as [run-05.log](balance/run-05.log) but not written up here until now. 52 snapshots,
~4.3 in-game years, zero errors, starting from a save of the lead's own (Spring 1137, one
vassal link) rather than `di_phase1_full`. It measured the §2 changes:

- **The submission cap held.** Three submissions against run 04's nine, at 64.5, 61.9 and 63.8
  against the bar of 55, with `threat` at +50.0, +50.0 and +41.0 — never above its new ceiling.
- **`avgHold` ended at 52.3**, from 26.7 at the start, but the set of links changed underneath
  it (1 → 4 → 3) and it sat at 23.1 halfway, so this is not the same links recovering.
  Tribute was withheld **125** times.
- **No poach and no revolt happened**, so neither of the paths §2 changed was exercised. The
  "watch for" lines above are still unanswered.
- Wars: 13 ended, 11 through the peace table, median 76 days, no week with every kingdom at
  war.

### 3. The design review of run 04: the hegemony's structure, fixed — unverified in a campaign

Branch `feature/hegemony-structural-fixes`, 2026-09-16. The three changes in §2 each capped a
number. A game-theory review of run 04 concluded that the collapse to one sphere was mostly
**structural** rather than numerical: a bargain in which one side's duty did not exist in code,
and no counterweight anywhere above the level of a single link. What changed, in the order the
review ranked it:

| # | Defect found by reading the code | Change | Where |
|---|---|---|---|
| 1 | **Protection was measured but never provided.** `CallToArms.Applies` refused every call from a vassal to its patron, and no AI code ever joined a vassal's war — so the Hold term `protection` could only ever read 0 or −20. The bargain had no enforceable patron side, and a sullen vassal was the rational equilibrium | A patron is called when its vassal is **attacked** (never into a war the vassal started), judged by the ally rules including the trust floor, and is called at signing into the wars its new vassal was already defending. A patron's refusal costs trust and Hold, never a mark | `CallToArms.cs` (`Applies`, `DefendNewVassal`), `Hegemony.Submit` |
| 1b | `Protection` counted wars the patron could never be called into | Counts only wars where the vassal is the defender **and** no treaty stops the patron joining — the same rule as `Applies`. Found live: Battania showed −20 for a war with Aserai, a fellow vassal of the same patron | `Hegemony.Protection` |
| 3 | **Submission never read the patron's strength.** Threat and pride are identical for every candidate patron, so the choice came down to reach, trust and culture — a cornered kingdom knelt to its nearest same-culture neighbour even when weaker | A patron no stronger than the candidate scores 0. The threat term is scaled by *cover*: the share of the attackers the patron may fight × how much of them it could match | `Hegemony.SubmissionValue` |
| 5 | **Withholding tribute cost nothing** — no mark, no trust, no lever for the patron. Run 04: 193 withheld | Withholding earns a defiance mark, at most one per 28 days (`TributeWithheldMarkIntervalDays`). A vassal that keeps it up reaches two marks in about a month: no renewal, and its next refused summons breaks the link | `TreatyRegistry.PayDueTribute` |
| 2a | **Nothing balanced against a rising sphere.** Every pact term reads the present | `PactValue` gains a balancing term: how far the strongest sphere neither party belongs to outweighs the two of them, weight 40 | `AiDiplomacy.BalancingPull`, `PactWeightBalancing` |
| 2b | **Revolt was a lone act** against the patron plus half its other vassals, which is why run 04's rebels knelt again | When one vassal revolts, siblings under Hold 25 after the contagion rise with it. All rebels renounce before anyone declares, so the patron's call reaches only the loyal | `Hegemony.TryRevolt`, `RevoltJoinBelowHold` |
| 8 | **`TryPoach` broke the old link before knowing the new one could be signed** — and the break's own −12 observer trust could be what made the signing fail, leaving the vassal free and nobody's | Checks `CanSign` first, with the old link set aside (`replacing:`). The old link now closes without charging the client: the poacher pays (trust, relation, casus belli, war), not both parties | `Hegemony.TryPoach`, `TreatyRegistry.CanSign` |
| — | `HoldAfterFailedRevolt` was a constant nothing read | A vassalage imposed on a kingdom that walked out on the same winner inside two years starts at 20 | `Hegemony.StartingHoldWhenImposed`, `PeaceTable.ImposeSubmission` |
| — | Run 04 could not say whether tribute ever arrived | `[SNAPSHOT]` carries `tributePaid=` and `tributeWithheld=`, cumulative per session | `Telemetry.cs` |

No save data changed: every new behaviour reads existing fields.

**Verified live on `di_phase1_full`, zero errors in the log.** Driven from the run-04 world
through `diplomacy.break_treaty` and `campaign.declare_war`, never saved:

```
Southern Empire freed, declares on Khuzait (NE trusts Khuzait 97):
  (CallToArms) Northern Empire answered Khuzait and declared war on Southern Empire.
  (Core) War opened: Northern Empire -> Southern Empire (CausedByCallToWarAgreement => DefendAlly, legitimacy 1.00)
  Khuzait   protection +20.0 ... => 49.8        (was +0.0 => 28.9)

Western Empire freed, declares on Battania (NE trusts Battania -9):
  (CallToArms) Northern Empire refused Battania - does not trust Battania (-9.0).
  Battania  protection -20.0  trust +12.8 ... => 17.5

Battania on load, at war only with Aserai (a fellow vassal):
  protection +0.0 => 39.7                        (was -20.0 => 19.7 before fix 1b)

diplomacy.submission_value Battania | Northern Empire
  threat +0.0 (cover 0.00) ...                   (its attacker is NE's own vassal)
diplomacy.submission_value Southern Empire | Northern Empire
  Northern Empire is no stronger than Southern Empire and has no protection to offer => 0.0
diplomacy.pact_value Western Empire | Southern Empire
  Balancing pull, included above: 40.0 against Northern Empire's sphere (106389 strength)
```

A one-week `diplomacy.ai_week` on that world ran clean: Western Empire signed an alliance with
Aserai and a non-aggression pact with Khuzait, both defiant vassals of NE. **That is not
evidence for the balancing term** — `pact_value` shows it at 0 for both pairs, because the
partner belongs to NE's sphere; shared threat carried them.

**Not verified, and a tool call cannot verify it:** the joint revolt (needs Hold under 15 for
30 days of real clock), the tribute marks (no payment fell due on the frozen date), the lower
Hold on a re-imposed vassalage, a poach going through `CanSign(replacing:)`, and
`DefendNewVassal` at the moment of submission — the run-04 world has no free kingdom that
would kneel. The player's patron prompt text was built, not seen.

**What run 06 should watch**, beyond §2's list: `answered <vassal> and declared war` and
`leaves its vassal ... to fight alone` lines, whether `avgHold` still settles in the 15–36
band, `tributePaid` against `tributeWithheld`, and whether a sphere ever loses more than one
vassal at a time (`other vassal(s) rose with it`). The balancing weight and the join threshold
are both un-tuned.

**Deliberately left for after run 06**, so it can be measured against these changes rather
than confounded with them — the other findings of the same review:

- **Trust behaves as a grim trigger.** Non-decaying, broadcast to every observer on a breach,
  repaid only bilaterally — and several breaches are forced by the system (revolt at Hold 15,
  two automatic refusals below 40, tribute default). Two breaches put a kingdom below the pact
  floor with every court, with no route back.
- **Alliances do not deter.** `EvaluateWar` never reads the target's allies or patron.
- **The weariness gate almost never binds.** A war ending at exhaustion 60–70 carries 30–35,
  under the gate of 45.
- **Vassals of one patron can be at war with each other** (Aserai / Battania in this save,
  both NE's), and the patron has no way to impose peace between them.
- **The same save holds a hegemon paying tribute to its own vassal**:
  `TributaryPact(Khuzait / Northern Empire)`, 500 from NE, beside `Vassalage(NE / Khuzait)`.
  Left over from an earlier peace; nothing stops the two coexisting.

### 3b. Strength, read where it decides — unverified in a campaign

Raised by the lead, who found reading the save by hand that the hegemon of the whole map was
nearly its weakest kingdom. `diplomacy.strength` (new) on `di_phase1_full`:

```
rank  kingdom            strength   share  fiefs  sphere
   1  Khuzait               28329   19.9%    31  vassal of Northern Empire (balance vs patron +1.00)
   2  Vlandia               20486   14.4%    23  vassal of Northern Empire (balance vs patron +0.66)
   3  Southern Empire       18487   13.0%    12  vassal of Northern Empire (balance vs patron +0.51)
   4  Western Empire        17254   12.1%    13  vassal of Northern Empire (balance vs patron +0.42)
   5  Aserai                16572   11.7%    13  vassal of Northern Empire (balance vs patron +0.36)
   6  Sturgia               16016   11.3%    14  vassal of Northern Empire (balance vs patron +0.31)
   7  Northern Empire       12938    9.1%     7  hegemon, 7 vassal(s), sphere 142130
   8  Battania              12047    8.5%     7  vassal of Northern Empire (balance vs patron -0.10)
```

The strongest kingdom on the map, at 2.2× its patron's strength and 4.4× its fiefs, was a
vassal. **How it happened, from run-04.log:** every one of Northern Empire's links came by
voluntary submission or by poaching, never through the peace table, and neither route read
the patron's strength. Khuzait knelt at 55.5 with `pride -50.0` — the valuation knew it was
the strongest kingdom and let it kneel anyway, because pride compares against the strongest
kingdom overall, not against the patron. §3 already closed that route; this section is what
still let strength fail to matter once a link existed.

Strength was already read in ten places before this branch (war gate and value, pact aggression, tribute demand, land hunger, the hopeless-call check, casualty exhaustion, Hold fear, rival pull, submission threat and pride). The problems were in **how** it was read:

| Where | Defect | Change |
|---|---|---|
| Hold's `fear` term | `ratio − 1`, clamped ±1: twice the vassal's strength scored +25 but half of it only −12.5, and −25 needed a patron with no army. Northern Empire lost at most 13.6 to any vassal for being weaker than six of them | `Hegemony.PowerBalance` — log2 of the ratio, clamped ±1, so the scale is symmetric. One helper for the hegemony system's two-sided comparisons |
| Revolt | Read resentment only. A vassal a fifth of its patron's size revolted at the same Hold as one twice its size: the weak marched to certain defeat, the strong sat under a patron they could have thrown off | The revolt line moves with strength: `15 + 15 × PowerBalance(vassal, patron)`, clamped 0..30. Twice the patron: revolts below 30. Half: never alone — its link still lapses at term, and it can rise with a stronger sibling. `SecessionCapabilityWeight`, un-tuned |
| Peace table | Vassalage could be imposed by a winner weaker than the loser — the one route into vassalage that still asked nothing | `IsDemandable` refuses it; `DescribeAllowance` says why. All three routes now ask `Hegemony.IsStrongEnoughToHold` |
| `Treaty.SetHold` | A Hold stored as exactly 0 read back as the load default of 40 (`HoldOf` treats 0 as "unset"), drifted to 0 and read 40 again — a sawtooth, reachable because the target can be 0 | Set Hold floors at 0.1 |

The same world, before and after, nothing else changed:

| Vassal | fear before → after | Hold target before → after | revolts below |
|---|---|---|---|
| Khuzait (2.19× NE) | −13.6 → **−25.0** | 28.9 → **17.5** | **30.0** — at 28.9, now counting down |
| Vlandia | −9.2 → −16.6 | 33.6 → 26.2 | 24.9 |
| Southern Empire | −7.5 → −12.9 | 32.8 → 27.5 | 22.7 |
| Western Empire | −6.3 → −10.4 | 44.9 → 40.8 | 21.2 |
| Aserai | −5.5 → −8.9 | 35.7 → 32.2 | 20.4 — at 15.7 it starts counting, but its target is 32.2 and it climbs out in about five days |
| Sturgia | −4.8 → −7.7 | 36.6 → 33.7 | 19.6 |
| Battania (0.93× NE) | +1.8 → +2.6 | 39.7 → 40.4 | 13.5 |

The peace-table gate, live: with Aserai freed, `offer_peace Battania | Aserai | vassalage` is
refused with *"Battania is no stronger than Aserai and could not hold it as a vassal"*; the
reverse passes the strength check and stops at the existing one (Battania already has a
patron). A one-week `ai_week` afterwards ran with zero errors and zero warnings.

**Not verified:** a revolt actually firing on the moved line, which needs 30 days of real
clock. The arithmetic, if nothing else in the world moved: Khuzait's target (17.5) sits under
its line (30), so it counts down the full 30 days and revolts. By then every link has drifted
to its target, and after §3's contagion of −10 Southern Empire (17.5), Aserai (22.2), Vlandia
(16.2) and Sturgia (23.7) are under 25 and rise with it; Western Empire (30.8) and Battania
(30.4) stay. Five of seven in one event. The world will move in 30 days, so this is a
prediction to check in run 06, not a result.

**What strength still does not mean, and why it was left:** `CurrentTotalStrength` is the
engine's live military figure. It swings after every large battle, and it counts nothing a
kingdom owns - Khuzait's 31 fiefs and Northern Empire's 7 weigh the same in it. A smoothed or
economic measure (fiefs, prosperity) would describe power better, but a smoothed one needs saved
state and an economic one is a new concept with its own balance. Not started; worth deciding
after run 06 shows how much the swings matter.

### 3d. Run 06 in progress, and the log it writes

Run 06 started 2026-09-16 from `di_phase1_full` and has covered Winter 1136 to Summer 1140 so
far, in two sessions archived as [run-06-part1.log](balance/run-06-part1.log) and
[run-06-part2.log](balance/run-06-part2.log) (part 2 continues from the save part 1 ended on).
It is resumed unattended from **`di_run06_resume`** (Summer 1, 1140; the test hero cured and aged
35, cheat mode off), launched with `pwsh ./scripts/play.ps1 -Without BirthAndDeath` so the module
set matches the first two parts, which ran under GABS without that module.

What it showed before the telemetry was extended, from the monitor, not yet analysed:

- The prediction in §3b held exactly: Khuzait revolted after 30 days at breaking point and
  Southern Empire, Aserai, Vlandia and Sturgia rose with it; Western Empire left through defiance
  the same day. Battania revolted alone ten months later. Northern Empire's sphere went from
  seven vassals to none.
- Western Empire rose first, taking Northern Empire as a vassal (value 90.4); Khuzait followed,
  taking Battania (68.3, cover 0.97) and then poaching Northern Empire from Western Empire.
- A coalition answered Southern Empire against Khuzait: Vlandia, Sturgia and Western Empire.

**The log was rebuilt for runs nobody watches** (2026-09-17). Beside the prose it now writes:

| Record | When | What |
|---|---|---|
| `[RUN]`, `[CONFIG]` | session launch | build time, settings, and **every constant** in `DiplomacyConstants` |
| `[SNAPSHOT]` | weekly, and at launch | world totals, as before |
| `[KINGDOM]` | weekly, per kingdom | live and smoothed strength, dominance, ambition, greed, towns/castles/villages, clans, ruler, influence, gold, weariness, wars, worst exhaustion, patron, vassals, pacts, tribute, trust in and out, last AI move |
| `[LINK]` | weekly, per vassalage | hold, target and every term of it, marks, revolt line, days at breaking point |
| `[WAR]` | weekly, per war | exhaustion, score, casualties, fiefs taken, called by |
| `[EVENT]` | as it happens | `ai_war_declared` (every valuation term, sides, support), `ai_pact_signed` (balancing pull), `war_opened`, `vassalage_formed` (route, value), `poach`, `revolt`, `annexation_breach`, `vassalage_collapsed`/`renewed`, `defiance_mark`, `call_to_arms` (role, outcome, reason), `treaty_signed`/`broken`/`repudiated`/`dissolved`/`expired`, `kingdom_eliminated`, `fief_changed`, `clan_changed_kingdom`, `ruler_changed`, `ruler_died`, `player_died`, `yearly_report` |
| report file | each campaign year | the full world, with the strength table and every sphere |

Every record is `[KIND] day=<absolute day> date=<Season_D;_Year> key=value ...`, no spaces inside
a value. Logs kept: 60, up from 10. Verified live on `di_run06_resume`: the header, the weekly
records, `ai_pact_signed`, `treaty_signed`, `fief_changed` and `war_opened` wrote correctly with
zero errors, and the analyser read them. `call_to_arms` and the hegemony events were not
triggered in that check.

`python tools/analyse-log.py <log> [<log> ...]` reads several logs, oldest first, and drops what a
later log re-covers after a reload. New sections: RUN (flags constants that changed between
sessions), POWER by year, TOP KINGDOM, AI MOVES, EVENTS, HEGEMONY TIMELINE, FIEFS, COALITIONS,
ENGINE (rulers, clans, the player), VASSAL LINKS.

### 3c. Power — the lead's design, built and verified piecewise, unverified in a run

Spec: [design/06-power.md](design/06-power.md). The lead's decisions: strength breeds ambition,
the strong provoke coalitions, a ruler grown too strong turns greedy and wants provinces rather
than vassals, **annexation only through war**, and **a kingdom that loses all its land is gone**.
How strength is measured was left to the implementation: **live** strength for what a kingdom can
do now (ambition, revolt capability, allies), a **smoothed 84-day average** for what it is becoming
(greed, dread, the balancing pull). New save data: `KingdomPower`, definer id 9, `ModState` 10.

Found and fixed on the way, both real:

- **Elimination would have frozen the conqueror.** The engine already destroys an AI kingdom on its
  last settlement (verified by IL), but raises no peace event, and our ledger closed wars only on
  that event. The war would have stayed open forever, counted as a chosen war. Wars now close with
  `endedBy=Eliminated`.
- **Coalitions could not work.** Allies judged a war hopeless against the caller plus one ally, so
  against a strong enemy every ally refused in turn; and the war valuation ignored the target's
  allies, so an alliance never deterred. Both read whole sides now (`CallToArms.ExpectedSupport`).
- **An annexation was filed as a just war** — BrokenTreaty at 0.95 — on a revolt claim the patron
  had forgiven by taking the vassal back. Submission now settles breach claims between the two; a
  sweep at load settles the old ones (1 in `di_phase1_full`).

**Verified live on `di_phase1_full`, zero errors, never saved:**

| What | Evidence |
|---|---|
| Strength table | Khuzait dominance 1.59, ambition 0.40; every greed 0.00 — nobody dominant |
| Greed and dread (smoothed strength set by the new test command) | NE at greed 0.54: every link `dread -13.4`, revolt lines up 8.1 |
| Annexation by the real AI evaluation | `Northern Empire tore up its vassalage with Sturgia to annex it (greed 0.93); 6 other vassal(s) saw it happen` — war declared, vassals refusing the summons, one vassalage broken on its third mark |
| Coalition carried by the balancing pull | Sturgia / Southern Empire defensive pact at 63.9, of which the pull was 40.0 — 23.9 without it, under the bar of 35 |
| Sides in the war valuation | NE → Battania: alone 1.07, with expected support 17,254 vs 0 → 2.51 |
| Elimination | Battania's seven fiefs given away; on the last: both its wars closed `endedBy=Eliminated`, the vassalage dissolved, two further AI weeks ran clean |
| Old breach claim settled on load | `Settled 1 broken-treaty claim(s)`; `Northern Empire vs Sturgia: BrokenTreaty` gone |

**Not verified:** anything that needs the clock or a genuinely dominant kingdom — greed arising on
its own, dread-driven revolts, the second-war allowance in use, coalitions deterring over years.
Run 06 is that measurement.

### 4. Phase 2 — court intrigue

Superseded on 2026-09-23 — Phase 2 is no longer a future item, and the current version of
this is the **Phase 2 — where it starts** section in the handoff at the top of this file.
One correction this entry needs recording rather than deleting: it claimed
`CallToArms.WouldAnswer` carries a note about vassal defiance reading grievances. It does
not, and never did.

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

---

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
