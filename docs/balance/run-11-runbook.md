# Run 11 — runbook: Phase 3's validation and acceptance

**For:** the tech lead (Claude Code) on the lead's Windows machine, with the game and the GABS MCP
server. **Written:** 2026-10-02, story 3.12 ST-1. **The lead reviews this file before the run**, and
the four questions in §0 are the lead's to answer first.

Stories 3.8-3.11 are merged into `development`, and 3.12's telemetry is built (ST-2). After the merges
the only live checks were the devs' own, run before them (story 3.8 §10, 3.9 §10, 3.10 §10). Story
3.11, the 3.12 telemetry and `test_found_network` have **never run in a game**. This run is the one
validation the lead asked for ("chạy 1 run để validate tất cả sau cùng"). It closes the checks each
story left open, then plays 3.12's acceptance scenario and its long run.

| Part | What | Time (est.) |
|---|---|---|
| §2 | Setup: build, deploy, the load check | 20 min |
| §3 | The checks 3.8-3.11 left open, story by story | ~1.5 h |
| §4 | 3.12 Part A: the acceptance scenario, live | ~1 h |
| §5 | 3.12 Part B: 5+ years of AI espionage, monitored | ~40 min |
| §6 | What goes back: `run-11.md`, the acceptance table, TODO | - |

---

## 0. For the lead, before the run

**Approved by the lead, 2026-10-02: all four recommendations below, as written.**

1. **Part B through GABS, monitored, rather than `play.ps1`.** Story 3.12 says Part B runs unattended
   through `scripts/play.ps1` with no GABS module (CLAUDE.md §1: GABS once crashed a game). But no lever
   reaches `test_set_speed` without the bridge, and at the default multiplier five years take about
   2 h 20 min unattended against about 25 min at 50. Runs 09 and 10 were monitored through GABS with 0
   errors. **Recommended: GABS, watched the whole time** - not unattended, so the rule's case does not
   arise. The alternative is `play.ps1` at default speed, with the lead starting it.
2. **Which DLL.** Story 3.12 says to run the one built against the v1.4.8 references (what ships).
   Run 10 used the local v1.5.3 build. **Recommended: the v1.4.8-reference DLL** (`scripts/compile-check.sh`,
   copied over the deployed one after `deploy.ps1`) for §4 and §5, the local build for §3. The report
   names the DLL and its SHA-256 for every part.
3. **The player hero's death (3.10 AC2).** One case ends the campaign on vanilla's Game Over screen. It
   is run on a throwaway copy of a save (`run11_death_*`), never on a save anyone needs.
4. **Part A's seat.** **Recommended: the player rules** (Khuzait on `di_pretender_test`), as story 3.12
   §4 suggests: the casus belli lands on the player's own realm, and nothing has to be staged to make the
   player a vassal.

## 1. Rules for this run

CLAUDE.md §1 and §2 apply in full. The run-09 runbook's §1 restates the ones that bite: one shared
game, never force-killed, confirm a stop with `Get-Process Bannerlord*`; launch only through
`games_start`, and dismiss the "Mod change detected" prompt; `load_save` works only from the main menu;
**cheat mode on** before any lever; a quiet log is usually an inquiry (`check_blockers`), and a quiet
log with a dead bridge is a crash (`tools/DumpProbe`); `answer_inquiry` takes `affirmative`; never save
over `di_phase1_full`, and save every new state as `run11_*`.

Two more, for this run:

- **No code changes during the run.** A defect is recorded with its evidence and fixed afterwards in
  its own commit, and the step that found it is run again (3.12 AC4). Nothing is fixed silently.
- **Every lever is named in the report**, and every forced roll is said to be forced. Levers stage a
  starting state; they never stand in for an outcome that is being measured.

## 2. Setup

```powershell
git switch development; git pull origin development; git log -1 --oneline   # record the commit
pwsh ./scripts/build.ps1
pwsh ./scripts/deploy.ps1          # check-save-ids and LoadProbe first; refuses while the game runs
bash scripts/compile-check.sh      # the v1.4.8-reference DLL, for §4-§5 if the lead agrees (§0.2)
```

**2.1 The load check.** `games_start`, load `di_pretender_test`, wait for `Session launched`. Expect
`Harmony patched 6 methods` and 0 `ERROR`. No save id changed in 3.8-3.12, so an old save must load
exactly as before. Then `diplomacy.networks`: every handler line now ends with its realm and
`[in the target realm]` or `[OUTSIDE the target realm]`.

