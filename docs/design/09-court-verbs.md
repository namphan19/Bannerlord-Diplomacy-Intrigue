# Design 09 — Court verbs: the ruler's hands

Status: **decided** 2026-09-26 (§7). **C1 built and run live** the same day (§8); C2 and C3 not built.

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
years: influence p10 **624**, median **2,374**, p90 **5,330**; gold median **551,000**.

**The pricing rule** — the lead's, 2026-09-26, for these verbs and for every political act
built after them:

1. **An act costs influence and gold, both.** Neither alone buys it. Influence is what a ruler
   also needs for votes, wars and armies; gold, at a median of 551,000, only bites when the
   price is sized to it (review finding F). Asking for both is what makes the act a decision.
2. **Each part scales with the skill that does the work**, strongly: a factor of
   `2^(−level)`, where `level` is the statecraft Level (skill against the realms' median, −1 to
   +1 over 150 points, design 08 §4.2) or, where the act is a bargain with another party, the
   Contest of the two sides' Levels. At the top of the scale a part costs **half**, at the
   bottom **double**. The influence part follows the act's political skill (Charm to persuade,
   Leadership to command, Roguery to act covertly); the gold part follows the Treasurer's Trade,
   who handles the money. One function, `StatecraftTerms.PriceFactor`, so every act prices skill
   the same way. With statecraft off the factor is 1.
3. **The prices are high on purpose.** Sized so that answering one heavy wrong is a real outlay
   for a median ruler and answering a whole court is out of reach: the verbs are for choosing
   which houses matter, not for keeping everyone content.

