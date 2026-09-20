# Balance run 07 — the first run of the §12 vassalage work

**2026-09-20.** Branch `feature/run-06-fixes`, with the changes from
[design/04 §12](../design/04-hegemony.md#12-the-vassalage-drought-and-the-design-that-answers-it-2026-09-20).
A **fresh sandbox campaign from Summer 1, 1084**, not a resumed save.

| | |
|---|---|
| Span | Summer 1, 1084 → Spring 1, 1105 — **20.8 in-game years** |
| Wall clock | ~2 hours, `diplomacy.test_set_speed 50` |
| Log | `diplomacy-intrigue-20260920-150502.log` |
| Saves | `di_fresh_1084` (pristine start, hero parked), `di_run07_1104` (end state) |
| **Errors** | **0** — and 0 warnings, across 100 wars |

## 1. Why a fresh campaign

Every measurement before this one was taken on `di_review_0919_b`: Winter 1162, six kingdoms,
974 treaties, trust already dragged to the floor by a decade of war, and Khuzait at 29% of world
strength against Southern Empire's 6.5%. Every number from it carried that history.

A fresh start gives the condition the lead's design assumes: **eight kingdoms between dominance
0.89 and 1.17 — near parity.** That premise was doubted in discussion and the doubt was wrong;
at a true start the spread is small.

This matters because §12's central claim is about a *process* — war creates disparity, disparity
creates vassals — and a run that begins after the disparity already exists cannot test it.

## 2. Headline: the drought is over, through all four doors

[vassalage-absence-2026-09-19.md](vassalage-absence-2026-09-19.md) recorded **zero vassal links
in ~6 in-game years** across two runs, and zero peace-table vassalage in the whole history of the
project. This run produced **seven links through all four routes**:

| Date | Patron | Vassal | Route | Start Hold |
|---|---|---|---|---|
| Spring 1, 1091 | Western Empire | Northern Empire | `submitted_to_attacker` | 45 |
| Spring 15, 1094 | Battania | Northern Empire | `voluntary` | 60 |
| Autumn 8, 1096 | Vlandia | Northern Empire | `defection` | 45 |
| Autumn 7, 1098 | Vlandia | Sturgia | `imposed` | 35 |
| Autumn 15, 1101 | Northern Empire | Aserai | `imposed` | 35 |
| Winter 13, 1104 | Khuzait | Aserai | `imposed` | 35 |
| Spring 1, 1105 | Southern Empire | Northern Empire | `submitted_to_attacker` | 45 |

The three starting Holds are exactly the design's ladder: 35 coerced, 45 desperate, 60 voluntary.

**The two origins split by era, and nobody designed that.** From 1091 to 1096, while the kingdoms
were still near parity, every link came from *choice* — a cornered realm picking a protector, or
kneeling to the kingdom attacking it. From 1098, once Vlandia had pulled away, the *coerced*
route opened. War score measures disparity, so the door that opens depends on how far the world
has diverged. It falls out of the arithmetic rather than being written anywhere.

## 3. The band table, measured

§12.4.1 predicted tribute below war score 130 and vassalage above it, from arithmetic on
constants, and §12.6 flagged it as unmeasured. Four settlements bracket the boundary:

| War score | Winner wants (`min(score/2, 95)`) | Tribute + prisoners = 65 | Settled as |
|---|---|---|---|
| 98.1 | 49.1 | clears by 15.9 | tributary pact |
| **122.4** | **61.2** | **clears by 3.8** | tributary pact |
| 173.1 | 86.6 | short by 21.6 | **vassalage** |
| 175.8 | 87.9 | short by 22.9 | **vassalage** |

The boundary is `score × 0.5 > 65`, i.e. **score > 130**, and the 122.4 case sits 3.8 points
inside it. The predicted table is the observed table.

**The uncap is what makes this possible.** Peak scores reached **225.6**; the old clamp at 100
meant a winner could never want more than 50, so tribute at 65 always settled first. Four wars
peaked above 130, and every one of them was a war that the clamp would have cut off at 100.

## 4. Peak war scores, and a correction

Reconstructed per war from weekly `[WAR]` telemetry (104 war segments):

| Peak `|score|` | Wars |
|---|---|
| 0–20 | 28 |
| 20–40 | 24 |
| 40–60 | 20 |
| 60–80 | 15 |
| 80–95 | 8 |
| 95–130 | 5 |
| **130+** | **4** |

Median peak 40.6, max 225.6. **9 of 104 wars peaked at 95 or above; 4 reached 130.**

**Correction to a claim made during the session.** It was reported mid-run that war score bleeds
substantially between a war's peak and its settlement, and that this made the 95 and 130 gates
much harder than §12.4.1 suggests. That was generalised from a single war and is wrong. Measured
across this run the mean drop is **6.5 points**, and run 06 gives a median of 0.8. The one war
that dropped 23 points had inherited a high score from a save and then ran 80 days without a
decisive battle — an outlier, not the rule. Most wars settle near their peak.

## 5. A full vassal lifecycle, and §12.4.6 vindicated

Northern Empire went through every path the system has, in fourteen years:

1. **Spring 1091** — knelt to its attacker Western Empire. Value 58.4 against a threshold of 55,
   with `relief 1.00` because Western Empire was its only attacker.
2. **Spring–Winter 1091** — Western Empire was dogpiled by three kingdoms and shrank. `fear` went
   from **+7.11 to −21.84** as the patron became weaker than its own vassal; `protection` stayed
   at 0 throughout. Hold fell 44 → 15, defiance marks reached 2.
3. **Winter 6, 1091** — revolt, after 30 days at breaking point. War of independence.
4. **Spring 1094** — chose Battania voluntarily. Start Hold 60.
5. **Autumn 1096** — defected to its attacker Vlandia at Hold 38.6, Battania having never
   defended it.
6. **Autumn 1101** — that link *expired* at Hold 27.4, below the renewal threshold of 70.
7. **Spring 1105** — knelt to Southern Empire.

Step 2 settles an open design question. An earlier draft of §12 proposed a rule dissolving a
defeated hegemon's sphere outright; the lead rejected it in favour of letting Hold erode
naturally, and §12.4.6 records that. **This run shows the erosion happening with no rule at all** —
an eight-month arc driven entirely by `fear` reversing sign. A dissolve-on-defeat rule would have
collapsed that arc into an instant.

## 6. Acceptance

| Metric | Result |
|---|---|
| War duration, chosen wars | mean 92.9d, median 84d, max 208d — **PASS** (acceptance: under 252d) |
| War duration, obligation wars | mean 89.4d |
| Errors / warnings | **0 / 0** |
| Wars ended by our peace table | 78 of 100 |
| Kingdoms | 8 → 7 (Western Empire eliminated, Summer 1092, cleanly: 3 wars closed, 7 treaties **Dissolved** not Broken) |
| End state | 2 hegemons, 2 links, avgHold 42.1, no kingdom greedy |

Terms conceded across 100 wars: 30 white peace, 18 tributary pacts, 24 fief cessions, 3 imposed
vassalage, 2 submissions that *were* the peace, 1 defection, 3 closed by elimination.

## 7. Findings for the lead

### 7.1 `IsStrongEnoughToHold` has no margin, and admits links that are dead on signing

`Hegemony.IsStrongEnoughToHold` is a strict inequality with no margin:

```csharp
=> Power.Strength(patron) > Power.Strength(vassal);
```

On Autumn 15, 1101, Northern Empire (strength 9700) imposed vassalage on Aserai (9584) — **1.2%
stronger**. The link's Hold target was **0 from the day it was signed**:

| Date | Hold | Target | `fear` | `rival` |
|---|---|---|---|---|
| Autumn 15, 1101 | 34 | **0** | +0.44 | −25 |
| Winter 1 | 27 | **0** | −6.84 | −25 |
| Winter 8 | 20 | **0** | −15.39 | −25 |

`fear` reads `log2(ratio)`, so a 1.2% edge is worth +0.44 on a base of 40 — nothing. Meanwhile
`rival` sat at its maximum −25, because Aserai could see Vlandia as a far better umbrella.

The system self-corrects, but it spends a war doing it: a peace ends in submission and the
submission unravels within weeks. **Options:** (a) require a real margin — 1.25× would match
`GreedStartsAtDominance` and give `fear ≥ +8`; (b) accept the churn as a winner overreaching.
The argument for (a) is consistency: `SubmissionValue` already returns 0 for a patron that is not
meaningfully stronger, so the same concept is currently enforced at two different strictnesses.

### 7.2 The 95–130 band, and the open 12b decision

Two wars settled inside the band where §12b matters:

- **98.1** — Aserai tired first, sued for peace, and bought it with tribute. Had Vlandia been the
  side to open negotiations, its reverse walk would have reached vassalage (95 ≤ budget 98.1, and
  95 ≥ the 49.1 it wanted) and Aserai would have become a vassal.

So the outcome in this band depends on **who tires first**. That asymmetry is by design — a loser
offers the cheapest acceptable package, a winner demands the dearest bearable one — but 12b is
what makes the winner's direction reach submission at all.

The decision is now purely one of taste rather than necessity: the voluntary, attacker and
defection routes produced four links on their own, so 12b is no longer needed to escape the
drought. The question is only whether **a 98-point victory should be able to end in submission**.

### 7.3 Voluntary submission: leave the threshold at 55

The threshold was questioned on the evidence that the best value measurable on the old save was
4.0, and a provisional 35 was floated. Both submissions here cleared 55 on their own:

| | Value | Culture penalty | Enemy charge |
|---|---|---|---|
| Northern Empire → Western Empire | **58.4** | 0 (same culture) | −10 |
| Northern Empire → Battania | **58.9** | **−20 applied** | 0 |

Both cleared by about 4 points — the margin a well-set threshold should have. A threshold of 35
would have passed both with more than 20 points to spare and admitted far less desperate cases.

The second row also corrects a claim in the diagnosis: **culture −20 is not the blocker it was
described as.** It applied in full here and the submission still went through.

More broadly, cause 3 of the original diagnosis (`cover` near zero because would-be patrons are
treaty-bound to the attackers) is **a disease of an old world, not a flaw in the formula**.
Battania scored `relief 1.00` as an outsider because the treaty web had not yet closed around it.
The proposal to rewrite `cover` to weigh a patron's *willingness* should be considered withdrawn
pending evidence from a late-game state.

## 8. Still unverified

- **12f (`ReconcileWithSiblings`)** — has not executed its real branch once. It runs on every
  `Hegemony.Submit` and did so seven times without throwing, but no kingdom ever knelt to a
  hegemon while at war with one of that hegemon's other vassals. It needs a narrow coincidence or
  a hand-built save.
- **The dissolution rung in live play** — verified end to end by console command on 2026-09-20
  (see design/04 §12.8) but never chosen by the AI here: no war had a hegemon as its loser inside
  the affordable band.
- **`Hegemony.DissolveChains`** — still needs a save holding a chain.
- **Player-offer cooldown** — the test hero is not a ruler.

## 9. How to reproduce

```bash
pwsh ./scripts/deploy.ps1
# games_start, then core/new_game, click through character creation with ui/click_widget
# (the intro video needs ESC sent from outside the bridge)
# park the hero in a town, then:
#   diplomacy.test_set_speed 50
```

Or resume from `di_fresh_1084`, which is the pristine 1084 start with the hero already parked.

**Parking the hero is not optional.** On the way to a town the party was stopped by bandits
twice; each encounter halts the clock until a human or the bridge clears it, and an unattended
run dies there.
