# Run 09 — runbook for the local agent

**For:** a Claude Code agent on the lead's Windows machine, with the game installed and the GABS
MCP server available. **Written:** 2026-09-27, in a cloud session that had no game, by the tech
lead. **Replaces** `run-09-plan.md`; this file is the one procedure.

Everything built on 2026-09-27 (TODO.md, "Decided 2026-09-27") was compile-checked against the
v1.4.8 reference assemblies and passed the save-id check. **None of it has run in a game.** Every
"expect" below is a prediction read from the code. Your job is to find out which predictions hold.

The run has four parts, in this order:

| Part | What | Time |
|---|---|---|
| §2 | Setup, the old-save load and the cheat-mode gate: **stop if either fails** | 20 min |
| §3 | Targeted checks, one per feature built on 2026-09-27 | ~1.5 h |
| §4 | **The lead's question: does Phase 3 (espionage) play by the same rules for AI and player?** | ~1.5 h |
| §5 | Balance runs: 09A (20 years), 09B (10 years), 09C (civil war, 5 years) | ~2.5 h, monitored |

§6 is what only a human can do; §7 is what to hand back.

---

## 1. Rules for this run

Read CLAUDE.md in full first; §1 and §2 are the ones this run leans on. Restated because a mistake
here costs the run:

- **One game, shared.** Before starting anything: `Get-Process Bannerlord*`. If the game is running
  and you did not start it, **ask the lead** before stopping it. Never force-kill it. Stop only
  with `games_stop`, then confirm with `Get-Process` and `bannerlord.core.get_game_state` -
  `games_stop` can report success while the game still runs (CLAUDE.md §1).
- **Launch only through `games_start`.** After a `started_bridge_pending`, wait for the game; do
  not `games_connect` over it (GABS crashed the game that way once). A changed DLL opens a
  "Mod change detected" prompt before anything loads: `scripts/dismiss-mod-change-prompt.ps1`.
- **Saves.** Never save over `di_phase1_full`. Save every new state under a `run09_` name. Load any
  test save expecting that someone may have saved over it. "Save and Exit" overwrites the loaded
  save; `games_stop` does not save. `bannerlord.core.load_save` works only from the main menu -
  confirm the world actually changed (`diplomacy.wars`, `core.get_campaign_time`).
- **Cheat mode** (`bannerlord.core.set_cheat_mode true`) before any `campaign.*` command and, since
  this build, before every `diplomacy.test_*` lever and `sign_treaty`, `break_treaty`,
  `offer_peace`, `fabricate_claim`, `tick_days`, `ai_week`, `set_smoothed_strength`, `set_war_score`.
- **A quiet log is usually an inquiry**, not a stall: `bannerlord.core.check_blockers`
  (`inquiry_active`), `ui/get_inquiry`, `ui/answer_inquiry`. If the log stops **and** the bridge
  times out, it is a crash: CLAUDE.md §1 "Reading a crash with no stack trace", `tools/DumpProbe`.
  Never answer Yes to the crash dialog's upload question.
- **Do not change code during the run.** A fix mid-run invalidates what was measured before it.
  Record every defect with its evidence (command, output, log line, save name) and propose the fix
  in the report. The one exception is §2.3's one-line gate, and only if that check fails.