Acts built before this rule (the war-declaration cost, fabricating a claim, espionage's mission
prices, a civil war's side-change price, counter-intelligence) do not follow it yet. The rule
applies from here on; bringing them into line is the lead's call.

---

## 1. C1 — Make amends (the acceptance piece)

**Who:** the ruler of the realm. **On what:** one grievance a sworn house of that realm holds
against the crown. Every grievance aimed at the crown has the ruling clan as its target
(`GrievanceSources`, `SuccessionModel`, `Missions`), so this is exactly the set that loyalty
reads.

**Effect:** the grievance's weight goes to 0. The house's loyalty rises at once by
`weight × 1.5`, the same factor that took it away; one resolver, no second number.

**Price, influence and gold together** (§0's rule):

    influence = weight × AmendsInfluencePerPoint × Standing × Memory × PriceFactor(Charm contest)
    gold      = weight × AmendsGoldPerPoint      × Standing × Memory × PriceFactor(Trade contest)

- `AmendsInfluencePerPoint` = **40**, `AmendsGoldPerPoint` = **5,000**. A weight-8 wrong, the
  heaviest single grievance, costs an ordinary house **320 influence and 40,000 gold** at median
  skills: half the purse of a p10 ruler, a seventh of the median's influence, 7% of the
  median's gold. Undoing a forged letter costs more than forging it (15,000 gold, design 03):
  repair is dearer than harm. Clearing a whole nine-house court after one maximal unjust war
  (72 points) would cost ~2,900 influence and 360,000 gold, more influence than the median
  ruler holds: a ruler answers the houses that tip the balance, not all of them.
- **Charm contest:** the crown's Envoy against the house's best Charm. A persuasive envoy
  facing a plain house pays half; a clumsy one facing a silver-tongued house pays double.
- **Trade contest:** the crown's Treasurer against the house's, the pairing the civil war's
  side-change price already uses (design 08 S-10).
- `Standing` = the house's influence against the court's average, clamped to 0.5–2.0. A great
  house is dearer to placate. It reads `SuccessionModel.InfluenceRatio`, the resolver the
  magnate test and the Encyclopedia's "house weight" band already share.
- `Memory` = **×2** if this house has had amends from this crown within the last two years. A
  king who keeps apologising to the same house pays more for it.

**A wrong repeated after amends weighs more.** When a grievance of the same type from the same
house against the same crown is raised again within two years of being answered, it arrives at
**×1.5**. The house forgave once, on terms.

**Worked example** (`di_grievance_test`, STATUS 2.2): Harfit holds `UnjustWar` 7.8 against the
Khuzait crown, −11.7 loyalty, total 30.5 (Disaffected). An average house, both sides at the
median: **312 influence and 39,000 gold**, and Harfit's loyalty goes to 42.2 (Transactional).
With an envoy 150 Charm above Harfit's best, the influence part halves to 156; with a treasurer
150 Trade below Harfit's, the gold part doubles to 78,000. The first version of this spec
priced it at 78 influence and no gold; the lead judged that far too cheap.

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
to war, and from gold above the AI's gold reserve (50,000, today `EspionageConstants.AiGoldReserve`;
one number for "what an AI ruler keeps back", moved to a shared place when C1 is built). An AI
ruler that answers a grievance held by the player's house tells the player.

**Statecraft:** amends trains the skills that priced them, on design 08's list of political
acts: the Envoy's Charm and the Treasurer's Trade, XP per point of weight answered.

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
- **Cost**, by §0's rule: an appointment costs **200 influence and 25,000 gold** at median
  skills, the influence part scaled by the ruler's Leadership (the authority to raise a house),
  the gold part by the Treasurer's Trade. No contest: an honour offered is not a bargain. It is
  priced near what amends would charge for the same loyalty (+8 is about five points of weight),
  because a seat is also a skilled voice for the realm and lasts until it is taken back.
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
as the Court and Intelligence tabs did: the "Court verbs" row of the court canvas
(https://claude.ai/artifact/1FrpG5in328WYfNi6sP8Pf), published 2026-09-26 for the lead's review.
Its prices are computed by §1-§3's formulas on sample skills.

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

## 7. Decisions — taken by the lead, 2026-09-26

The lead agreed to the recommendations, with two changes: every payment scales with the skills
involved, for these verbs and for every mechanism after them, and the prices go much higher and
take influence and gold together (§0's pricing rule). The first version priced Harfit's amends at
78 influence and no gold; the lead judged it far too cheap.

| # | Question | Decision |
|---|---|---|
| D1 | Scope | **C1, C2, C3, in that order** |
| D2 | What amends cost | **Influence and gold, both** — changed by the lead from "influence only" |
| D3 | The price | **40 influence and 5,000 gold per point, × standing 0.5–2** — raised by the lead from 10 influence per point |
| D4 | Memory | **×2 price for repeat amends and ×1.5 for a repeated wrong, within two years** |
| D5 | Statecraft | **Each part scaled by its skill, ×0.5 to ×2 (`PriceFactor`): influence by the Charm contest, gold by the Trade contest; amends train both** — strengthened by the lead from ±15% on the influence alone |
| D6 | How eager the AI is | **Only under threat, one a week, above an influence and a gold reserve** |
| D7 | Seats | **Five, one per portfolio** |
| D8 | Holders from other houses | **Yes** |
| D9 | Patronage strength | **+8, +4 for a second seat** (un-tuned) |
| D10 | Grievances from offices | **Dismissal only (weight 4)** |
| D11 | Appointment cost | **200 influence and 25,000 gold, by Leadership and Trade** — changed with D2 and D5 from 50 influence |
| D12 | The AI's appointments | **Own house by skill; patronage under threat** |
| D13 | Seat offers to the player as a vassal | **Yes, as an inquiry** |
| D14 | Tribute levels | **None / 250 / 500 / 1,000, AI by Hold** |
| D15 | UI | **Mockup first, then build** |

---

## 8. C1 as built and run live, 2026-09-26

Built: `Intrigue/Amends.cs` (the one price, the act, the AI's weekly choice), `Grievance` properties
**6** `AnsweredOn` and **7** `Answers`, the Court tab's price lines and button, the line under the
roster that prices the whole court, `StatecraftTerms.PriceFactor`, `diplomacy.amends` (the dry run,
with the AI's pick) and `diplomacy.test_amends` (the real act, paid by whoever rules). The court's
week is now one list, `IntrigueUpkeep.Weekly`, shared by the campaign and `diplomacy.ai_week`; the
AI's gold reserve is one constant, `DiplomacyConstants.AiGoldReserve`, read by espionage and amends.
No Harmony; no new class id; the save check counts 126 saved members.

### What running it changed in this spec

- **Standing counts peers only.** As first written, standing read the court's average with the
  crown in it, and on `di_grievance_test` a ruler who gained 1,000 influence saw Urkhunait's standing
  fall from 1.59 to 1.12: the richer the crown, the cheaper its court. `SuccessionModel.InfluenceRatio`
  now takes `peersOnly`, and amends reads it that way; the magnate test and the Encyclopedia keep the
  crown in.
- **The AI scores what an answer really moves.** Loyalty is clamped at 0, so a house deep below it
  gains nothing from one amends (fen Uvain on `di_pretender_test`, 0.0 -> 0.0). The AI's score reads
  the quote's loyalty before and after, and skips a zero. Compiled and reviewed; **not seen choosing
  differently live** (on the one court where it could matter, the old score picked the same house).
- **The button takes two clicks**, the first naming the price: the shape conceding a civil war
  already has, because the act is dear and final and the test bridge cannot click inside an inquiry.
  The approved mockup showed one click.
- The button is 56 high: at the column's 310 width the price wraps to two lines.

### Checked live

On `di_grievance_test` (Khuzait, the player rules), with the player's Charm set to 232 and Trade to
150 and the purse topped up with vanilla's cheats; every figure predicted by hand first.

| Check | Result |
|---|---|
| Urkhunait's price, predicted 459 influence and 79,200 denars (standing 1.44, Charm x1.02, Trade x1.41) | **459 and 79,200**, on the Court tab and in `diplomacy.amends` |
| Two clicks from the Court tab | the first armed it ("Confirm: pay 459 influence, 79,200 denars"), the second paid |
| The crown's purse | 1,006 -> 547 influence; 201,000 -> 121,800 denars |
| Loyalty | 39.7 -> **51.4**, Disaffected -> Transactional, the grievance term 0.0 |
| The ledger | "answered x1, remembered", shown on the Court tab under the live grievances |
| A day of upkeep | the answered record stays (it is remembered) |
| The same wrong again (a war on Vlandia with no claim) | Urkhunait **12.0** (8 x 1.5); every other house 8.0 |
| The next price for Urkhunait | "within 2 years: x2", 707 influence and 86,100 denars; refused for want of influence, with the reason |
| Save, a new process, load | the answered record, its weight 12.0 and the x2 all came back |
| Telemetry and XP | `kind=amends`; `skill_xp` 1,950 each to the envoy's Charm and the treasurer's Trade (7.8 x 250) |
| The AI's weekly pass, through `ai_week` | Aserai answered Banu Habbab: 101 influence, 13,800 denars, loyalty 3.0 -> 8.6 |

### The acceptance line, measured

§5's pass condition was **not met** on the court it was written for, and the finding is the point
of the check. `test_player_rule battania` cannot run on `di_pretender_test`: the player already rules
Khuzait there, and a ruler cannot leave a realm without a crown. So the verbs were run in Battania
itself, through the same `Amends.Execute` the player's button calls, with Battania's own ruler paying.

| | Before | After two amends (fen Penraic, the AI's own pick, and fen Caernacht) |
|---|---|---|
| The trigger | READY | still **READY** |
| Pretender bloc's share | 61% | 56% (Penraic 62.3 -> 70.8 left the bloc's power) |
| Houses below 25 | 4 | 3 (Caernacht 21.3 -> 29.8) |
| Crown legitimacy | 25 | 25 - amends do not reach it |
| The rising, next daily tick | would take 5 houses | took **4**; the crown held 44% of the court |

What held, and by how much: **legitimacy** (25 against the 35 needed) is out of every court verb's
reach; the **bloc** stays above 40% because only a house at 70 leaves the bloc's power, and the others
sit at 0-30; **three houses** stay below 25 because relation, not grievance, sank them after the
contested succession (fen Uvain: relation -34, raw loyalty -10.2 before the amends, -1.7 after).
C2's +8 would not change any of the three here.

What C1 does do: it moves a house across a band (Khuzait, Disaffected -> Transactional), it can pull a
great house out of a pretender bloc before the war (Penraic), and it makes the war that comes smaller.
In the unjust-war court the acceptance line describes, no internal war can start at all without a
standing claimant, which only a contested succession makes; there, "surviving" is keeping houses from
sliding, and amends does that.

Two more findings from the same runs:

- **The AI cannot answer a sudden crisis.** The trigger is checked daily and the AI makes amends
  weekly; a contested succession creates all three conditions at once, so the rising comes the next
  day. The AI only prevents a threat that builds over weeks.
- **Spending influence can make a claimant.** After the player's 459 influence went on amends, Arkit
  crossed the magnate line (x1.30 of a court average that counts the crown) and the Court tab marked
  it "claimant". Two systems meeting, not a fault: a crown that spends its influence looks weaker.

Not checked: the AI's zero-gain skip in a case where it changes the pick; a vassal player being told
an AI ruler answered their house (the message exists; no test put the player's house on the
receiving end); the Encyclopedia ledger, which now leaves answered records out; the test lever
`test_set_skill`, which sets a skill without its XP, so the first XP grant puts the old value back -
the player's Charm read 503 again after one amends.
