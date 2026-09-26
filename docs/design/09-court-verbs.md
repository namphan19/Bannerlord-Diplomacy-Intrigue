# Design 09 — Court verbs: the ruler's hands

Status: **spec for review**, 2026-09-26. Phase 2. Nothing is built.

The lead's call of 2026-09-26: build R-2 of the 2026-09-24 mechanics review (court and patron
verbs for the player) so that Phase 2 meets its acceptance line. Espionage's default and the
rarity of civil war stay as they are (TODO.md items 3 and 4, the same call).

Constants will live in `Intrigue/IntrigueConstants.cs` (C1, C2) and
`Diplomacy/DiplomacyConstants.cs` (C3). Every number below is **UN-TUNED** and a first guess
with its reasoning, the same standard as design 02.

---

## 0. The problem

ROADMAP's acceptance for Phase 2: *"an AI kingdom that loses a long illegitimate war visibly
fractures — blocs shift, then either sues for peace or splits. **The player can survive it by
managing grievances.**"* The player has no act that reduces a grievance. The Court tab shows
and selects; the only court actions are the civil war's (`CivilWarVM`: change side, concede).

The review's rule, which this design adopts: **every meter the player is shown has at least one
act that moves it, and the act costs something.** Loyalty today, term by term
(`Intrigue/LoyaltyModel.Explain`):

| Term | Size | What moves it today | The ruler's own lever |
|---|---|---|---|
| Relation | ±50 (relation × 0.5) | vanilla: quests, marriage, freed prisoners, dialogue | vanilla only |
| **Grievances** | −1.5 per point of weight | decay, 0.02 a day at the base rate: a weight-8 wrong takes ~400 days | **none** |
| Fiefs | ±10 | vanilla fief votes | vanilla |
| War exhaustion | down to −20 | peace | the peace table |
| Crown legitimacy | ±10 | wars, treaties, the peace dividend | indirect |
| Presence | ±5 | the ruler's Leadership (design 08 S-7) | levelling |
| Foreign gold | −20 | a foreign bribe (design 03) | counter-intelligence |

And what the verbs have to beat. An internal war starts when **all three** hold
(`InternalWars.Assess`): the pretender bloc holds ≥ 40% of the court's influence, crown
legitimacy is below 35, and at least two sworn houses sit below loyalty 25. A house at loyalty
70 or more is not counted in its bloc's power. A ruler survives by breaking **any one** of the
three. Grievances reach the first through the loyalty-70 exclusion and the third directly.

**What the ruler has to spend.** Run 08, 1,904 weekly samples of AI rulers over 20 in-game
years: influence p10 **624**, median **2,374**, p90 **5,330**; gold median **551,000**. Gold
does not bite at these sizes (review finding F), so the court verbs are priced in **influence**,
the currency a ruler also needs for votes, wars and armies. That is the trade-off that makes
them decisions.

---

## 1. C1 — Make amends (the acceptance piece)

**Who:** the ruler of the realm. **On what:** one grievance a sworn house of that realm holds
against the crown. Every grievance aimed at the crown has the ruling clan as its target
(`GrievanceSources`, `SuccessionModel`, `Missions`), so this is exactly the set that loyalty
reads.

**Effect:** the grievance's weight goes to 0. The house's loyalty rises at once by
`weight × 1.5`, the same factor that took it away; one resolver, no second number.

**Price, in influence:**

    price = weight × AmendsPerPoint × Standing(house) × Memory × Persuasion

- `AmendsPerPoint` = **10**. A weight-8 wrong, the heaviest single grievance, costs 80 from an
  ordinary house: a third of a p10 ruler's purse, a thirtieth of the median's. Clearing a whole
  nine-house court after one maximal unjust war (72 points) costs ~720: a real choice for the
  median ruler, out of reach for a poor one.
- `Standing` = the house's influence against the court's average, clamped to 0.5–2.0. A great
  house is dearer to placate. It reads `SuccessionModel.InfluenceRatio`, the resolver the
  magnate test and the Encyclopedia's "house weight" band already share.
- `Memory` = **×2** if this house has had amends from this crown within the last two years. A
  king who keeps apologising to the same house pays more for it.
- `Persuasion` = the realm's Envoy (Charm) against the realms' median, ±15%, the shape of the
  existing haggling term (design 08). Off with statecraft off.

**A wrong repeated after amends weighs more.** When a grievance of the same type from the same
house against the same crown is raised again within two years of being answered, it arrives at
**×1.5**. The house forgave once, on terms.

**Worked example** (`di_grievance_test`, STATUS 2.2): Harfit holds `UnjustWar` 7.8 against the
Khuzait crown, −11.7 loyalty, total 30.5 (Disaffected). An average house: price 78 influence;
Harfit's loyalty goes to 42.2 (Transactional).

