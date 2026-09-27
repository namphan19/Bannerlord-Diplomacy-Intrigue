# Design 10 — War score, measured in the fighting

Status: **built 2026-09-27 (W1 and W2 together), given one live check (§9a), decisions closed.**
The lead said to build in this direction. D1 and D3-D5 below are at their recommended values, D2
at the lead's own, kept after the live check; D6 was dropped (§10). What is left is the balance
run in §9, step 4. The resolver is
`Diplomacy/WarScore.cs`; the battle scoring is `WarExhaustion.AddBattleWarScore`.

The lead's call of 2026-09-26, after a live session against the Northern Empire: the war score
"is too unreasonable" and is to be redesigned. v1 of this document (2026-09-26) scored held land
and put it at the centre of subjugation. The lead rejected that on 2026-09-27, and rightly: a
power that holds most of a realm has no need of its submission, it takes the rest. v1 also made
land pay twice, since the winner already keeps every fief it holds at the peace (the table's town
and castle prices buy fiefs the loser *still* holds). The lead's direction for v2:

> **War score depends on the battles. A siege assault counts as the soldiers it cost. Lords
> held prisoner count.**

This replaces how war score is **earned**. What it **buys** is unchanged: the peace-table prices,
the subjugation cliff at 75, the envoy's ×0.85-×1.15 and the white-peace floor at 20 stay as
design 01 §4.2 and design 04 §13 have them.

Constants will live in `Diplomacy/DiplomacyConstants.cs`. Every number below is **UN-TUNED**, a
first guess with its reasoning.

---

## 0. What is wrong today

| # | Fault | Evidence |
|---|---|---|
| F1 | **Subjugation needs the whole kingdom.** The only realistic road to 75 is 6 towns, or 5 towns and 2 castles | town +12, castle +6 |
| F2 | **The battle cap is fake.** `|diff| / total × 6`, clamped 1-8: the difference never exceeds the total, so the ceiling is 6 | `AddBattleWarScore` |
| F3 | **Small battles beat big ones.** Ten skirmishes of 100 kills for 20 score 40; one battle of 1,000 for 200 scores 4 | same |
| F4 | **Size is ignored.** 1,000 dead is the same whether that is a fifth of the enemy's army or a twentieth | no term reads either kingdom's size |
| F5 | **Sieges earn nothing for the fighting.** Assault casualties are skipped (`ApplyBattleResult`, FieldBattle only) | `WarExhaustion.cs` |
| F6 | **Captured armies count for nothing.** A party taken whole has few casualties | casualties only |
| F7 | **Land pays twice.** A captured town scores +12 and is also kept at the peace | `ApplyFiefCapture`, `CedeFiefs` |

## 1. What history says

The historical survey behind this version (reported to the lead 2026-09-27). Five patterns recur:

1. **The weaker side submits when its army is broken or its ruler is taken, while it still holds
   most of its land.** Submission is the price of keeping the throne. Zama (202 BC): Carthage kept
   its African core and lost its freedom of war. Cynoscephalae (197 BC), Magnesia (190 BC): the
   loser kept its kingdom. William the Lion captured at Alnwick (1174): Scotland became a vassal
   at Falaise. Injo besieged at Namhansanseong (1637): Joseon submitted to the Qing without losing
   territory. Kosovo (1389), Mohács (1526): Ottoman vassals.
2. **A realm that has lost most of its land is annexed, not made a vassal.** Macedon after Pydna,
   Carthage in 146 BC, Serbia in 1459, Buda in 1541.
3. **The stronger side takes a vassal when ruling the land itself costs more than the tribute.**
   The Golden Horde and the Rus principalities, Wallachia and Moldavia under the Ottomans, the
   British princely states. The Ming annexation of Đại Việt (1407-1427) is the counter-example that
   proves the cost.
4. **Tribute is the price of a favourable stalemate, not of a rout.** Chanyuan (1005): the Song
   paid the Liao after a campaign neither side could finish. Adrianople (1547): the Habsburgs paid
   the Ottomans for their part of Hungary.
5. **Escalation is stepwise, and annexation punishes a revolt.** Assyria, Rome and the Ottomans all
   ran tribute, then vassalage, then a puppet, then a province.

What a peace table should read, then, is **how badly the loser's armies are broken and whose
lords sit in whose dungeons**, not how much land has changed hands. Land already taken is kept.

