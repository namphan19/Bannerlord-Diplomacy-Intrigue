# Design 01 — Diplomacy

Status: **spec for review**. Numbers here are first-cut defaults meant to be tuned in the
Phase 4 balance pass, not sacred values. Every constant lives in one file
(`Diplomacy/DiplomacyConstants.cs`) so tuning never means hunting through logic.

## 0. The problem we are solving

Vanilla Calradia converges on permanent total war. Kingdoms declare war readily, never
accumulate a reason to stop, and peace is a binary yes/no with no terms. Consequences:
the map churns, the player cannot plan, and there is no political texture.

Three mechanisms fix it, in dependency order:

1. **War exhaustion** gives wars an ending condition.
2. **Casus belli** gives wars a beginning that costs something.
3. **Treaties** give peacetime a shape worth protecting.

Everything else in this document hangs off those three.

---

## 1. War exhaustion

One `WarRecord` per kingdom pair holds two exhaustion values, 0–100, one per side.
Already modelled in `Models/WarRecord.cs`.

### 1.1 Accrual

Applied on the daily tick and on the events listed. All values multiplied by the
`WarExhaustionRate` setting (0.25–4.0, default 1.0).

| Source | Amount | Applies to |
|---|---|---|
| Time at war | **+0.08 / day** | both sides |
| Battle casualties | **`losses / max(1, manpower/100)`** | the side that took them |
| Town lost | **+6.0** | former owner |
| Castle lost | **+3.0** | former owner |
| Village raided | **+1.0** | owner |
| Siege endured (settlement besieged, per day) | **+0.15 / day** | defender |
| Enemy-occupied fiefs held against you | **+0.02 / day per fief** | the side that lost them |

`manpower` = total troops in the kingdom's parties plus garrisons. Dividing by it is what
makes exhaustion **relative**: 500 losses wrecks a 2,000-man kingdom and barely troubles a
15,000-man empire. This is the single most important line in the system — without it, large
kingdoms sue for peace as readily as small ones, which is exactly the vanilla feel we are
replacing.

### 1.2 Decay

Exhaustion does not decay while the war runs — that is the point. On peace the record closes
and its final exhaustion is carried into a per-kingdom **weariness** pool that decays at
**−0.15/day**. Weariness raises the influence cost of declaring a *new* war, so a kingdom
that just fought a long war cannot immediately start another.

### 1.3 Thresholds

| Exhaustion | Effect |
|---|---|
| ≥ 40 | Court pressure begins — doves gain bloc support (Phase 2 hook) |
| ≥ 60 | AI actively seeks peace; accepts white peace |
| ≥ 80 | AI accepts unfavourable terms; fief loyalty penalty **−1.0/day** while at war |
| = 100 | Kingdom must offer peace at the next weekly evaluation; heavy legitimacy loss if refused |

### 1.4 War score

Separate from exhaustion, range **−100..100**, positive = aggressor winning. Exhaustion says
*how tired*; war score says *who is winning*. Peace terms read war score; peace *willingness*
reads exhaustion.

| Event | War score delta |
|---|---|
| Field battle won | `±clamp(6 * (enemyLosses − ownLosses) / max(100, total), 1, 8)` |
| Town captured | **±12** |
| Castle captured | **±6** |
| Village raided | **±1.5** |
| Drift toward zero | **−0.05/day toward 0** |

The drift matters: a stalemate should trend to white peace rather than sit at a stale score
from one battle two years ago.

---

## 2. Casus belli

A war is declared *for* something. `Models/CasusBelliType` and the legitimacy scale in
`Diplomacy/CasusBelli.cs` are already in place.

### 2.1 Legitimacy and what it buys

| Casus belli | Legitimacy |
|---|---|
| DefendAlly | 1.00 |
| BrokenTreaty | 0.95 |
| EspionageExposed | 0.85 |
| AvengeRaid | 0.75 |
| ReclaimAncestralLand | 0.70 |
| SupportClaimant | 0.55 |
| TradeDispute | 0.45 |
| Conquest | 0.20 |
| None | 0.00 |

