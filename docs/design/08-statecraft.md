# Design 08 — Statecraft: the character behind the crown

Status: **plan for review, 2026-09-25. Nothing is built.** It changes formulas in Phase 1 and
Phase 2 alike, so where it sits on the roadmap is the lead's call (§15, D10).

The lead's brief, 2026-09-25: the mod's mechanics run independently of the game's skill system,
and that is what matters most right now. Map the skill system against the mod's mechanics,
decide how they integrate, update the formulas, and plan the work.

How to read this: **§1 and §2 are facts.** Each one was read out of the v1.4.8 assemblies
(`tools/ApiDump`, `tools/CallSites`, Cecil) or out of the game's own XML, and says which.
**§3 onward is proposal.** Every weight and threshold in it is UN-TUNED, and every choice the
lead has to make is collected in §15.

---

## In one page

| Skill | What it will decide | Whose skill counts | Largest single effect |
|---|---|---|---|
| **Leadership** | How fast a realm tires in a war. How firmly its vassals hold. What its court thinks of the crown | The ruler's own. Never delegated | War exhaustion accrues ±15% |
| **Charm** | What a victory buys at the peace table. Whether a court accepts a pact. Whom houses back for the throne. Who speaks for a bloc | At the table, the envoy: the best Charm in the acting house, companions included. A hero's own Charm when standing for a throne or leading a bloc | Peace budget ±15% |
| **Steward** | How fast grievances fade, and how fast a crown's legitimacy recovers in peace | The steward: the best Steward in the house | Grievances fade ×0.5 to ×1.5 |
| **Trade** | What a house costs to turn in a civil war | The treasurer: the best Trade in the house | Price ±15% |
| **Roguery** | Whether a fabricated claim is caught | The spymaster: the best Roguery in the house | Exposure 10–30% |
| **Scouting** | Catching someone else's fabrication | The watch: the best Scouting in the house | The defending half of the above |

Each skill is scored against the median of the same role across every living realm. A ruler as
good as their peers moves nothing, so the AI-only world keeps the balance seven runs measured.
A player well below or above their peers feels it (§4).

In the other direction, the acts this mod adds train the skill they use. A peace treaty trains
the envoy's Charm, a vassal won trains the ruler's Leadership, a forged claim trains the
spymaster's Roguery (§6).

Before any of that, three things the takeover of vanilla diplomacy quietly broke get repaired
(§2). One of them is a real fairness bug: **the player pays 200 influence to propose a war that
costs an AI ruler 52–100.**

---

## 0. The problem

The mod reads kingdoms, clans, relations, strength and its own ledgers. It never reads the hero.

- **No formula in `src/` reads a skill, a perk or a trait** (grep, 2026-09-25). A skill enters
  in one place only, and indirectly: 2.6b's house division reuses vanilla's
  `HeirSelectionCalculationModel`, which gives a family's most skilled hero a bonus.
- A ruler with Charm 300 negotiates exactly like one with Charm 10. A king with Leadership 250
  holds his vassals no better than one with 50.
- The player's character build means nothing in the political game. Companions have no
  political use. Nothing the player does as a ruler teaches them anything.
- Every AI ruler facing the same numbers decides the same way. The takeover replaced vanilla's
  AI diplomacy, which read personality, with an evaluation that reads none (§2, finding 4).

Two directions are missing, and this plan builds both:

1. **Skill → outcome.** The hero who acts brings their skill to the formula.
2. **Act → skill.** The acts this mod adds train the skills they exercise.

---

## 1. The character system as v1.4.8 ships it

### 1.1 The six skills this plan uses

There are eighteen skills and six attributes. This plan uses six skills: the ones whose vanilla
meaning is political or informational. The engine reads the other twelve in battles, sieges,
travel and crafting. They reach this mod only through outcomes it already records, such as
casualties, captures and war score (§3, rule 1).

| Skill | Attribute (IL of `DefaultSkills.InitializeAll`) | What vanilla does with it per point (`DefaultSkillEffects`) | Its job here |
|---|---|---|---|
| Charm | Social | +0.5% relation gain with NPCs | Persuasion |
| Leadership | Social | +0.1 morale for parties under your command, +0.2 garrison size | Resolve and authority |
| Steward | Intelligence | +0.25 party size | Recovery |
| Trade | Social | −0.2% trade penalty | Bargaining over gold |
| Roguery | Cunning | +0.25% battle loot | Subterfuge |
| Scouting | Cunning | sight, tracking and speed (not read for this plan) | Vigilance |

**Charm already reaches this mod, indirectly.** `DefaultDiplomacyModel.GetRelationIncreaseFactor`
scales every relation gain by the Charm of the player when the player is one of the pair, and
otherwise by the Charm of one of the two heroes picked at random. Relation feeds four of the
mod's formulas: pact value, loyalty, the side-change price and succession backing. So Charm
already moves them slowly, as relations build. This plan's Charm terms are the other half: the
argument made at the table, on the day. Every Charm term here is kept small for that reason.

### 1.2 Perks

- Tiers unlock at **25, 50, … 300**, two perks per tier (`DefaultPerks.TierSkillRequirements`,
  read with Cecil). AI heroes get perks too, through `HeroDeveloper.SelectPerks`.
- Each perk's effects are tied to a `PartyRole`. v1.4.8's enum has a **`Ruler` role (value 1),
  and no perk in any skill uses it**. The engine has a slot for ruler perks and ships none.
- The perks that touch politics, and where vanilla reads them:

