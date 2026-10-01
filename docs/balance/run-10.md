# Balance run 10 — the vassal summons and the bound patron's choice, in an unattended world

Story 1.10c ST-8 and story 1.10d AC7, run together: one 10-year run, because both stories change
what a patron does in a war and a second run would only repeat the first. Run on 2026-10-01,
18:03-18:34 (real), monitored through GABS.

| | |
|---|---|
| Build | the working tree committed as `d4ccde0`, built locally against v1.5.3 and installed with `deploy.ps1` (the v1.4.8-reference DLL was not used for this run) |
| Game | v1.5.3 (Steam beta), BLSE Standalone through `games_start`, NavalDLC not loaded |
| Save | `di_fresh_1084`, statecraft on, intrigue on, the player parked in a town (Myzea, then Lageta, then Pravend after sieges), +50,000 gold as in run 09 |
| Length | Summer 3, 1084 → Autumn 1, 1094: **10.3 years**, 124 weekly snapshots, **79 wars ended** |
| Speed | `test_set_speed 50`; about 4.5 real minutes per game year |
| Errors | **0 `ERROR`, 0 `WARN`** in a 15,444-line log |
| Files | `run-10.log`, `run-10-analysis.txt`; the end state is saved as `run10_end` |

## 1. Headline

1. **The AI never summoned: 0 in 10 years.** 1.10c's AC10 ("AI hegemons issue summons, count > 0") is
   **not met**. The mechanism is not broken - it served, refused, excused and released correctly
   every time it was forced (STATUS.md, the two live passes) - it is *unreachable* in this world.
2. **Why, measured:** a vassal serves only at **Hold ≥ 40**. Of 154 link-weeks recorded, **15
   (9.7%)** had Hold at or above 40; the highest any link reached was 57.1. Vassals answered a
   patron's call **2 times** in the run (defied 2, excused 5). A summons needs a serving vassal
   *and* a patron ruler commanding an army at the weekly decision, on top of that.
3. **1.10d works in an unattended world: 13 choices, 6 honoured the vassal, 7 honoured the treaty.**
   Both answers occur (AC7). The patrons' own reasons are in the log and match
   `diplomacy.bound_choice`.
4. **No war volume problem:** `DefendAlly` is **31.6%** of 79 wars, under the ~35% bar. It is higher
   than run 09's 26-27%, with the 1.10d rule the most likely difference; one run each, so not shown.
5. **Wars are as short as before:** mean 56.3 days, median 49, PASS (the bar is 252).

## 2. 1.10d, the bound patron's choice

| ~Date | Patron → vassal, attacker | Treaties between patron and attacker | Answer | The patron's own reason |
|---|---|---|---|---|
| Winter 1084 | Aserai → Southern Empire, Battania | DefensivePact | **defended** | broke it |
| Summer 1085 | Western Empire → Southern Empire, Northern Empire | NAP | **defended** | broke it |
| Summer 1085 | Western Empire → Southern Empire, Khuzait | DefensivePact + Alliance | stayed out | worth 80 against 118 |
| Winter 1088 | Southern Empire → Western Empire, Northern Empire | Truce | stayed out | worth 22 against 33 |
| Spring 1089 | Battania → Western Empire, Northern Empire | Alliance + DefensivePact | stayed out | worth 71 against 124 |
| Summer 1089 | Battania → Western Empire, Aserai | Truce | stayed out | worth 3 against 33 |
| Winter 1089 | Aserai → Western Empire, Sturgia | Truce | **defended** | broke it |
| Winter 1089 | Aserai → Western Empire, Southern Empire | Truce | **defended** | broke it |
| Winter 1090 | Aserai → Western Empire, Battania | Truce | **defended** | broke it |
| Summer 1093 | Vlandia → Battania, Western Empire | NAP + DefensivePact | **defended** | broke both |
| Summer 1093 | Vlandia → Battania, Sturgia | TributaryPact | stayed out | worth 88 against 110 |
| Summer 1093 | Khuzait → Northern Empire, Vlandia | DefensivePact | stayed out | worth 39 against 56 |
| Summer 1094 | Western Empire → Southern Empire, Khuzait | Alliance + DefensivePact | stayed out | worth 5 against 118 |

