# Game-mechanics review — 2026-09-24

A game-design review of the mod's mechanics as they stand on `development` (Phase 1 accepted,
Phase 2 through 2.6c merged, Phase 3 not started). Asked for by the lead; written from a
designer's seat rather than an engineer's: what the systems make a player *do and feel*, where
they fail to, and what would fix it.

**Nothing here has been acted on.** Every proposal below is a proposal for the lead. No code,
constant or design doc was changed by this review.

## How it was done, and how far to trust it

- Read: design docs 01–07, `PLAYER-GUIDE.md`, `STATUS.md`, `balance/run-07.md`, and the two
  constants files.
- Checked against the code wherever a finding depends on what the code does rather than what a
  doc says. Those findings carry a file reference and are marked **verified in code**.
- **Not run in the game.** No number here was measured by this review. Figures quoted from runs
  are the runs' own, with the doc they come from.
- Three confidence tags are used throughout:
  - **verified in code**: read in the source on this date;
  - **measured**: observed in a live run or balance run, cited;
  - **inference**: follows from formulas and constants, not observed. Treat as a hypothesis for
    run 08, not a fact.

---

## 1. Verdict

The **diplomacy and hegemony layer is unusually well designed**: few strategy mods have a model
of war and peace this coherent, and it is already producing emergent history (run 07).

The **court layer is strong as simulation and weak as gameplay**. It records consequences in
detail but gives the player almost nothing to *do* about them, and it does not yet push back on
the AI's foreign policy. Those two gaps matter more than any constant in either file.

---

## 2. What to keep — do not "fix" these

Recorded so that a later balance or refactor pass does not erode them by accident.

1. **Exhaustion and war score are separate numbers deciding separate things.** Score says what
   the winner may *demand*; exhaustion says whether a court will *sign*. A tired winner takes a
   white peace; a fresh loser refuses to be carved up. This is the single best idea in the mod.
2. **Betrayal is priced, never blocked.** Casus belli, breaking a treaty, fabricating a claim:
   all always possible, all with a visible cost. Player agency with consequences, which is
   exactly the right shape.
3. **Hegemony is derived, and Hold is the one load-bearing number.** The `log2` fear term and
   greed-driven dread combine into an inverted U (a patron can be too weak *or* too strong to
   hold a vassal); the revolt line rises with the vassal's strength. Run 07's two eras —
   vassals by choice while the map was near parity, by coercion once it diverged — were written
   nowhere and fell out of the arithmetic. That is a system working as a system (**measured**,
   run-07 §2).
4. **The AI plays by the player's rules, and a number shown is the number the AI used.** For a
   strategy game this is the largest single asset: it is what makes the player trust the
   simulation enough to plan against it.
5. **Bands sit on behavioural thresholds.** "Exhausted" means "will sign a white peace", not
   "exhaustion is between 60 and 79". A band that predicts behaviour is information; a band
   that decorates a number is noise.

---

## 3. Findings

Ordered by how much they limit the game, not by how hard they are to fix.

### A. The pillars are coupled in one direction only — the most important finding

**Observation.** Diplomacy writes into the court: a broken treaty costs crown legitimacy, an
unjust war grieves the court. Nothing flows back. The AI's foreign policy never reads loyalty,
crown legitimacy, blocs, or whether a realm is at war with itself.

**Evidence** (verified in code):
- In `Diplomacy/`, `Behaviors/` and `GameModels/`, the only reader of `LegitimacyRegistry.Of`,
  `LoyaltyModel`, `BlocModel` or `InternalWars.OngoingIn` is `Behaviors/IntrigueBehavior.cs`.
  `AiDiplomacy`, `CallToArms`, `PeaceTable` and `Hegemony` read none of them. The only calls
  from diplomacy into intrigue are writes (`TreatyRegistry.cs:274`, `ClaimRegistry.cs:286`).
