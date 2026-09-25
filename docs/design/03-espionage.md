# Design 03 — Espionage

Status: **decided and in build** - the lead's decisions are §9, what is built is §10 (3.1, 3.2
and 3.4 on 2026-09-25, verified live; 3.5 and 3.3 the same day, compiled but not yet run in game). Phase 3. Depends on Phase 1 (claims, treaties, trust) and
Phase 2 (grievances, loyalty) already existing — espionage in this design is mostly a way to
*reach into* those systems, not a separate scoreboard.

Constants live in `Espionage/EspionageConstants.cs`.

## 0. The problem we are solving

Calradia has no information asymmetry and no covert options. You either march an army at a
problem or you ignore it. There is no way to weaken a neighbour without declaring war, no
reason to fear a kingdom you are at peace with, and nothing useful to spend money on once
your army is maxed.

The design rule for this pillar: **espionage must never be a free action.** Every operation
either costs something real or risks handing the target a legitimate reason to invade. If a
mission is strictly better than doing nothing, it is mis-specified.

---

## 1. Spy networks

One strength value per **owner clan and target kingdom** (our network *in* their realm), 0–100.
Ordered because Vlandia spying on Battania is not the same asset as the reverse. (Written as a
kingdom pair first; the lead moved ownership to the clan on 2026-09-25, §9 decision 5.)

**Building:** an ongoing weekly investment, not a purchase.

```
weeklyGrowth = (goldSpent / 2000) × (1 + agentRoguery / 200)
             − targetCounterIntelligence × 0.08
             − 0.7                                  // baseline attrition
```

- Requires a designated **handler** — a companion or clan member assigned to the target
  realm. Their Roguery and Charm set the ceiling: `maxStrength = 40 + roguery/2 + charm/4`.
- A network in a realm you are **at war with** grows at half rate: borders are watched.
- **Decay −0.1/day** on top of the above, always.

Networks are the slow, boring part on purpose. It means a war you planned three years ago
goes better than one you improvised, which is the strategic texture the pillar exists for.

## 2. Missions

All costs are per attempt, paid up front. Duration is the delay before resolution.

| Mission | Network req. | Gold | Days | On success |
|---|---|---|---|---|
| **ScoutArmies** | 15 | 1,000 | 3 | Enemy army positions and strengths revealed for 7 days |
| **ReadCourt** | 25 | 2,000 | 5 | Target's pending kingdom decisions, bloc powers and war exhaustion revealed exactly for 14 days |
| **SabotageGarrison** | 35 | 5,000 | 7 | Target garrison −25%, siege engines in progress destroyed |
| **SpreadDissent** | 30 | 4,000 | 10 | Settlement loyalty −15, unrest rises |
| **BribeLord** | 45 | 25,000 | 14 | Target clan loyalty −20; flips to us if a civil war starts within 2 years |
| **ForgeLetters** | 50 | 15,000 | 14 | A fabricated grievance of weight 8 against their ruler |
| **StealTreasury** | 40 | 3,000 | 7 | Steal `min(20% of ruler gold, 50,000)` |
| **Assassinate** | 70 | 60,000 | 21 | Target hero dies; see §5 for why this is the worst-value option |

`ReadCourt` is the mission that answers the open question in spec 01 §8: if the default
visibility of enemy exhaustion is a qualitative band, this is what buys the exact number.

## 3. Counter-intelligence

A per-kingdom defensive value, 0–100, built by standing investment rather than per-mission:

```
counterIntelligence = 10
                    + (weeklyGoldSpent / 1500)
                    + rulerClanSecurityFocus × 5      // a policy slot the AI also uses
                    + 0.05 × averageSettlementSecurity
```

