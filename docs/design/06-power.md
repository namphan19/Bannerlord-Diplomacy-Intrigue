# Design 06 — Power: ambition, coalitions, greed

**Phase 1.12.** Proposed by the project lead on 2026-09-16, after `diplomacy.strength` showed the
run-04 world's hegemon ranked seventh of eight by strength. Code: `Diplomacy/Power.cs`, with its
effects in `AiDiplomacy`, `CallToArms` and `Hegemony`. Constants: the *Power* block of
`DiplomacyConstants.cs`. **Every number here is un-tuned; run 06 is the first measurement.**

## 0. The problem

Strength already appeared in ten places, but always as a comparison between two kingdoms. Nothing
asked how strong a kingdom was *against the world*, so:

- a kingdom holding a fifth of Calradia's strength behaved exactly like one holding a tenth;
- nothing pushed back against a rising power until it was at somebody's gates;
- a hegemony could only ever grow — nothing about being too strong made it less stable.

## 1. The lead's decisions

| Decision | Detail |
|---|---|
| Strength breeds ambition | A ruler whose live strength is high wants war more, and wages more of it |
| The strong provoke coalitions | The other kingdoms find it easier to ally against the strongest |
| Greed | A ruler grown too strong stops accepting vassals and wants to take them whole; its vassals, fearing extinction, revolt and leave |
| **Annexation only through war** | No action folds a vassal into its patron. A greedy patron tears up the oath and conquers |
| **Kingdoms may be eliminated** | A kingdom that loses its last settlement is gone |
| How strength is computed | Left to the implementation — §2 |

## 2. Measuring strength

**Two readings, one rule.** The engine's `CurrentTotalStrength` is live military strength and
moves by a fifth after a single large battle.

| Reading | Answers | Used by |
|---|---|---|
| **Live** | what a kingdom can do *now* | ambition, the second-war allowance, revolt capability, fear, whether allies suffice |
| **Smoothed** — 84-day exponential average, sampled daily | what a kingdom is *becoming* | greed, dread, the balancing pull |

Why the split: greed and dread are reputations. Read live, a vassal would revolt in dread after
its patron's recruiting season and regret it after its patron's next defeat; and a player could
disband an army before the weekly evaluation to look harmless. 84 days is one in-game year —
longer than a war (medians of 63 and 76 days in runs 04 and 05), so one war moves the average
by about half.

**Dominance** is the common scale: `share of the world's strength × number of living kingdoms`.
1 is an even split. With eight kingdoms, dominance 2 is a quarter of Calradia's strength.

Saved as `KingdomPower` (definer id 9, `ModState` property 10). A save without it starts each
average at the live figure on its first daily sample.

## 3. Ambition — live

```
ambition = clamp((dominance - 1) / 1.5, 0, 1)        // 0 at an even split, 1 at 31% with 8 kingdoms
```

| Effect | Value |
|---|---|
| War valuation | `+15 × ambition` — under the threshold of 18, so never a reason on its own |
| Wars of its own choosing at once | 1, or **2** at dominance ≥ 2 |
| Pact valuation | `−20 × ambition` |

Distinct from land hunger: land hunger reads strength against the fiefs a kingdom holds, so a
strong kingdom that already owns much feels none. Ambition reads strength against the world.

## 4. Coalitions that hold

The balancing pull (added the same day, `PactWeightBalancing = 40`) made weaker kingdoms *want*
pacts against a dominant sphere. Two defects meant those pacts could not work:

1. **Allies abandoned each other against the strong.** An ally refused a call when the enemy
   outweighed the caller plus *that one ally* by 1.5×. Against a dominant power every ally
   refused in turn, each reckoning it would stand alone.
2. **Alliances never deterred.** The war valuation weighed the target alone, so an aggressor
   discovered the coalition only when the call to arms went out.

