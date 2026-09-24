# Mechanics review, 2026-09-24

A game-design review of Diplomacy & Intrigue, done with the lead (product owner) on 2026-09-24,
with Claude as game designer. It covers the mechanics already built, the ones not yet built,
and how hero skills and personality traits should enter the mod.

**It changed no code, constant or design doc.** Three decisions came out of it (S-1 to S-3);
everything else is a recommendation waiting on the lead.

## Reading order

| File | What it holds | Read it when |
|---|---|---|
| [decisions.md](decisions.md) | **Every decision and open question, with its status.** The only place status is kept | always first: it says what is settled and what must not be built yet |
| [01-built-mechanics-review.md](01-built-mechanics-review.md) | What to keep, and findings A–J on the built mechanics, each with evidence | before touching diplomacy, hegemony or the court |
| [02-unbuilt-mechanics.md](02-unbuilt-mechanics.md) | Phase 2 remnants, Phase 3 espionage, petitions, a proposed order | before specifying or building anything not yet built |
| [03-skills-and-traits.md](03-skills-and-traits.md) | How skills reach the mod today (they do not, directly), decisions S-1 to S-3, and the design direction | before adding any skill, perk or trait effect |

## At a glance

- **Decided:** political acts award skill experience (S-1); traits shape AI courts and a
  ruler's reputation (S-2); no cap on skill effects to protect the AI (S-3).
- **The two findings that matter most:** the court never feeds back into the AI's foreign
  policy (R-1), and the player has almost no verbs in the court (R-2) — which also means Phase
  2's own acceptance line, *"the player can survive it by managing grievances"*, cannot be met
  yet (02 §2).
- **Largest proposal:** petitions (U-2), and one set of court verbs pointed both ways, with
  espionage as the outward half (02 §2).

## Conventions

- **Ids.** Findings are letters A–J (01). Decisions are `R-n` (from 01), `U-n` (from 02) and
  `S-n` (from 03). Other files refer to decisions by id and never restate their status.
- **Confidence tags**, used in 01 and 03:
  - *verified in code*: read in the source on 2026-09-24, with a file reference;
  - *measured*: observed in a live or balance run, cited;
  - *inference*: follows from formulas and constants, not observed; a hypothesis for run 08.
- **Code paths** are relative to `src/DiplomacyIntrigue/`. Line numbers are as of branch
  `review/game-mechanics` on 2026-09-24 and will drift; search for the named symbol if a line
  no longer matches.
- **Scope of the evidence.** Nothing was run in the game for this review. Figures from runs are
  the runs' own (`docs/balance/`, `docs/STATUS.md`, the design docs).

## Keeping it current

This folder is a point-in-time record, like a balance-run report. When the lead decides
something, update its row in [decisions.md](decisions.md) and nothing else. When a decided item
is built, its rules belong in the design doc it extends (`docs/design/`); the register row then
points there. Do not edit findings after the fact to match later code; write a new review
instead.
