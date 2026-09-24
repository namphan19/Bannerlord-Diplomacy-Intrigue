# Decision register — 2026-09-24 mechanics review

> **Part of** [the 2026-09-24 mechanics review](README.md). This file is the **only** place the
> status of these decisions is recorded. The other files argue for them and link here.

## Rules for agents

- **Decided** means the lead has chosen a direction. It may be specified and built, following
  the file named in *Source*. It does **not** fix details the source leaves open, or an order of
  work.
- **Open** means nobody may build it on the strength of the recommendation alone. Ask the lead.
- **Superseded** means a recommendation the lead overruled. Do not reintroduce it without new
  evidence and a new decision.
- When the lead decides something, change its row here: status, date, the choice in one line.
  Nothing else in this folder needs editing.
- When a decided item is built, its rules move into the relevant design doc (`docs/design/`),
  which remains the spec; the row here gets a pointer to it.

## Decided

| Id | Decision | Date | Source |
|---|---|---|---|
| **S-1** | Political acts (diplomacy, court, espionage) award skill experience; political play is a character build | 2026-09-24 | [03](03-skills-and-traits.md) §2, §3.1 |
| **S-2** | Personality traits shape AI courts (agenda baseline) and a ruler's reputation (acts move traits) | 2026-09-24 | [03](03-skills-and-traits.md) §2, §3.2 |
| **S-3** | No cap on skill effects to protect the AI from a stronger player: AI heroes level and gain skills as the player does | 2026-09-24 | [03](03-skills-and-traits.md) §2 |

## Superseded

| Id | Recommendation | Overruled by |
|---|---|---|
| S-3a | Cap skill effects at ~10–15%, with diminishing returns, as a tie-break only | S-3 |

## Open — from the review of the built mechanics ([01](01-built-mechanics-review.md))

| Id | Question | Recommendation | Finding |
|---|---|---|---|
| **R-1** | Should court state feed the AI's foreign policy? | Yes: Doves share lowers the peace threshold; a divided or illegitimate neighbour is a war-valuation term; a standing pretender grants rivals `SupportClaimant` | A |
| **R-2** | Which court and patron verbs, at what cost? | Appease, patronage, per-vassal tribute | B |
| **R-3** | Should a bloc's agenda bias its vote? | Yes; and add a Crown party of the clans at loyalty ≥ 70 | C |
| **R-4** | Flat peace dividend or mean reversion toward 50? And should the dividend need a real year of peace (the spec) or any peaceful day after a year (the code)? | Mean reversion; the second question then matters less | D |
| **R-5** | Rally round the flag: a foreign attack on a divided realm eases internal pressure? | Yes, once run 08 confirms the spiral | D |
| **R-6** | Fiefs lost count toward internal-war exhaustion; the ruler redistributes rebel-taken fiefs after a crown win? | Yes to both | E |
| **R-7** | Price in a share of the payer's income or treasury, or in influence? | Share of income for tribute; influence for side changes | F |
| **R-8** | A margin on `IsStrongEnoughToHold`? (also TODO.md decision 1) | 1.25× | G |
| **R-9** | Relax the pact trust floor under a balancing threat? | Defensive pacts only, not alliances | I |
| **R-10** | Assassination? | Same question as U-4 | J |

## Open — from the discussion of the unbuilt mechanics ([02](02-unbuilt-mechanics.md))

U-1 to U-4 were put to the lead on 2026-09-24 and are **awaiting an answer**. U-5 to U-9 are
recommendations recorded in 02 and not yet asked as questions.

| Id | Question | Recommendation | Source |
|---|---|---|---|
| **U-1** | Insert a Phase 2.8, court verbs, before Phase 3? | Yes; and a 2.9 wiring pass (R-1) before Phase 3 too | 02 §2, §6 |
| **U-2** | Petitions — houses ask the ruler, and the player can petition an AI ruler — in scope? | Yes; the single largest proposal | 02 §3 |
| **U-3** | Espionage v1: four missions or all eight? | Four: `ReadCourt`, `ForgeLetters`, `BribeLord`, `SpreadDissent` | 02 §4.3 |
| **U-4** | Assassination: cut, non-rulers only, or as specified? | Non-rulers only, or cut from v1 | 02 §4.2 |
| **U-5** | A handler whose operation is exposed: captured, killed, or safe? | Captured | 02 §4.2 |
| **U-6** | What happens to a network when war is declared? | Loses half | 02 §4.2 |
| **U-7** | Can the player be the target of AI espionage? | Yes, with a warning first | 02 §4.2 |
| **U-8** | What triggers secession? | A third rising by a claimant who has twice fought the crown to a stalemate | 02 §5 |
| **U-9** | The loser's fate after an internal war | The victor chooses clemency, exile or execution, each with a price; the AI chooses by the same rule | 02 §5 |