Both now read **sides**. `CallToArms.ExpectedSupport(principal, opponent, attacked)` is the one
resolver: every kingdom bound to the principal whose obligation applies and who is willing to
honour it (exhaustion, trust, a vassal's Hold — not the odds, which would recurse), plus any
already at war with the opponent. The vassal cascade cap applies.

- An ally judges hopelessness as `their side > our side × 1.5`.
- An aggressor's strength ratio is `(us + our support) / (them + their support)`, for the gate and
  for the strength term.

The balancing pull itself now reads smoothed sphere strength.

## 5. Greed, dread, and annexation — smoothed

```
greed = clamp(smoothedDominance - 1.25, 0, 1)          // from ~16% of the world, full at ~28% (8 kingdoms)
```

> Threshold lowered from 2.0 — the lead's F5 call after run 06, where the highest smoothed
> dominance the run produced was ~1.76 and greed never fired at all, leaving the annexation
> branch dead content. At 1.25 that same peak yields greed ~0.5, the `GreedRefusesVassals`
> line: reachable only at the extreme, unreachable in an even world where dominance sits
> near 1. **Un-tuned beyond that arithmetic; run 07 is the first measurement.**

**The greedy ruler, from greed 0.5:**
- takes no new vassals — no voluntary submission to an AI patron, no poaching, no vassalage on
  its side of the peace table;
- may **turn on its own vassal**: the vassal becomes a candidate in its war valuation, with
  `+20 × greed − 15` (breach) on top of the ordinary terms. Declaring it breaks the vassalage and
  every war-forbidding treaty between them, charged as a breach — the vassal's trust, every
  other court's, a BrokenTreaty casus belli to the victim — and every other vassal of the same
  patron loses 10 Hold, as when watching a revolt.

**The vassals, who see the same numbers:**
- **dread** in Hold: `−25 × greed(patron)`;
- the revolt line rises by `15 × greed(patron)` — a vassal expecting extinction has less to lose
  by fighting at poor odds;
- a kingdom considering submission counts the same dread against the patron.

**Why fear and dread do not cancel into nonsense.** Fear reads the patron against *this vassal*
(`log2` balance); dread reads the patron against *the world*. A patron twice its vassal's
strength at full greed nets zero: strong enough to hold them, too strong to be trusted with them.
Hold, as a function of how strong the patron is, becomes an inverted U.

## 6. Elimination

Already in the engine: `FactionDiscontinuationCampaignBehavior.OnSettlementOwnerChanged` destroys
an AI kingdom the moment its last settlement changes hands (verified by IL, v1.4.8). The player's
kingdom is exempt.

What was missing was ours. `DestroyKingdomAction` removes the kingdom from its wars through
`FactionManager` and raises **no peace event**, and the war ledger closed wars only on that event.
The conqueror would have carried the war forever, counted as a chosen war, and its evaluation
could never start another. `CoreBehavior` now handles `KingdomDestroyedEvent`: every war closes
with `endedBy=Eliminated` and carries its weariness, followers called in by the destroyed kingdom
are released, its vassalages collapse as a destroyed patron's always did, and its other treaties
dissolve.

## 7. Settling old breaches

Found while verifying annexation: a patron that took back a vassal after a revolt still held the
BrokenTreaty claim from that revolt, and its annexation war was filed at legitimacy 0.95. A
submission now settles every live BrokenTreaty claim between the two kingdoms; a sweep at session
launch settles the ones older saves carry. Land claims are untouched — a submission settles a
betrayal, not a border.

## 8. What the player sees

- Ctrl+D → Other kingdoms: *"Power: X holds 27% of Calradia's strength; growing greedy — its vassals
  are uneasy"* (`Power.Describe`).
- `diplomacy.strength`: live and smoothed strength, dominance, ambition, greed.
- `diplomacy.war_value`: sides, expected support, ambition, annexation terms.
- `diplomacy.hegemony`: dread and each link's revolt line.
- The Kingdom-screen integration (the UI team's `DiplomacyItemMixin`) does not show power yet.

## 9. Known limits

- **Greed can only be tested with a set average.** No kingdom in the run-04 world is dominant on
  its own; `diplomacy.set_smoothed_strength` exists for that and must never touch a kept save.
- **Strength is military only.** Fiefs and prosperity weigh nothing. Deliberately deferred.
- **Tuned by nothing yet.** Thresholds (2.5, 2, 0.5) and weights (15, 20, 25, 40) are reasoned,
  not measured.
