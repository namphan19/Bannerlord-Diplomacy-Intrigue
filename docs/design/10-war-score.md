# Design 10 — War score, measured as damage

Status: **draft for the lead's review**, 2026-09-26. Nothing here is built.

The lead's call of 2026-09-26, after a live session against the Northern Empire: the war score
"is too unreasonable" and is to be redesigned from the game-master's seat. This replaces how war
score is **earned**. It does not touch what war score **buys**: the peace-table prices, the
subjugation cliff at 75, the envoy's ×0.85-×1.15 and the white-peace floor at 20 stay exactly as
design 01 §4.2 and design 04 §13 have them. Those were the lead's calls and nothing measured
since argues against them; the fault is on the earning side.

Constants will live in `Diplomacy/DiplomacyConstants.cs`. Every number below is **UN-TUNED**,
a first guess with its reasoning.

---

## 0. What is wrong today

Measured against the code (`WarExhaustion.cs`, `PeaceTable.cs`) and the 2026-09-26 session:

| # | Fault | Evidence |
|---|---|---|
| F1 | **Subjugation needs the whole kingdom.** The only realistic road to 75 is 6 towns, or 5 towns and 2 castles. The Northern Empire has 5 towns. By the time a vassalage is possible there is little left to make a vassal of | design 01 table: town +12, castle +6 |
| F2 | **Battles barely count, and the cap is fake.** A battle scores `|diff| / total × 6`, clamped 1-8. Since the difference can never exceed the total, the ceiling is **6**, not 8. Killing 1,000 for 200 is worth 4 | `AddBattleWarScore` |
| F3 | **Small battles beat big ones.** The formula reads the *ratio* of losses, not their size. Ten skirmishes of 100 kills for 20 score 40; one battle of 1,000 for 200 scores 4 | same |
| F4 | **Size is ignored.** Taking three towns off a five-town realm scores the same as taking three off a realm of sixteen fiefs. 1,000 dead is the same whether that is a fifth of the enemy's army or a twentieth | no term reads either kingdom's size |
| F5 | **Sieges earn nothing for the fighting.** Assault casualties are skipped to avoid counting a capture twice; the defenders destroyed in Amprela were worth exactly the +12 of the capture | `ApplyBattleResult`, FieldBattle only |
| F6 | **Captured armies count for nothing.** An enemy party that surrenders or is taken whole has few casualties; its men became prisoners, and the formula sees a small battle | casualties only |
| F7 | **Drift eats occupation.** Land a kingdom still holds drifts away at 0.05/day like a battle two years old. A winner at 76 who does not sign within ~20 days loses the right to demand submission while holding every town it took | `Drift` |

The common root: the score counts **events**, each at a flat price, when what a peace should
read is **how much damage the loser has actually taken, relative to what it had**.

## 1. The principle

> **War score = how much of the enemy you hold, plus how much of its army you have broken
> recently, each measured against what it had.**

Two components, with different natures:

| | **Land** | **Momentum** |
|---|---|---|
| What | the share of the loser's pre-war fortifications the winner now holds | battles won and villages burned |
| Nature | a **state**, read live | an **accumulation**, event by event |
| Drift | **none** - held land is a fact, and it reverses itself when retaken | **decays** - a victory fades over a season |
| Relative to | the loser's holdings on the day the war began | the loser's manpower on the day of the battle |

`WarScore = Land + Momentum`, aggressor-positive as today, and everything that reads the score
(the peace table, the AI's war and peace logic, the Realm tab) keeps reading one number.

## 2. Land

```
value(fief)    = 2 for a town, 1 for a castle          (villages follow their fief)
heldShare(A,B) = value of fiefs B held when the war began that A holds now
                 / value of every fief B held when the war began
Land           = WL × heldShare(aggressor, defender) − WL × heldShare(defender, aggressor)
WL             = 150
```

- **Relative to the loser's own size.** Three towns off the Northern Empire (5 towns, 10 castles,
  value 20) is 30% of it; three towns off a realm of value 30 is 20%.
- **Read live, never stored.** The fief ledger (`FiefHistory`, `FiefOwnershipRecord`) already
  records who held each settlement from when to when, so "held by B when the war began" and
  "held by A now" are both queries. Retaking a town removes its share at once, with no
  reverse event to remember.