| Perk (skill, tier) | Vanilla effect | Read by | Status under this mod |
|---|---|---|---|
| **Firebrand** (Charm 125) | −25% influence cost to initiate kingdom decisions | `DefaultDiplomacyModel.GetPerkEffectsOnKingdomDecisionInfluenceCost` | **Half dead**: see §2, finding 2 |
| Flexible Ethics (Charm 125) | −30% influence to vote for others' proposals | `KingdomDecision.GetInfluenceCostOfSupport` | Works; votes are still vanilla's |
| Good Natured, Tribute (Charm 175) | Influence back on a failed proposal; extra relation with merciful or cruel lords | `KingdomElection`, `GetRelationIncreaseFactor` | Works |
| Forgivable Grievances (Charm 75) | Fewer critical persuasion failures; a daily chance to mend bad relations | `Persuasion`, `CharacterRelationCampaignBehavior` | Works. Its name promises more than it does here (§7) |
| Effort For The People, Slick Negotiator (Charm 150) | Smaller barter penalty | `DefaultBarterModel.GetBarterPenalty` | **No longer touches peace**: §2, finding 3 |
| Immortal Charm (Charm 275) | +5 influence a day | vanilla influence | Works |
| **Silver Tongue** (Trade 250) | −15% gold to persuade lords to defect to your faction | `JoinKingdomAsClanBarterable` | **Never reaches the mod's own version of that act**: §2, finding 5 |
| Inspiring Leader (Leadership 175), Call To Arms (Tactics 150) | Cheaper army calls | vanilla armies | Works |

More perks with a political reading are listed in Appendix A.

### 1.3 Experience

- `Hero.AddSkillXp(skill, xp)` calls `HeroDeveloper.AddSkillXp`. That multiplies the raw XP by
  the hero's focus factor and the generic XP multiplier, and **ignores any amount ≤ 0**. So XP
  granted this way respects the learning rate and learning limit the player already builds for.
- What one skill level costs (`DefaultCharacterDevelopmentModel`):

  | At skill | 50 | 100 | 150 | 200 | 250 | 300 |
  |---|---|---|---|---|---|---|
  | XP for the next level | 1,866 | 6,191 | 13,016 | 22,341 | 34,166 | 48,491 |

- Vanilla's own grants give the scale. One point of relation gained with a kingdom's ruler is
  **600 Charm XP**, and with a clan head **400**. A bribe is **0.1 × gold** as Roguery XP, and
  only for the main party. Leading an army pays Leadership XP of 0.0004 × strength × morale
  each time the hook is called.
- **A vanilla hook that looks useful is inert.** `Clan.set_Influence` calls
  `SkillLevelingManager.OnInfluenceSpent(leader, new − old)`, which is meant to grant Steward
  XP. It passes a negative delta, and `AddSkillXp` drops it, so spending influence trains
  nothing, in vanilla or here. This plan grants XP explicitly and relies on no vanilla hook.

### 1.4 Who holds which skills at the start

Read from `SandBox/ModuleData/lords.xml` and the `SkillSet` templates it references. **These
are template values at the start of a campaign.** AI heroes gain XP as they play, so S0
measures the real figures in game (§11).

The eight rulers' own skills:

| Kingdom | Ruler | Charm | Leadership | Steward | Trade | Roguery | Scouting |
|---|---|---|---|---|---|---|---|
| Northern Empire | Lucon | 240 | 200 | 160 | 170 | 190 | 110 |
| Western Empire | Garios | 160 | 190 | 240 | 240 | 120 | 150 |
| Southern Empire | Rhagaea | 210 | 180 | 190 | 170 | 240 | 190 |
| Sturgia | Raganvad | 160 | 220 | 220 | 90 | 230 | 180 |
| Aserai | Unqid | 160 | 220 | 230 | 250 | 170 | 200 |
| Vlandia | Derthert | 160 | 220 | 220 | 90 | 230 | 180 |
| Battania | Caladog | 200 | 230 | 170 | 240 | 90 | 180 |
| Khuzait | Monchug | 160 | 220 | 220 | 90 | 230 | 180 |

The best adult of each ruling house (family, from `heroes.xml`). This is who §4's delegation
rule would pick:

| Kingdom | Charm | Steward | Trade | Roguery | Scouting |
|---|---|---|---|---|---|
| Northern Empire | 240 | 240 | 210 | 190 | 180 |
| Western Empire | 230 | 240 | 240 | 150 | 150 |
| Southern Empire | 230 | 190 | 220 | 240 | 190 |
| Sturgia | 220 | 240 | 170 | 230 | 180 |
| Aserai | 220 | 230 | 250 | 240 | 200 |
| Vlandia | 170 | 220 | 120 | 230 | 180 |
| Battania | 200 | 170 | 240 | 90 | 180 |
| Khuzait | 230 | 220 | 220 | 230 | 220 |
| **Median** | **225** | **225** | **220** | **230** | **180** |

The rulers' median Leadership is **220**.

Clan heads as a whole sit lower. Across the 67 templated heads, rulers included, the medians
are Charm 140, Leadership 160, Steward 120, Trade 60, Roguery 70, Scouting 160.

**The design consequence:** AI rulers start near the top of the scale. A player who founds or
inherits a kingdom is almost always the least skilled ruler on the map at first, and the only
skilled help available is the player's own family and companions. §4 and D3 are about exactly
this.

### 1.5 Traits, for completeness

`DefaultTraits` holds the personality traits (Honor, Mercy, Valor, Generosity, Calculating) and
three political ones: Authoritarian, Oligarchic, Egalitarian. Vanilla reads the political three
in policy votes, king selection and a clan's policy support. It reads Honor and Calculating in
its own AI diplomacy: `DefaultAllianceModel`, and
`DefaultDiplomacyModel.GetPersonalityEffects` and `UpdateOurBenefitMinusOurRiskBasedOnEvaluatingFaction`.
Traits are not skills and are outside this plan's core. §9 sketches them as a later stage.

---

## 2. What the takeover already took from the skill system

Found while writing this plan. Each item says how it was established.