- **Treaties torn up: 7** (Truce 3, DefensivePact 2, NAP 2), by Aserai 4, Vlandia 2, Western Empire
  1. No patron lost all its treaties to this rule (AC7). 0 wars refused after a breach.
- **The cost reads the way the live check fixed it.** Before the sign fix the same arithmetic
  made every breach free; here a link worth 3 does not buy a 33-point breach, and a link worth 80
  does not buy a 118-point one. Aserai tore up three truces; a truce is the cheapest
  treaty to break (33 in the logged costs).
- **The same patron answers differently by case**: Western Empire broke a NAP for a link worth more
  than it cost, and kept an alliance and a defensive pact against the same war's other attacker.
  That is the function working, not a coin flip.
- **Not seen:** a player patron (the player was a bystander), an exhausted patron, and the 60-second
  prompt.

## 3. 1.10c, the vassal summons

| Question | Answer |
|---|---|
| Did any AI hegemon summon? | **No. 0 `issued`, 0 `served`, 0 `refused`** |
| Hold at or above 40, link-weeks | 15 of 154 (9.7%); the top four links: Aserai→Western Empire 7 of 18 weeks (max 57.1), Western Empire→Southern Empire 5 of 23 (45.9), Southern Empire→Western Empire 2 of 28 (42.0), Southern Empire→Northern Empire 1 of 7 (44.0) |
| Vassal answers to a patron's call | answered 2, defied 2, excused 5 (run 09A: answered 8, defied 11, excused 3 in twice the years) |
| Did a spent or besieged vassal get summoned anyway? | No - nothing was issued, so nothing was excused wrongly |
| War share (the design 04 §10 check) | `DefendAlly` 31.6%, under ~35% |

**What this does and does not say.** It does *not* say summons is balanced: it says the AI's
weekly path is almost never open. Three things must hold at once - a serving vassal (Hold ≥ 40,
about one link-week in ten), an obligation war it is already fighting for the patron (the vassal
answered a call twice), and a ruler with an army that week. The story's prices (150 + 40 per party
influence, 20,000 + 5,000 per party gold) were meant for a world where that happens often; they were
never exercised by an AI here, so **no price in 1.10c has been measured**. The forced orders of
the live passes (a ruler holding 65,000 influence and 4.6 million gold in `di_hegemony_1166`) say
only that a ruler in that save can pay; this run's rulers' purses were not read.

## 4. For the lead

1. **Is a summons that the AI almost never reaches what is wanted?** The player can always reach it
   (the Realm tab, any serving vassal); the AI gets it about 0 times a decade at today's Hold. Options:
   (a) leave it - a player lever that the AI seldom reaches; (b) let the AI summon a vassal at Hold 30-39 as well, accepting refusals as marks (a
   hegemon harassing its own vassals), or (c) leave summons as it is and raise Hold elsewhere - that
   is the vassalage question of design 04 §12, not this story's.
   Recommendation: **(a)**. A summons the AI cannot reach is a feature, not a fault.
2. **Hold is below 40 for 90% of link-weeks.** This was already true in run 09 (mean 17-38) and
   is why "refuses summons" is the commonest state in the Hold table. It is a design fact about
   vassalage today. Nothing in this run changes it.
3. **1.10d's AI looks right.** If the lead wants it more or less willing to break treaties, the three
   constants that decide it are `BoundChoiceCaution`, `BoundChoiceHoldPerPoint` and the per-treaty
   values - all marked UN-TUNED; this run gives one sample of 13.

## 5. Not measured

- Any price, the duration (20 days) and the cooldown (42) of 1.10c: no AI summons.
- A player as patron or vassal in the unattended world.
- The 20-year horizon; this is one 10-year run.
- The exhausted-patron gate in 1.10d (no case arose).
- The v1.4.8-reference DLL on v1.5.3 over a long run (it started a rising and ran a summons; see STATUS.md).