- **Only this war's gains.** A fief taken from B in an earlier war was not B's when this one
  began and does not count. A fief B loses to a third kingdom counts for nobody.
- **Town 2, castle 1.** Rejected: prosperity-weighted value. It moves daily, nobody can read it
  off the map, and a sacked town would be worth less for having been sacked.
- **WL = 150** puts the full conquest of a realm at 150 and half of it at 75, the subjugation
  cliff. See §5 for why half and not a third.

## 3. Momentum

### 3.1 Battles, sieges included

```
lossShare(side)  = men that side lost in the battle / that kingdom's manpower before it
battlePoints     = WM × lossShare(enemy) − WM × lossShare(own),   capped at ±BattleCap
WM               = 80
BattleCap        = 20
```

- **Men lost = casualties + men taken prisoner.** A party captured whole is an army destroyed
  (F6). Measured as each party's roster before the battle minus after, rather than the engine's
  casualty counter; which of the engine's counters include the wounded is to be confirmed with
  `tools/ApiDump` at build time.
- **Manpower** = the men in the kingdom's lord parties and garrisons, read live, plus the
  battle's own losses so it is the figure *before* the battle. Militia count on neither side:
  they are townsfolk, and the town they defend already counts through Land.
- **Sieges count** (F5). The assault's losses score like any battle; the capture scores through
  Land. They are different things - destroying a garrison and holding the walls - so this is not
  the double count the current exclusion guards against.
- **Size matters in both directions** (F3, F4): 1,000 men out of 4,200 is 24% of the Northern
  Empire's army; thirty men from a caught party are 0.7%.
- **BattleCap = 20**, a quarter of the army destroyed in a day. One freak battle should not decide
  a war on its own.

### 3.2 Raids

`+1.0` per village raided, and raids together contribute **at most 15** to Momentum in one war.
Ravaging the countryside wears a realm down; it cannot by itself make one kneel.

### 3.3 Decay

Momentum loses **1% of itself per day**, with a floor of 0.05/day so it finishes clearing. A big
victory has a half-life of roughly 70 days - about a season. Land does not decay (F7): a
stalemate with land held stays a stalemate *in the holder's favour*, which is what occupation
means. A stalemate with no land changed hands still drifts to zero and to a white peace, as the
drift was always meant to do.

## 4. What stays the same

- The price ladder: prisoners 5, castle 25, town 45, tribute 60, subjugation 70 (+5 = 75),
  8 per 1,000 denars.
- The subjugation cliff at 75, its two faces, and every gate on it (`IsStrongEnoughToHold`,
  one patron only).
- The envoy's contest on the budget, the white-peace floor at a raw score of 20.
- Exhaustion, and the loser's willingness rule `exhaustion ≥ 60 − score/2`.
- One war record per pair of kingdoms; a battle scores in the war between the two kingdoms that
  fought it.

## 5. The scenarios, recalculated

Northern Empire as the loser: fief value 20, manpower ~4,200 (about 2,200 in lord parties and
2,000 in garrisons, read off `testmod` on 2026-09-26 - an estimate, to be measured at build).
Khuzait as the winner, manpower ~4,500. Envoys even unless stated.

| Scenario | Today | This design | Result under this design |
|---|---|---|---|
| **A.** 3 towns, 6 villages, one battle killing 1,000 for 200 | 49 - 3 drift = **46** | Land 45 + raids 6 + battle 15.5 = **66.5** | Tribute. With a better envoy (×1.15) **76.5: vassal** |
| **B.** 5 towns and 2 castles, 6 villages, 3 battles | 92 - 4.5 = **87.5** | Land 90 + raids 6 + battles ~25 = **~121** | Vassal - long before this point |
| **C.** 4 towns and one great battle | 48 + 4 = **52** | Land 60 + 15.5 = **75.5** | **Vassal** |
| **D.** Battles only: three that each break a fifth of their army | ~12 | ~3 × 13, decaying = **~35** | A castle or money. Land is what makes a vassal |
| **E.** Ten skirmishes of 30 men each | 10 × 1.7 = **17** | 10 × 0.5 = **5** | Nothing - the F3 exploit is closed |
| **F.** A small realm (2 towns, 3 castles; value 7): 1 town, 1 castle, one battle | 18 + 4 = **22** | Land 64 + ~12 = **~76** | **Vassal** - minor realms fall fast |
| **G.** A large realm (value ~24): 3 towns | 36 | Land 37.5 | Land alone buys a town back |
| **H.** Won to 76, then 60 days without signing | ~73: submission lost | Land unchanged, Momentum halved | Still vassal if the land carries it |

