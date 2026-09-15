# Design 03 — Espionage

Status: **spec for review**. Phase 3. Depends on Phase 1 (claims, treaties, trust) and
Phase 2 (grievances, loyalty) already existing — espionage in this design is mostly a way to
*reach into* those systems, not a separate scoreboard.

Constants live in `Espionage/EspionageConstants.cs`.

## 0. The problem we are solving

Calradia has no information asymmetry and no covert options. You either march an army at a
problem or you ignore it. There is no way to weaken a neighbour without declaring war, no
reason to fear a kingdom you are at peace with, and nothing useful to spend money on once
your army is maxed.

The design rule for this pillar: **espionage must never be a free action.** Every operation
either costs something real or risks handing the target a legitimate reason to invade. If a
mission is strictly better than doing nothing, it is mis-specified.

---

## 1. Spy networks

One strength value per **ordered** kingdom pair (our network *in* their realm), 0–100.
Ordered because Vlandia spying on Battania is not the same asset as the reverse.

**Building:** an ongoing weekly investment, not a purchase.

```
weeklyGrowth = (goldSpent / 2000) × (1 + agentRoguery / 200)
             − targetCounterIntelligence × 0.08
             − 0.7                                  // baseline attrition
```

- Requires a designated **handler** — a companion or clan member assigned to the target
  realm. Their Roguery and Charm set the ceiling: `maxStrength = 40 + roguery/2 + charm/4`.
- A network in a realm you are **at war with** grows at half rate: borders are watched.
- **Decay −0.1/day** on top of the above, always.

Networks are the slow, boring part on purpose. It means a war you planned three years ago
goes better than one you improvised, which is the strategic texture the pillar exists for.

## 2. Missions

All costs are per attempt, paid up front. Duration is the delay before resolution.

| Mission | Network req. | Gold | Days | On success |
|---|---|---|---|---|
| **ScoutArmies** | 15 | 1,000 | 3 | Enemy army positions and strengths revealed for 7 days |
| **ReadCourt** | 25 | 2,000 | 5 | Target's pending kingdom decisions, bloc powers and war exhaustion revealed exactly for 14 days |
| **SabotageGarrison** | 35 | 5,000 | 7 | Target garrison −25%, siege engines in progress destroyed |
| **SpreadDissent** | 30 | 4,000 | 10 | Settlement loyalty −15, unrest rises |
| **BribeLord** | 45 | 25,000 | 14 | Target clan loyalty −20; flips to us if a civil war starts within 2 years |
| **ForgeLetters** | 50 | 15,000 | 14 | A fabricated grievance of weight 8 against their ruler |
| **StealTreasury** | 40 | 3,000 | 7 | Steal `min(20% of ruler gold, 50,000)` |
| **Assassinate** | 70 | 60,000 | 21 | Target hero dies; see §5 for why this is the worst-value option |

`ReadCourt` is the mission that answers the open question in spec 01 §8: if the default
visibility of enemy exhaustion is a qualitative band, this is what buys the exact number.

## 3. Counter-intelligence

A per-kingdom defensive value, 0–100, built by standing investment rather than per-mission:

```
counterIntelligence = 10
                    + (weeklyGoldSpent / 1500)
                    + rulerClanSecurityFocus × 5      // a policy slot the AI also uses
                    + 0.05 × averageSettlementSecurity
```

It suppresses enemy network growth (§1) and raises exposure chance (§4). The AI budgets for
it out of the same purse as troops, so a realm that over-invests in spies is militarily
weaker — the trade-off has to be visible or the whole pillar is just extra income.

## 4. Resolution

```
successChance = clamp(0.15
                    + 0.005 × networkStrength
                    + 0.004 × (handlerRoguery + handlerCharm) / 2
                    - 0.006 × targetCounterIntelligence
                    - missionDifficulty,              // per-mission constant
                    0.05, 0.95)
```

On **failure**, roll exposure:

```
exposureChance = clamp(0.25 + 0.008 × targetCounterIntelligence - 0.003 × networkStrength, 0.05, 0.90)
```

Three outcomes, matching `Models.MissionOutcome`:

- **Success** — effect applies. Network −5 (assets get used up).
- **Failure** — nothing happens. Network −10, gold spent.
- **Exposed** — network drops to **0**, and §5 fires.

## 5. Exposure is a diplomatic event

This is the hinge that keeps espionage honest, and the most important section here.

When an operation is exposed, the **victim**:

- gains the `EspionageExposed` casus belli against us (legitimacy **0.85** — near the top of
  the Phase 1 scale, because being spied on is a very defensible reason for war),
- loses **25 trust** in us, permanently until rebuilt,
- and for `Assassinate` specifically, every *other* kingdom loses **15 trust** in us too.
  Assassination is cheap in effect and catastrophic in reputation; it should be the tool of
  someone who has run out of better ideas.

So a caught operation does not just fail — it can drag the player into a war they did not
choose, at a moment they did not pick. That is the intended feeling.

## 6. Cross-pillar payoffs

| Mission | Reaches into | Effect |
|---|---|---|
| `BribeLord` | Phase 2 loyalty | −20 loyalty, defection during civil war |
| `ForgeLetters` | Phase 2 grievances | Manufactured grievance against their ruler |
| `ReadCourt` | Phase 2 blocs, Phase 1 exhaustion | Reveals the numbers the player would otherwise guess |
| `SpreadDissent` | Settlement loyalty | Feeds rebellion pressure |
| Exposure | Phase 1 claims, trust | `EspionageExposed` casus belli, −25 trust |

Note the shape: espionage owns almost no state of its own. Networks and missions are the
only new data; everything an operation *does* lands in Phase 1 or Phase 2. That is what
keeps three pillars from becoming three unrelated games.

## 7. UI

- **Network map** — a strength figure per kingdom, with the trend and the assigned handler.
- **Mission board** — available missions for the selected target, each showing its real
  success and exposure chance. The AI reads the same numbers; nothing is hidden from the
  player that the AI gets to use.
- **Operations in progress** — with days remaining, cancellable at the cost of the payment.

## 8. Implementation order

| Step | Deliverable | Depends on |
|---|---|---|
| 3.1 | Network model, growth/decay, handler assignment | Phase 1 complete |
| 3.2 | Mission model, scheduling, resolution math | 3.1 |
| 3.3 | Counter-intelligence | 3.1 |
| 3.4 | Exposure → casus belli + trust (the §5 hinge) | 3.2, Phase 1.2/1.4 |
| 3.5 | Cross-pillar effects into Phase 2 | 3.2, Phase 2 complete |
| 3.6 | AI running its own networks and missions | 3.2–3.4 |
| 3.7 | Espionage UI | all above |

## 9. Open questions

1. **Assassination at all?** It is in the enum and specced above, but it is the one mission
   that can permanently delete content a player cares about (a named lord, a marriage
   prospect, a claimant). Options: keep as specced with heavy reputation cost; restrict to
   non-ruler targets; or cut it. Recommend keeping it with the §5 penalties — it makes the
   other seven missions look reasonable by comparison.
2. **Can the player be a target?** For symmetry the AI should run operations against the
   player's realm. That means the player occasionally loses a garrison or a loyal vassal to
   an enemy network, which is either the best part of this pillar or the most frustrating,
   depending entirely on how well it is telegraphed.
3. **Handler risk.** Should a companion running a network be capturable or killable on
   exposure? It gives the pillar personal stakes; it also means losing a companion to a dice
   roll the player did not see.
4. **Do networks survive a war?** Currently they grow at half rate in wartime but persist.
   The alternative — war collapses networks — makes pre-war preparation matter much more.
