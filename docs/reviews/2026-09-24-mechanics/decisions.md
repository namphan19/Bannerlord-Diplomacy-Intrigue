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

Brought up to date on 2026-09-27, when this review was merged into `development`. Rows decided
after 2026-09-24 point to the design doc that now holds them.

| Id | Decision | Date | Source |
|---|---|---|---|
| **S-1** | Political acts (diplomacy, court, espionage) award skill experience; political play is a character build | 2026-09-24 | [03](03-skills-and-traits.md) §2, §3.1. **Built** as Phase 2.8 Statecraft, S0-S2 ([design/08](../../design/08-statecraft.md)) |
| **S-2** | Personality traits shape AI courts (agenda baseline) and a ruler's reputation (acts move traits) | 2026-09-24 | [03](03-skills-and-traits.md) §2, §3.2. **Not built**: design/08's S5, "its own plan after S3" (design/08 D8) |
| **S-3** | No cap on skill effects to protect the AI from a stronger player: AI heroes level and gain skills as the player does | 2026-09-24 | [03](03-skills-and-traits.md) §2. Carried into design/08, whose terms are bounded only by the project's standing rule that no single term decides alone (design/08 §2 rule 5), for AI and player alike |
| **R-2** | Court and patron verbs: make amends, offices and patronage, tribute per vassal, priced in influence and gold scaled by skill | 2026-09-26 | **Built** as Phase 2.9 C1-C3 ([design/09](../../design/09-court-verbs.md)) |
| **R-10 / U-4** | Assassination kept as specified; the AI assassinates only at war, and only a commander in the field, never a ruler or the player's house | 2026-09-25 | [design/03 §9](../../design/03-espionage.md), decisions 1 and 11 |
| **U-5** | An exposed handler is captured, never killed, and can be ransomed | 2026-09-25 | design/03 §9, decision 3 |
| **U-7** | The player can be a target of AI espionage, clearly telegraphed | 2026-09-25 | design/03 §9, decisions 2 and 12 |
| **U-2, U-8, U-9** | Deferred: petitions, secession's trigger and the loser's fate are not built in this phase; secession stays unbuilt | 2026-09-27 | — |

## Superseded

| Id | Recommendation | Overruled by |
|---|---|---|
| S-3a | Cap skill effects at ~10–15%, with diminishing returns, as a tie-break only | S-3 |
| R-7 | Tribute priced as a share of income; side changes priced in influence | Side changes are bought with gold (design/07 §6, 2026-09-24); per-vassal tribute is set at fixed levels of 250 / 500 / 1,000 (design/09 D14) |
| R-8 | A 1.25× margin on `IsStrongEnoughToHold` | No margin: balance run 08 found no link doomed at signing (TODO.md, "Answered") |
| U-3 | Espionage v1 with four missions | Phase 3 was specified and built with all eight (design/03) |
| U-6 | A network loses half when war is declared | Networks survive a war, growing at half rate (design/03 §9, decision 4) |
| R-3 | A bloc's agenda biases its vote; a Crown party of the clans at loyalty ≥ 70 | **No** (2026-09-27): C2's seats already give the Centralists a source, and the bloc vote already follows its leader's preference; agenda bias waits until a run measures bloc votes |
| R-5 | Rally round the flag eases a divided realm's internal pressure | **No** (2026-09-27): civil war is already too rare (run 08 saw none); this would make it rarer |

## Overtaken

| Id | Question | What happened |
|---|---|---|
| U-1 | A Phase 2.8 of court verbs, then a wiring pass, before Phase 3? | The lead started Phase 3 on 2026-09-25; 2.8 became Statecraft and the court verbs were built afterwards as 2.9. The wiring pass (R-1) is still open |

## Open

Nothing. Every R- and U- question was answered by 2026-09-27: the rows are under Decided,
Superseded or Overtaken above.