- **A command whose syntax you are unsure of:** run it with no arguments; it prints its usage.
- Write the results in English (the repo's language); **report to the lead in Vietnamese**.

The mod log is `Documents\Mount and Blade II Bannerlord\DiplomacyIntrigue\Logs\` (newest file).
`[EVENT] ... kind=...` lines are telemetry; `(Area)` lines are prose.

---

## 2. Setup — stop if any step fails

### 2.1 Build and deploy

```powershell
git fetch origin; git switch development; git pull origin development
git log -1 --oneline            # record this commit in the results
pwsh ./scripts/build.ps1
pwsh ./scripts/deploy.ps1       # runs check-save-ids.ps1 and LoadProbe first, refuses on either
```

### 2.2 New save data loads on an old save

This build adds `SpyMission` 12, `InternalWar` 15 and `InternalWarMember` 2 (CLAUDE.md §3); all
default sensibly on an older save.

1. `games_start`, load `di_pretender_test`, wait for `Session launched`.
2. **Expect** `Loaded: ...` and **0 errors / 0 warnings** in the log.
3. Save as `run09_roundtrip`, `games_stop`, confirm stopped, `games_start`, load `run09_roundtrip`.
4. **Expect** 0 errors. **Fail → stop the run and report**: a save-data fault breaks every campaign.

### 2.3 The cheat-mode gate

1. **Before** `set_cheat_mode`: `diplomacy.tick_days 1`. **Expect** one line starting
   `Refused: diplomacy.tick_days changes the campaign for testing and needs cheat mode (`.
2. `diplomacy.wars` - **expect** a normal answer (diagnostics stay open).
3. `bannerlord.core.set_cheat_mode true`, then `diplomacy.tick_days 1` - **expect** it runs.
4. **If step 3 still refuses**, every lever in this runbook is shut. The gate is
   `DebugCommands.CheatsAllowed` → `CampaignCheats.CheckCheatUsage`. This is the one allowed code
   change: make `CheatsAllowed` read `Game.Current.CheatMode` directly, rebuild, redeploy, redo 2.3,
   and record it.

---

## 3. Targeted checks

For each: run it, compare with **Expect**, mark **PASS / FAIL / NOT RUN** with the evidence. Cheat
mode on throughout. Where a save is named, load it fresh.

### 3.1 The court reaches foreign policy (R-1) — `di_pretender_test`

1. `diplomacy.war_value Vlandia | Battania` → **expect** a line `value from their court: 5.0 (crown
   Failing +3, a claimant stands +2)` and a line `value from our court: ...`.
2. `diplomacy.court_bands Battania` → the same Failing band and the named claimant. (The AI's
   term must read exactly what the player is shown - that is the point of this term.)
3. `diplomacy.test_set_legitimacy Battania | 45`, repeat 1 → `3.0 (crown Questioned +1, a
   claimant stands +2)`.
4. `diplomacy.test_start_internal_war Battania` →
   `diplomacy.war_value Battania | Vlandia` shows `internal war: ... it chooses no new war BLOCKED`;
   `diplomacy.war_value Vlandia | Battania` reads `6.0` with `capped at 6`.
5. `di_grievance_test` (Khuzait): `diplomacy.blocs Khuzait` prints two shares - the effective
   share D% is what the terms read. `diplomacy.peace_allowance Khuzait | <an enemy>` ends with
   "Where each court puts its peace bars": seek bar = 60 × (1 + 0.15·Hawks − 0.30·Doves). Doves
   exist only above exhaustion 40; with no war, both shares read 0% (by design, not a fail).
6. **Switch off Court intrigue** in MCM (`EnableIntrigue`), repeat 1 and 5: **every court term must
   read 0**. Switch it back on.

### 3.2 A threat relaxes the defensive-pact floor (R-9) — any save

1. `diplomacy.set_smoothed_strength <X> | <about 3× the combined strength of A and B>`.
2. Push B's trust in A to about −24: `sign_treaty A | C | NonAggressionPact`,
   `break_treaty A | C | NonAggressionPact`, twice (each breach costs every observer 12).
3. `diplomacy.pact_value A | B` → **expect** `defensive pact: -30.0 (-20 - pull 1.00 x 10, against
   X)`, `DefensivePact: trust allows it`, NAP and Alliance blocked.
4. `sign_treaty A | B | DefensivePact` succeeds; `sign_treaty A | B | Alliance` is refused.

### 3.3 Tribute: at most two at once, a year's cooldown, telemetry — `di_tribute_test`

Pick a realm X at peace with A, B and C (not Sturgia, a vassal on this save).
1. `sign_treaty A | X | TributaryPact | 500`, then `B | X`, then `C | X` → the third **refused**:
   `X already pays tribute to A and B, and no realm is made to pay more than 2 at once.`
2. `diplomacy.tribute_value C | X` → `tributes paid: 2 ... BLOCKED`.
3. If C is at war with X: `diplomacy.peace_allowance C | X` → `tributary pact 60 (blocked: ...)`.
4. `diplomacy.test_expire_treaty A | X | TributaryPact`, then `tribute_value A | X` → `last tribute
   ... ended 0.0 days ago ... BLOCKED`, "cannot be demanded again for 84 more days".
5. `diplomacy.test_demand_tribute A | X` while capped or cooling → log `[EVENT] ...
   kind=tribute_refused ... reason=cap` (or `cooldown`) `courtShare=none`.

### 3.4 A war bleeds trust from its first day — any save with a war A–B

`sign_treaty A | B | Truce`, `test_expire_treaty A | B | Truce` (pays +12, starts the 30-day
grace), note `diplomacy.trust A`, `tick_days 1` → **expect** A→B to fall by 0.6 + 0.01 × the war's
days elapsed (unless already at −35). Before this build it did not move for 30 days.

### 3.5 An indemnity priced against the loser's treasury — `di_hegemony_1166`

(Or `di_phase1_full` - **do not save it**.)
1. Pick a war A–B from `diplomacy.wars`. `diplomacy.set_war_score A | B | 70`.
2. `diplomacy.peace_allowance A | B` → **expect** rate = max(125, 0.5% × B's ruler's gold) a point,
   ceiling 40% of that gold, the largest indemnity ≈ 60 points ≈ 30% of the treasury, rounded down
   to thousands.
3. `diplomacy.offer_peace A | B | indemnity, prisoners` → on signing: `[Peace] B paid A an indemnity
   of G denars, ~30% ...` and `kind=indemnity_paid`. A refusal ("exhaustion") is not a fail; the
   reply still shows the indemnity line.
4. `diplomacy.test_open_peace A | B` → the peace-table row shows the same denars, share and points.
   Screenshot it.

### 3.6 Legal neglect — `di_hegemony_1166` (Vlandia with two vassals)

1. `diplomacy.hegemony` → every link's line carries `legal neglect +0.0` after `protection`.
2. If a Vlandian vassal V is defending against an attacker X that Vlandia is not at war with:
   `sign_treaty Vlandia | X | NonAggressionPact` → `protection +0.0  legal neglect -10.0` and a line
   naming X and the pact.
3. **An attacker that is another Vlandian vassal must NOT produce legal neglect.**
4. The Realm tab shows a chip for it: screenshot the row (a tenth chip may overflow - record it).

### 3.7 A cadet branch takes part of its house's influence — `di_pretender_test`

`diplomacy.heirs` → "a cadet branch would take N of M influence (k of n adults, share x%)".
`diplomacy.test_divide_clan <clan>` → "Influence: <clan> A -> B, <cadet> C" with A − B = C = A ×
min(k/n, 50%); the split's log line has the same numbers.

### 3.8 The legitimacy peace dividend needs a real year of peace — any save

`diplomacy.legitimacy` → per realm "at peace N days (since ...); next dividend +3.0 in M days", or
"at war". A payment needs the real clock: `test_set_speed 50`, watch for `Legitimacy <K> +3.0 ...
(a year of peace ...)`. A realm that fought part of the year must not be paid.

### 3.9 Civil war: captured fiefs, and restitution after a crown win — `di_pretender_test`

Needs sieges on the real clock and a war **started on this build** (older captures are not
recorded). Battania rises on the first daily tick.
1. `test_set_speed 30`. Wait for `(InternalWar) ... losing <fief> costs the crown exhaustion -> ...`
   (a town 6, a castle 3, × rate × resolve).
2. `diplomacy.internal_wars` → `fiefs taken across the line: N`, each "first taken from ...; held
   now by ...", the daily cost line, and "if the crown won today, N fief(s) would be restored".
3. **Round trip:** save `run09_civilwar`, restart the game, reload → the same captures listed.
4. `diplomacy.test_end_internal_war Battania | crown` → `restitution - X passes from <rebel> to
   <holder>, who held it when the war began.` and `N fief(s) restored after the crown's win`.
5. `diplomacy.grievances Battania` → no new grievance for the rebels from the restitution.

### 3.10 Smaller fixes

1. **`test_set_skill` sticks:** `diplomacy.test_set_skill <hero> | charm | 232` → "Skill XP N, what
   232 requires" (a "DISAGREE" line is a fail). Then `diplomacy.test_amends <clan>` (grants the
   envoy XP) → `diplomacy.statecraft` still ~232, not the old value.
2. **The zero-manpower siege (design 10 §9a):** any save with wars, `test_set_speed 30`, until a
   `kind=battle_scored type=Siege winner=attacker` line. Read `defenderParties`: `yes(walls)` on a
   garrison with `settlementNow` = the attacker confirms the cause found in code. Copy three such
   lines into the results with design 10 §9a's table read against them.
3. **Occupied fiefs:** `diplomacy.wars` prints `fiefs=a/b (held now c/d)`. A fief retaken stops
   counting in `held now`.
4. **Realm tab:** an agreement turns red only in its last 21 days (was 60). Screenshot.

---

## 4. The lead's question: does Phase 3 play by the same rules?

The lead asked on 2026-09-27 whether espionage follows the project rule that **the AI plays by the
same rules as the player** (CLAUDE.md §3). A code reading the same day found:

- **The rule layer is shared.** AI and player go through the same functions -
  `Missions.CanPlan / CanLaunch / Launch / OddsOf`, `SpyNetworks.Assign / SetBudget`,
  `CounterIntelligence.SetBudget` (`Espionage/AiEspionage.cs:377-422`) - same prices, strength
  requirements, odds, exposure. No rule takes an "is this the player" argument. The Intelligence tab
  reads the same `OddsOf / CanPlan / CanLaunch`.
- **Four asymmetries**, outside the rules:

| # | Asymmetry | Who it favours | Where | Status |
|---|---|---|---|---|
| 1 | **AI networks never grow in practice**: vanilla makes an AI handler a governor or a party leader within days; the player's companion handler stays | player (outcome) | design 03 §10, 3.6 check 3 | parked; needs vanilla IL |
| 2 | **The AI picks bribe and forgery targets from exact rival figures** (each house's loyalty, the crown's legitimacy < 50) that the player sees only as bands unless it pays for ReadCourt | AI (information) | `AiEspionage.cs:230, 242`; admitted at `:83` | open - R-1 fixed the same fault in the war valuation on 2026-09-27; espionage was not changed |
| 3 | **The AI never assassinates anyone of the player's house** (and only at war, only a field commander, never a ruler); the player may target any lord, rulers included, in peace | player | `AiEspionage.cs:510`; the lead's decision 11 (2026-09-25) | the lead's call |
| 4 | **The AI never forges letters to the player's house** | player | `AiEspionage.cs:250` | the lead's call |

Differences that are **not** violations (record, do not test further): a bribe that reaches the
player's house asks the player (decision 12 - the player decides for the player's house);
the AI runs networks only from ruling houses (decision 5 - the AI's choice, the rule is the same);
the player is notified when harmed (decision 2 - UI only).

**Your job is evidence, not a fix.** The lead decides #2-#4 from what you bring back; #1 needs the
IL you can read and the cloud could not.

### 4.1 The numbers shown are the numbers used

On any save with a player network (use `di_espionage_missions`, or set one up:
`diplomacy.test_hire_companion <wanderer>`, `diplomacy.test_assign_handler <hero> | <kingdom> |
6000`, `diplomacy.test_set_network <player clan> | <kingdom> | 50`):
open the Intelligence tab (`diplomacy.test_intel open`), note each mission's odds; run
`diplomacy.mission_odds <player clan> | <kingdom>`; launch one (`test_launch_mission`) and read
the log's `launched ... success X%, exposure if it fails Y%`. **PASS** if all three agree.

### 4.2 Why AI handlers are taken — vanilla IL (asymmetry #1)

On this machine the real assemblies are available. `tools/CallSites` reads IL:

```powershell
dotnet run --project tools/CallSites -- --members "ChangeGovernorAction"
dotnet run --project tools/CallSites -- --callers "ChangeGovernorAction::<each Apply* method>"
dotnet run --project tools/CallSites -- --members "LordPartyComponent"
dotnet run --project tools/CallSites -- --callers "LordPartyComponent::<the Create* method>"
dotnet run --project tools/CallSites -- --members "MobilePartyHelper"
dotnet run --project tools/CallSites -- --callers "MobilePartyHelper::<the Spawn*Lord* method>"
dotnet run --project tools/CallSites -- --il "<each calling behaviour's method>"
```

For each path that assigns an AI clan's hero as a governor or a party leader, record: the calling
behaviour and method, **every condition it checks on the hero** (e.g. `IsActive`, `GovernorOf`,
`PartyBelongedTo`, `IsPrisoner`, `HeroState`, `IsFugitive`, a `GameModel` call), and whether any of
those is something the mod can answer **without Harmony** - a `GameModel` the mod already
overrides or could, or a hero state the handler could legitimately be in. Known already:
`ModClanPoliticsModel.CanHeroBeGovernor` returns false for a handler and vanilla's assignment does
not ask it (design 03 §10). CLAUDE.md §3: a Harmony patch needs exactly this evidence and the lead's
yes - **propose, do not build**.

### 4.3 Measure the outcome gap (asymmetry #1)

On `di_run07_1104` (7 kingdoms, evolved; Intrigue and Espionage on), cheat mode on:
1. Give the player a network under the same terms an AI gets: a companion handler, 6,000 a week,
   in the realm that is a rival to most AI realms.
2. `test_set_speed 30`, run **two in-game years** (168 days). Every 14 in-game days, record
   `diplomacy.networks` and `diplomacy.ai_espionage`: for every network, owner, target, handler
   present or not, strength; and every `(Espionage)` log line that releases a handler, with its
   reason (governor, party, captured, died).
3. **Deliver** a table: network strength over time for each AI network and for the player's;
   handler losses by cause; AI operations launched, and their outcome.
   **Expect from the code:** the player's network grows about 1.3 a week; AI networks lose their
   handler within days and stay near 0; no AI operation in two years.

### 4.4 The information gap (asymmetry #2)

During 4.3, whenever `diplomacy.ai_espionage <realm>` plans a **BribeLord** or **ForgeLetters**, record:
the target house, the exact loyalty the plan line prints ("at loyalty N"), the target crown's
legitimacy, and what the player would see - `diplomacy.court_bands <target realm>` (the house's
band, the crown band). Count the cases where the exact figure decides between houses the bands show
**the same** (e.g. two houses both "Sullen" and the AI takes the one at 26.1 over 38.9). If none
arise naturally in the run, stage one: on `di_pretender_test` give an AI ruler a network in Battania
(`test_assign_handler`, `test_set_network ... | 60`) and read its plan.
**Deliver** the count and three examples. It decides whether reading bands (as R-1 now does) would
change what the AI does, or only how it is justified.

### 4.5 The two exemptions (asymmetries #3, #4)

Confirm by reading `AiEspionage.cs` at the current commit that the two `Clan.PlayerClan` exclusions
are the only player-specific lines in AI espionage planning (`grep -n "PlayerClan\|MainHero"
src/DiplomacyIntrigue/Espionage/AiEspionage.cs`). If during 4.3 a realm at war with an AI that runs
a network has **the player's house leading its largest army**, record what `ai_espionage` chooses
(it must skip that commander and pick the next). Do not stage it otherwise.

### 4.6 A bribe on the player's house is asked again, not re-rolled

The 2026-09-27 fix (`SpyMission.OfferOwed`). Player as a vassal of a realm with an AI ruler that
has a network there (3.6 check 4 in design 03 §10 has the recipe: `di_36_espionage_test` or set it
up with `test_player_join`). Launch **two** BribeLord operations on the player from two AI houses
(`test_launch_mission`; each house needs its own network and handler there), then force the first
to success (`test_resolve_mission <house 1> | <realm> | success`): "Foreign gold" opens. While it is
open, force the second the same way: it cannot be shown (one offer at a time), so it is marked owed.
Answer the first (either way). Let one in-game day pass: the second opens by itself, and its log
line carries `(the roll made before this offer was first shown)`. **PASS** if that note appears and
no fresh roll (`success was N%, exposure on failure M%`) is logged for the second. An open inquiry
stops the clock, so the day must pass after the first answer, not before.

### 4.7 What to write for §4

A section "Phase 3 and the same-rules principle" in the results, with one verdict each:
- **Rules:** shared or not (4.1, plus any gate you found that only one side meets).
- **Outcome:** the 4.3 table, in one sentence.
- **Information:** the 4.4 count and examples.
- **Exemptions:** 4.5.
- **The lever for #1:** 4.2's table and the proposal.

End it with the three questions for the lead, each with a recommendation:
(1) the lever for the handler blocker; (2) should AI espionage read bands, like R-1 (the tech
lead's recommendation: yes); (3) keep or drop the player's-house exemptions (#3, #4).

---

## 5. Balance runs

Monitored runs through GABS, as run 08 was (`test_set_speed 50`, cheat mode on, the player's party
parked in a town so bandits do not stop the clock). Every 5-10 real minutes: the game is alive
(`get_game_state`), the log is advancing, `check_blockers` is clear (answer an AI inquiry to the
player with the option that changes least, and record it), no `ERROR` in the log. If you cannot
keep watching, say so and stop; do not leave GABS attached to an unwatched game (CLAUDE.md §1).

| Run | Start | Setting | Length |
|---|---|---|---|
| **09A** | `di_fresh_1084` | statecraft on | **20 in-game years** (~50 min at 50) |
| **09B** | `di_fresh_1084` | `diplomacy.test_statecraft off` right after load | **10 in-game years** |
| **09C** | `di_pretender_test` | Battania rises on the first tick; the player does nothing | **5 in-game years** |

After each: `python tools/analyse-log.py <log> > docs/balance/run-09a-analysis.txt` (09b, 09c
likewise). The script reads all of this build's telemetry; sections it cannot fill say so.

**What 09A/09B must answer** (put each answer, with its numbers, in the results):

| # | Question | Section of the analysis |
|---|---|---|
| 1 | Design 10: the prisoner term's share of each war's closing score (D2 was kept knowing it dominated the first live check) | WAR SCORE |
| 2 | Tributary pacts and subjugations per decade; wars still well under 252 days | PEACE OUTCOMES, the old sections |
| 3 | Is the §13 tribute band still unreachable (run 08: 0 of 123)? The band was deliberately not moved until this number exists | PEACE OUTCOMES |
| 4 | Indemnities: how often, how large against the treasury, how many at the 40% cap; any AI realm left under 50,000 (`AiGoldReserve`) afterwards | INDEMNITY |
| 5 | Tribute demands by reason; do the cap and the cooldown ever bind | TRIBUTE DEMANDS |
| 6 | Court terms on AI war declarations; how many would have fallen under the bar without them | COURT AND FOREIGN POLICY |
| 7 | Legal neglect link-weeks; F3 defections compared with run 08 | HEGEMONY LEGAL NEGLECT |
| 8 | Internal wars, side changes, restitution, cadet splits (run 08: none) | INTERNAL WARS |
| 9 | Pacts: 09A's first decade against 09B (run 08: 47 vs 31) - the second statecraft pair | old pact section |
| 10 | Errors and warnings | header |

**09C must answer:** how the war ended and when; side changes (paid, forced) and their prices
against the purses; whether conceding at 75 ended it early; restitution on a crown win; any cadet
split and whether its founder stood at a later succession.

---

## 6. Only a human can

- **Esc over the peace table** (UI-INTEGRATION.md §0c.7): needs a display and a key press.
- **The intermittent launcher crash:** one Play from the official launcher with the mod unticked
  (STATUS-history, "Intermittent").

Ask the lead for these at the end; do not block on them.

---

## 7. What to hand back

1. **`docs/balance/run-09.md`**, in run-08.md's shape: the commit tested; §2's results; a table of
   every §3 check (PASS / FAIL / NOT RUN, one line of evidence each); §4 as 4.7 describes; §5's
   answers; defects found, each with evidence and a proposed fix; what was not run and why.
2. The logs as `docs/balance/run-09a.log`, `run-09b.log`, `run-09c.log`, `run-09-checks.log`, and
   the three analysis files.
3. Screenshots named in §3 into `docs/balance/run-09-shots/`.
4. Commit on `development` with a plain message; push. Update `docs/STATUS.md` ("Start here") and
   `TODO.md` (tick run 09, add what it opened).
5. **A short report to the lead in Vietnamese:** what passed, what failed, the three §4 questions,
   and the decisions 09A-C now call for (the §13 band, D2's prisoner weights, the indemnity).