Legitimacy `L` feeds four places:

1. **Influence cost to declare war** — `base * (2 − L)`. Naked aggression costs double.
2. **Relation with uninvolved kingdoms** — `−round(10 * (1 − L))` with every third-party
   kingdom. Serial aggressors become diplomatically isolated, which is the mechanism that
   punishes warmongering without a hidden hand-of-god modifier.
3. **Internal opposition** — clans whose agenda opposes the war gain a grievance of weight
   `(1 − L) * 8` (Phase 2).
4. **What you may demand at the peace table** — see §4.2.

### 2.2 How a claim is acquired

| Casus belli | Acquired when |
|---|---|
| ReclaimAncestralLand | We held that fief within the last **20 years** (tracked by `FiefHistory`, see §2.3) |
| AvengeRaid | Their party raided our village in the last **60 days** |
| BrokenTreaty | They breached a treaty with us in the last **2 years** (`Treaty.BreachedBy`) |
| DefendAlly | An ally we hold a pact with is under attack right now |
| SupportClaimant | A pretender to their throne is in our kingdom (Phase 2) |
| EspionageExposed | We caught their spy network on our soil (Phase 3) |
| TradeDispute | They embargoed us or seized our caravans |
| Conquest | Always available — this is the fallback, and it is expensive |

Claims **expire after 2 years** unless renewed by a fresh triggering event.

### 2.3 `FiefHistory` — why we need our own record

The base game does not keep a queryable history of who owned a settlement. Without it,
`ReclaimAncestralLand` cannot exist. So we listen to `OnSettlementOwnerChangedEvent` and
record `(settlement, kingdom, fromDate, toDate)`. This ledger is small (a few hundred rows
over a long campaign) and is the backbone of historical claims.

For a campaign started before the mod was installed there is no history, so ancestral claims
simply are not available on day one — an honest limitation, and it heals itself as the
campaign runs.

### 2.4 Fabricating a claim

Player and AI can manufacture a claim on a specific fief:

- Cost: **150 influence + 20,000 denars**, resolves after **30 days**.
- On resolution: **80%** a `ReclaimAncestralLand` claim appears; **20%** it is exposed.
- Exposure: the target gains a `BrokenTreaty`-grade grievance against us, we take
  **−15 legitimacy** and **−5 relation** with every kingdom. Fabrication should feel like a
  gamble, not a purchase.

---

## 3. Treaties

The six types in `Models/TreatyType` become live. All are bilateral, all expire, all can be
broken.

| Type | Duration | Influence cost | Effect |
|---|---|---|---|
| NonAggressionPact | 2 years | 60 | War between parties blocked |
| Truce | 1 year | 0 (part of peace) | War blocked; re-declaring early costs 3× |
| DefensivePact | 3 years | 100 | Call to arms when the partner is **attacked** |
| Alliance | 3 years | 180 | Call to arms in all wars; shared war goals; cannot ally a third party at war with your ally |
| TributaryPact | 2 years | negotiated | Payer sends tribute each **7 days**; war blocked |
| Vassalage | 5 years | negotiated | Tribute + call to arms in **all** the patron's wars + the client cannot declare war or sign treaties with outsiders on its own account |

### 3.0 Tributary versus vassal

These two are the easiest pair to conflate, so the line between them is worth stating:

- A **tributary** buys peace. It pays, and that is the whole of it: its army, its wars and
  its treaties remain its own.
- A **vassal** buys protection. It pays, **owes troops in every one of its patron's wars**,
  and gives up the right to declare war or sign treaties with outsiders on its own account.

Everything else stays with the vassal, and this matters for how the mod feels: its ruler,
its succession, its fiefs, its policies, its clans' loyalties and its internal politics are
untouched. A vassal kingdom is a subordinate state, not an annexed province - which is also
why it can still resent, scheme and eventually defect.

### 3.1 Signing

Both sides must value the treaty positively. AI valuation, roughly:

```
value =  60 * threatSharedWith(other)          // common enemies
       + 40 * tradeExposure(other)             // caravan traffic between us
       + 30 * trust(other) / 100
       - 50 * ownAggressionToward(other)       // do we want their land?
       - 40 * (1 - borderSecurity)             // a long shared border with a weak neighbour is a temptation, not a partner
       + relationOfRulers / 2
```

Player proposals go through the same valuation, so the number the player is shown is the
number the AI actually used. No hidden difficulty modifiers.

### 3.2 Breaking

Breaking an active treaty:

- costs **−35 trust** with the wronged party and **−12 trust** with everyone else,
- hands the wronged party a `BrokenTreaty` casus belli,
- costs **−20 crown legitimacy** (Phase 2),
- for Alliance specifically, refusing a call to arms is **not** a breach — it is
  **−15 trust** and the alliance lapses at its next expiry. Refusal has to be a real option,
  or alliances become suicide pacts.

---

## 4. Diplomatic trust and the peace table

### 4.1 Trust

One value per **ordered** kingdom pair, −100..100, default 0. Ordered because "Vlandia
trusts Battania" and the reverse are different facts.

| Event | Delta |
|---|---|
| Treaty honoured to expiry | +12 |
| Call to arms answered | +20 |
| Call to arms refused | −15 |
| Treaty broken | −35 (victim), −12 (observers) |
| War declared with legitimacy < 0.3 | −10 (all observers) |
| Peace held 2 years | +8 |
| Spy network exposed | −25 (victim) |

Trust gates alliances: below **−20** a kingdom will not sign anything but a truce with you.
This is the durable punishment for treachery — not a relation penalty that decays in a
season, but a reputation that follows the player for the rest of the campaign.

### 4.2 Peace terms

Peace is a package, not a yes/no. What the stronger side may demand is bounded by **war
score** and gated by **casus belli**.

> **Implemented as a point budget, not the tiers below.** Each demand costs war-score points
> and the package must fit inside the winner's war score. It reproduces the intent of the
> table without exclusive-or branches, and adding a demand type is one constant in
> `DiplomacyConstants` rather than a rewritten table. The tiers are kept here as the
> calibration reference they became.
>
> The ladder as built, cheapest first:
>
> | Demand | Cost |
> |---|---|
> | Release prisoners | 5 |
> | Indemnity | 8 per 1000 denars |
> | Castle | 25 |
> | Town | 45 |
> | Tributary pact | 60 |
> | **Submission as a vassal** | **90** |
>
> Submission is the top rung and the one that changes what the loser *is* rather than what it
> owns - it is also what makes the winner a hegemon. See
> [design 04](04-hegemony.md) for everything downstream of it.


