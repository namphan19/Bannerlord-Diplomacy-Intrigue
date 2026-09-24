# 03 — Hero skills and personality traits

> **Part of** [the 2026-09-24 mechanics review](README.md). **Decisions** live in
> [decisions.md](decisions.md) as `S-1` …. **Code paths** are relative to
> `src/DiplomacyIntrigue/`.

The lead asked whether hero skills take part in anything the mod does today. They do not, and
the lead has decided that they should. This file records what the code does now, the three
decisions of 2026-09-24, and the design direction they set. **Nothing here is built, and none of
it is scheduled.**

---

## 1. What the code does today — verified in code, 2026-09-24

**The mod reads no skill, no perk and no personality trait, anywhere, and awards no skill or
trait experience for anything.** A search of the whole source for `DefaultSkills`,
`GetSkillValue`, `SkillObject`, `DefaultPerks`, `GetPerkValue`, `DefaultTraits`,
`GetTraitLevel`, `AddSkillXp`, `ChangeTraitAction` and `TraitLevelingHelper` finds nothing.

Skills reach the mod only through vanilla:

| Channel | How |
|---|---|
| **Succession inside a house** | `Intrigue/ClanSuccession.cs:95-106` scores heirs with vanilla's `HeirSelectionCalculationModel`, which gives `HighestSkillPoint` to the most skilled member of the family. So skill decides who inherits, and through that whether a house divides (design 07 §5) |
| **Military strength** | Leadership, Steward and the combat skills make parties larger and stronger. That strength feeds `Power`, Hold's fear term, the war valuation, and the casualties that drive exhaustion and war score |
| **Relation** | Vanilla's Charm perks move relations, and relation is a term in loyalty and in the pact valuation (`Diplomacy/AiDiplomacy.cs:1158`) |

Design 03 (espionage, not built) is the only design document that plans to read a skill
directly: the handler's Roguery and Charm.

**Why it matters.** Bannerlord is an RPG first, and skills are its spine of progression. Today
political play is not a *build*: a ruler with Charm 300 and one with Charm 10 sign treaties,
soothe a court and settle a civil war identically. The part of the game this mod does best does
not notice the character the player has spent tens of hours on.

---

## 2. Decisions of 2026-09-24