A siege assault on its own scores little net: at Amprela the attacker took ~400 casualties to
the defenders' ~390, so the fighting roughly cancels and the capture is what counts. That is the
honest reading of a costly assault.

**Why half the realm for subjugation rather than a third.** At WL = 200 scenario A reaches 75
without a battle, and a kingdom that has lost three towns of five while its field army is intact
would be made a vassal by a siege campaign alone. At 150 it takes land *and* a broken army, or
most of the land. That matches the lead's own narrative in design 04: a decisive victory takes a
tributary, an overwhelming one takes a vassal. **D1** asks the lead to choose.

## 6. Risks

- **More vassalages, sooner, for small realms.** Scenario F subjugates a two-town realm after one
  campaign. Run 08 found 2 imposed links in ten years; this will raise that, and every extra
  imposed link is a chance at the doomed 1.2%-stronger patron design 04 §13.6 left alone. A
  balance run is required before this ships as default.
- **Manpower is read live.** A kingdom that has just lost two armies has little manpower left, so
  its next battle is worth more. That is intended (the finishing blow), but it makes the last
  battles of a war swingy.
- **A defecting clan carries its fief with it**, and that fief then counts as the new kingdom's
  Land against the old one if they are at war. Counted deliberately - the realm did lose it - but
  it is a path to score without a battle. **D2.**

## 7. Save data

- `WarRecord` gets **one new property, id 15** (`Momentum`, float). Its ids today run 1-14.
- Land is derived from the fief ledger and needs nothing saved.
- `WarRecord.WarScore` (id 7) stops being written; the score becomes `Land + Momentum`. Its
  meaning changes, so **`CurrentSchemaVersion` goes 4 → 5**, with a one-time migration for wars
  already running: `Momentum = old WarScore − 9 × (fiefs taken by aggressor − fiefs taken by
  defender)`, 9 being the average of the old 12 and 6. Approximate, and says so. **D4.**
- `scripts/check-save-ids.ps1` must pass; id 7 is retired, never reused.

## 8. Surfaces

- `diplomacy.wars` and `diplomacy.peace_allowance` print the breakdown:
  `war score 66.5 = land 45.0 (3 of 5 towns, 0 of 10 castles) + battles 15.5 + raids 6.0`.
- The Realm tab's war row shows the total as today; its hint gives the same breakdown. A number
  shown is the number the AI used.
- `[WAR]` telemetry gains `land`, `momentum` and `raidMomentum`, so a balance run can see which
  component ends wars.

## 9. Order and proof

1. **W1** - Land from the ledger, the score as `Land + Momentum`, migration, the breakdown in
   `diplomacy.wars`. Compile, LoadProbe, check-save-ids.
2. **W2** - Battle scoring by lost-share, sieges and captures included, the raid cap, decay.
3. **Live check** on `testmod_claude_1`: take Amprela and one more NE town, win one field battle,
   and read each line of the breakdown against §2 and §3 by hand.
4. **Balance run** (20 years): subjugations per decade, which component closed each war, and
   whether any link formed on the 1.2% margin.

## 10. Decisions for the lead

| | Question | Recommendation |
|---|---|---|
| **D1** | Where subjugation sits: WL = 150 (half the realm, or land and a broken army) or 200 (a third of the realm) | **150** |
| **D2** | Does a fief carried off by a defecting clan count as Land | **Yes**, the realm lost it |
| **D3** | Momentum half-life: ~70 days (1%/day) | **70 days**, a season |
| **D4** | Running wars on an old save: approximate migration, or reset their battle history to zero | **Migrate**; a reset erases real victories |
| **D5** | Raid cap at 15 | **15**: raids alone reach a castle's worth, never a vassal |