*As built (3.3, the lead's decisions 9 and 10):* `10 + weekly gold paid / 1500 + 0.05 × average
security`, clamped to 0-100, the budget paid by whoever rules the realm. The security-focus term is
dropped - vanilla has no such policy, and the policies that raise security already count through
the last term.

It suppresses enemy network growth (§1) and raises exposure chance (§4). The AI budgets for
it out of the same purse as troops, so a realm that over-invests in spies is militarily
weaker — the trade-off has to be visible or the whole pillar is just extra income.

## 4. Resolution

```
successChance = clamp(0.15
                    + 0.005 × networkStrength
                    + 0.004 × (handlerRoguery + handlerCharm) / 2
                    - 0.006 × targetCounterIntelligence
                    - missionDifficulty,              // per-mission constant
                    0.05, 0.95)
```

On **failure**, roll exposure:

```
exposureChance = clamp(0.25 + 0.008 × targetCounterIntelligence - 0.003 × networkStrength, 0.05, 0.90)
```

Three outcomes, matching `Models.MissionOutcome`:

- **Success** — effect applies. Network −5 (assets get used up).
- **Failure** — nothing happens. Network −10, gold spent.
- **Exposed** — network drops to **0**, and §5 fires.

## 5. Exposure is a diplomatic event

This is the hinge that keeps espionage honest, and the most important section here.

When an operation is exposed, the **victim**:

- gains the `EspionageExposed` casus belli against us (legitimacy **0.85** — near the top of
  the Phase 1 scale, because being spied on is a very defensible reason for war),
- loses **25 trust** in us, permanently until rebuilt,
- and for `Assassinate` specifically, every *other* kingdom loses **15 trust** in us too.
  Assassination is cheap in effect and catastrophic in reputation; it should be the tool of
  someone who has run out of better ideas.

So a caught operation does not just fail — it can drag the player into a war they did not
choose, at a moment they did not pick. That is the intended feeling.

## 6. Cross-pillar payoffs

| Mission | Reaches into | Effect |
|---|---|---|
| `BribeLord` | Phase 2 loyalty | −20 loyalty, defection during civil war |
| `ForgeLetters` | Phase 2 grievances | Manufactured grievance against their ruler |
| `ReadCourt` | Phase 2 blocs, Phase 1 exhaustion | Reveals the numbers the player would otherwise guess |
| `SpreadDissent` | Settlement loyalty | Feeds rebellion pressure |
| Exposure | Phase 1 claims, trust | `EspionageExposed` casus belli, −25 trust |

Note the shape: espionage owns almost no state of its own. Networks and missions are the
only new data; everything an operation *does* lands in Phase 1 or Phase 2. That is what
keeps three pillars from becoming three unrelated games.

## 7. UI

- **Network map** — a strength figure per kingdom, with the trend and the assigned handler.
- **Mission board** — available missions for the selected target, each showing its real
  success and exposure chance. The AI reads the same numbers; nothing is hidden from the
  player that the AI gets to use.
- **Operations in progress** — with days remaining, cancellable at the cost of the payment.

## 8. Implementation order

| Step | Deliverable | Depends on |
|---|---|---|
| 3.1 | Network model, growth/decay, handler assignment | Phase 1 complete |
| 3.2 | Mission model, scheduling, resolution math | 3.1 |
| 3.3 | Counter-intelligence | 3.1 |
| 3.4 | Exposure → casus belli + trust (the §5 hinge) | 3.2, Phase 1.2/1.4 |
| 3.5 | Cross-pillar effects into Phase 2 | 3.2, Phase 2 complete |
| 3.6 | AI running its own networks and missions | 3.2–3.4 |
| 3.7 | Espionage UI | all above |

## 9. Decisions, 2026-09-25

The lead answered the four open questions below, and one more that the data model raised,
before any Phase 3 code:

| # | Question | Decision |
|---|---|---|
| 1 | Assassination at all? | **Keep it, as specced, with the §5 penalties.** |
| 2 | Can the player be a target? | **Yes, and clearly telegraphed.** The AI runs operations against the player's realm under the same rules; every one aimed at us leaves a visible trace, and nothing is lost to a roll the player cannot see |
| 3 | Handler risk on exposure | **Captured, never killed.** The victim holds the handler prisoner; the owner can ransom them back |
| 4 | Do networks survive a war? | **Yes, growing at half rate**, as §1 says: a network built before a war is what the wartime missions run on |
| 5 | Who owns a network? | **Each clan, not each kingdom.** §1 said "per ordered kingdom pair", which leaves a player who serves a king, most of a campaign, with no espionage at all. A network is owned by a clan and paid from its purse, and it sits in a target kingdom. An exposure still hands the victim a casus belli against the owner's **kingdom**, so a vassal's operation can drag its liege into a war. The AI runs networks only from ruling clans: the rule is the same for every clan, and that limit is the AI's choice, not an exemption |

Consequences written into the model: a network is keyed by (owner clan, target kingdom); a clan
cannot run one inside its own realm; the handler is a hero of the owning clan.

Three more, asked before 3.5's code on 2026-09-25, where §2 left the two Phase 2 missions open:

| # | Question | Decision |
|---|---|---|
| 6 | BribeLord "flips to us if a civil war starts within 2 years": flips to whom? | **The rising.** When an internal war starts in the bribed house's realm inside the window, the house takes the rising's side whatever its bloc or relations say. Not defection to the briber's own realm, which would move fiefs across a border and has nowhere to go for an owner outside any realm |
| 7 | How long does the -20 loyalty last? | **The same two years, then gone.** One window for both effects, not a second curve to tune |
| 8 | Whose grievance is a forged letter? | **A lord the player chooses**, as a bribe is paid to one; not the least loyal house picked automatically |

And two before 3.3's, the same day:

| # | Question | Decision |
|---|---|---|
| 9 | Who pays a realm's counter-intelligence budget? | **Whoever rules**, from their own purse - the purse that pays the troops, which is §3's trade-off. One budget per realm; it outlives a change of ruler. A vassal cannot contribute |
| 10 | §3's "security focus × 5, a policy slot the AI also uses" | **Dropped.** Vanilla has no such policy, and the ones that raise security already reach counter-intelligence through the security term; a second term would count them twice |

### The questions as they were asked

1. **Assassination at all?** It is in the enum and specced above, but it is the one mission
   that can permanently delete content a player cares about (a named lord, a marriage
   prospect, a claimant). Options: keep as specced with heavy reputation cost; restrict to
   non-ruler targets; or cut it. Recommend keeping it with the §5 penalties — it makes the
   other seven missions look reasonable by comparison.
2. **Can the player be a target?** For symmetry the AI should run operations against the
   player's realm. That means the player occasionally loses a garrison or a loyal vassal to
   an enemy network, which is either the best part of this pillar or the most frustrating,
   depending entirely on how well it is telegraphed.
3. **Handler risk.** Should a companion running a network be capturable or killable on
   exposure? It gives the pillar personal stakes; it also means losing a companion to a dice
   roll the player did not see.
4. **Do networks survive a war?** Currently they grow at half rate in wartime but persist.
   The alternative — war collapses networks — makes pre-war preparation matter much more.

## 10. What was built

### 3.1, spy networks - built and verified live, 2026-09-25

| Piece | Where |
|---|---|
| The saved network: owner clan, target realm, strength, handler, budget, last week (class id 15, `ModState` property 15) | `Models/SpyNetwork.cs` |
| Founding, handlers, the weekly sum, daily decay, releasing a handler who no longer qualifies | `Espionage/SpyNetworks.cs` |
| Counter-intelligence, base and security terms only until 3.3 | `Espionage/CounterIntelligence.cs` |
| Upkeep under its own settings toggle | `Behaviors/EspionageBehavior.cs` |
| Levers | `diplomacy.networks`, `test_assign_handler`, `test_network_budget`, `test_network_week`, `test_hire_companion` |

A handler must be of the owning clan, grown, free, not the clan's head, not leading a party and
not governing. They are stationed in the target realm's most prosperous town. Validity is
checked every day rather than hooked on events, so no listener order matters (CLAUDE.md §1).

| Check | Result |
|---|---|
| Eligibility | A governor and two party leaders of Urkhunait refused with the reason; Chaghan accepted and sent to Sargot, Vlandia's richest town |
| The sum, by hand | Ceiling 40 + 48/2 + 76/4 = **83**; gold 10,000/2,000 x (1 + 48/200) = **6.20**; counter-intelligence 10 + 0.05 x 51 = **12.6**, x 0.08 = 1.01; weekly **+4.49**. The upkeep applied exactly that |
| Wartime | Urkhunait in the Northern Empire, at war: 6.40 x 0.5 = 3.20, weekly **+1.49** |
| Daily decay | `tick_days 7`: 4.5 -> **3.8** |
| Gold | the owner's head paid exactly 10,000 |
| **Save round trip** | saved `di_espionage_test`, new process, reloaded: `2 spy networks`, handlers, strengths and budgets intact |
| The campaign's own weekly tick | a player companion hired and assigned from the main party (moved out of it to Pravend), then the real clock: "Calastides in Vlandia: 0.0 -> 4.4 (weekly +4.36, spent 10000)" |
| A handler taken by vanilla | on the real clock, vanilla made **both AI handlers governors** within days, and the daily check released them with the reason |

**Found, for 3.6:** vanilla puts an AI clan's idle lords to work - as governors here, and it
raises parties from them too. An AI handler will be taken away the same way unless it is kept
out of those choices. The player's companions are not touched, since vanilla leaves the player's
appointments to the player. How to reserve an AI handler is 3.6's first problem.

**Not verified:** a network owned by a clan whose realm later becomes the target's (it goes
idle by rule; not seen), and a handler dying or captured (the same daily check as the governor
case, not seen for those causes).

### 3.2 missions, with 3.4 exposure - built and verified live, 2026-09-25

| Piece | Where |
|---|---|
| The saved operation, kept 60 days after it resolves (class id 16, `ModState` property 16) | `Models/SpyMission.cs` |
| The table of §2, the odds of §4, launching, resolving, six effects | `Espionage/Missions.cs` |
| Exposure: casus belli, trust, the handler captured, the vassal's crown displeased | `Espionage/Exposure.cs` |
| Levers | `diplomacy.mission_odds`, `missions`, `test_launch_mission`, `test_resolve_mission`, `test_set_network` |

**Decided in building, not in the spec:**
- §4 names a per-mission difficulty and gives no values. It is `(requirement - 15) / 200`: one rule,
  so ScoutArmies costs nothing and Assassinate 0.275. UN-TUNED.
- A handler runs one operation at a time. An operation whose handler is gone when it comes due
  fails, with nobody left to be caught.
- ScoutArmies and ReadCourt reveal for 7 and 14 days, derived from the mission's own record.
  Today they report once, to the owner and the log; the screens that keep showing the reveal are 3.5
  (the Encyclopedia) and 3.7.
- Sabotage removes a quarter of each regular troop line. The siege engines of §2 are not touched.
- Dissent is the loyalty loss of §2; "unrest rises" has no separate number.
- An assassination names no killer: unexposed, it is traced to nobody.
- Exposure's trust loss goes through the one trust ledger, so its grudge decays like any other.
  §5 said "permanent until rebuilt"; a second trust rule for spies would be a second resolver.
- The lead's decision 5 said a vassal who drags the realm into trouble answers to the crown. That
  is a relation loss of 15 between the ruler and the house's head, not a grievance: nothing reads a
  grievance a crown holds against its vassal, and relation is what loyalty reads.

| Check | Result |
|---|---|
| The odds, by hand | network 80 -> 0.400, handler (48 + 76) / 2 -> 0.248, counter-intelligence 12.6 -> 0.076: ScoutArmies **0.72**, exposure on failure 0.25 + 0.101 - 0.240 = **11%** |
| Gates | a second operation while one runs refused; Assassinate at network 45 refused ("needs 70") |
| Sabotage | Sargot's garrison **275 -> 210**; network -5 |
| Dissent | Sargot's loyalty **44 -> 29** |
| Treasury | Derthert **158,443 -> 126,755** (20%, 31,688) and Monchug +31,688 less the 3,000 paid |
| Scout, read court | reports: "army of Aldric, 10 parties, 269 men, near Pravend"; "legitimacy 60.0, worst war exhaustion 5.1" |
| A natural roll | ScoutArmies at 65% came up **Failure**: network -10 |
| Assassinate | Morcon, Derthert's son, **dead** |
| **Exposed assassination** | Vlandia's trust in Khuzait **-7.5 -> -32.5**; every other realm -15 (Battania -7.5 -> -22.5, Northern Empire -20.3 -> -35.3); **EspionageExposed** for Vlandia against Khuzait, legitimacy 0.85, two years; Chaghan **taken prisoner** at Sargot; network burned to 0 |
| The vassal answers | Urkhunait's exposure: the player, ruling Khuzait, **0 -> -15** with Monchug, and told "Vlandia now holds a casus belli against Khuzait" |
| The player's own operation | "Naselos the Scholar begins scouting the armies in Vlandia: 3 days, 54% to succeed" - 0.15 + 0.20 + 0.27 - 0.076 = 0.544 - then the report |
| The player's realm as victim | "Agitators have been stirring Chaikand: loyalty 34 -> 19" - no name, since it was not exposed |
| **Save round trip** | saved `di_espionage_missions` with an operation pending; new process: `4 spy networks, 4 spy missions`, the pending one intact |
| The campaign's own clock | after the reload, the real clock resolved that pending ReadCourt on its day - a natural roll, **Success** at 47% - and reported it. In the same days vanilla made two more AI handlers governors, as at 3.1; the player's companion handler stayed |

**Balance, recorded not acted on:** once a network is strong the overall chance of exposure is
small - 3-6% at 80 against a counter-intelligence of 12.6. At 15 it is 18%. The risk that is meant to
keep the pillar honest falls away just as the network becomes useful. Counter-intelligence budgets
(3.3) are what should push it back up; measure after 3.3.

**Not verified:** a mission whose handler is lost before it resolves (the rule is written, not
seen), an exposure by a clan outside any realm, cancelling, and the reveal durations, which have
no reader yet.

### 3.5, the cross-pillar effects - built and compiled, NOT run in game, 2026-09-25

Built in a cloud session with no game. It compiles clean against the v1.4.8 reference assemblies
(`scripts/compile-check.sh`), 0 warnings. **Nothing below has been seen in game.**

| Piece | Where |
|---|---|
| Whether a house is bought: one resolver, derived from the BribeLord record, no new save data | `Espionage/Bribes.cs` |
| The -20: a "foreign gold" term in the loyalty sum, shown on the Court tab only when it is not zero | `Intrigue/LoyaltyModel.cs`, `UI/KingdomScreen/CourtVM.cs` |
| A bought house takes the rising's side when an internal war starts | `Intrigue/InternalWars.Assess` |
| A successful bribe kept on the record for its two years, the rest still 60 days | `Missions.KeepDays` |
| Forged letters: `GrievanceType.ForgedLetters = 10`, weight 8, through the one grievance ledger | `Models/Enums.cs`, `IntrigueConstants.GrievanceForgedLetters`, `Missions.ApplyEffect` |
| ReadCourt's exact figures on the Encyclopedia, in the "Their ledger" block reserved for it | `UI/EncyclopediaPages/EncyclopediaCourtVM.cs` |
| Levers | `diplomacy.bribes`; the existing `test_launch_mission`, `test_resolve_mission`, `loyalty`, `grievances`, `court_bands`, `internal_wars`, `test_start_internal_war` |

**Decided in building, not in the spec or by the lead:**
- Both missions need the **head** of a house sworn to the target, not the crown's own house and not
  a mercenary: what they change is the house's standing with its crown, and loyalty is read from
  the head. Checked at launch and again before the roll; a mark who has died, been succeeded or left
  the realm fails the operation before any roll, with no exposure risk - the rule 3.2 already uses
  for a lost handler. So a Success on the record always means the effect landed.
- **A bribe binds the head who took it.** It lapses early if that lord stops heading the house
  (death, succession) or the house leaves the realm. The heir took nothing.
- Several bribes on one house do not stack: it is bought or not. A second purse only extends the window.
- The 25,000 is spent, not handed to the lord's purse. §2 prices every mission as a cost paid up
  front, and a bribe that moved gold into the target's treasury would make it a transfer as well.
- The -20 is a flat loyalty term, not a grievance: a bought lord has been paid, not wronged.
- The victim's court is told a bribe landed ("foreign gold has reached ...") and sees the term on the
  Court tab, never the buyer - the lead's decision 2. The forged grievance is labelled
  "forged" on the ledger: the victim was told when the letters surfaced, and anyone else reading
  that ledger has an agent inside the court.
- ReadCourt reads the court **live** for its 14 days, not a snapshot of the day it landed. The ledger
  gives legitimacy, each war's exhaustion, each party's power, what is before the council (vanilla's
  own decision titles) and every house's loyalty and grievances. Everything above the ledger on the
  page stays in bands. The reveal is the player's own house's; a liege's or vassal's does not count.

**What to check in game, and how** (none done yet):
1. `test_set_network <clan> | <kingdom> | 60`, `test_launch_mission <clan> | <kingdom> | BribeLord | <a house head>`,
   `test_resolve_mission ... | success` - then `diplomacy.bribes` and `diplomacy.loyalty <kingdom>` should show
   `foreign gold -20.0` on that house, and its Court tab row the "Foreign gold" line if the player sits in that court.
2. On `di_pretender_test` (Battania rises on the first tick): bribe a Battanian house that the
   relation roll would leave with the crown, then `test_start_internal_war Battania` - it must be in
   the rebels, marked "(bought by ...)" in the log.
3. ForgeLetters the same way, then `diplomacy.grievances` - `ForgedLetters 8.0` against the ruling clan.
4. ReadCourt by the player's house, resolved as success, then `diplomacy.court_bands <kingdom>` and the
   Encyclopedia page: the ledger block must show the figures, one per line. **Whether Gauntlet breaks
   the lines of that TextWidget on `\n` is unverified** - if it renders as one run, that is the first fix.
5. Save and reload with a live bribe: `diplomacy.bribes` must still read it (no new save data, so
   this should hold, but it has not been seen).

**Open, for 3.6:** under today's rules an AI network could bribe the player's own house, and the
player would then be asked at the next internal war as for any other rebel side. Nothing launches
an AI operation until 3.6, and whether the player should instead be offered the gold is 3.6's call.

### 3.3, counter-intelligence budgets - built and compiled, NOT run in game, 2026-09-25

Built in the same cloud session as 3.5, with no game: 0 warnings against the v1.4.8 reference
assemblies. **Nothing below has been seen in game.**

| Piece | Where |
|---|---|
| A realm's standing order and what was actually paid last week (class id 17, `ModState` property 17) | `Models/CounterIntelligenceBudget.cs` |
| The budget term in the one resolver, clamped to 0-100; setting a budget; the weekly payment | `Espionage/CounterIntelligence.cs` |
| One weekly list - budgets paid, then networks grown - for the campaign, `ai_week` and `test_network_week` alike | `Espionage/EspionageUpkeep.cs` |
| Levers | `diplomacy.counter_intelligence [kingdom]`, `test_counter_budget <kingdom> \| <weekly denars>`; `test_network_week` now runs the whole espionage week |

**Decided in building, not by the lead:**
- The term reads what was **paid** at the last weekly upkeep, not what was ordered: a ruler with a
  large order and an empty purse defends at what the purse covered. A new order therefore takes
  effect at the next weekly upkeep.
- Budgets are paid **before** networks grow, in one fixed list, so a network meets the defence
  paid for the same week - and nothing depends on the engine's listener order (CLAUDE.md §1).
- A player ruler whose purse falls short is told; an AI ruler's shortfall is only logged.
- A budget with nothing ordered and nothing paid is dropped, so the save does not keep empty rows.
- **No AI realm orders anything yet.** Every AI realm still defends at its base and its towns until
  3.6 decides what an AI ruler spends. The player's missions against AI realms therefore see the same
  counter-intelligence as before 3.3.

**By hand, not measured:** on §10's example (network 80, handler skill 62), 15,000 a week lifts
counter-intelligence 12.6 -> 22.6, and overall exposure from 3.1% to 6.4% for ScoutArmies and from
6.1% to 11.7% for Assassinate. That is the push back against §10's balance note that 3.3 was meant
to provide; whether it is enough is for a run with AI budgets in it, after 3.6.

**What to check in game** (none done yet):
1. `test_counter_budget <player's realm> | 15000`, then `test_network_week`: the ruler's purse falls
   by 15,000, and `diplomacy.counter_intelligence` shows `budget +10.0 from 15000 paid`.
2. The same with a purse below the order: the shortfall paid, the notice shown to a player ruler.
3. `diplomacy.mission_odds` against that realm before and after: the counter-intelligence term moves,
   and so do the success and exposure figures.
4. Save and reload with a budget set: `counter_intelligence` must still read it - **the first run of
   a new savable type**, so load the save in a fresh process, not only the same session.
5. The real weekly tick, not the lever: one budget paid on the campaign's own clock.