| War score (winner's view) | May demand |
|---|---|
| 0–20 | White peace only |
| 20–45 | Tribute, prisoner release |
| 45–70 | One castle **or** tribute + prisoners |
| 70–90 | One town, or two castles |
| 90–100 | Two towns, or a tributary pact |

Gate by casus belli: **fief transfers require a territorial claim** — `Conquest` or
`ReclaimAncestralLand`. A war fought on `AvengeRaid` can extract tribute and prisoners but
not land. This is what makes the choice of casus belli matter beyond a cost modifier.

Acceptance takes **two** signatures, and the second was missing for the project's first
thirteen measured in-game years:

- **The loser signs** when `exhaustion >= 60 - warScoreAgainstThem/2` and the package costs no
  more than `warScoreAgainstThem x 1.25`.
- **The winner signs** when the package is worth at least **half** of what the war earned, or
  it earned nothing, or the winner's own exhaustion has reached 70 and it no longer cares.

Without the second rule the side suing for peace offered a white peace, the only willingness
check asked *that same side*, and it granted itself a free peace - `terms=white_peace` 13 times
out of 13 in balance run 02. The concession ladder was unreachable and looked tuned wrong.

One more way a war can end, added when peace was taken from vanilla: a **dormant** war - past
42 days with under 300 casualties, or under 3 a day - lapses by mutual indifference, on white
terms only. Vanilla had quietly been closing the wars nobody fought; nothing else would.

---

## 5. AI diplomacy cadence

One weekly evaluation per kingdom, spread across days so we never process 12 kingdoms in one
tick. Each kingdom scores its options and takes **at most one** diplomatic action per week:

1. Seek peace in the war with the highest own-exhaustion
2. Offer a pact to the kingdom with the highest shared threat
3. Demand tribute from a weak neighbour we have a claim on
4. Declare war on the best-value target we hold a claim against
5. Do nothing — the most common outcome, deliberately

Rate limiting is a design feature, not an optimisation. AI diplomacy that fires daily reads
as random noise to the player; weekly, with a visible reason attached to each action, reads
as intent.

---

## 6. Implementation order

| Step | Deliverable | Depends on |
|---|---|---|
| 1.1 | War exhaustion + war score, event hooks, daily accrual | Phase 0 |
| 1.2 | `FiefHistory`, claim registry, acquisition, expiry, fabrication | 1.1 |
| 1.3 | Treaty engine: sign, expire, renew, break, tribute payments | 1.1 |
| 1.4 | Trust ledger | 1.3 |
| 1.5 | Peace table valuation and terms | 1.1, 1.2 |
| 1.6 | Call to arms | 1.3, 1.4 |
| 1.7 | AI weekly evaluation | all above |
| 1.8 | Diplomacy UI | all above |

## 7. Decisions taken by the project lead (2026-09-15)

1. **Minor factions are out of scope.** Treaties, claims and exhaustion are kingdom-only.
   Minor-faction diplomacy is not planned; revisit only if the pillar feels thin.
2. **The AI plays by the same rules.** Every valuation, cost and threshold in this document
   is evaluated through the same functions for AI and player - see `ClaimRegistry` and
   `WarExhaustion`, which take no "is this the player" argument anywhere. No hidden
   modifiers, no difficulty fudge. The player cannot out-cheese the AI by learning seams,
   and any exploit the player finds is one the AI benefits from too.
3. **Enemy exhaustion is shown as a qualitative band**, upgradeable to the exact
   figure through espionage in Phase 3. See §8.
4. **Vassalage stays in Phase 1** as a treaty type, alongside the tributary pact.

## 8. How much of the enemy does the player see?

**Decided: option B — qualitative bands, upgraded to exact figures by espionage in Phase 3.**

Own exhaustion and own war score are always exact. Of the enemy, the player sees a bar and a
label, never a number:

| Band | Range | What it means mechanically |
|---|---|---|
| **Fresh** | 0–19 | Nothing is pressing them |
| **Strained** | 20–39 | Costs are being felt, no political consequence yet |
| **Weary** | 40–59 | Past `ExhaustionCourtPressure` — doves gain bloc support (Phase 2) |
| **Exhausted** | 60–79 | Past `ExhaustionSeekPeace` — they will accept a white peace |
| **Breaking** | 80–100 | Past `ExhaustionAcceptBadTerms` — they will accept unfavourable terms, and their fiefs are losing loyalty |

The band edges are not arbitrary fifths: each one is an existing behavioural threshold from
§1.3. So a band is a real statement about what the enemy will now do, not a decorative
label - "Exhausted" literally means "will accept peace".

What this buys each pillar:

- **Phase 1** stays playable on its own. The player can read the situation and plan around
  it without being able to time a peace offer to the exact day.
- **Phase 3** gets something concrete to sell. The `ReadCourt` mission (spec 03 §2) turns a
  band into the exact value plus its trend, stamped with the date it was gathered. Turning
  a label into a number is a legible reward for having run a network for two years.
- **Phase 2** reuses the same treatment for court politics: your own court is exact, a
  rival's is banded.

Two implementation notes for when this is built:

- The band is computed from the same value the AI reads. There is no separate "displayed
  exhaustion" to drift out of sync, and no rounding that could make the bar disagree with
  the behaviour.
- Regardless of the band, an AI kingdom past the seek-peace threshold sends an envoy (§5).
  That in-fiction channel means the player is never blind to an enemy that is ready to
  talk, which is what keeps banding from feeling like withheld information.
