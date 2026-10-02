# Testing guide for agents without the game

Some agents working on this repo have no Bannerlord install, no GABS bridge and no display
(cloud sessions, Linux boxes, sandboxes). They cannot launch the game, load a save or run a
`diplomacy.*` command. This file says what such an agent **can** prove, what it **cannot**,
and how to hand the rest to a session that has the game.

The rule that governs all of it is CLAUDE.md §5: never claim something is verified when it is
not. "It compiles" is not "it works".

---

## 1. What you can verify without the game

| Check | Command | What a pass means |
|---|---|---|
| Compiles against the real v1.4.8 API | `scripts/compile-check.sh` | Names and signatures exist. Nothing has run. |
| Save-data rules | `pwsh ./scripts/check-save-ids.ps1` | No id used twice; every class, enum and container is in `ModSaveDefiner`. It cannot see a renumbering - read the diff of every `Saveable` line yourself. |
| The real API surface | `BANNERLORD_GAME_DIR=<compile-check stand-in folder> dotnet run --project tools/ApiDump -- "TypeName"` | You are writing against the real v1.4.8 types, not guessing. |
| Code review by reading | your own eyes | See §2. |

On Linux/cloud the .NET SDK comes from `apt-get install dotnet-sdk-8.0`; Microsoft's install
script is blocked.

**Not available to you:** `deploy.ps1`, `play.ps1`, `LoadProbe`, `DumpProbe`, any
`mcp__gabs__*` tool. They need the real install.

## 2. What you must check by reading, because nothing will run it

These are the failures that a compile does not catch and a live session finds late:

1. **No throw crosses the engine boundary** (CLAUDE.md §3). Every campaign event handler,
   `SubModule` hook and Harmony patch has `try/catch`, logs, continues. A new handler without
   one is a bug even if it never throws today.
2. **Save ids.** A new `[SaveableProperty]` takes the next free id listed in CLAUDE.md §3 and
   the number is then frozen. A new savable type needs a class definition **and** a container
   definition in `ModSaveDefiner`; a missing container crashes on save.
3. **No event-order assumption.** Campaign listeners do not fire in registration order.
4. **Same rules for the AI and the player.** No "is this the player" argument in the registries.
5. **One resolver per concept.** If you derive a value that something else already derives,
   call the existing one.
6. **Derived, not stored.** Hegemon status is never a stored flag.
7. **Reflection-only calls.** A method whose signature changed between v1.4.8 and v1.5.3
   (`Kingdom.InitializeKingdom`) is called by reflection. A direct call to a vanilla method
   you have not confirmed with ApiDump is a risk.
8. **Cheat gate.** A new `diplomacy.test_*` lever must refuse without cheat mode, like the rest.

Write down which of these you checked in your hand-off. Do not write "reviewed" with no list.

## 3. What you cannot verify, and must say so

Say **"not run in the game"** for each of these in your report, by name:

- That the feature behaves as designed (numbers, AI choices, UI).
- That the DLL loads (the `net472` target, `SubModule.xml` and dependencies are the usual
  causes of a silent load failure; only LoadProbe on a real install sees it).
- Anything about timing: the clock cannot be advanced by `tick_days`/`ai_week`, so expiry,
  ageing and long-run balance are unmeasured by those commands anyway.
- UI layout, including anything below a scroll fold.
- That a Harmony patch actually applied. The mod log line `Harmony patched N methods:` is
  the only proof.

## 4. Handing the live test to a session that has the game

You cannot run it, but you can make it cheap for whoever can. Put a **test request** at the
end of your change, in the PR description or in `docs/STATUS.md` under the current part. It
must be runnable by someone who has not read your code.

Use this shape:

```
### Live test request: <feature name>

Build: <commit hash>. Save: <di_phase1_full | di_treaty_test | di_phase0_test | ...>.
Needs NavalDLC loaded: yes/no.

Staging (cheat mode on first):
  1. diplomacy.<lever> <args>      -> expect: <exact text or state>
  2. diplomacy.<lever> <args>      -> expect: ...

Act:
  <the one thing under test, as a command or a UI path>

Pass if:
  - <observable 1: a command's output, a log line, a screenshot>
  - <observable 2>

Fail if / look for:
  - <the specific wrong thing you fear, e.g. NullReference in the mod log>

Not covered by this request: <what stays unverified>
```

Rules for a good request:

- **Use the existing levers** (CLAUDE.md §2 lists them). If staging the state needs a lever
  that does not exist, adding that lever is part of your change.
- **Every step has an expected result.** "Run X" is not a step; "run X, expect `Y`" is.
- **Name the log line** you will look for, and make sure your code writes it.
- **Say what state to reach**, not the history of how. `sign_treaty Vassalage` makes a row
  with no Hold; if that matters, say to use `test_set_hold`.
- **Say what would be a false pass.** A debug command that drives only part of a tick looks
  right and is wrong (CLAUDE.md §1).

## 5. If you do get a game session

Everything above is superseded by CLAUDE.md §2 ("Verifying in the live game") and the
`bannerlord-gabs` skill. The short version:

1. If the game is running and you did not start it, **ask** before stopping it. Never kill it.
2. `games_stop` (only your own session) -> `deploy.ps1` -> `games_start`. Launch through
   `games_start`, never by hand.
3. A changed DLL shows a "Mod change detected" prompt that blocks startup silently. Run
   `scripts/dismiss-mod-change-prompt.ps1` while the game starts. Symptom: `games_connect`
   times out while the process is alive.
4. `load_save` (main menu only) -> `set_cheat_mode true` -> drive with `run_command`.
5. Read the mod log (`Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\`).
6. A hang with no log and no bridge is probably a crash dialog, not a stall. See CLAUDE.md §1
   for `DumpProbe`, and answer the dialog **No**, never Yes.
7. This machine's game may be v1.5.3 while players are on v1.4.8. State which one you tested.

## 6. Long runs (balance runs, hours of game time)

A long run is where an agent most often stalls without knowing it. The game stops advancing
and the log goes quiet, with no error anywhere. **If you cannot clear the stops below, do not
start a long run.** Hand it over as a live test request (§4) to a session that can, and say
which stop you could not handle.

### The stops, and how each is cleared

| What you see | What it is | How to clear it |
|---|---|---|
| Log quiet, bridge alive | An inquiry addressed to the player stops the clock | `core.check_blockers` (`inquiry_active`), read with `ui/get_inquiry`, answer with `ui/answer_inquiry`. The key is `affirmative`; a wrong key silently answers **No**. |
| Same, with a scene notification ("... joined the Kingdom of ...") | `ChangeKingdomAction` raises it; `save_game` then fails silently | `ui/answer_inquiry` dismisses it, but it answers a queued inquiry **first**, even one hidden behind it. Read `get_inquiry` before you call it. |
| `map_conversation_overlay` blocker | A conversation left open | `ui/click_widget { widgetId: "ContinueButton" }` |
| `mission_active` blocker | A mission (battle, scene) | `mission/leave` |
| `games_connect` times out, process alive, window title "Mod change detected" | The DLL changed since the last run | `scripts/dismiss-mod-change-prompt.ps1`, run **while** the game starts. |
| Log quiet **and** every main-thread tool times out, process `Responding` at ~0% CPU | A crash dialog (`#32770`, title `*_*`), not a stall | Read the exception first: `dotnet run --project tools/DumpProbe -- --pid <pid>`. Then post `IDNO` (7) to the dialog. **Never Yes**: it uploads files to TaleWorlds. |
| Game over screen | The player hero died of old age (illness, then death) | `diplomacy.test_set_player_age` resets age **and** illness. Test saves only. |
| `games_stop` said success, process still there | BLSE tracks the wrong pid | `Get-Process Bannerlord*`. Do not kill it; ask the lead. |

Capabilities these need: the GABS bridge tools (`ui/*`, `core/*`), and for the Win32 dialogs a
desktop session where `SendKeys` or `PostMessage` reach the window. **A machine with no
display attached (Windows reports a 640x480 `WinDisc` screen) delivers no real input**, so
the mod-change prompt and the crash dialog cannot be answered there.

### Rules for a monitored run

- **Poll, do not sleep.** Check the mod log every few minutes, and `check_blockers` the first
  time it goes quiet. A silent log is a stop, not progress.
- **Watch for a stop between check-ins, not just at the end.** A run stalled for an hour at
  its first inquiry has produced nothing, and the log will not say so.
- **Speed.** `diplomacy.test_set_speed <1-50>` buys wall clock (default multiplier 4 is ~3
  game days per real minute; 50 is ~33). Use it to *reach* a state, not to measure how fast
  one arrives.
- **Never save over a test save by closing from the menu.** "Save and Exit" overwrites the
  save that was loaded. Work from a copy, or `games_stop`, which does not save.
- **Record what the run stopped on.** If an inquiry paused it for 40 minutes, the report says so.

### Unattended runs

Do not leave the GABS module loaded when nobody is watching: it has crashed a game once
(`TcpConnection.SendMessageAsync`). Unattended means `scripts/play.ps1`, started by the lead,
at default speed, with no bridge and no way to answer a popup. Choose that only when the run
cannot raise an inquiry, and say in the request what it would do if one arrived. The run-11
runbook (`docs/balance/run-11-runbook.md`) is the worked example of that trade-off.

### Writing a long run for someone else

Add to the §4 request:

```
Run length: <game days / wall clock at speed N>.
Stops expected: <which of the table's stops this scenario can raise>.
Stop condition: <what ends the run: day N, a state, an error count>.
Check-in: <what to read and how often>.
If it stalls: <what the person should do, and what they should NOT do (never kill the game)>.
```

## 7. How to report

End every hand-off with three lists, in this order:

- **Verified** - what you ran, the command, and its result.
- **Verified by reading only** - which §2 items you checked.
- **Not verified** - the §3 items that apply, plus the live test request from §4.

If a result surprised you, say so, and do not round it up. A reported failure is more useful
than a claimed pass.