**What it cannot rescue, said now rather than found later.** Amends clear the grievance term
and nothing else. After a contested succession, the relation term collapses (a new king starts
near zero with everyone, STATUS 2.5), and legitimacy falls with it. On `di_pretender_test` most
of Battania's court sat at loyalty 0–7.5; there, answering every grievance may still leave
houses below 25. Section 5's check measures exactly this, and C2's patronage is the second
lever for it.

**The AI**, under the same price and effect. Once a week, an AI ruler whose court is under
threat (a Pretenders bloc has formed, or a sworn house is below 25) makes **one** amends: the
grievance whose answer moves the most bloc power out of danger per point of influence, paid only
from influence above a reserve of **twice** its current war-declaration cost, so it can still go
to war. An AI ruler that answers a grievance held by the player's house tells the player.

**Statecraft:** amends trains the ruler's Charm (XP per point of weight answered), on the same
list as design 08's other political acts.

**Save data:** two properties on `Grievance`, the next free there: **6** `AnsweredOn`
(`CampaignTime`) and **7** `Answers` (`int`). An answered grievance stays in the ledger at
weight 0 until its two-year memory lapses; `IsSpent` reads both. No new class, no container.

---

## 2. C2 — Offices and patronage

This **absorbs design 08 §8** (S4, "Offices"). Both documents describe the same thing, a court
seat held by a hero that is at once a statecraft actor and royal favour; building two would be
two answers to "who speaks for the realm", which CLAUDE.md §3 forbids.

- **Seats:** the five non-ruler portfolios of design 08: Envoy (Charm), Steward (Steward),
  Treasurer (Trade), Spymaster (Roguery), Watch (Scouting). Leadership stays the ruler's own.
- **One resolver:** `StatecraftModel.Actor(kingdom, portfolio)` reads the seat first and falls
  back to today's rule, the ruling house's best, for an empty seat. Every statecraft term
  follows with no change of its own. A captured holder cannot act; the fallback speaks, as today.
- **Holders:** any adult hero of a sworn house of the realm, the ruling house's included. One
  seat per hero.
- **Patronage:** a house holding a seat gains **+8** loyalty (a second seat adds **+4**; no
  more after that), a new `LoyaltyBreakdown` term, "office". It also pulls toward the
  Centralists, which gives that bloc a source for the first time: STATUS 2.3 found it could
  never form.
- **Cost:** an appointment costs **50** influence, below the 72 an AI pays to declare a war on
  a Conquest claim (STATUS, 2.8 A-1): a real price, not a war's.
  Dismissing a holder is free in influence and costs a grievance: their house takes
  `DismissedFromOffice`, weight **4**, a new `GrievanceType` value **11**. Design 08 §8 also had
  houses *passed over* take a grievance; that is left out of the first version, because it
  would make every appointment anger somebody and the log noisy before anyone knows whether
  patronage is strong enough.
- **The AI:** fills an empty seat from its own house by skill, which is today's result, so an
  AI world with no threat plays exactly as run 08 did. Under the same threat as C1, it appoints
  from the endangered house instead, dismissing the holder whose house needs it least.
- **The player as a vassal** can be offered a seat by an AI king, as an inquiry: accept, and
  the player's hero speaks for the realm in that skill; refuse, and nothing happens. Serving a
  king becomes a position with a desk.

**Save data:** `CourtOffice` at class id **18** (`Kingdom`, `Portfolio` seat, `Hero` holder,
`CampaignTime` since), `ModState` property **18** (`List<CourtOffice>`), and its container
definition. `Portfolio` is registered as enum **28**, with its values written out explicitly
before it is. After this, only class id **19** is left below the enum block (CLAUDE.md §3).

---

## 3. C3 — Tribute set per vassal (the patron's verb)

Tribute is a fixed 500 a period at every creation site (`AiDefaultTributePerPeriod`), and
design 04 §1.2 promised the patron "how much tribute it demands" as a lever. Hold already has
the term: `Hegemony.TributeBurden` weighs `Treaty.TributeAmount` against what the vassal holds.
Only the lever is missing.

- **Levels:** None, Light 250, Standard 500, Heavy 1,000 a period. The patron sets it; income
  and Hold move by the formulas that already exist.
