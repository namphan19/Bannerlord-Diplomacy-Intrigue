# Run 09 — plan for the lead's local session

**Written 2026-09-27**, after the pass that closed TODO items 2 and 4-12 on the lead's delegation
(STATUS, "Start here — 2026-09-27", has the list). Nothing below has been run: the build was
compile-checked in a cloud session against the v1.4.8 reference assemblies, and the save-id check
passes. **Everything in this file is a prediction to be checked, not a result.**

Three parts, in this order: **A** a short GABS session of targeted checks (about an hour), **B** the
balance run proper (unattended), **C** a civil-war run. Part D lists what needs a human at the
keyboard.

## 0. Before anything

1. `pwsh ./scripts/build.ps1`, then `pwsh ./scripts/deploy.ps1` (it runs LoadProbe and
   `check-save-ids.ps1` first and refuses on either). Note the commit deployed.
2. **New save data in this build** — `SpyMission` 12 (`OfferOwed`), `InternalWar` 15 (`Captures`),
   `InternalWarMember` 2 (`Fief`). All default sensibly on an older save. The first load of an old
   save is the test that they do: load `di_pretender_test`, expect `Loaded: ...` with **0 errors**,
   save it under a **new name** (`di_run09_roundtrip`), stop the game, start it again, load that
   save, 0 errors.
3. **Cheat mode now gates the test levers.** Every `diplomacy.test_*`, and `sign_treaty`,
   `break_treaty`, `offer_peace`, `fabricate_claim`, `tick_days`, `ai_week`,
   `set_smoothed_strength`, `set_war_score`, answers `Refused: ... needs cheat mode` until
   `bannerlord.core.set_cheat_mode true`. Check this first: run `diplomacy.tick_days 1` before
   setting cheat mode (expect the refusal), then after (expect it to run). **If it still refuses
   after `set_cheat_mode`, stop** - the gate reads `CampaignCheats.CheckCheatUsage`, and if GABS sets
   a different flag every lever is shut; say so and it is a one-line change.

Never save over `di_phase1_full`. Load the other test saves expecting that they may have been saved
over.

## A. Targeted checks (GABS, cheat mode on)

Each line: what to run, what should come back. "Expect" figures are from reading the code.

### A1. The court reaches foreign policy (R-1) - `di_pretender_test`

- `diplomacy.war_value Vlandia | Battania` → a line `value from their court: 5.0 (crown Failing
  +3, a claimant stands +2)`, and `value from our court: ...`. `diplomacy.court_bands Battania`
  shows the same Failing band and the named claimant.
- `diplomacy.test_set_legitimacy Battania | 45` → the same command reads `3.0 (crown Questioned
  +1, a claimant stands +2)`.
- `diplomacy.test_start_internal_war Battania` → `diplomacy.war_value Battania | Vlandia` shows
  `internal war: ... it chooses no new war   BLOCKED`; `war_value Vlandia | Battania` reads 6.0
  (capped).
- `di_grievance_test` (Khuzait): `diplomacy.blocs Khuzait` prints two shares; the effective one (D%)
  is what the new terms read. `diplomacy.peace_allowance Khuzait | <enemy>` ends with a "Where each
  court puts its peace bars" block: seek bar = 60 × (1 − 0.003·D) when all the court's blocs are
  Doves. Doves exist only above exhaustion 40 - if Khuzait has no war, both shares read 0% by design.

### A2. A threat relaxes the defensive-pact floor (R-9) - any save

`diplomacy.set_smoothed_strength <X> | <~3x the pair's combined strength>`, push B's trust in A
to about −24 (sign and break a pact twice), then `diplomacy.pact_value A | B` → `defensive pact:
-30.0 (-20 - pull 1.00 x 10, against X)`, `DefensivePact: trust allows it`, NAP and Alliance
blocked. `sign_treaty A | B | DefensivePact` succeeds; `Alliance` is refused.