| Id | Decision | Lead's reasoning, where given |
|---|---|---|
| **S-1** | **Political play becomes a character build.** Diplomatic, court and covert acts award skill experience | accepted as proposed |
| **S-2** | **Personality traits shape AI courts and the player's reputation.** Traits give each court a character from day one, and a ruler's acts move their traits | accepted as proposed |
| **S-3** | **No cap to protect the AI from a stronger player.** Skill effects are not limited to a tie-break | *"AI can level up and gain skills just as the player does, so there is no need to worry."* (in the lead's words: "AI hoàn toàn có thể gia tăng lv và skill như player nên k cần lo") |

**S-3 supersedes a recommendation.** Claude had proposed capping skill effects at about 10–15%
with diminishing returns, on the worry that a player grinding to 300 would out-scale AI lords
under rules that are symmetric on paper. The lead rejected the worry on the grounds above. **Do
not reintroduce the cap** without new evidence and a new decision. The project's other rules
still apply to skill effects (§4); only the asymmetry cap is gone.

---

## 3. The design direction

Three layers, in the order of value for cost. Magnitudes are placeholders for the spec to set.

### 3.1 Experience for political acts (S-1)

| Act | Skill |
|---|---|
| Signing a treaty, negotiating a peace | Charm |
| Fabricating a claim; later, running an espionage mission | Roguery |
| Managing the court: appeasing, patronage, answering petitions (when [02](02-unbuilt-mechanics.md) §2–§3 are built) | Steward |
| Setting or collecting tribute, indemnities | Trade |

The same acts award the same experience to an AI ruler or handler.

**Guard against farming.** Signing and renewing treaties is repeatable, and a pact can be
signed, let lapse and signed again. Experience per kind of act should be capped per season, or
paid only for acts that change something (a new partner, a war actually ended). This is a
design guard, not the cap S-3 rejected.

### 3.2 Personality traits (S-2)

**A court's character from its lords' traits.** Today a clan's agenda comes only from pressure
of circumstance, which is why four of eight kingdoms start with no bloc at all (STATUS 2.3). A
trait-driven baseline gives every court a character on day one, and addresses finding C:

| Trait | Leans toward |
|---|---|
| Valor | Hawks |
| Mercy | Doves |
| Calculating | Autonomists; willingness to fabricate a claim |
| Generosity | proposal: a better response to patronage |
| Honor | proposal: less willing to break a treaty (an AI ruler's side of the same rule) |

**A ruler's reputation, carried by the ruler.** Trust today is a number between realms; the
person on the throne has no reputation of their own. Proposed trait changes:

| Act | Trait |
|---|---|
| A treaty honoured to its term | Honor + |
| A treaty broken | Honor − |
| Clemency to the losers of an internal war (U-9) | Mercy + |
| Executing them | Mercy − |

And, as a proposal: the trust a court starts with toward a new ruler read from that ruler's
Honor, so a new king inherits the realm's record *and* brings their own.

Whatever changes a player's traits changes an AI ruler's by the same rule.

### 3.3 Skills as modifiers

| Skill | Where it would act | Note |
|---|---|---|
| **Roguery** | The chance a fabricated claim is exposed | Today a flat **20%** (`Diplomacy/ClaimRegistry.cs:239`, `FabricateClaimExposureChance`). The most natural hook in the current build |
| **Charm** | The price of appeasing a grievance; the price of buying a house's side in an internal war; how persuasive a petition is | |
| **Steward** | How fast grievances fade, or how many houses a ruler can hold before loyalty starts to slip | |
| **Trade** | The size of tribute and indemnities | ties to finding F, money that does not bite |

---

## 4. Rules that still apply

S-3 removes one worry; it does not suspend the project's rules.

- **One function for AI and player.** Every skill or trait effect is read inside the resolver
  the AI uses, with no "is this the player" argument (CLAUDE.md §3).
- **Name the actor for each act**: the ruler for treaties and the peace table, the handler for
  espionage, the clan head for petitions and side changes. A kingdom has no skills; a hero does.
- **Show it.** Every skill or trait effect appears as its own line in the breakdown that
  explains a number ("the king's Charm: −8%"), so the number shown is still the number the AI
  used.
- **Constants in one file per pillar**, each marked UN-TUNED until measured (CLAUDE.md §4).
- **One skill scale.** Bannerlord skills run to about 300. Any formula written for 0–100 (design
  03's is — see [02](02-unbuilt-mechanics.md) §4.1) is normalised once, in one helper, and every
  pillar reads that helper.

---

## 5. What to find out before building

None of these has been checked. Each is small.

| Question | How | Why it matters |
|---|---|---|
| How are traits distributed among vanilla lords? | a debug command, e.g. `diplomacy.traits`, listing every lord's trait levels | if most lords sit at 0, the court-character layer (§3.2) has nothing to read |
| Where does vanilla already change traits? | `dotnet run --project tools/CallSites -- --callers "TraitLevelingHelper::"` and the same for `ChangeTraitAction` | to avoid awarding a trait change vanilla already awards for the same act |
| How do AI lords' skills grow over a long campaign? | log the distribution in run 08 | S-3 rests on AI lords levelling as the player does; the distribution sizes the constants. A measurement, not a reopening of S-3 |
| What experience rates does vanilla use for comparable acts? | ApiDump / CallSites on `HeroDeveloper` | so political experience sits on vanilla's scale, neither negligible nor dominant |

---

## 6. Where it fits

Not scheduled; the lead has not placed it in the order ([02](02-unbuilt-mechanics.md) §6 is a
proposal). The natural fit:

| Layer | With |
|---|---|
| Traits (§3.2) | 2.8, together with agendas that vote and petitions: the same thread of design |
| Experience (§3.1) | any time; cheap and independent |
| Roguery on fabrication (§3.3, first row) | any time; one term in one existing roll |
| The other modifiers (§3.3) | with the verbs they modify: 2.8 for Charm and Steward, Phase 3 for espionage |
