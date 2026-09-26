# Balance run 08 — statecraft on against statecraft off

**2026-09-26.** Branch `development` at `669a431` (Phase 2.8 S0-S2), built 2026-09-26 01:51.
Two fresh campaigns from the same save, `di_fresh_1084`, run as an A/B pair: A with
`EnableStatecraft` on, B with it off. This is design/08's S3 measurement, and it also carries
the five questions of [design/04 §13.7](../design/04-hegemony.md#137-what-the-next-run-must-answer)
that run 08 was owed.

| | Run A (statecraft on) | Run B (statecraft off) |
|---|---|---|
| Span | Summer 3, 1084 → Spring 8, 1094 | Summer 3, 1084 → Spring 12, 1094 (snapshots end Spring 8) |
| In-game years | 9.9 (119 weekly snapshots) | 9.9 (119 weekly snapshots) |
| Speed | `test_set_speed 50`, the player's party parked in a town | same |
| Log | `diplomacy-intrigue-20260926-015816.log` | `diplomacy-intrigue-20260926-023107.log` |
| **Errors / warnings** | **0 / 0** | **0 / 0** |

**How B was switched off.** B's `[RUN]` header reads `statecraft=true`, because the switch was
flipped with `diplomacy.test_statecraft off` after the session had started and the header is written
at launch. The flip itself is not logged. The evidence that B ran with the layer off is that B's log
has **zero `skill_xp` events**, against 341 in A. Nothing grants XP while the switch is off.

**One pair is one sample.** Both runs start from the same save, but the worlds split within the
first year: Vlandia led B from 1084 to 1089 and Khuzait from 1091, while Aserai and Sturgia traded
the lead in A. Every A/B difference below sits on top of that divergence. A difference in this
table is a question for a second pair, not a measured effect.

## 1. Headline

- **The layer does not break the war economy.** Chosen wars last the same (mean 71 vs 73 days,
  median 57 vs 60), the peace table ends the same share of wars (57% vs 61%), and white peace comes
  out the same (28 vs 29). Phase 1's acceptance still holds with the layer on: mean war 64 days,
  well below the 252-day line.
- **XP does not inflate skills.** Over ten years the layer granted 1.04 million XP, and the Envoy's
  median Charm moved from 232 to 235. The other five offices did not move in A. D6 was accepted on
  the worry that XP might drift the medians; it does not.
- **The one large difference is pacts**, and there is a mechanism that could cause it (§3). One pair
  cannot tell that effect apart from noise.
- **design/08 §12's acceptance is not yet met.** Its point 1 (±15%) holds on most measures but not
  on total wars (+20%, all obligation wars). Points 2 (each term's direction across realms) and 4
  (a player's year) were not measured. design/08 §17 has the table.
- **No civil war happened in either run.** No internal war, no contested succession, no side change.
  A ten-year run from a fresh 1084 start cannot measure the Phase 2.6 balance questions.

## 2. The war economy, side by side

| | A (on) | B (off) |
|---|---|---|
| Wars ended | 67 | 56 |
| Chosen wars: n / mean / median | 45 / 71.2 d / 57 d | 45 / 73.1 d / 60 d |
| Obligation wars: n / mean | 22 / 48.7 d | 11 / 46.2 d |
| All wars: mean / median | 63.8 d / 52 d | 67.8 d / 57 d |
| Ended by the peace table | 38 (56.7%) | 34 (60.7%) |
| Ended dormant | 17 (25.4%) | 13 (23.2%) |
| Follower release | 11 (16.4%) | 5 (8.9%) |
| Submission | 0 | 2 (3.6%) |
| Defection | 1 | 2 |
| White peace | 28 | 29 |
| Indemnity (2,000-7,000) | 24 | 11 |
| Vassalage conceded at the table | 0 | 4 |
| Fiefs changing hands | 90 | 85 |
| Mean final war score (absolute) | 26.9 | 21.0 |
| Weeks with every kingdom at war | 37 / 119 (31%) | 55 / 119 (46%) |
| Average exhaustion (max) | 22.3 (38.0) | 23.6 (53.7) |
| Kingdoms alive at the end | 8 | 8 |

The chosen-war count matches exactly (45). The extra eleven wars in A are all obligation wars, calls
to arms answered by allies, and that follows from A's larger alliance web (§3).

## 3. Pacts: the difference worth a second look

| | A (on) | B (off) |
|---|---|---|
| AI pacts signed | 47 | 31 |
| Treaties signed (all kinds) | 119 | 94 |
| Most alliances at once | 8 | 4 |
| Weeks with any alliance | 113 / 119 | 108 / 119 |
| Most defensive pacts at once | 7 | 5 |

The layer touches pacts in two places:

- **Persuasion** is added to the value of the kingdom that is asked (S-3).
- **Treaty influence cost** is scaled by the proposer's Envoy (S-2).

Persuasion is centred on the median, so on average it should cancel out. Two things may break that
average:

- **Proposers are not a random sample.** A realm whose Envoy is above the median pays less to
  propose and is refused less often.
- **The median moves.** Charm is the one skill whose median moved.

Neither is shown here. What is shown is that A signed about half again as many pacts and carried
twice the alliances at its peak, and that this was the only place the two runs diverged by more
than the world did. **A second A/B pair from a different seed is the next step** before any
constant is changed on the strength of it.

## 4. Statecraft's own record (run A)

| Act that trained a skill | Grants | Total XP | Skill |
|---|---|---|---|
| Peace signed | 74 | 390,200 | Charm |
| War ended | 70 | 258,028 | Leadership |
| Peace dividend | 33 | 165,000 | Steward |
| Alliance signed | 16 | 80,000 | Charm |
| Defensive pact signed | 17 | 51,000 | Charm |
| Submission received | 5 | 40,000 | Leadership |
| Non-aggression pact signed | 14 | 28,000 | Charm |
| Tribute paid / received (vassal tribute) | 56 + 56 | 28,000 | Trade |

By skill: Charm 549,200; Leadership 298,028; Steward 165,000; Trade 28,000. Roguery and Scouting
received none, because nothing in these runs trained them: no claim was fabricated or exposed, and
Scouting has no act in S2.

**The median actor's skill, first weekly snapshot to last:**

| Office | A (on) | B (off) |
|---|---|---|
| Ruler — Leadership | 226 → 226 | 226 → 226 |
| Envoy — Charm | 232 → **235** | 232 → 230 |
| Steward — Steward | 232 → 232 | 232 → 228 |
| Treasurer — Trade | 228 → 228 | 228 → 228 |
| Spymaster — Roguery | 238 → 238 | 238 → **217** |
| Watch — Scouting | 189 → 189 | 189 → 188 |

B's Roguery fell by 21 with nothing training or untraining it. That is a change of who holds the
office (a death, a marriage, a house leaving), and it is the size of the noise. A's +3 on Charm is
well inside that noise. The XP amounts in §6 of design/08 are safe to keep as they are.

## 5. The §13.7 questions

A game year is 84 days, so twelve weekly snapshots make one year below.

**1. How many links form, and how many survive their first year?** Five in each run. In A, all
four that were old enough to judge lasted past their first year. The fifth formed in Autumn 1093,
too late to judge. In B, two of five did:

| Run | Patron ← vassal | Route | Formed | How it ended | Weeks |
|---|---|---|---|---|---|
| A | Aserai ← Western Empire | imposed | Autumn 1088 | vassal defected to Battania, Summer 1091 | 33 |
| A | Northern Empire ← Southern Empire | voluntary | Autumn 1089 | alive at the end | 53 |
| A | Battania ← Western Empire | defection | Summer 1091 | revolt, Summer 1093 | 22 |
| A | Sturgia ← Vlandia | imposed | Summer 1092 | alive at the end | 23 |
| A | Sturgia ← Battania | voluntary | Autumn 1093 | alive at the end | 5 |
| B | Western Empire ← Northern Empire | submitted to attacker | Autumn 1085 | vassal broke it, Winter 1089 | 52 |
| B | Sturgia ← Battania | imposed | Winter 1088 | vassal defected to Northern Empire, Summer 1090 | 18 |
| B | Northern Empire ← Battania | defection | Summer 1090 | revolt, Summer 1091 | 11 |
| B | Southern Empire ← Western Empire | submitted to attacker | Spring 1091 | vassal defected to Vlandia, Summer 1091 | 3 |
| B | Vlandia ← Western Empire | defection | Summer 1091 | vassal broke it, Spring 1092 | 9 |

B shows the churn §13.6 warned of: Western Empire had three patrons in a single year. §13.2 was
expected to multiply the imposed route. It did not: two imposed links in A and one in B, against
three in run 07's twenty years. At ten years, the rate looks the same as before.

**2. Does tribute survive in its 65-75 band, or vanish?** **At the peace table it vanished.** Not
one of 123 peace settlements across both runs conceded a tributary pact. The only tributary pacts in
either run were B's two weekly AI demands (Aserai on Northern Empire and on Southern Empire), a
different route. Run 07 had 18 peace-table tributes. A peace-table tribute needs a war score between
65 and 75. The mean final war score here was 21-27, and few wars came near that band.

**3. The 50-55 band of `[SUBMIT]`: was lowering the threshold worth anything?** **Once, in B.**

- **A:** 685 weekly lines, maximum 49.5. No line reached 50.
- **B:** 766 lines, 3 at or above 50, 1 of them between 50 and 55.

The link that the lower threshold bought is B's Southern Empire ← Western Empire, which submitted
at value 53.3. It lasted three weeks before the vassal defected. A's two voluntary submissions came
at 59.9 and 58.0, and would have formed under the old threshold of 55 too.

**4. How often does the doomed-link case appear?** **Never at signing.** No link started with a
Hold target of 0; the lowest starting target was 24.1. That was A's Sturgia ← Vlandia, where the
patron was only 1.10 times as strong as its vassal, the closest either run came to §13.6's case.
That link was still alive at the end. Two links did reach a target of 0 after signing:

- **B's Western Empire ← Northern Empire**, within about eight weeks, as protection turned negative.
- **The two defection links that ended in revolt.**

Doomed links come from what happens after signing, not from the margin at signing. The strength
margin stays where the lead left it.

**5. Does `ReconcileWithSiblings` execute?** **No.** Neither run logged an
`overlord_imposed_peace` event. It needs a hegemon whose new vassal is at war with one of its older
vassals. A's Sturgia had two vassals at once only from Autumn 1093, and they were at peace. All
three defections warned no siblings (`siblingsWarned=0`), because no patron had a second vassal at
the time.

## 6. What this run could not measure

- **Civil war.** Zero internal wars, contested successions, houses divided or side changes in either
  run. The Phase 2.6 questions need a run that starts where a court is already strained. Two options:
  a longer run, or one from a save with a low-legitimacy realm (`test_set_legitimacy`). Both remain
  open.
- **The court's refusal of tribute (`AiTributeCourtRefusalShare`).** Acceptances are logged (two in
  B, none in A); refusals are not. A `tribute_refused` telemetry event is needed before that share
  can be tuned.
- **Terms that need the player or a rare state:**
  - Negotiation at the table did change the budgets. That part is live-verified in design/08 §17.
  - Haggling, Silver Tongue and the S-8 succession backing got no use in either run, because no
    house was bought and no succession was contested.
- **Whether statecraft changes the pact rate.** §3: one pair is not enough.

## 7. Files

- `run-08a.log`, `run-08a-analysis.txt`: run A, statecraft on.
- `run-08b.log`, `run-08b-analysis.txt`: run B, statecraft off.

The analysis files come from `tools/analyse-log.py`. The statecraft tables (§4) and the §13.7
answers (§5) come from the `skill_xp`, `[KINGDOM]`, `[LINK]`, `[SUBMIT]` and `[EVENT]` lines of the
same two logs.
