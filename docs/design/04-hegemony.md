# Design 04 — Hegemony: the Emperor and the Khagan

Phase 2.8. Specced, no code. This document records the shape and the decisions still open;
nothing here is built.

---

## 0. What this is for

The project lead's idea: one faction declares itself greater than all the others — an
**Emperor** over kingdoms that have submitted, or a **Khagan** over khans who have bent the
knee in a nomadic empire.

Phase 1 already built the bottom of that ladder. A tributary buys peace with money; a vassal
buys protection with money, troops and its foreign policy. Hegemony is the top rung: the
same subordination, but declared as a **title** rather than a private arrangement, held
against the whole map, and carrying the political weight that comes with being called Emperor.

What it is *not*: an annexation. Members keep their ruler, their succession, their fiefs,
their policies and their court. That is what leaves them able to resent, scheme, and revolt.

## 1. What the engine allows, and what hegemony therefore has to be

Verified against v1.4.8, not inferred:

- The faction model has exactly two tiers, `Clan` → `Kingdom`. `IFaction` exposes `IsClan`,
  `IsKingdomFaction`, `IsMinorFaction` and `MapFaction`. **There is no parent-of-kingdom
  slot**, and war/peace is a flat `StanceLink` between factions.
- So a hegemony cannot be an engine faction, and no amount of Harmony will make it one
  without rewriting the faction graph — which would break sieges, armies, diplomacy screens
  and every save.

Hegemony is therefore **mod state**: a title record held by one kingdom, plus a set of
subordination treaties that all point at the same overlord. The campaign map keeps showing
eight kingdoms. The hegemony is a political fact layered over them.

This is a constraint worth being happy about. It means members remain real polities with
their own internal politics, which is the whole reason the system can be interesting.

## 2. Prerequisite: vassalage has never actually happened

Measured in balance run 02 (13.1 in-game years, 157 weekly snapshots): **`vassalage=0` in
every single week.** Tributary pacts reached 9, alliances 10, and vassalage never once formed.

Reading the code explains it: nothing but `DebugCommands` can create a `Vassalage` treaty.
`PeaceTerms` tops out at `ImposeTributaryPact` and has no vassalage field; `AiDiplomacy`
offers non-aggression pacts, defensive pacts, alliances and tribute demands, and never
proposes subordination.

So the machinery hegemony is built on — `Treaty.SubordinateParty`, `CarriesCallToArms`,
`SubordinatesForeignPolicy`, the call-to-arms path — has never run in a real campaign. It
compiles and it is unit-shaped, but it is unexercised.

**Consequence for ordering:** vassalage needs a route into the game and one measured run
showing it forms, is honoured, and is occasionally broken, *before* hegemony is built on top
of it. The cheapest route is the one already designed: a rung above tributary on the peace
concession ladder. At `PeaceCostTributaryPact = 60` a tributary pact already needs a war
score of 60; vassalage belongs around **90**, i.e. only after a war that was won decisively.

## 3. The title

Two flavours, one machinery:

| | Emperor | Khagan |
|---|---|---|
| Who may claim it | imperial cultures | steppe cultures |
| Members called | subject kingdoms | submitted khans |
| Flavour | a restored empire, legitimacy by descent | a confederation held by the strongest horse |

The difference is founding requirements and text, not two code paths. If a steppe kingdom
conquers its way into imperial lands it claims a Khaganate over them anyway, and the map
reads the way it should.

### 3.1 Founding

Proposed requirements, all of them checkable from existing state:

| Requirement | Value | Why |
|---|---|---|
| Existing vassals | **≥ 2** | a title with no subjects is a boast, not a hegemony |
| Strength share of Calradia | **≥ 25 %** | in run 02 eight kingdoms sat within 6,000–7,100 strength; 25 % means someone actually pulled ahead |
| Crown legitimacy (Phase 2.4) | **≥ 70** | a shaky throne cannot claim to outrank other thrones |
| Influence | **one-time, large** — proposed 300 | the ruling clan spends real political capital |
| Cooldown after losing a title | **3 years** | stops a collapsing hegemon re-proclaiming every spring |

Proclaiming is public and costly by design: every kingdom that is **not** a member takes
**−15 trust** toward the new hegemon, and any kingdom of the same culture family gains a
standing grievance. Declaring yourself Emperor is supposed to make enemies.

## 4. How a kingdom becomes a member

Two routes, one treaty:

- **Coerced.** Lose a war to the hegemon badly enough and submission is the peace term
  (§2: score ≈ 90). The member starts with low trust and an immediate grievance.
- **Voluntary.** A weak kingdom under threat submits for protection. AI valuation, in the
  same shape as the existing treaty valuation so the number the player sees is the number
  the AI used:

```
value =  70 * (threatFromOthers / ownStrength)     // who is about to eat me
       + 40 * hegemonProtectionReach(me)           // can they actually help
       + 30 * weariness(me) / 100                  // I cannot fight another war
       - 60 * (1 - trust(hegemon) / 100)           // I do not trust them
       - 50 * ownLegitimacy / 100                  // a proud throne does not kneel
```

Voluntary members start with neutral trust and no grievance, and that difference should
matter for how long they stay.

## 5. What membership costs and what it buys

Obligations — all of them already have Phase 1 machinery:

| Obligation | Mechanism |
|---|---|
| Troops in the hegemon's wars, offensive included | `CarriesCallToArms`, as vassalage already does |
| No treaties with outsiders | `SubordinatesForeignPolicy` |
| Tribute | existing tribute fields |
| The hegemon's **ruler** may summon parties | the lead's earlier proposal; see §8 |