Patterns 2, 3 and 5 (when a winner prefers annexation) are **not** in this design. The lead
dropped the question on 2026-09-27 (§10, D6): a winner that wants the rest of a realm takes it by
continuing the war.

## 2. The formula

```
WarScore = Battles + Prisoners            aggressor-positive, as today
```

| | **Battles** | **Prisoners** |
|---|---|---|
| What | men each side lost in battle, sieges included, against its army at the war's start | enemy lords held in the other side's parties and dungeons |
| Nature | an **accumulation**, battle by battle | a **state**, read live |
| Decay | half-life **42 days** | none: it lasts exactly as long as the captivity |
| Saved | yes, in the existing `WarScore` field | no, derived |

Everything that reads the score (the peace table, the AI's war and peace logic, the Realm tab)
keeps reading one number.

## 3. Battles

```
lostShare(side) = men that side lost in the battle / that side's manpower at the war's start
battlePoints    = W × lostShare(enemy) − W × lostShare(own),   capped at ±BattleCap
                + WinPoints to the side that won the battle
W               = 120
BattleCap       = 30      (the proportional part only)
WinPoints       = 3       (only when the losing side fielded at least WinMinMen)
WinMinMen       = 100
```

- **A fixed award for winning** (the lead, 2026-09-27): a victory is worth something in itself,
  however even the losses. A repelled assault is a victory for the defender.
- **Only a battle of some size earns it.** Without a floor, ten skirmishes against 30-man parties
  would pay 30 points and reopen F3. The floor reads the loser's men in the field, militia
  excluded, so a lord's full party qualifies and a patrol does not. The old formula had the same
  guard (`WarScoreBattleMinTotal = 100`).

- **Men lost = killed + taken prisoner.** A party captured whole is an army destroyed (F6). The
  wounded are **not** lost: they rejoin their party within days. Checked by IL at build
  (v1.4.8): `MapEventSide.TroopCasualties` counts killed **and** wounded, so it is not used here
  (exhaustion keeps it). `MapEvent.CaptureDefeatedPartyMembers` empties a defeated side's rosters
  *before* `MapEventEnded` fires, so the rosters cannot be read after the fact either. What is
  used: per party, `DiedInBattle` for a side that held the field or got away, and
  `HealthyManCountAtStart` - everyone - for a side defeated without retreating. That count
  includes a party's heroes, a handful per party.
- **Every battle type scores**: field battles, sally-outs, and **siege assaults** (F5). When a
  fortification falls, the garrison that surrenders is men taken prisoner and scores like any
  other loss. The capture itself scores nothing (F7): the town is kept at the peace, and the
  exhaustion for losing it stays where it is.
- **Militia and villagers count on neither side**, in the losses or the manpower. They are
  townsfolk, not the realm's army. So a **village raid scores nothing**: raids still cost the
  victim exhaustion, as today, but they are not battles between armies.
- **Manpower** = the men in the kingdom's lord parties and garrisons, **snapshotted when the war
  begins**. Measured against what the realm had (F4), and fixed so the same defeat is worth the
  same on day 1 and day 200. The alternative, live manpower, lost because it makes each battle
  against a beaten realm worth more than the last and turns the end of a war into a lottery.
- **BattleCap = 30**, a quarter of the army lost net in one day. One freak battle cannot decide a
  war (every valuation in the mod follows "no single term clears the bar alone").
- **W = 120** so that breaking about a fifth of the enemy's army at modest cost is worth ~23, and
  three such victories in a season reach the tribute rung.

### Decay

Battle points lose **1.65% of themselves per day**, with a floor of 0.05/day so they finish
clearing: a **half-life of 42 days**, two seasons, half of the 84-day year. A victory in the
opening season still counts at the peace, at about a quarter. The old flat drift (0.05/day) goes.
A stalemate still drifts to zero and to a white peace; each side keeps the fiefs it holds, which
is *uti possidetis*, how most real stalemates ended.

## 4. Prisoners

```
Prisoners = Σ weight of every enemy lord the side holds now, no cap
  ruler            20
  clan leader      10
  other lord        5
```

- **A state, not an event.** A lord counts while he is held by any party or settlement of the
  other kingdom, and stops counting the moment he is ransomed, released or escapes. This is the
  historical lever (William the Lion signed at Falaise in captivity), and it gives the player a
  real choice: ransom the lords for gold now, or keep them in the dungeon until their king signs.
- Vanilla frees lords taken in ordinary battles within days. As a state, a brief capture scores
  briefly and does no harm; an event with decay would have made each capture-and-escape pay again.
- A lord is a hero of a noble clan in the enemy kingdom. Heroes of mercenary and minor clans and
  companions do not count.
- **No cap**, the lead's call of 2026-09-27, with clan leader 10 and lord 5. This knowingly departs
  from the rule every other valuation in the mod follows (no single term clears the bar alone):
  a side holding the enemy's ruler and five of its clan leaders reaches 75 without another battle.
  That is Falaise, and it is meant to be possible.

## 5. The scenarios

Northern Empire as the loser, manpower ~4,200 at the war's start (2,200 in lord parties, 2,000 in
garrisons, read off `testmod` on 2026-09-26 - an estimate to be measured at build). Khuzait as the
winner, ~4,500. Envoys even.