**2.2 The new telemetry appears.** After the first weekly tick (`test_set_speed 20`, a week of game
time): the log holds `[NETWORK]` lines, and `[KINGDOM]` lines carry `counterIntel=`, `spyHandlers=`,
`spyFree=`, `rulerIsPlayer=` and `warParties=`. **Fail → stop**: Part B cannot be measured without them.

## 3. The checks 3.8-3.11 left open

### 3.1 Story 3.8 - a handler stays at their post

| AC | Save | Steps | Expect |
|---|---|---|---|
| AC1 | any; the player's clan | Make a member of the player's clan governor of a distant town through the town's governor slot, so vanilla sends them **travelling**; at once `test_assign_handler <that hero> \| <a foreign realm>` | `Refused: ... is on the way to a post.` If no governor can be set from the bridge, record AC1 as compile only |
| AC2 | `di_run07_1104` (never run for 3.8) | `test_set_speed 20`, three in-game weeks on the real clock | No `handler_lost` with `cause=party` or `cause=governor`; every `forced-party` counted and named |
| AC3 | the same, day 15 | `diplomacy.networks` | Every AI handler `[in the target realm]`, held by the daily return (story 3.8 §10, finding 1) |
| player | the same, or `di_36_espionage_test` | `test_intel open`; post a member as handler; open the Clan screen's Parties tab, "create party" | The handler is not offered as a party leader (screenshot) |
| Lilizha | `di_36_espionage_test` | Load, then before any tick: `diplomacy.networks Gundaroving` and her `GovernorOf` | Says whether her day-one governorship is carried in the save (story 3.8 §10) |

AC4 and AC5 are measured by Part B (§5).

### 3.2 Story 3.9 - the AI reads courts as bands

**AC4, the set of shaky realms unchanged**, on `di_phase1_full`, `di_pretender_test` and
`di_36_espionage_test`. The old code is gone, so "before" is the old rule applied by hand: a realm was
shaky if its exact legitimacy was under 50 (`diplomacy.legitimacy`) or a pretender stood
(`diplomacy.pretenders`). "After" is `diplomacy.ai_espionage`'s `worth subverting:` line for every realm.
Print both side by side. Expect them to agree on every realm; a disagreement is a defect.

### 3.3 Story 3.10 - the player's house is a mark like any other

Staged with `test_found_network`, which puts a ruling house's busy lord on a network without moving
them. **The arrangement lasts only until the next daily tick**: run each sequence below without
`tick_days` and without unpausing between the found and the resolve.

| AC | Steps | Expect |
|---|---|---|
| AC2a, the mark | Player a vassal commanding the realm's largest army (`test_player_join`, `test_raise_army`, or a real army) of a realm at war with an AI realm; `test_found_network <AI ruler's kin> \| <player's realm> \| 80`; `diplomacy.ai_espionage <AI realm>` | The player hero named as the Assassinate mark. With a companion of the player's house commanding instead, the companion |
| AC2b, a companion | `test_launch_mission <AI ruling house> \| <realm> \| Assassinate \| <companion>`, then `test_resolve_mission ... \| success` | The companion dead; the red "... is dead. Nobody has been punished for it." |
| AC2c, the player with an heir | Copy as `run11_death_heir`; the same on `Hero.MainHero` | Vanilla's heir prompt; 0 `ERROR` in the mod log and nothing in `rgl_log_errors_*` |
| AC2d, the player without an heir | Copy as `run11_death_noheir`; the same | Vanilla's Game Over screen; no crash |
| AC3, staging | A shaky realm with the player a vassal in the Disaffected band (`test_set_legitimacy`, grievances); `test_found_network ... \| 80`; `test_launch_mission <AI ruling house> \| <realm> \| ForgeLetters \| <player>`; `test_resolve_mission ... \| success` | "Letters in the crown's hand" opens (`ui/get_inquiry`). The body names no forger and does not say "forged" |
| AC3, believe | `answer_inquiry affirmative=true` | `diplomacy.grievances`: `ForgedLetters 8.0` on the player's house; the Court tab row reads "Letters in the crown's hand", without "- forged" (screenshot) |
| AC3, dismiss | the same, `affirmative=false` | No grievance; `diplomacy.missions` shows a Failure; no exposure; the yellow notice |
| AC3, reload | Save while the inquiry is open, `games_stop`, a fresh process, reload | The offer is asked again, not rolled again (`mission_resolved how=offer` once only). *Run 11 found a save cannot be made while the inquiry is up (`save_game` waits until it is answered), so the reload was staged through the guard instead: two offers at once, the second held, saved, reloaded, re-asked* |

### 3.4 Story 3.11 - the odds shown are today's