What it buys is **open question 2**. Three candidates:

- **(a) Nothing. Pure coercion.** Simplest. Also means every member always wants out, and the
  system collapses to a permanent rebellion timer.
- **(b) Protection and a share.** The hegemon is obliged to answer when a member is attacked
  (a call to arms that runs *upward*), and members receive a share of tribute collected from
  outside the hegemony.
- **(c) A seat.** Members vote on hegemony-wide war, which couples straight into the Phase 2
  bloc and voting machinery.

**Recommendation: (b).** It gives a member a reason to stay that is not merely fear, which is
what makes the decision to revolt interesting rather than automatic. (c) is attractive but it
should wait until 2.3 blocs and voting exist and are measured.

## 6. Call-to-arms cascades

This is the mechanical danger, and run 02 gives a number for it: with only alliances and
defensive pacts on the map, **`DefendAlly` was 27 % of all wars** (45 of 167). A hegemony of
five members owing offensive service turns one hegemon war into six.

Proposed caps — **open question 3**:

| Cap | Value |
|---|---|
| Members called per war | ⌈N/2⌉, chosen by proximity to the target |
| Obligation wars per member | **1** at a time |
| Excused when | own exhaustion > 50, or already in an obligation war |

The proximity rule is what makes it read correctly: a Khagan marching west calls the khans
whose grazing lands face west, not the ones three kingdoms away.

## 7. Defiance, resentment, and independence

A member is not a puppet, and defiance is the whole point of the system being worth building:

- **Refusing a call to arms** — not a treaty breach. **−15 trust** with the hegemon and a
  defiance mark; two marks and the subordination lapses at its next expiry. This mirrors the
  Phase 1 alliance rule, deliberately: refusal has to be a real option or hegemony becomes a
  suicide pact.
- **Resentment accrues** through Phase 2.1 grievances: tribute paid, wars fought for someone
  else, parties summoned, fiefs lost while fighting a hegemon's war.
- **Independence war.** A member may renounce its subordination. That is a war with its own
  casus belli, and it is watched: a successful revolt costs the hegemon **−20 legitimacy**
  and raises a contagion counter that lowers the submission value for every other member.
  Empires fall the way they historically fall — one successful defection at a time.

## 8. Summoning vassal parties (the lead's proposal, recorded)

Ruler only. Assessed feasible without touching the Army system: `MobilePartyAi
.SetDoNotMakeNewDecisions(bool)` plus `MobileParty.SetMoveEscortParty(...)` is enough to make
a member's party follow the hegemon's ruler, and both are public in v1.4.8.

Proposed shape: influence cost per summon; a cap of about half the member's parties; a fixed
duration and then a cooldown; the member may refuse at a trust cost, and repeated refusal
breaks the subordination. Sits naturally here as a hegemony obligation rather than a
free-standing feature, and it should land **after** 2.1 grievances, so that refusing has
political weight instead of being a dice roll.

## 9. When the hegemon falls

**Open question 4.** The overlord can be destroyed, lose its capital, or simply be beaten
into a rump. Candidates:

- title goes **vacant**, all subordination treaties dissolve;
- title **passes** to the strongest member, who inherits the subordinations;
- everything **shatters**: treaties break, everyone is at war with everyone.

**Recommendation: vacant, with a grace period.** Treaties end as `Dissolved` rather than
`Broken` — nobody chose this, so nobody should eat a trust penalty — and every ex-member
gets a two-year non-aggression grace. Inheritance is the more dramatic option and is worth
having later as a *claim* on the vacant title rather than an automatic transfer: the
strongest member gets the right to proclaim at reduced cost, and has to actually do it.

## 10. The player

Same rules, same numbers, no exceptions — the standing project decision. A player kingdom may
found a title, be coerced into one, submit voluntarily, refuse a summons, and revolt. The
valuation in §4 is shown to the player as the number the AI used.

## 11. Where this couples to the rest

- **Phase 1** — the `Vassalage` treaty type, the peace concession ladder, trust, call to arms.
  All of it exists; see §2 for the part that is unexercised.
- **Phase 2** — legitimacy gates founding and is what a revolt destroys; grievances are how
  resentment accrues; a hegemon fighting its own civil war is an invitation to revolt.
- **Phase 3** — espionage gives a rival a way to *fund* defiance instead of fighting the
  hegemon directly.

## 12. Implementation order

0. **Vassalage gets a route into the game** and one measured run (§2). Not optional.
1. Title state, founding requirements, proclamation and its trust cost.
2. Submission as a peace term; voluntary submission with the §4 valuation.
3. Obligations: call to arms with the §6 caps, tribute, foreign-policy lock.
4. Defiance marks, grievance accrual, independence wars, contagion.
5. Collapse rules (§9), then UI: a hegemony panel in the Ctrl+D menu showing members,
   tribute, defiance marks and who owes what.

## 13. Open questions for the project lead

1. **One title per flavour, or rival claimants?** One Emperor at a time is cleaner and makes
   the title feel like a prize; allowing two rival Emperors of the same culture creates a
   legitimacy war, which is more interesting and more work.
2. **Does membership buy anything, or is it pure coercion?** §5, three candidates,
   recommendation (b).
3. **Cascade cap shape.** §6 proposes ⌈N/2⌉ by proximity, one obligation war at a time,
   excused above exhaustion 50.
4. **What happens when the hegemon falls?** §9, recommendation: vacant + dissolved treaties +
   two-year grace, with inheritance later as a discounted claim rather than an automatic
   transfer.
