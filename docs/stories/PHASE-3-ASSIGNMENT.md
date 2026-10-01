# Phase 3 completion - who does what

Written 2026-10-01. Stories 3.8-3.12 are in this folder. Every design decision in them was taken by
the lead on 2026-10-01; a dev who finds a question the story does not answer stops and asks, and does
not decide.

## Split

| Dev | Stories | Why |
|---|---|---|
| **A** (1M context, stronger) | **3.9, then 3.10**, then **3.12** | 3.9 and 3.10 both rewrite `AiEspionage.PlanMission`, and 3.10 builds on 3.9's band rule, so one person does them in that order. 3.10 is the risky one: it generalises the bribe-offer machinery, and it lets an AI assassination reach the player hero, so vanilla's death path must be read and run on a throwaway save. 3.12 needs the whole phase in the head and a long log to analyse |
| **B** (262k) | **3.8**, then **3.11** | Both are well bounded and mostly additive: 3.8 is a few listeners and one rule in `CanHandle` (its first sub-task is a ten-minute API check); 3.11 is one sentence of UI text and one notice. Neither touches `AiEspionage.cs`. Neither needs the whole codebase in context |

Order inside each: A does 3.9 -> 3.10; B does 3.8 -> 3.11. The two tracks do not wait on each other
until 3.12.

## Shared files - how not to collide

| File | Touched by | Rule |
|---|---|---|
| `Espionage/Missions.cs` | A (3.10: `Resolve`, the offer machinery) and B (3.11: the resolution notice) | **B changes only the notice text and the launch log line.** A owns `Resolve`'s structure. Whoever merges second rebases; the overlap is a few lines |
| `Espionage/SpyNetworks.cs` | B only (3.8) | |
| `Espionage/AiEspionage.cs`, `EspionageConstants.cs` | A only | |
| `tools/analyse-log.py` | B (3.8 R8), A (3.12 ST-2, later) | A starts 3.12 after B has merged 3.8 |
| `docs/STATUS.md`, `TODO.md`, design 03 | both | **Do not edit these while the story is in progress.** Put the verification table in the story file's own section "Verification", and update STATUS.md / design 03 / TODO.md once, in the story's last sub-task, after rebasing on `development` |

## Rules for both (CLAUDE.md applies in full; these are the ones that bite here)

- One branch per story off `development`: `feature/3.8-handler-stays-posted`, etc. Merge into
  `development` only after the story's acceptance criteria are marked verified live, compile only, or
  not verified. Never claim an AC verified that was not run.
- `scripts/check-save-ids.ps1` must pass and must show **no change**. None of these stories adds a
  save id.
- No new Harmony patch. Story 3.8 says so in terms.
- Deploy needs the game closed; never force-kill it (CLAUDE.md §1). Test levers need cheat mode on
  (CLAUDE.md §2).
- A changed DLL shows a "Mod change detected" prompt at launch: it must be dismissed or the bridge
  never connects (CLAUDE.md §1).
- This machine is on v1.5.3; players are on v1.4.8. Build for any release against the v1.4.8
  references (`scripts/compile-check.sh`); say in each story's verification which build ran.
- Developer 262k: if a task needs a file larger than ~1,500 lines in full (`DebugCommands.cs` is
  3,800), read the range with grep first; do not load it whole.

## What to hand each dev

- **A:** [3.9](3.9-ai-reads-courts-as-bands.md), [3.10](3.10-player-house-exemptions.md), later
  [3.12](3.12-phase-3-acceptance.md). Read first: design 03 §6, §9, §10 (3.5 and 3.6),
  [balance/run-09.md](../balance/run-09.md) §4, `Espionage/AiEspionage.cs`, `Espionage/Missions.cs`.
- **B:** [3.8](3.8-handler-stays-posted.md), [3.11](3.11-odds-shown-are-odds-rolled.md). Read first:
  [balance/run-09.md](../balance/run-09.md) §4, `Espionage/SpyNetworks.cs`,
  `GameModels/ModClanPoliticsModel.cs`.