| # | Finding | Evidence | Fix |
|---|---|---|---|
| 1 | **A war costs the player a different price from the AI.** The player's *Declare war* button proposes a vanilla `DeclareWarDecision`, which charges `DiplomacyModel.GetInfluenceCostOfProposingWar`: **200** influence, **400** under the War Tax policy when the proposer rules, and −25% with Firebrand. An AI ruler pays the mod's `AiDiplomacy.WarDeclarationCost`: 40 × (2 − legitimacy) × (1 + weariness / 100), which comes to **52–100** typically, and no perk ever applies. This breaks the standing rule that the AI plays by the same rules, and it contradicts PLAYER-GUIDE §2 ("the base is 40 influence…") | IL of `DeclareWarDecision.GetProposalInfluenceCost` and `DefaultDiplomacyModel.GetInfluenceCostOfProposingWar`. `DiplomacyMenu.DeclareWar` calls `AddDecision(decision, false)`. `ModDiplomacyModel` does not override the cost | S0, A-1 |
| 2 | **Firebrand lost its main diplomatic use.** Vanilla alliances and trade agreements were `KingdomDecision`s, so Firebrand discounted proposing them. The mod's treaties cost a flat 60 / 100 / 180, and its AI war cost reads no perk | Callers of `Charm.get_Firebrand`: only `GetPerkEffectsOnKingdomDecisionInfluenceCost` and the volunteer model | S0, A-2 |
| 3 | **Barter perks no longer touch peace**, because peace is no longer bartered (design 05) | `EffortForThePeople` and `SlickNegotiator` are read only in `DefaultBarterModel.GetBarterPenalty` | Accepted. S1's negotiation term gives Charm its place at the peace table instead |
| 4 | **The AI's diplomacy lost its personality.** Vanilla weighed Honor and Calculating when valuing alliances, war and peace. The mod's evaluation reads no trait | Callers of `DefaultTraits.get_Honor` and `get_Calculating` in `DefaultAllianceModel` and `DefaultDiplomacyModel` | Later: S5, D8 |
| 5 | **Silver Tongue never reaches the mod's own defection deal.** Buying a house mid-civil-war is exactly "persuading lords to defect for gold", and the price ignores the perk | Callers of `Trade.get_SilverTongue`: `JoinKingdomAsClanBarterable`, the price model and the villagers | S0, A-3 |

---

## 3. Design rules

1. **Only the political six, and only in the mod's own judgments.** The engine already reads
   Tactics, Engineering, Medicine and the combat skills in battles and sieges. War score,
   casualties and captures reach this mod after they have done so, so a skill term on any of
   those counts the skill twice.
2. **One skill per question.** Each question the mod asks is answered by one skill, so a player
   can tell which skill to raise. A covert contest pairs an attacking skill with a defending
   one, the way a battle pairs two commanders.
3. **The hero who acts is the hero whose skill counts.** Leadership belongs to the ruler, since
   a king cannot delegate being followed. So does anything a hero does in person: standing for
   a throne, leading a bloc. The rest is delegable: the acting house's best hero in that skill,
   family or companion (§4.1). A house bargaining for itself, such as one being bought in a
   civil war, is represented the same way.
4. **Contests where two sides meet, levels where nobody does.** A contest compares the two
   heroes' skills directly. A level compares one hero with the median of the same role across
   every living realm. Both are zero for a typical ruler, so the AI world's balance does not
   move merely because this layer exists (§4.2).
5. **No skill term decides alone.** This is the project's standing rule for terms (the
   submission threat cap, the war-value strength cap). An additive term is at most about a
   third of the threshold it feeds. A multiplier on an outcome stays within ±15%. One on a
   rate, a chance or a ranking may reach ±50%, because it changes how fast or who, not how
   much.
