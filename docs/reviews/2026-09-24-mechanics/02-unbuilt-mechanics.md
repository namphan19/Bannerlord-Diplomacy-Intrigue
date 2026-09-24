# 02 — The mechanics not yet built

> **Part of** [the 2026-09-24 mechanics review](README.md). **Decisions** live in
> [decisions.md](decisions.md) as `U-1` …; this file argues for them and does not record their
> status. **Code paths** are relative to `src/DiplomacyIntrigue/`.

What is left to build across Phases 2–4, read as a game designer: which pieces are worth
building, in what shape, and in what order. It follows from
[01-built-mechanics-review.md](01-built-mechanics-review.md) and reuses its finding letters
(A–J). Discussed with the lead on 2026-09-24; **none of the questions in §7 has been answered
yet.**

---

## 1. Inventory

| Area | Piece | State | Source |
|---|---|---|---|
| **Phase 3** | Networks, eight missions, counter-intelligence, exposure as diplomacy, UI | spec only, four open questions | design 03 |
| **Phase 2 remnants** | Secession, the last rung of the internal-politics ladder | not built | design 07 §2 |
| | The loser's fate after an internal war (exile, execution, demotion) | deferred from v1 | design 07 §3a Q1 |
| | Neutrality in an internal war | deferred | design 07 §3a Q2 |
| | Grievance sources `PolicyAgainstAgenda`, `PeaceWhileWinning`, `RequestRefused` | not wired; the last "needs a request mechanism that does not exist" | `Intrigue/GrievanceSources.cs:19-20` |
| | A crown-legitimacy term in Hold | promised, not built (`Diplomacy/Hegemony.cs` reads no legitimacy) | design 04 §1.2, ROADMAP Phase 2 hooks |
| **Parked** | Titles (Emperor, Khagan) | waiting on 2.4, which is done | ROADMAP Phase 2 |
| | Vassal-party summons | parked as "least load-bearing" | design 04 §5.2, §11 |
| **Phase 4** | Cross-pillar wiring pass, balance, localisation, release | not started | ROADMAP Phase 4 |

---

## 2. The central gap: Phase 2 cannot meet its own acceptance line

ROADMAP's acceptance for Phase 2 ends: *"The player can survive it by managing grievances."*
The player has **no action that manages a grievance** (finding B). The line cannot be met by
anything built so far.