### A3. Tribute: the cap, the cooldown, the telemetry - `di_tribute_test`

Pick a free realm X at peace with A, B, C (not Sturgia, a vassal there).
- `sign_treaty A | X | TributaryPact | 500`, then B, then C → the third refused: `X already pays
  tribute to A and B, and no realm is made to pay more than 2 at once.`
- `diplomacy.tribute_value C | X` → `tributes paid: 2 ... BLOCKED`.
- `diplomacy.test_expire_treaty A | X | TributaryPact`, then `tribute_value A | X` → `last tribute
  ... ended 0.0 days ago ... BLOCKED`, "cannot be demanded again for 84 more days".
- `diplomacy.test_demand_tribute A | X` while capped or cooling → `[EVENT] ... kind=tribute_refused
  ... reason=cap` (or `cooldown`) in the log.

### A4. A war bleeds trust from its first day - any save with a war

`sign_treaty A | B | Truce` on a pair already at war, `test_expire_treaty A | B | Truce` (+12,
grace starts), note `diplomacy.trust A`, `tick_days 1` → A→B falls by 0.6 + 0.01 × the war's days.
Before this build it did not move.

### A5. An indemnity that bites - `di_hegemony_1166` (or `di_phase1_full`, **do not save**)

`diplomacy.set_war_score A | B | 70`, then `diplomacy.peace_allowance A | B` → the indemnity lines:
rate = max(125, 0.5% of B's ruler's gold) a point, ceiling 40%, the largest indemnity ~30% of the
treasury at ~60 points. `diplomacy.offer_peace A | B | indemnity, prisoners` → on signing, a log line
`[Peace] B paid A an indemnity of G denars, ~30% ...` and `kind=indemnity_paid`. The peace table
screen (`test_open_peace A | B`) shows the same denars and share.

### A6. Legal neglect - `di_hegemony_1166` (Vlandia, two vassals)

`diplomacy.hegemony` → every link's line now has `legal neglect +0.0`. To see it pull, a Vlandian
vassal V defending against X with Vlandia not at war with X: `sign_treaty Vlandia | X |
NonAggressionPact` → `protection +0.0  legal neglect -10.0` and a line naming X and the pact. **A
sibling attacker (another Vlandian vassal) must NOT produce legal neglect** - that exemption was
kept on review. The Realm tab gains a chip for it; screenshot the row - a tenth chip may overflow.

### A7. A cadet branch takes part of its house's influence - `di_pretender_test`

`diplomacy.heirs` → "a cadet branch would take N of M influence". `diplomacy.test_divide_clan
<clan>` → "Influence: <clan> A -> B, <cadet> C" with A − B = C = A × min(adults leaving / adults,
50%). The split's log line carries the same numbers.

### A8. The legitimacy peace dividend needs a real year of peace - any save

`diplomacy.legitimacy` → per realm "at peace N days (since ...); next dividend +3.0 in M days", or
"at war". A payment needs the real clock (`test_set_speed 50`); a realm that fights part of the
year must not be paid.

### A9. Civil war: fiefs wear a side down, and a crown win gives them back - `di_pretender_test`

This one needs the real clock and sieges, and a war **started on this build** (captures made before
it are not recorded). Battania rises on the first daily tick.
1. `test_set_speed 30`, let it run until the log shows `... passed from <crown house> to <rebel>`
   by siege and `losing X costs the crown exhaustion -> ...` (6 a town, 3 a castle, × rate ×
   resolve).
2. `diplomacy.internal_wars` → `fiefs taken across the line: N`, each "first taken from ...; held
   now by ...", the daily cost line, and "if the crown won today, N fief(s) would be restored".
3. **Save mid-war (`di_civilwar_09`), restart the game, reload** → the same captures listed. This is
   the round trip of `InternalWar.Captures`.
4. `diplomacy.test_end_internal_war Battania | crown` → `restitution - X passes from <rebel> to
   <holder>, who held it when the war began.` and `N fief(s) restored after the crown's win`.
   `diplomacy.grievances Battania` shows no new grievance for the rebels.