6. **Shown where the AI's number is shown.** Every term is a named line in a breakdown that
   already exists, carrying the hero's name, the skill and its value: *"Our envoy, <hero>
   (Charm 145; the realms' median 225): −5.3"*. The formulas take no "is this the player"
   argument.
7. **Doing trains the skill.** The acting hero gets XP through `Hero.AddSkillXp`, on vanilla's
   scale and through vanilla's learning rate. AI heroes earn it too (§6).
8. **Derived, never saved.** Every term reads live hero data. No save id is spent before
   offices (S4, optional).
9. **Repair before adding.** Fix what the takeover broke (§2) before building anything new.

---

## 4. Who acts, and how a skill becomes a number

### 4.1 Portfolios and actors

| Portfolio | Skill | Actor for a realm (S1) | Answers |
|---|---|---|---|
| Resolve and authority | Leadership | The ruler (`Kingdom.Leader`) | War exhaustion rate, Hold, court presence |
| Persuasion | Charm | **Envoy**: the ruling house's best | Pacts, the peace table, and (house-level) backing and bloc voice |
| Recovery | Steward | **Steward**: the ruling house's best | Grievance fade, legitimacy dividend |
| Bargaining | Trade | **Treasurer**: the ruling house's best | Gold prices |
| Subterfuge | Roguery | **Spymaster**: the ruling house's best | Fabrication |
| Vigilance | Scouting | **Watch**: the ruling house's best | Catching fabrication |

**"The house's best"** is the highest skill among the clan's heroes who are alive, not
children, not prisoners and not disabled, drawn from `Clan.AliveLords` and `Clan.Companions`.
If nobody qualifies, it falls back to the clan leader. One hero may fill several portfolios.

**Personal acts** read the hero's own skill, never the house's best: a claimant's Charm when
standing for the throne (S-8), a bloc member's Charm when the bloc picks who speaks for it
(S-9), and every leader's Leadership.

This is the cheapest rule that gives companions and family a political job. It is automatic,
needs no UI to assign, and saves nothing. S4 (offices, optional) would replace it with
appointments, and changes nothing downstream, because every formula asks
`Statecraft.Actor(kingdom, portfolio)` rather than finding the hero itself.

A captured envoy makes the next best hero speak for the realm. That is a real lever: taking the
enemy's chancellor prisoner weakens their diplomacy. The Realm tab says so (§10).

### 4.2 Level and Contest

```
Level(hero, skill)   = clamp((skill − Pivot(portfolio)) / 150, −1, +1)
Contest(a, b)        = clamp(Level(a) − Level(b), −1, +1)
Pivot(portfolio)     = median of that portfolio's actor across every living realm, derived daily
```

**Why a peer median rather than a fixed number.**

- It centres the AI world by construction. The median realm scores 0, so the absolute terms add
  no net push to a world runs 01–07 calibrated.
- It absorbs drift. AI heroes gain XP for decades (§6), and so do mods that change skills. A
  fixed pivot would let that inflate every effect over a long campaign.
- It is the honest statement: *"your envoy against the envoys of your time."*

The cost is that a Level can move when somebody else's changes. The diagnostics and the UI line
therefore print the median beside the skill. The fallback when no peer exists is 200.

**Why span 150.** Every 15 points of skill away from the median moves the Level by 0.1. With
the medians of §1.4 near 220:

| Skill | 50 | 100 | 150 | 200 | 220 | 250 | 300 | 330 |
|---|---|---|---|---|---|---|---|---|
| Level | −1.00 | −0.80 | −0.47 | −0.13 | 0 | +0.20 | +0.53 | +0.73 |

The scale is lopsided because the world is. AI rulers sit near the top, so a player can fall
all the way to −1 but can only climb to about +0.7 above them. D3 asks whether low skill should
bite as hard as this.

**The memo trap.** The pivot is cheap enough to compute on demand: eight realms times a
house's heroes. If a memo is ever added, it must be keyed the way `BlocModel`'s is (day plus a
generation counter bumped by XP grants, deaths, captures and successions), never on the day
alone. A day-keyed cache lies under `diplomacy.tick_days` (CLAUDE.md §1).

---

## 5. The formulas, today and proposed

`L(x)` is the Level of the portfolio's actor. `C(a, b)` is a Contest. All weights UN-TUNED.

| ID | System | Today (code) | Proposed | Range | Stage |
|---|---|---|---|---|---|
| **A-1** | War cost | Player: vanilla 200 (400 under War Tax, −25% with Firebrand). AI: 40 × (2 − L) × (1 + weariness / 100) | **One price**: both pay `WarDeclarationCost` | — | S0 |
| **A-2** | Influence costs | Treaties a flat 60 / 100 / 180. AI war cost reads no perk | × 0.75 when the proposing ruler holds **Firebrand**: wars and treaties, for everyone | −25% | S0 |
| **A-3** | Side-change price | Ignores perks | × 0.85 when the buyer holds **Silver Tongue** | −15% | S0 |
| **S-1** | War exhaustion | Every accrual (0.30/day of war, casualties / (strength / 20), town 6, castle 3, village 1, siege 0.15/day, occupied fief 0.02/day) × `WarExhaustionRate` | Every accrual × **(1 − 0.15 · L(ruler, Leadership))**. Internal wars: each side by its leader's (the ruler's, the claimant's) | ×0.85 to ×1.15 | S1 |
| **S-2** | Peace table | `BudgetFor` = war score (0 at or below 20). `WouldAccept` tolerance = raw score × 1.25 | Budget = score × **(1 + 0.15 · C(winner's envoy, loser's envoy))**, and the tolerance reads the same budget | ×0.85 to ×1.15 | S1 |
| **S-3** | Pact value | 60·threat + 40·proximity + 30·trust − 50·aggression + 25·relation + 40·balancing − 20·ambition. NAP 35, defensive 55, alliance 70; the lower side decides | The side **being asked** adds **+10 · L(proposer's envoy, Charm)** | ±10 | S1 |
| **S-4** | Hold target | 40 + fear 25 + protection 20 + trust 15 − tribute 20 − wars 15 − rival 25 − culture 10 − dread 25 | **+10 · L(patron's ruler, Leadership)**, "authority" | ±10 | S1 |
| **S-5** | Fabrication | Exposed on a 20% roll | Exposure = 0.20 × **(1 − 0.5 · C(fabricator's spymaster, target's watch))** | 10–30% | S1 |
| **S-6** | Grievance fade, legitimacy dividend | Grievances −0.02/day flat. +3 legitimacy per full year of peace | Both × **(1 + 0.5 · L(crown's steward, Steward))** | 0.01–0.03/day; 1.5–4.5/yr | S2 |
| **S-7** | Loyalty | 50 + relation/2 − 1.5·grievances + 10·fiefs − 0.2·worst exhaustion + 0.2·(legitimacy − 50) | **+5 · L(ruler, Leadership)**, "the crown's presence" | ±5 | S2 |
| **S-8** | Succession backing | Each house backs the claimant with the best score: relation (+100 for its own claimant; the incumbent adds loyalty × 0.5) | Each claimant's score **+15 · L(claimant, Charm)** | ±15 relation-equivalents | S2 |
| **S-9** | Bloc leader | The most influential member | The largest **influence × (1 + 0.5 · L(head, Charm))**. Bloc power is unchanged | ×0.5 to ×1.5 on the ranking only | S2 |
| **S-10** | Side-change price | (2,000 + 15·strength + 4,000/town + 2,000/castle) × relation × bond × momentum | × **(1 − 0.15 · C(buyer's treasurer, the house's treasurer))**, then A-3 | ×0.85 to ×1.15 | S2 |

### 5.1 Notes, with worked numbers

Medians are §1.4's template figures. S0 replaces them with measured ones.

**A-1: one price for a war.** The player's route charges `AiDiplomacy.WarDeclarationCost` and
adds the decision with the influence cost already paid. Any other vanilla route that proposes
the same decision is priced by `GetInfluenceCostOfProposingWar(Clan)`. That call has no target
argument, so it cannot know the casus belli. S0 finds every such route and settles it: charge
it the conquest-grade price, or send it through the same button. A-1 **lowers the player's
price by half or more.** It is a fairness fix under the standing rule, and D4 asks the lead to
confirm it.

**S-1: resolve.** One helper, `WarExhaustion.Resolve(kingdom)`, is applied at every accrual
site, so no source escapes it. A ruler with Leadership 100 (L −0.80) tires ×1.12 as fast: a war
their realm would feel at exhaustion 60 in 90 days reaches it in about 80. At Leadership 300
(+0.53) the rate is ×0.92, about 98 days. The enemy's band is untouched: it reads the value, not
the rate.

**S-2: negotiation.** This is where a player feels Charm most. War score 80, winner Vlandia:

- AI against AI: Vlandia's envoy (Charm 170, L −0.37) faces Battania's (200, L −0.17). The
  contest is −0.20, so the budget is ×0.97 = **77.6**, still past the subjugation cliff at 75.
- The player winning, with an envoy at Charm 100 (L −0.83), against the same Battania: the
  contest is −0.67, so the budget is ×0.90 = **72.0**. Subjugation is off the table, and the
  winner settles for tribute. *The envoy's Charm cost a vassal.*
- An envoy at Charm 300 (L +0.50): a war score of **70** becomes a budget of 77. Subjugation
  becomes reachable.

The §13 cliff therefore moves with the envoys, anywhere from a war score of about 65 to 88. The
white-peace floor (score ≤ 20) stays on the raw score, so a stalemate stays a stalemate.

**S-3: persuasion.** Only the side being asked is persuaded, because the proposer already wants
the pact. In the AI's weekly pact scan that is `theirValue`. For the player it is the "what
their court would sign" chooser. An envoy at Charm 120 (L −0.70) puts −7 on the other court's
value, so an alliance they valued at 74 becomes 67: they would sign a defensive pact instead.
The cap of 10 is under the smallest threshold (35), by rule 5.

**S-4: authority.** A patron ruler with Leadership 100 puts −8 on every link's Hold target. A
link that would have settled at 45 settles at 37, in the passive-resistance band: it withholds
tribute. That is where a weak king's hegemony starts to leak.

**S-5: subterfuge.** The player's spymaster at Roguery 60 (L −1) against a watch at the median
(L 0) is exposed **30%** of the time. A companion spymaster at Roguery 250 brings it to about
**19%**. The chance is shown before the gold is paid. The AI never fabricates today (only the
player's menu and the console do), so in practice this is the player's term, and the AI's watch
is the defence.

**S-6: recovery.** Grievances are held against a house (the crown when they were taken), so
their fade reads that house's steward. At Steward 100 (L −0.83) the fade is ×0.58: a weight-8
grievance lasts **685 days** instead of 400. At Steward 300 it is ×1.25, 320 days. The dividend
is 1.75 or 3.75 a year. `TributeCourt.Assess` inherits all of this through `LoyaltyModel`, with
no change of its own. This is the lever against the downward spiral found after 2.6: **a
realm with a good steward heals faster.**

**S-7: presence.** A ruler with Leadership 100 costs the whole court −4 loyalty; one with 300
adds +2.7. It is kept small because relation already carries vanilla's Charm (§1.1), and
because loyalty's bands are 15 points apart: 5 moves a house at the edge of a band, never a
whole court.

**S-8: backing.** The term compares claimants against each other, so the pivot cancels out. A
claimant at Charm 240 (+0.10) against an incumbent at 140 (−0.57) gains a **10-point**
relation swing in every house's comparison. A charismatic pretender splits a court more
readily. That feeds the 30% pretender line and, through it, the Pretenders bloc and 2.6.

**S-9: bloc voice.** Bloc power stays the sum of influence, so the civil-war trigger (bloc
≥ 40% of the court) does not move. Only *who speaks* for a bloc changes, and its members follow
that leader's preference (2.3b). Take member A with influence 300 and Charm 100 (factor 0.58,
score 175) against member B with influence 200 and Charm 240 (1.05, 210): B leads. **A
charming vassal, the player included, can lead a bloc it does not bankroll.**

**S-10: bargaining.** A typical house costs 10,800. The buyer's treasurer is at Trade 240
(+0.13) and the house's best at Trade 60 (L −1). The contest is +1, so the price is ×0.85 =
**9,180**. With Silver Tongue it is ×0.85 again, **7,800**. The column in the civil-war panel
gains these two lines. The AI's "strength per denar" choice reads the same quote, so it inherits
the change.

### 5.2 Considered and left out

| Candidate | Why not |
|---|---|
| Tactics in war value, or Tactics, Medicine and Engineering anywhere | The engine reads them in battles and sieges, and war score, casualties and captures reach us after that. Rule 1 |
| Combat skills, attributes directly | Attributes act through skills (the learning rate), and combat skills through battles. Either would be counted twice |
| Charm softening trust loss or legitimacy loss | Both are verdicts on deeds: trust is reputation (design 01 §4.1), and legitimacy prices acts the same for every crown. Honor (S5) is the better fit for trust |
| Scouting revealing enemy exhaustion | The exact figure is what Phase 3's `ReadCourt` sells (decision 01 §8) |
| Steward on weariness decay; Trade on tribute size and indemnity | Plausible, but each moves a calibrated war or peace rate, or an open question the lead holds (the indemnity price, design 04 §13.4). They are candidates after S3 measures the first ten terms |
| Leadership on call-to-arms gates; Charm on poaching | Niche; after S3 |

---

## 6. Doing trains the skill

The hero who performed the act gets the XP. Amounts are raw, before the learning rate. They are
sized against vanilla's grants (§1.3) and against one target: **an active player ruler gains
2–4 levels a year in a portfolio's skill around 100–150.** All are UN-TUNED.

| Act | Skill | Hero | Raw XP | Levels at 100 / 150 / 200 (learning rate 1) | Stage |
|---|---|---|---|---|---|
| Pact signed: non-aggression / defensive / alliance | Charm | the proposer's envoy | 2,000 / 3,000 / 5,000 | alliance: 0.8 / 0.4 / 0.2 | S1 |
| Peace signed at the table (not dormant) | Charm | both envoys | 2,000 + 100 × the package's cost | subjugation (75): 9,500 → 1.5 / 0.7 / 0.4 | S1 |
| Tribute demand accepted | Charm | the demander's envoy | 3,000 | 0.5 / 0.2 / 0.1 | S1 |
| A kingdom submits, by any route | Leadership | the patron's ruler | 8,000 | 1.3 / 0.6 / 0.4 | S1 |
| A war ends (not dormant, not by elimination) | Leadership | each side's ruler | 50 × days at war, at most 8,000 | 90 days: 4,500 → 0.7 / 0.3 / 0.2 | S1 |
| A claim fabricated / exposed | Roguery | the spymaster | 5,000 / 2,000 | 0.8 / 0.4 / 0.2 | S1 |
| A fabrication against the realm exposed | Scouting | the watch | 3,000 | 0.5 / 0.2 / 0.1 | S1 |
| An internal war won | Leadership | the winning leader | 10,000 | 1.6 / 0.8 / 0.4 | S2 |
| A year of peace pays the dividend | Steward | the crown's steward | 5,000 | 0.8 / 0.4 / 0.2 | S2 |
| Tribute paid or received | Trade | each treasurer | 0.5 × the payment | 500: 250 | S2 |
| A house bought / paid in a civil war | Trade | the buyer's treasurer / the house's head | 0.1 × / 0.05 × the price (vanilla's bribe rate) | 10,800: 1,080 | S2 |

**Why AI XP is harmless.** The curve does the balancing. The same 5,000 XP is almost a level
for a player at 100 and a fifth of one for an AI envoy at 220. AI skills drift up slowly, and
the peer median absorbs the drift. What the pivot cannot absorb is vanilla's own per-point
effects: more Steward is bigger parties, more Leadership is higher morale. At these amounts that
is about a level a year for a busy AI ruler. S3 measures it.

Each grant writes `[EVENT] skill_xp hero= skill= xp= act=`, so a run can total it.

---

## 7. Perks

- **Restore (S0).** Firebrand (A-2) and Silver Tongue (A-3). Each is vanilla's own text applied
  to the act the mod took over, so the tooltip already tells the truth.
- **Leave alone.** Everything that still works (§1.2).
- **Extend (optional, D5).** Some perks name exactly what this mod added. *Forgivable
  Grievances* could make grievances against its holder fade faster, *Oratory* could add to
  persuasion, *Presence* to presence. The catch: vanilla's tooltip would not say so. Only our
  breakdown lines would, and a player picking perks on the character screen would not know.
  Recommended only if the lead accepts that, and after S3.

---

## 8. Offices (S4, optional)

The automatic "house's best" rule gives companions a political job with no UI. Offices would
make it a choice, and would also answer the gap raised on 2026-09-25, that a king has few verbs
to manage his court.

- **Seats**: Chancellor (Charm), Steward (Steward), Treasurer (Trade), Spymaster (Roguery and
  Scouting). Leadership stays the ruler's own.
- **Holders**: any adult hero of the realm, the ruling house's family and companions or **a
  member of another sworn house**.
- **Patronage**: a house given a seat gains loyalty and a Centralist pull. That is design 02
  §3's "benefits from crown patronage", the missing half of why **Centralists never form**
  (STATUS 2.3). Land-hungry, influential houses passed over take a small grievance, a new
  `GrievanceType` value. Dismissing a holder is a grievance for their house.
- **The AI** appoints by rule: skill first, and patronage when its court's loyalty sinks. That
  gives it a way back from a court in decline.
- **The player as a vassal** can be appointed by an AI king. Serving a king becomes a political
  position with a desk, not only a vote.
- **Cost**: size L, covering a Court-tab panel, an AI rule and save data: `CourtOffice` at
  class id **15**, `ModState` property **15**, and a `CourtSeat` enum at **28**, each with its
  container definition (CLAUDE.md §3).

## 9. Traits (S5, optional)

Personality in the AI's diplomacy, restoring what finding 4 lost: Honor in treaty-keeping and
in the trust given and received, Valor in the war value, Calculating in the pact and peace
values, Mercy in captivity, Generosity in presence. It needs its own plan and its own balance
run, and it would read live traits, with no save data. It is recorded here so that S1–S2 do not
paint over the slots it would use.

---

## 10. Where the player sees it

Every surface already exists. This plan adds lines, not screens.

| Surface | What it gains | Stage |
|---|---|---|
| **Realm tab** | A *Statecraft* strip: the six actors, with hero, skill, the realms' median, and what each one moves. A captured actor shows who stands in | S1 |
| Diplomacy tab, the pact chooser | "Our envoy's persuasion (Charm 120, median 225): −7.0" | S1 |
| Peace-table popup | "Negotiation: our envoy (Charm 100) against theirs (Charm 200): budget ×0.90 (80 → 72)" | S1 |
| War rows (own side) | "Resolve (Leadership 100): exhaustion ×1.12" | S1 |
| Realm tab, vassal Hold terms | "authority −8.0" | S1 |
| Fabrication prompt | "Chance of exposure 30% (Roguery 60 against their Scouting 180)", before paying | S1 |
| **Court tab** | Loyalty WHY gains "the crown's presence". The ledger header shows the fade rate with the steward, and the legitimacy note the dividend | S2 |
| Civil-war price column | "Haggling (Trade 240 against 60): −1,620", "Silver Tongue: −1,380" | S0/S2 |
| Succession footer | Each claimant's Charm term | S2 |
| Encyclopedia rival court | Nothing new. It stays bands only | — |
| PLAYER-GUIDE | A new section, "Your skills in politics" | S1/S2 |

XP uses vanilla's own notifications (`AddSkillXp(…, shouldNotify: true)`).

---

## 11. The work, stage by stage

Each stage is one branch, merged only when its exit criteria hold.

### S0 — Groundwork and repairs (M)

| Task | Where |
|---|---|
| `Statecraft`: actors, `Level`, `Contest`, pivots, and the line text every UI reads. One constants file | new `Statecraft/Statecraft.cs`, `Statecraft/StatecraftConstants.cs` |
| `SkillXp`: grants and their telemetry | new `Statecraft/SkillXp.cs` |
| Settings toggle **`EnableStatecraft`** (default on). Off, every term is neutral (Level 0, factor 1, no XP), so an A/B comparison is one switch | `Core/ModSettings.cs` |
| `diplomacy.statecraft [kingdom]`: actors, skills, medians, Levels, and each term they would feed. Test levers `test_set_skill <hero> \| <skill> \| <value>` and `test_add_perk <hero> \| <perk>` | `Core/DebugCommands.cs` |
| `[KINGDOM]` gains the actors' skills; `[CONFIG]` prints `StatecraftConstants` | `Core/Telemetry.cs` |
| A-1, A-2, A-3 | `UI/DiplomacyMenu.cs`, `Diplomacy/AiDiplomacy.cs`, `Intrigue/SideChange.cs` |
| Measure the medians on `di_fresh_1084` (the start) and `di_run07_1104` (20 years on), and replace §1.4's template figures with them | this document |

**Exit criteria:**

- With `EnableStatecraft` **off**, `pact_value`, `war_value`, `peace_allowance`, `hegemony`,
  `loyalty` and `civil_war_prices` print exactly what they print today on the same save. This is
  the regression proof.
- The player's *Declare war* charges the figure `war_value` prints.
- A ruler given Firebrand pays 75%.
- LoadProbe is clean, and the logs show 0 errors.

### S1 — Diplomacy: S-1 to S-5, their XP, the Realm-tab strip (M)

`Diplomacy/WarExhaustion.cs`, `Intrigue/InternalWars.cs`, `Diplomacy/PeaceTable.cs`,
`Diplomacy/AiDiplomacy.cs`, `Diplomacy/Hegemony.cs`, `Diplomacy/ClaimRegistry.cs`, and the UI
files in §10.

**Verification**, in the project's usual way: predict by hand, then read the value off the
game. On a player-ruled test save (`test_player_rule`), set the envoy's Charm to 50, 150 and
300 with `test_set_skill`, and read `pact_value`, `peace_allowance` and `hegemony` at each
value. Each difference must equal weight × ΔLevel. `tick_days 10` on a war must show the
accrual ratio of S-1. Sign a pact and read the envoy's XP before and after. Screenshot the
Realm strip. 0 errors.

### S2 — Court: S-6 to S-10, their XP, the Court-tab lines (M)

`Intrigue/GrievanceRegistry.cs`, `Intrigue/LegitimacyRegistry.cs`, `Intrigue/LoyaltyModel.cs`,
`Intrigue/SuccessionModel.cs`, `Intrigue/CourtBloc.cs`, `Intrigue/SideChange.cs`,
`UI/KingdomScreen/CourtVM.cs` and `CivilWarVM.cs`.

**Verification:** as S1, on `di_pretender_test` and `di_civilwar_2_6c`. Grievance fade is
checked with `tick_days`, which runs the decay. The legitimacy dividend is **dated**, so the
frozen clock cannot show it. It is checked at speed with the real clock, as 2.5 was.

### S3 — Measure and tune (M, mostly wall clock)

Folded into the owed **run 08**, from `di_fresh_1084`: ten in-game years with `EnableStatecraft`
on, and a control of ten with it off. At speed 50 that is about 25 minutes each (CLAUDE.md §1).
§12 lists what it must show.

### S4 — Offices (L, optional, D7). S5 — Traits (L, optional, D8, its own plan)

### Documentation, alongside each stage

PLAYER-GUIDE §"Your skills in politics". Design 03 (Phase 3) re-expressed in `Level`/`Contest`
with the spymaster as the default handler: its raw terms (`agentRoguery / 200`,
`0.004 × (roguery + charm) / 2`) predate this layer. A row in CLAUDE.md §6. STATUS and ROADMAP.

**Who builds it (CLAUDE.md §7).** S0 by Claude: it sets the resolver every later stage reads,
and A-1 touches the player's war path. S1 and S2 are briefed to opencode, one branch each, and
reviewed against §3's rules before merge. The game is shared by both clones, so S3 runs only
when no delegated task is testing in game.

---

## 12. Measuring it

**What S3 must show:**

1. **The world did not move.** With `EnableStatecraft` on and off, these stay within ±15% of
   each other: wars per year, median war length, the share of peace by subjugation, tribute and
   white peace, internal wars started, and houses changing sides.
2. **Each term points the right way, across realms.** Kingdoms whose rulers score higher
   Leadership accrue exhaustion more slowly. Better envoys win bigger budgets and more pacts.
3. **AI skill drift** over ten years, per portfolio, and the medians' path.
4. **A player's year.** On a player-ruled save, a scripted year of normal statecraft (two pacts,
   one peace, a fabrication) and the XP it paid. The target is 2–4 levels at 100–150.

**Proposed acceptance, for the lead to confirm:** every one of the six skills moves at least one
number on a screen the player uses, naming the hero; recruiting or losing a specialist changes
it the same day; points 1–4 above hold; 0 errors; and no save data changes before S4.

---

## 13. Save data, Harmony, compatibility

- **Save data: none through S3.** Every term is derived. XP lives in vanilla's own hero data,
  which the game saves. S4 would take class id 15, `ModState` property 15 and enum 28.
- **Harmony: none.** Everything is in the mod's own resolvers. A-1's Decisions-tab route may
  need `ModDiplomacyModel.GetInfluenceCostOfProposingWar`, which is a model override, not a
  patch.
- **Other mods that change skills** are absorbed by the peer median.

## 14. Risks

| Risk | Handling |
|---|---|
| **The upstart penalty.** A new player king is usually far below AI rulers. At Leadership and Steward 100 with a weak envoy: exhaustion ×1.12, loyalty −4, grievances last 70% longer, Hold −8, budgets −10%, pacts −7 | Companions cover every delegable portfolio, which is the gameplay this plan wants. D3 offers halved penalties. S3 measures a player-ruler year |
| Charm counted twice through relation | The Charm terms are small (§1.1), and loyalty gets Leadership rather than Charm |
| **Civil-war tilt.** Claimants are clan heads, below rulers in Leadership (median 160 against 220), so risings tire faster. Crown treasurers out-bargain claimants | The mechanism is honest, but it moves outcomes 2.6 measured once. S3 compares the rebel win rate on and off |
| **The subjugation cliff moves** with the envoys (score ~65 to ~88) | Intended, since that is where Charm is felt. Watched by §12 point 1's subjugation share |
| Vanilla side effects of AI XP (party size, morale) | Small at these amounts. §12 point 3 |
| Too many lines in breakdowns | One line per term, always the same format (rule 6) |
| A day-keyed memo lying under `tick_days` | §4.2's memo rule |

---

## 15. Decisions for the lead

| # | Question | Options | Recommended |
|---|---|---|---|
| D1 | Whose skill speaks for a realm in S1? | (a) The ruler for everything. (b) The ruler for Leadership, **the house's best for the other five**. (c) Appointed offices now | **(b)**. Offices later (D7) change one resolver |
| D2 | What is a skill measured against? | **The median of the same role across living realms, derived daily**, or fixed constants set in S0 | **Peer median**: balance-neutral and drift-proof |
| D3 | How hard should low skill bite? | Symmetric, as in §4.2, or penalties at half strength | **Symmetric.** It is honest, companions soften it, and S3 checks the player's year |
| D4 | A-1: one price for a war? | **The player pays what the AI pays** (40 × (2 − legitimacy) × (1 + weariness / 100), −25% with Firebrand), or the player keeps vanilla's 200 | **One price.** The standing rule is that the AI plays by the same rules |
| D5 | Perks? | **Restore what the takeover broke** (Firebrand, Silver Tongue), or also give political effects to perks that name them (Forgivable Grievances…), shown only in our UI | **Restore only**; revisit after S3 |
| D6 | Do the mod's acts train skills? | Yes, sized to 2–4 levels a year for an active ruler at 100–150; or no | **Yes** |
| D7 | Offices (S4)? | After S3 / never / now | **After S3** |
| D8 | Traits: personality in the AI's diplomacy (S5)? | Its own plan after S3 / never | **Its own plan after S3** |
| D9 | `EnableStatecraft` toggle | Default on / off / no toggle | **Default on.** It is also how S3's control is run |
| D10 | Where in the roadmap? | As Phase 2.8, before Phase 3; or deferred behind the civil-war gaps and run 08 | **Phase 2.8, with S3 folded into run 08.** Phase 3's spec then starts on this layer instead of raw skill terms |

---

## Appendix A — Perks with a political reading, v1.4.8 (a selection)

From the IL of `DefaultPerks.InitializeAll`, tier in skill points. The primary effect is shown,
and the secondary one where it matters. No perk in any skill uses the `Ruler` role.

| Skill | Tier | Perks (the alternatives at that tier) |
|---|---|---|
| Charm | 25 | Virile · Self Promoter |
| | 50 | Oratory (renown and influence per issue resolved) · Warlord (+30% influence from battles) |
| | 75 | **Forgivable Grievances** (fewer critical persuasion failures; mends bad relations) · Meaningful Favors |
| | 100 | In Bloom · Young And Respectful (relation gain by gender) |
| | 125 | **Firebrand** (−25% influence to initiate kingdom decisions) · Flexible Ethics (−30% influence to vote for others' proposals) |
| | 150 | Effort For The People · Slick Negotiator (barter penalty) |
| | 175 | Good Natured · Tribute |
| | 200 | Moral Leader · Natural Leader (persuasion) |
| | 225 | Public Speaker · Parade |
| | 250 | Camaraderie |
| | 275 | Immortal Charm (+5 influence a day) |
| Leadership | 75 | Authority · Heroic Leader (+1 daily loyalty in a governed town) |
| | 125 | Presence (+5 security a day while waiting in a town) · Leader of the Masses |
| | 175 | Inspiring Leader (−20% influence to call an army) · Uplifting Spirit |
| | 250 | We Pledge our Swords · Talent Magnet (+1 clan party limit) |
| | 275 | Ultimate Leader |
| Steward | 125 | Giving Hands · Logistician (secondary: +10% tax in a governed town) |
| | 275 | Price of Loyalty |
| Trade | 250 | **Silver Tongue** (−15% gold to persuade lords to defect) · Spring of Gold |
| | 275 | Man of Means · Trickle Down |
| | 300 | Everything Has a Price (settlements can be bartered) |
| Roguery | 125 | Scarface · White Lies (faster crime rating decay) |
| Scouting | 200 | Village Network · Rumor Network |
| Tactics | 150 | On The March · Call To Arms (−15% influence to call an army) |