| AC | Steps | Expect |
|---|---|---|
| AC1 | `test_intel open`, `select`, `mission`, `plan` on a player network; then `send` | The overlay's second line under the odds: "The roll is made on the day it resolves, at that day's odds..."; the operation under way reads "N% to succeed now" (screenshots) |
| AC2 | Launch; `test_counter_budget <target> \| 30000`; `test_network_week`; `test_resolve_mission <clan> \| <realm>` **with no outcome** | The notice ends "Success was N% on the day", where N is the figure in the log's `Resolved:` line ("success was N% on the day"); the launch line's figure differs |

## 4. Part A - the acceptance scenario (story 3.12 §4)

On `di_pretender_test`, the player ruling Khuzait (§0.4); save the start as `run11_partA_start`. The
player acts through the Intelligence tab (`test_intel`, the same methods the buttons call). The steps
and their bar are 3.12 §4's; the levers each step may use:

| Step | Allowed levers | Record |
|---|---|---|
| A1 post a handler in a **neighbouring** realm, build the network | `test_set_network` for the weeks of growth (say so); `test_set_legitimacy` to make that crown shaky if none is | tab, `diplomacy.networks` and `mission_odds` agreeing |
| A2 bribe the head of a **border** house | none for the roll if it lands within three tries; otherwise forced (say so) | `foreign gold -20.0`; the Encyclopedia band moving; `diplomacy.bribes` binding it |
| A3 the internal war | its own trigger first (`tick_days 1` runs the trigger); `test_start_internal_war` if not (say which) | the bought house with the rebels, "bought with our gold" in the notice and the log |
| A4 an exposure in a realm **at peace** with the player | launch at low odds and repeat; force only if it never comes (say so) | red notice; `EspionageExposed` held against Khuzait; trust -25; handler a prisoner; network 0; `espionage_exposed` in the log |
| A5 the victim's war | **none**: the real clock only, `test_set_speed 20-50`, up to the claim's 168 days; `diplomacy.war_value <victim> \| Khuzait` read weekly | `war_opened ... casusBelli=EspionageExposed`, or the weekly war values that explain why not |
| A6 the "caught in our realm" list | `test_found_network` on an AI house aimed at Khuzait, then a forced exposure (say so) | the list with the entry. *Corrected in run 11: the list is the Realm tab's counter-intelligence block (`DiRealmPanel.xml`, `CounterIntelVM`), at the foot of the tab, not the Intelligence tab* |

## 5. Part B - the long run (story 3.12 §5)

| | |
|---|---|
| Save | `di_fresh_1084` (the run 08-10 baseline). The player parked in a town, out of every throne; `test_set_player_age 30` first |
| Speed | `test_set_speed 50`, about 4.5 real minutes a game year (run 10) |
| Length | at least **5 in-game years**; 10 if the time is there, which makes run 10 directly comparable |
| Watch | the log every few minutes; `check_blockers` when it goes quiet. Once a game year: `diplomacy.networks`, `diplomacy.ai_espionage`, `diplomacy.counter_intelligence`, saved to the notes |
| End | save as `run11_end`; copy the log(s) to `docs/balance/run-11.log` |

Then `python tools/analyse-log.py docs/balance/run-11.log > docs/balance/run-11-analysis.txt`. Its
ESPIONAGE section prints each row of 3.12 §5 per run year with the bar beside it, HANDLER POSTS the
`handler_lost` causes, LOG HEALTH the `ERROR` count. Two rows need run 10 beside them: war share and mean
war length (run-10.md: 79 wars over 10.3 years, mean 56 days). 3.8 AC5 reads `warParties` per realm
over the run, for the first time; there is no earlier figure to compare it with, so the report states
it as a baseline and looks only for a fall.

## 6. What goes back

- `docs/balance/run-11.md`, in run-10.md's shape: the build, the DLL and its hash, every lever used and
  every forced roll, §3's tables filled in, Part A step by step with shots, Part B's numbers per year.
- Each story file's verification table updated (3.8 §10, 3.9 §10, 3.10 §10, 3.11 §7), each AC as
  verified live, compile only, or not verified.
- **The Phase 3 acceptance table** in STATUS.md and ROADMAP.md (3.12 AC3), as Phase 2's was, with what
  goes to Phase 4.
- Every defect, fixed in its own commit with the step re-run, or in TODO.md for the lead (3.12 AC4).
- Findings for the lead regardless of pass or fail: the `forced-party` rate (above 3 a year goes back,
  3.8 R5), wars citing `EspionageExposed` (zero is a finding), and whether the player's house was ever a
  mark.