- `SupportClaimant` (legitimacy 0.55) is produced only by translating vanilla's
  `CausedByClaimOnThrone` / `CausedByRebellion` war details (`Diplomacy/CasusBelli.cs:28-30`).
  No rival kingdom ever gains it from a standing `Pretender`, although `Models/Pretender.cs:11`
  says that is what a pretender is for, and design 02 §7 lists it as a coupling.
- Design 01 §1.3's "≥ 40: court pressure begins — doves gain bloc support" is built as bloc
  formation only. A court that is 100% Doves (**measured**, STATUS 2.3: Khuzait taken to
  exhaustion 93 with `diplomacy.tick_days 300`) does not make its AI seek peace one day sooner.
- STATUS "What to do next" #4 already records the same gap for `AiDiplomacy.TryDemandTribute`.

**Why it matters.** Design 03 §6 names the risk itself: three pillars becoming three unrelated
games. Today the court is a consequence *tracker*. It cannot yet be a *force*, so a player's
careful court management changes nothing a neighbour does, and a rival's collapsing court is
invisible to the AI except through raw strength.

**Proposal** — three cheap hooks, each one term in an existing valuation, so each stays
explainable in the UI:
1. The share of the court in the Doves bloc lowers the exhaustion at which a court seeks peace.
2. A neighbour's crown legitimacy below the pretender line, or an internal war under way, is a
   visible term in the war valuation ("their court is divided").
3. A standing pretender grants rivals `SupportClaimant`. Whether that war can *join* the internal
   war, or only exploit it, is a separate and larger decision.

### B. The player has almost no verbs in the court or as a patron

**Observation.** Outside a civil war, the Court tab is read-only. Loyalty is a sum of relation,
grievances, fiefs, war exhaustion and crown legitimacy; the player can move only the first and
third, and only through vanilla (relation actions, fief grants). As a patron the player has no
lever on a vassal's Hold beyond answering its calls.

**Evidence** (verified in code):
- The court VMs expose selection and display only (`UI/KingdomScreen/CourtVM.cs`); the only
  court *actions* are the civil-war ones in `CivilWarVM.cs` (change side, concede).
- Tribute is the fixed `AiDefaultTributePerPeriod = 500` at every creation site
  (`DiplomacyConstants.cs:1028`; used in `PeaceTable.cs`, `AiDiplomacy.cs`, `Hegemony.cs`,
  `UI/DiplomacyMenu.cs`). Design 04 §1.2 promised the patron two levers, "how often it calls,
  how much tribute it demands"; the tribute lever does not exist and summons are parked.

**Why it matters.** Pressure without levers produces spectators, not players. The court shows a
ruler exactly how their realm is coming apart and offers no way to spend anything to stop it.

**Proposal** — one rule, then a minimum set. *Every meter the player is shown should have at
least one player action that moves it, and that action should cost something.*
- **Appease** a grievance: spend influence to reduce its weight, with diminishing returns per
  clan so it cannot be spammed.
- **Patronage** (a council seat, an office, a stipend): the missing input that lets the
  Centralist bloc exist at all (see C).
- **Set tribute per vassal**, so a patron trades income against Hold. The Hold formula already
  has the term (`Hegemony.cs:191`, tolerable = fiefs × 200); only the lever is missing.
- The same verbs must exist for AI rulers, by the project's standing rule.

### C. A bloc's agenda does not decide how it votes

**Observation.** A bloc member votes for whatever its bloc *leader* would prefer by vanilla's own
evaluation. The agenda — Hawks, Doves, Autonomists, Centralists — is not an input to the vote.
A Doves bloc can vote for a war if its leader's vanilla preference is war.

**Evidence** (verified in code): `Patches/KingdomDecision_DetermineSupportOption_Patch.cs:91`
redirects to `BestFor`, which asks vanilla's `DetermineSupport(leader, outcome)` (line 112).
Outside `BlocModel` and the Court tab's display, `CourtAgenda.Doves/Hawks/Autonomists/Centralists`
is read nowhere; only `Pretenders` has an effect (the internal-war trigger).

Related gaps in the loyalty bands, against design 02 §2:

| Band | Spec says | Code does (verified) |
|---|---|---|
| ≥ 70 | votes with the ruler, answers call to arms | exempt from the bloc override (patch line 89); votes its *own* vanilla preference, not the ruler's |
| 25–39 | votes against the ruler, will not volunteer troops | nothing band-specific. The only line near it is < 40 as half of a power claim (`SuccessionModel.cs:448`) |
| < 25 | defection risk | counts toward the internal-war trigger and picks a side by relation. Vanilla's own clan defection does not read it: no model or patch touches clans leaving a kingdom, as design 05 §1.1 intended for Phase 1 |

And two gaps STATUS 2.3 already found by running it (**measured**): the Centralist bloc never
forms (its only source, crown patronage, does not exist), and four of eight kingdoms start with
no bloc at all.

**Proposal.**
- Make the agenda a bias on the bloc's support per decision type: Hawks toward war, Doves toward
  peace, Autonomists against policies that raise crown power, Centralists for them. Still a
  postfix on the same funnel, so no new patch.
- Treat the clans at ≥ 70 as a **Crown party**, so every court has two sides by construction and
  the loyalty the player builds is visible as a political force, not only as an exemption.

### D. A death spiral with a slow way out — inference, one live signal

**Observation.** The court's negative terms compound, and recovery is slow and fixed-rate.

The chain (constants verified in code):
1. A lost war: legitimacy −10 (`LegitimacyLostWar`).
2. Exhaustion 100 takes −20 loyalty from *every* clan (`LoyaltyWarExhaustionFactor` 0.2).
3. A new ruler starts near relation 0 with the whole court, resetting the largest positive
   loyalty term; a contested succession adds −15 legitimacy.
4. Pretender bloc, internal war, legitimacy near 0.
5. Neighbours attack the weakened realm.

The way out:
- Peace dividend: at most **+3 legitimacy a year** (`LegitimacyPeaceDividend`). From 0 back to
  the internal-war line of 35 is about 12 years, or several just wars won at +12 each.
- After a stalemate the claimant keeps the claim, and the cooldown is 365 days
  (`InternalWarCooldownDays`). A realm that still meets the three conditions can rise again every
  year.

**Signals** (measured, single observations): after one contested succession and one broken
treaty most of Battania's court sat at loyalty 0–7.5 (STATUS 2.5); on the day its civil war
ended, Sturgia and Vlandia both declared war on it (design 07 §3d); after a stalemate it met all
three trigger conditions the next day and was held only by the cooldown (design 07 §3d, second
round).

**A detail found while checking, which softens this slightly** (verified in code): the peace
dividend does not need a *year of peace*, which is what design 02 §4 says. It pays on the first
day the realm is at peace once a year has passed since the last one
(`LegitimacyRegistry.cs:155-162`); a war does not reset the timer. So a realm at war most of the
year still collects it. That makes the spiral less steep than a strict reading of the spec, and
it is itself a spec/code mismatch the lead may want resolved one way or the other.

**Proposal.**
- Mean reversion instead of a flat dividend: legitimacy drifts toward 50, faster the further
  away it is, so a crown at 5 recovers faster than one at 45.
- **Rally round the flag**: a foreign attack on a divided realm eases internal pressure, for
  example by pausing grievance accrual against the crown or granting a loyalty term while the
  realm is defending. It breaks the dogpile loop, and it is historically plausible.
- Make run 08 measure this explicitly: internal wars per realm per decade, repeat risings in
  the same realm, and whether a realm that loses a civil war is eliminated within N years.

### E. In a civil war, taking castles does not help you win

**Observation.** Internal-war exhaustion comes from the calendar and from casualties only.
Capturing a fief moves nothing, unlike a foreign war (+6 town, +3 castle lost).

**Evidence** (verified in code): `Intrigue/InternalWars.cs:884-886` adds
`ExhaustionPerDayAtWar` (0.30) to **both** sides daily; `OnMapEventEnded` adds casualty
exhaustion; `OnSettlementOwnerChanged` (`InternalWars.cs:987`) only re-syncs the rising's fief
list.