Phase 3 as specified sharpens the asymmetry. It gives the player `BribeLord` (lower a *foreign*
lord's loyalty) and `ForgeLetters` (manufacture a grievance in a *foreign* court), while the
player still cannot soothe one grievance in their own. The tools to break someone else's house
would arrive before the tools to hold one's own together.

**Proposal: one set of court verbs, pointed both ways.** The domestic half is Phase 2's missing
piece; the foreign half is Phase 3. They share resolvers and units, so espionage becomes the
outward half of a system that already exists rather than a third game — the project's
one-resolver-per-concept rule applied to verbs.

| Domestic (proposed Phase 2.8) | Foreign (Phase 3) | Acts on |
|---|---|---|
| **Appease** — reduce a grievance | **ForgeLetters** — create one | the grievance ledger |
| **Patronage** — an office, a stipend | **BribeLord** — buy disloyalty | loyalty |
| **Petitions** — hear what a house wants | **ReadCourt** — learn what a court plans | information |

The per-vassal tribute lever (R-2) belongs with the domestic half: it is the patron's verb.

---

## 3. Petitions — the single largest proposal

AI houses **ask their ruler for things**, and each request is a decision with a price on both
answers.

| A house asks for | Granting costs | Refusing costs |
|---|---|---|
| A fief just taken | the fief; `FiefToRival` for the other houses that wanted it | `RequestRefused` |
| A war on a realm it resents | a war, with its casus belli and influence cost | `RequestRefused`; Hawks lean toward the pretender |
| Peace while it is exhausted | a peace, possibly on poor terms; `PeaceWhileWinning` for Hawks | `RequestRefused`; Doves lean toward the pretender |
| A relative ransomed | gold or a prisoner | `RelativeInCaptivity` keeps renewing |

Why this is the proposal with the most reach:
- It wires `RequestRefused`, which exists as a grievance type with nothing that raises it.
- It gives agendas something to *do* (finding C): Hawks ask for wars, Doves for peace.
- It turns the court from a readout into decisions, which is what finding B asks for.
- It runs the other way for a player who serves a king: **the player petitions an AI ruler**,
  and the AI answers by the same rule. No "is this the player" argument (CLAUDE.md §3).

**The experience it is for** — a scene, not a spec:

> Spring 1102. You rule Battania at legitimacy 38, three points above the line of 35 below which
> a pretender's party may take up arms (`InternalWarLegitimacy`). The fen Morcar (Hawks) ask for Llanoc Hen, the castle you have just taken; the
> fen Giall wanted it too. Grant it, and fen Giall takes a grievance of 8 and turns sullen.
> Refuse, and fen Morcar takes a grievance of 2 and the Hawks start to drift toward the pretender.
> That week counter-intelligence reports *"signs of a Vlandian network at court."* Thirty days
> later a forged letter surfaces…

That is "being a king is a political problem" (ROADMAP Phase 2's playable target) as a moment
of play rather than a number on a screen.

**Not designed yet:** how often petitions arrive, how the AI ruler weighs them, how many can be
pending, and what an unanswered petition costs. Those are the spec's job once U-2 is decided.

---

## 4. Espionage (Phase 3) — corrections to design 03 before it is built

### 4.1 Three problems in the formulas

**Networks grow far faster than the spec intends, because gold is not scarce** (finding F).
Design 03 §1: `weeklyGrowth = (goldSpent / 2000) × (1 + roguery/200) − CI × 0.08 − 0.7`, plus
decay 0.1/day. Assume a ruler spending 5% of a 452,986-denar purse a week (22,650; the purse is
Muinser's, measured in design 07 §6) against the formula's base counter-intelligence of 10:

| Handler Roguery | Net growth per week | Weeks to 70 (the assassination threshold) |
|---|---|---|
| 0 | about +9 | about 8 |
| 100 | about +15 | about 5 |

Design 03 says *"a war you planned three years ago goes better than one you improvised."* At
these rates a network is built in two months. *Inference from the spec's formula; nothing is
built.*
**Proposal:** diminishing returns on gold (for example on its square root), and make the
**handler's time** — a companion assigned to one realm — the real bottleneck.

**The skill terms assume a 0–100 scale; Bannerlord's runs to about 300.** The success chance
adds `0.004 × (Roguery + Charm) / 2`. A handler at Roguery 250 and Charm 150 adds **+0.8**, on
top of a base of 0.15 and up to +0.5 from the network. Skill swamps every other term and the
chance sits at the 0.95 clamp. **Proposal:** normalise the skill terms to the real scale when
the formula is rewritten (see [03](03-skills-and-traits.md) §4).

**`ReadCourt` sells what a band already says.** Bands sit on behavioural thresholds (01 §2.5),
so an exact figure adds little. **Proposal:** sell what a band cannot:
- the **trend and a forecast** ("about 20 days to Exhausted");
- the court's **war plans**: the realm its AI currently ranks first as a war target, read from
  the war valuation that already exists (`diplomacy.war_value` prints it). The player then sees
  the number the AI used, which keeps the project's transparency rule.

### 4.2 Design 03 §9's open questions

| Question | Recommendation | Why |
|---|---|---|
| Assassination at all? (U-4) | **Non-rulers only, or cut from v1** | killing a ruler now drives a contested succession and possibly a rising; far stronger than when design 03 was written |
| Can the player be a target? (U-7) | **Yes, with a warning first** ("signs of a network") | without the warning it plays as a loss to an unseen dice roll |
| Handler risk? (U-5) | **Captured on exposure, not killed** | Bannerlord already has capture and ransom: personal stakes without permanent loss |
| Do networks survive a war? (U-6) | **Lose half on a declaration** | between design 03's two options; preparing before a war still pays, and a war still costs |

### 4.3 Scope for a first version (U-3)

Build the four missions that reach systems already built:

| Keep for v1 | Reaches |
|---|---|
| `ReadCourt` | Phase 1 exhaustion, Phase 2 blocs, the war valuation |
| `ForgeLetters` | the grievance ledger |
| `BribeLord` | loyalty, and side-choice in an internal war |
| `SpreadDissent` | settlement loyalty |

Defer: `StealTreasury` (gold is not scarce, so the prize is worth little), `ScoutArmies`
(the vanilla map already shows much of it), `SabotageGarrison` (it needs timing against a siege
to matter). `Assassinate` is U-4.

---

## 5. Phase 2 remnants, tied to the review's findings

- **Secession as the exit from repeated risings** (finding D). Proposal: when the **same
  claimant** has fought the crown to a stalemate twice, the third rising secedes instead of
  fighting inside the realm. Secession then stops being a random end state and becomes the
  logical result of a realm that cannot settle itself, and it breaks the yearly civil-war loop
  D describes. (U-8)
- **The loser's fate is the most dramatic moment of an internal war**; give the victor the
  choice, each option with a price (U-9):
  - *Clemency*: legitimacy up, but the pretender remains;
  - *Exile*: the house leaves the realm, likely to serve a rival;
  - *Execution*: every house takes a "tyranny" grievance.
  An AI victor chooses by the same rule. Magnitudes are placeholders, not worked out.
- **Neutrality stays deferred.** A third map faction costs a great deal of engineering (design
  07 §3a Q2) for little play value.
- **Hold's legitimacy term**: cheap, and belongs in the same wiring pass as finding A.
- **Titles**: worth building only with a real effect, for example a legitimacy floor for a
  hegemon holding several vassals. Low priority.
- **Summons**: stay parked; per-vassal tribute (R-2) gives the patron a lever with far less
  intrusion into party AI.

---

## 6. Proposed order

| Step | Work | Why here |
|---|---|---|
| **2.8** | Court verbs: petitions, appease, patronage, per-vassal tribute; agendas that vote (C); traits (decided, [03](03-skills-and-traits.md)) | Phase 2's acceptance line needs it |
| **2.9** | Wiring: court into AI foreign policy (A), legitimacy into Hold | pulled forward from Phase 4: without it `ForgeLetters` and `BribeLord` move a number that barely changes what any AI does |
| — | **Run 08**, measuring internal wars and the spiral (D) as well as design 04 §13.7 | turns D and I from inference into evidence |
| **3 (v1)** | Networks, the four missions of §4.3, exposure | on top of verbs that already exist at home |
| later | Remaining missions, secession (U-8), the loser's fate (U-9), titles | each depends on something above |

This is a proposal. The lead has not decided the order (U-1).

## 7. Questions for the lead

U-1 to U-9 in [decisions.md](decisions.md). **U-1 to U-4 were put to the lead in the discussion
of 2026-09-24 and are awaiting an answer**; U-5 to U-9 are recommendations recorded here that
have not yet been asked as questions.