**Single events:**

| Event | Points |
|---|---|
| A great battle: NE loses 1,000, Khuzait 200 | 120 × (23.8% − 4.4%) + 3 = **26.2** |
| A crushing battle: NE loses 1,700, Khuzait 300 | 40.6, capped at 30, + 3 = **33** |
| A skirmish against a 30-man party, 30 for 5 | **0.7**, no win award (ten of them: 7) |
| The Amprela assault: Khuzait loses 400; NE 390 dead and 150 of the garrison captured | 120 × (12.9% − 8.9%) + 3 = **7.8** |
| An assault repelled: attacker loses 400, defender 100 | 120 × (2.4% − 8.9%) − 3 ≈ **−10.8** to the attacker |
| Holding NE's ruler and two clan leaders | 20 + 20 = **40** |

(Nothing here assumes a garrison surrenders to hunger: v2's first draft had such a row, and it is
not known that vanilla has that mechanic. A starving garrison loses men, which is exhaustion.)

**Reaching the table** (decay: a battle 3 weeks old keeps 70%, 6 weeks 50%, 9 weeks 35%):

| Scenario | Score | Buys |
|---|---|---|
| **A.** Two great battles three weeks apart | 18.5 + 26.2 = **44.7** | A castle, and prisoners |
| **B.** Three great battles in six weeks, one lord held | 57.7 + 5 = **62.7** | **Tributary pact** |
| **C.** Three great battles in six weeks, one clan leader and three lords held | 57.7 + 25 = **82.7** | **Vassal** |
| **D.** A great battle, then a crushing one three weeks later; the ruler and two clan leaders held | 18.5 + 33 + 40 = **91.5** | **Vassal** (the Zama-and-Alnwick case) |
| **E.** Prisoners alone: the ruler and five clan leaders, one lord | **75** | **Vassal**, with no battle needed after the capture |
| **F.** Sieges only: three towns taken by assault like Amprela | **23.4**, decaying | Prisoners or a little money; the towns are kept anyway |
| **G.** Ten skirmishes | **~7** | Nothing (F3 closed) |
| **H.** Scenario C, then 60 days without signing, every lord ransomed | ~21 | The victory is gone. Sign while it is fresh |

So: **tribute takes a realm's army beaten repeatedly in one season; vassalage takes that plus a
few of its great men in your dungeons, or its ruler and much of its court**, which is patterns 1
and 4 of §1. Prisoners now weigh as much as the battles, which is the lead's intent.

## 6. What stays the same

- The price ladder: prisoners 5, castle 25, town 45, tribute 60, subjugation 70 (+5 = 75),
  8 per 1,000 denars. Captured fiefs are kept at the peace, as in vanilla.
- The subjugation cliff at 75, its two faces, and every gate on it.
- The envoy's contest, the white-peace floor at a raw 20.
- Exhaustion, entirely: towns lost, raids, sieges, casualties all still wear a realm down, and the
  loser's willingness rule `exhaustion ≥ 60 − score/2` is untouched.
- One war record per pair of kingdoms; a battle scores in the war between the two kingdoms that
  fought it.

## 7. Risks

- **Much depends on the manpower estimate.** If real manpower is double the 4,200 read by eye,
  every battle is worth half and tribute recedes. The build measures it before any number moves.
- **Prisoner churn.** Vanilla takes lords in ordinary battles and frees many within days, and at
  10 and 5 a single battle that bags two clan leaders and four lords is worth 40 at once. The AI's
  peace evaluation, which is weekly, will read whatever the day happens to show. Accepted: that is
  what the captivity is worth, and it reverses honestly. The live check measures how long lords
  actually stay held.
- **Large courts are easier to hold hostage.** Prisoner weights are absolute, so a realm of ten
  clans exposes more points than one of three. Not corrected on purpose: more great men in the
  field is more great men to lose.