**Consequences** (inference from the constants):
- Both sides reach the stalemate line of 40 on the calendar alone by about **day 133**, so every
  civil war ends by then unless one side collapses or concedes first.
- A decisive result needs a casualty gap: about 35 points for an AI concession (75 against
  under 40), about 60 for a collapse at 100.
- Sieges matter only through the blood they cost. The three fiefs the rebels took in the live
  run (design 07 §3d) counted toward the outcome only through the casualties of taking them.
- On a crown victory the rebels keep what they took (design 07 §3a Q1), so taking castles pays
  off in the *losing* case and not in the winning one.

**Proposal.**
- Count fiefs lost into internal-war exhaustion with the same constants as Phase 1. One formula
  for "a realm losing ground", not two.
- On a crown victory, let the ruler redistribute the fiefs the rebels took. Each grant is a
  `FiefToRival` grievance for someone, so the next round of court politics starts from the war's
  outcome.

### F. Money never bites

| Price | Size | Against | Source |
|---|---|---|---|
| Tribute | 500 per 7 days, fixed | rulers holding 10⁵–10⁶ | `DiplomacyConstants.cs:1028` |
| Indemnity | 2,000–4,000 in the 2-year smoke test | ~10⁶ | design 04 §13.4, §13.7 |
| Changing sides | 2,100–45,900 | purses of 144,075 and 452,986; the half-purse rule never bound | design 07 §6 |

**Why it matters.** Gold is not scarce for AI rulers in Bannerlord, so every price in gold is
decorative on the AI side, and the player learns that money is never the constraint.
The lead already flagged the indemnity (§13.4). It is one instance of a pattern, not a single
mis-tuned constant.

**Proposal.** Price relative to the payer — a share of weekly income, or of treasury — or price
in influence, which *is* scarce. Tribute as a share of the vassal's income would make it matter
to both Hold and the patron's economy at once.

### G. The cliff at 75, and doomed links

**Observation.** Since §13.2, a victory that can take the loser's standing demands it. From
war score 75 upward the winner wants subjugation, and tribute survives only in a ten-point band
(65–75). Run 07 settled 13 of its 18 tributary pacts at 75 or more, so most of them become
subjugations.

**Why it matters.** The cliff is good drama. But the player on the losing side has to *see it
coming*, or losing independence feels like a rules surprise rather than a defeat.

**Proposal.**
- A warning line in the peace UI and the war view on the losing side: "at 75 they will demand
  your crown", with the current score against it.
- On §13.6's doomed links (a patron 1.2% stronger than its vassal, Hold target 0 on the day it
  signs, run-07 §7.1): **support option (a), a 1.25× margin** on `IsStrongEnoughToHold`.
  `SubmissionValue` already refuses a patron that is not meaningfully stronger, so the same
  concept is enforced at two strictnesses today. That is the smell `CasusBelli.Resolve` exists to
  prevent.

### H. Cognitive load, and one name used twice

**Observation.** A player meets about fifteen numbers, four of them measures of how two parties
feel about each other (relation, trust, Hold, loyalty). And "legitimacy" names two unrelated
things: a casus belli's 0–1 justification, and the crown's 0–100 pool.

**Proposal.**
- Rename the casus-belli value in the UI — *Justification* — and keep "legitimacy" for the crown.
- For each relationship meter, one line on screen answering "what does this decide?"
  (trust: will they sign; Hold: will they serve; loyalty: will they vote and fight for you).

**Stale numbers in the docs** (verified in code; the player guide is what a player reads):

