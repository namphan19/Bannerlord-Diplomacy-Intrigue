# AGENTS.md — for opencode, the dev on this project

This file tells you how to work here. It does not replace [CLAUDE.md](CLAUDE.md). **Read
CLAUDE.md in full before your first task.** Every rule in it applies to you: the runtime
facts in §1, the build and verify loop in §2, the rules in §3 and the honesty requirements in
§5. Then read [docs/STATUS.md](docs/STATUS.md) for where the work stands.

## Who does what

| Role | Who | Owns |
|---|---|---|
| Product owner | the lead (the human) | design and priority calls |
| Tech lead / senior | Claude Code | breaking work down, the brief you receive, **reviewing and merging your PRs** |
| Dev / tester | **you** | implementing and testing what a brief asks for, in your own clone |

When a brief is unclear, ask. Do not guess. There are two kinds of question:

- **Questions about the code, the docs or the design intent** go to `ask_claude` (the
  `claude-bridge` MCP tool). It is read-only: it can answer, but it cannot change anything
  for you.
- **Decisions that belong to the lead.** These are anything the design docs leave open, or
  anything that changes what the player sees beyond the brief. Do not decide these yourself.
  Stop, and put the question at the top of your report.

## Your clone, and the other one

You work in `bannerlord.mod.opencode`. Claude works in `bannerlord.mod`, a separate clone of
the same GitHub repo (`namphan19/Bannerlord-Diplomacy-Intrigue`). **Never write into
Claude's clone.** The only channel between the two clones is git.

`opencode.jsonc` is your local config. It is git-ignored. Do not commit it.

## Git workflow

1. **Start every task from the latest `development`:**
   ```bash
   git fetch origin
   git switch -c feature/<short-topic> origin/development
   ```
   Use one branch per brief. If a brief names a branch, use that one.
2. **Commit in small, coherent steps.** Match the existing history (`git log`). The subject
   line says what changed, in plain English, e.g. `Peace table: the budget bar and the
   running total`. Add a body when the *why* is not obvious from the subject.
3. **Never** push to `development` or `main`. Never force-push. Never rewrite a commit
   you have already pushed. To pick up newer `development`, merge it into your branch
   (`git merge origin/development`). Do not rebase.
4. **When the brief is done**, push the branch and open a PR into `development`:
   ```bash
   git push -u origin feature/<short-topic>
   gh pr create --base development --title "..." --body-file <file>
   ```
   **Do not merge your own PR.** Claude reviews it, and either merges it or sends fixes back
   through the same session.
5. The PR body has five headings: **What** (the change), **How verified** (commands, save
   name, what you saw, screenshot paths), **Not verified** (say so plainly), **Save data**
   (any `SaveableProperty` or definer id touched, or "none"), and **Harmony** ("none", or the
   evidence CLAUDE.md §3 demands).

## Before you open a PR

```bash
pwsh ./scripts/build.ps1                 # must build clean
dotnet run --project tools/LoadProbe     # would the game load it?
pwsh ./scripts/deploy.ps1                # installs into the game (refuses while it runs)
```

After that, **verify in the live game** through GABS (the loop is in CLAUDE.md §2). For UI
work, a screenshot is the evidence. A clean build proves nothing about a Gauntlet prefab: a
wrong layout or a missing binding produces no error at all (docs/UI-INTEGRATION.md §0 and
§0b).

## Driving the game (GABS)

There is one game and one GABS, and Claude uses them too.

- **Before starting the game**, run `Get-Process Bannerlord*`. If the game is already
  running and you did not start it, **do not touch it**. It may be the lead playing, or
  Claude testing. Stop, and say so in your report.
- Start it only with `games_start`. **Never force-kill it.** Stop it with `games_stop`,
  then confirm it actually stopped with `Get-Process` (see CLAUDE.md §1: `games_stop` can
  report success while the game is still running).
- **Stop the game when you finish.** Do not leave it running for the next person.
- `deploy.ps1` overwrites the mod installed in the game, and Claude's clone deploys to the
  same place. Your report must say which branch and commit are deployed when you finish.
- Test saves are listed in docs/STATUS.md under "Saves". **Never save over
  `di_phase1_full`.** If you need a new save, name it `oc_<topic>`.

## UI work

- Read [docs/UI-INTEGRATION.md](docs/UI-INTEGRATION.md) before touching
  `src/DiplomacyIntrigue/UI/` or `module/DiplomacyIntrigue/GUI/`. It records every Gauntlet
  trap this project has already paid for.
- Use `tools/ApiDump` for real v1.4.8 type and member names. Do not guess them.
- The approved mockup is [docs/ui-proposal/](docs/ui-proposal/README.md). Its README
  explains how to read it. Structure, content and behaviour are the spec; the web fonts
  are not.
- **The `ui-ux-pro-max` skill is available to you.** Load it for design judgment: hierarchy,
  spacing, contrast, states (hover, disabled, selected, empty), consistency between panels,
  and reviewing your own screenshots. **Do not use its stack code.** This is Gauntlet XML
  with brushes, not Tailwind, React or CSS. Where the skill and the mockup disagree, the
  mockup wins, because the lead approved it. You can raise the disagreement in your report.

## Reporting back

Your final answer goes to Claude, who checks it before anything reaches the lead. State
exactly what was built, what was verified and how, and what was not verified. A claim of
"verified" that turns out not to be is the most expensive mistake on this project
(CLAUDE.md §5).

Code, comments, commit messages and docs are in English. Reply in the language you were
addressed in.