- **More vassalages, or fewer?** Unknown until a run. Run 08 had 2 imposed links in ten years with
  a mean final score of 21-27. A balance run is required before this ships as default.

## 8. Save data

- `WarRecord.WarScore` (id 7) is kept and now holds **Battles** only, with the new decay, renamed
  `BattleScore` in code. The save system keys a member by type level and local id
  (`MemberTypeId`), never by name, so the rename is safe. Its meaning narrows; it does not change
  sign or scale.
- **Two new properties: id 15 `AggressorManpowerAtStart`, id 16 `DefenderManpowerAtStart`**
  (int). Ids today run 1-14.
- Prisoners is derived and needs nothing saved.
- `FiefsTakenByAggressor` / `FiefsTakenByDefender` (10, 11) are still written for telemetry.
- **Running wars on an old save:** the missing manpower is read live the first time it is needed,
  and the old `WarScore` is carried over as Battles and decays. It still contains the fief-capture
  points of the old formula, which clear at the new half-life. No schema bump: nothing is
  reinterpreted that the decay does not already wash out. **D5.**
- `scripts/check-save-ids.ps1` must pass.

## 9. Surfaces, order and proof

- `diplomacy.wars` and `diplomacy.peace_allowance` print the breakdown:
  `war score 63.0 = battles 51.0 + prisoners 12.0 (1 clan leader, 3 lords)`, and each war's
  manpower at start.
- The Realm tab's war row keeps the total; its hint gives the breakdown. A number shown is the
  number the AI used.
- `[WAR]` telemetry gains `battles`, `prisoners`, and both manpower snapshots.

1. **W1** - manpower snapshot, battle scoring by lost-share for every battle type, garrison
   captures, decay; remove the fief-capture and raid score. Compile, LoadProbe, check-save-ids.
2. **W2** - Prisoners, the breakdown in the commands and the Realm hint.
3. **Live check** on `testmod_claude_1`: measure NE's real manpower, fight one field battle and
   one assault, capture a lord, and read every line of the breakdown against §3 and §4 by hand.
4. **Balance run** (20 years): tributes and subjugations per decade, and which component closed
   each war.

## 9a. First live check, 2026-09-27

`testmod_claude_1` (the player rules Khuzait), four wars eight days old when loaded, run from
Summer 9 to Summer 17, 1084 at speed-up 30. Stopped there: a Kingdom Decision addressed to the
player held the clock, and the data below was enough to go back to the lead.

**Manpower at the wars' start, measured** (filled on load, the save predating the fields): 3,365
(Northern Empire) to 5,215 (Aserai). The §5 estimate of ~4,200 holds.

**Battles score as specified.** 14 battles scored in 8 days. Checked by hand:

| Battle | Lost / manpower | Points |
|---|---|---|
| Sally-out, Battania vs Western Empire | WE 1,141 of 1,141 fielded (taken whole) / 4,077; Battania 377 / 4,123 | 120 × (28.0% − 9.1%) = **22.6**, + 3 |
| Siege-outside, Khuzait vs Northern Empire | NE 921 of 921 / 3,365; Khuzait 105 / 3,936 | 120 × (27.4% − 2.7%) = **29.6**, + 3 |
| Field, Khuzait vs NE, a 30-man party | 30 / 3,365 against 1 | **1.0**, no award (under 100 men) |
| Siege, Khuzait vs NE, Summer 14 | Khuzait 101 of 972; **NE fielded 0** | −3.1 |

The last row is **not explained**. Vanilla's defenders of a siege are the settlement's own party
and the mobile parties inside it (`Town.GetDefenderParties`, by IL), so a garrison should count.
Either that castle held militia only, or garrisons are being missed. `battle_scored` now also
logs every man on each side (`attackerSideMen`, `defenderSideMen`) so the next run can tell.

**Read from the code, 2026-09-27** (a cloud session, no game: nothing below has been seen in a
battle). The code has one way to drop a real garrison from the count, and the row fits it:

- `CountLosses` decided which realm a party fought for by the party's **live** map faction, set
  against the side's faction. A garrison's map faction follows whoever holds its walls. The side's
  faction is kept in a field of its own (`MapEventSide._mapFaction`), which suggests it is fixed
  when the side forms - consistent with the scorer reading `defender = Northern Empire` on this
  very battle, though not proven by it.