| Where | Doc says | Code |
|---|---|---|
| `PLAYER-GUIDE.md:71` — Avenge a raid | 0.80 | 0.75 (`CasusBelli.cs:89`) |
| `PLAYER-GUIDE.md:73` — Espionage exposed | 0.60 | 0.85 (`CasusBelli.cs:88`) |
| `PLAYER-GUIDE.md:74` — Trade dispute | 0.40 | 0.45 (`CasusBelli.cs:92`) |
| `PLAYER-GUIDE.md:213` — voluntary submission | kneels at 55 | 50 (`AiSubmissionThreshold`, lowered in design 04 §13.5) |
| `design/01-diplomacy.md:35`, `design/05-vanilla-override.md:129` — time at war | 0.08/day | 0.30/day (`ExhaustionPerDayAtWar`; the constant's doc comment explains the change) |
| `design/01-diplomacy.md:117` — ancestral claim window | 20 years | 12 (`AncestralClaimMemoryYears`) |

CLAUDE.md §5 asks for a wrong rationale in a doc to be fixed, and the player guide rows are the
ones that mislead a player. Left unchanged by this review, since it was asked to document
rather than edit.

### I. The trust floor can veto the coalition that stops a hegemon — inference

**Observation.** A war drags a pair's trust to −35 (`TrustWarFloor`), and below −20
(`TrustFloorForPacts`) a court signs nothing but a truce. After a great war, most of the
kingdoms that would need to stand together against the winner have just fought each other.
Design 04 §12.5 already raises this as an open call.

**Proposal.** Let a high balancing pull relax the floor for **defensive pacts only**, not
alliances: the enemy of my enemy will guard my back even if I would not follow them into a war.
Symmetric, and one term in an existing gate.

### J. Espionage, before it is built

Two notes for when Phase 3 starts:
- Bands already say what a court will *do*, so `ReadCourt` is worth buying only if it sells what
  a band cannot: **the trend and a forecast** ("about 20 days to Exhausted"). Design 03 mentions
  the trend; it should be the centre of the mission, not a footnote.
- Assassination now reaches the succession and civil-war machinery. Killing a ruler can trigger
  a contested succession and a rising, which makes it far stronger than when design 03 was
  written. **Restrict it to non-rulers, or cut it** (design 03 §9 Q1).

---

## 4. Suggested order

| # | Work | Why this position |
|---|---|---|
| 1 | Run 08, with D's metrics added | already planned; the cheapest way to turn D and I from inference into fact |
| 2 | Intrigue feeding diplomacy (A) | highest value for its cost: three terms in existing valuations |
| 3 | Court and patron verbs (B), agendas that vote (C) | turns the court from a readout into a game |
| 4 | Relative prices (F), fiefs in internal-war exhaustion (E) | tuning-shaped, and each is one formula |
| 5 | UI wording and the stale docs (G's warning line, H) | small, and the player guide is actively wrong today |

## 5. Decisions this leaves for the lead

| # | Question | Recommendation |
|---|---|---|
| 1 | Should court state feed the AI's foreign policy (A)? | Yes, starting with the three hooks in A |
| 2 | Which court and patron verbs, and at what cost (B)? | Appease, patronage, per-vassal tribute |
| 3 | Should a bloc's agenda bias its vote (C)? | Yes; and add a Crown party |
| 4 | Flat peace dividend or mean reversion (D)? And should the dividend require a real year of peace, as the spec says, or any peaceful day, as the code does? | Mean reversion; then the second question matters less |
| 5 | Rally round the flag for a divided realm under attack (D)? | Yes, after run 08 confirms the spiral |
| 6 | Fiefs in internal-war exhaustion; redistribution on a crown win (E)? | Yes to both |
| 7 | Price in share-of-purse or in influence (F)? | Share of income for tribute; influence for side changes |
| 8 | Margin on `IsStrongEnoughToHold` (G, run-07 §7.1)? | 1.25× |
| 9 | Relax the pact trust floor under a balancing threat (I)? | Defensive pacts only |
| 10 | Assassination (J)? | Non-rulers only |

## 6. What this review did not do

- It did not run the game or a balance run. Findings tagged *inference* (D, I, parts of E) are
  hypotheses for run 08.
- It did not review code quality, performance, save compatibility or the Harmony patches, beyond
  what a finding needed.
- It did not read every balance run in full: run 07 in full, the others only through what
  STATUS and the design docs quote from them.