### A10. Smaller fixes

- `diplomacy.test_set_skill <hero> | charm | 232` → "Skill XP N, what 232 requires". Then
  `test_amends <clan>` (grants the envoy XP) → `diplomacy.statecraft` still shows ~232, not 503.
- **The zero-manpower siege (design 10 §9a).** Any war save at `test_set_speed 30` until a
  `kind=battle_scored type=Siege` line: read `defenderParties`. `yes(walls)` on a garrison with
  `settlementNow` = the attacker confirms the cause found in code; see design 10 §9a's table.
- Espionage wording: on `di_pretender_test`, bribe two houses and start the war (3.5 check 2); a
  house that would have risen anyway reads "as it would have without our gold".
- Realm tab: an agreement turns red only in its last 21 days now (it was 60).
- `diplomacy.wars` prints `fiefs=a/b (held now c/d)`: a retaken fief stops costing its owner.

## B. Balance run 09 - unattended

**Setup.** `di_fresh_1084`, the player's party parked in a town. Launch as run 08 was, with
cheat mode on and `diplomacy.test_set_speed 50`. CLAUDE.md §1: after a pending `games_start`, wait
rather than connecting over it; watch for an inquiry addressed to the player
(`check_blockers`), which stops the clock.

- **09A**: `EnableStatecraft` on, **20 in-game years** (~50 minutes at speed 50).
- **09B**: same start, `diplomacy.test_statecraft off` right after load, **10 in-game years**.
  09A's first ten years against 09B is the **second statecraft A/B pair** TODO 5 asked for.

Then `python tools/analyse-log.py <log>` on each (it has sections for today's telemetry).

**What it must answer:**

| # | Question | Where it reads |
|---|---|---|
| 1 | Design 10: what share of each war's closing score is prisoners? D2 was kept knowing it dominated the live check | war-score section |
| 2 | Tributary pacts and subjugations per decade; wars still well under 252 days | peace outcomes |
| 3 | Is the §13 tribute band still unreachable (run 08: 0 of 123)? Decided **not to move it** until this run shows it under design 10's score | peace outcomes |
| 4 | How often an indemnity is taken, how large against the treasury, and whether any AI realm is left below `AiGoldReserve` (50k) and stops making amends or seating anyone | indemnity + court verbs |
| 5 | Tribute demands: accepted/refused by reason; do the cap and the cooldown ever bind | tribute section |
| 6 | Court → foreign policy: how many war declarations carried a court term, and did any tip past the threshold | R-1 section |
| 7 | Legal neglect: how many link-weeks, and did F3 defection fire more | hegemony section |
| 8 | Civil war with the 84-day cooldown and AI amends: any internal war, side change, restitution | civil-war section |
| 9 | Pacts, 09A's first decade against 09B (run 08: 47 vs 31) | pacts |
| 10 | Errors / warnings | header |

## C. Civil-war run

From `di_pretender_test` (Battania rises on the first tick), **5 in-game years at speed 50**, the
player not interfering. Measures what run 08 could not: side changes over a real war, whether
conceding at 75 ends wars too early, prices against purses, restitution on a crown win, and
whether a cadet branch ever stands at a succession after its influence share.

## D. Needs a human

- **Esc over the peace table** (UI-INTEGRATION.md §0c.7): open it, press Esc, note what happens.
- **The intermittent launcher crash**: one official-launcher Play with the mod unticked
  (STATUS-history, "Intermittent").
- **Espionage 3.6** (parked): what assigns governors and parties to an AI clan's heroes needs IL
  of the real game assemblies - `dotnet run --project tools/CallSites` on the lead's machine. The
  cloud reference assemblies have no method bodies.

## What to send back

The mod logs of A, 09A, 09B and C, the `analyse-log.py` output of each, and screenshots of A6's
Realm-tab row and A5's peace table.