- The row's −3.1 is the proportional part alone. A repelled assault would have added the
  defender's award (972 attackers is over the 100-man floor), −6.1 in all, so the defender did not
  win it: the castle most likely fell.
- If vanilla hands a fallen fortress to its captor **before** `MapEventEnded` fires, its garrison
  reads as Khuzait's when counted, counts on neither side, and Northern Empire "fielded 0". Whether
  it does is not readable here: the reference assemblies carry no method bodies.

So, two changes:

- **Counted right either way.** A garrison of the assaulted fortress, on the defending side, that
  already reads as the attacker's is counted for the side that held the walls
  (`WarExhaustion.HeldItsOwnWalls`). If the owner has not changed when the event fires, the
  garrison reads as the defender's and is counted as it always was: the rule changes nothing. A
  garrison reading as a third realm stays uncounted, as before. With it counted, a won assault
  also clears the 100-man floor for the win award.
- **The log decides it.** `battle_scored` now also carries `winner`, `settlement`,
  `settlementNow` (who holds it as the event ends) and a list per side, `attackerParties` and
  `defenderParties`, one party per `/`-separated entry as `name:kind:faction:counted:men:died`,
  with `:gone` on a party no longer active when the event fired. Kind is lord, garrison, militia,
  patrol, villager, caravan, bandit, settlement or other; counted is `yes`, `yes(walls)` (the
  rule above), `no(army)` (not a lord party or garrison: militia, the settlement's own party) or
  `no(realm)`.

**How the next run settles it**, on the first `battle_scored type=Siege winner=attacker` line:

| What the line shows | Meaning |
|---|---|
| a garrison `yes(walls)`, and `settlementNow` is the attacker | the capture comes first; this was the cause, and it is fixed |
| the garrison `yes`, with its men | the owner changes after the event: garrisons are counted, and the §9a castle most likely had none to count |
| no garrison with men, militia only | that castle held militia only; the row was right |
| a `settlement` entry with men above 0 | vanilla folds the garrison into the settlement's own party in a siege; uncounted, and needs a rule |
| a `lord` entry `no(realm)`, faction `none`, `:gone` | a defeated lord's party destroyed before the event; a second fault, not fixed here |

**Prisoners dominate from the first week.** On load, eight days into each war:

| War | Battles | Prisoners held (each way) | War score |
|---|---|---|---|
| Northern Empire vs Khuzait | 9.1 | 5 vs **50** (NE's ruler, 2 clan leaders, 2 lords) | −35.9 |
| Western Empire vs Battania | −29.0 | 35 vs 55 | −49.0 |
| Southern Empire vs Aserai | −3.5 | 0 vs 20 | −23.5 |
| Sturgia vs Vlandia | 0.0 | **40** vs 0 | **+40.0** |

Six days later Southern Empire's score went from −23.5 to **+32.1** on prisoners alone (0 → 50),
and Western Empire stood at **−75.2**, the subjugation cliff, two weeks into its war. Under the
loser's rule (`exhaustion ≥ 60 − score/2`) a realm at −75 signs at exhaustion 22.5. Vanilla takes
lords in almost every battle and holds many of them for weeks, so at 20 / 10 / 5 uncapped the
prisoner term is the war score, and it swings by tens of points a week. §7 named this risk;
it is larger than written there. **D2 went back to the lead, who kept it as it was the same day
(§10).** The next balance run measures what that does to the rate of tributes and subjugations.

## 10. Decisions for the lead

All closed on 2026-09-27. The values are the lead's; none is yet measured by a balance run, so
the constants still say UN-TUNED.

| | Question | Decision |
|---|---|---|
| **D1** | Battle weight `W`, per-battle cap, the win award and its size floor | **120, cap 30, +3 for a battle whose loser fielded 100+ men**, as recommended. Taken by the lead 2026-09-27 |
| **D2** | Prisoner weights and cap | **Ruler 20, clan leader 10, lord 5, no cap.** Decided 2026-09-27, reopened the same day by the live check (§9a: the term reaches 40-60 inside two weeks), then **kept unchanged** by the lead |
| **D3** | Wounded: lost or not | **Not lost**: they are back in the ranks in days |
| **D4** | Battle half-life | **42 days**, two seasons |
| **D5** | Running wars on an old save: carry the old score over and let it decay, or reset to zero | **Carry over** |
| ~~D6~~ | ~~Annex the rest of a realm at the table~~ | **Dropped** by the lead 2026-09-27. Not in this design and not a pending question: a realm is annexed by conquering it |