- **The AI:** Heavy while the link's Hold is above 70, Light below 40, Standard between.
- **Where:** the vassal's row on the Kingdom screen's Diplomacy tab, the one-relationship scope
  (the lead's rule of 2026-09-20).
- **Save data:** none. `Treaty.TributeAmount` (property 8) exists and is saved.

C3 is not needed for the acceptance line. It is in R-2, so it is here, last in the order.

---

## 4. UI

Where each verb goes follows the lead's scope rule. The precise layout goes to a mockup first,
as the Court and Intelligence tabs did.

- **Court tab, the selected house's grievance list:** each grievance held against the crown
  carries **Make amends — N influence**, disabled with its reason when the player is not the
  ruler or cannot pay. Under the list: what answering all of them would cost and the band the
  house would reach.
- **Court tab, an Offices strip:** the five seats, each with holder, skill against the realms'
  median, and house; **Appoint** opens a chooser (the pattern of the Intelligence tab's handler
  picker), **Dismiss** names the grievance it costs.
- **Diplomacy tab, a vassal's row:** the tribute level.
- A player who is not the ruler sees the Offices strip read-only, and a seat offer arrives as an
  inquiry.

---

## 5. Proving the acceptance line

The pass condition, written before the build so it cannot drift: **a player-ruled realm that
meets all three internal-war conditions is brought below at least one of them by amends and
offices, using influence it holds, and no rising comes on the next daily tick.**

1. `di_grievance_test` (Khuzait, player-ruled, 21 grievances): every amends predicted by hand
   (price, loyalty, band) and matched on screen and in `diplomacy.loyalty`.
2. `di_pretender_test` + `test_player_rule battania`: a court one tick from rising. Assess, act,
   assess again: which condition broke, and at what cost. If the verbs cannot break one there,
   the report says which condition held and by how much. That is a finding for the lead, not a
   pass.
3. Control: the same save with no action rises on the first tick (known since 2.6).
4. Save round trip: `Grievance` 6–7 and `CourtOffice` in a fresh process.
5. An AI-only run: how often AI rulers use amends and offices, and what that does to the number
   of internal wars. The lead chose to leave civil war's rarity alone, and AI amends will make
   AI civil wars rarer still; this run says by how much, and the reserve in §1 is the knob.

---

## 6. Order

| Stage | Size | Deliverable | Meets |
|---|---|---|---|
| **C1** | M | Make amends: price, memory, AI rule, Court-tab buttons, `diplomacy.amends` diagnostic, telemetry | the acceptance line on its own, if §5 step 2 passes |
| **C2** | L | Offices and patronage: seats, the resolver change, patronage term, AI rule, vassal offers, save data | the Centralist gap; a second lever where amends is not enough |
| **C3** | S | Tribute per vassal | the patron's lever of design 04 §1.2 |

Each stage is built, verified live and committed before the next starts. The mockup covers C1
and C2 together, before C1's UI is built.

---

## 7. Decisions for the lead

| # | Question | Options | Recommendation |
|---|---|---|---|
| D1 | Scope now | C1 only / C1 + C2 / **C1 + C2 + C3** | **All three, in that order.** They are all R-2; C1 alone may not rescue a court after a contested succession (§1) |
| D2 | What amends cost | **Influence** / gold / gold for land wrongs, influence for honour | **Influence.** Gold does not bite at a median of 551k (§0) |
| D3 | The price | **10 per point × standing 0.5–2** | as written; un-tuned |
| D4 | Memory | **×2 price for repeat amends and ×1.5 for a repeated wrong, within two years** / none | **Memory.** Without it a rich crown keeps any court loyal forever |
| D5 | Statecraft | **Envoy's Charm ±15% on the price, amends train Charm** / neither | **Both**, the design 08 pattern |
| D6 | How eager the AI is | **Only under threat, one a week, above a reserve** / never unprompted / freely | **Under threat.** Same rules as the player (CLAUDE.md §3); the reserve keeps it able to wage war |
| D7 | Seats | **Five, one per portfolio** / four (design 08 §8 merged Spymaster and Watch) | **Five.** One seat per resolver input, no special case |
| D8 | Holders from other houses | **Yes** / ruling house only | **Yes.** It is the whole point of patronage |
| D9 | Patronage strength | **+8, +4 for a second seat** | as written; un-tuned |
| D10 | Grievances from offices | **Dismissal only (weight 4)** / also houses passed over (design 08 §8) | **Dismissal only** at first |
| D11 | Appointment cost | **50 influence** / free | **50** |
| D12 | The AI's appointments | **Own house by skill; patronage under threat** | as written: neutral to run 08 when no court is in danger |
| D13 | Seat offers to the player as a vassal | **Yes, as an inquiry** / no | **Yes** |
| D14 | Tribute levels | **None / 250 / 500 / 1,000, AI by Hold** | as written |
| D15 | UI | **Mockup first, then build** | as the Court and Intelligence tabs were |
