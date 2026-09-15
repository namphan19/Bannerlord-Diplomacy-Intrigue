# Design 02 — Court intrigue

Status: **spec for review**. Phase 2. Depends on Phase 1 being in place: this pillar spends
the data diplomacy produces (war exhaustion, casus belli legitimacy, broken treaties).

Constants live in `Intrigue/IntrigueConstants.cs`, same rule as Phase 1 — balance work edits
one file.

## 0. The problem we are solving

In vanilla, ruling a kingdom is an administrative screen. Vassals have opinions that never
accumulate into anything, kingdom decisions are votes with no visible reasoning and no
memory, and the only way a kingdom falls apart is military defeat. A king is never afraid of
his own court.

Four mechanisms change that:

1. **Grievances** give the court a memory.
2. **Loyalty** turns that memory into a number that matters.
3. **Blocs** turn individual loyalty into organised politics.
4. **Legitimacy** gives the crown something it can lose without losing a battle.

Civil war and contested succession are what those four produce when mismanaged. They are
outcomes, not systems in their own right.

---

## 1. Grievances

Event-sourced records attached to a **(clan → liege)** pair. Each has a type, a weight, and
a date. Nothing is recomputed from world state: a grievance is a thing that *happened*.

| Cause | Weight | Notes |
|---|---|---|
| A fief they wanted went to a rival clan | **8** | Weight scales with how strongly they bid |
| War declared that their bloc opposed | **`(1 − legitimacy) × 8`** | An illegitimate war offends more; ties straight into Phase 1 |
| Kingdom paid humiliating tribute | **5** | Only for the paying side |
| A relative left in enemy captivity > 1 year | **6** | Renews if still captive |
| A fief of theirs was lost to the enemy | **4** | They blame the crown for failing to defend it |
| Policy passed against their agenda | **3** | |
| Peace signed while they were winning | **3** | Hawks specifically |
| Ruler refused their request | **2** | |

**Decay: −0.02/day** on every grievance, removed at zero. A grievance nobody compounds
fades in roughly a year and a half; a court that keeps being slighted never clears.

Design note: grievances are the mechanism that makes *the player's* decisions as king have
consequences beyond a relation number. Handing a town to a favourite is no longer free.

## 2. Vassal loyalty

One derived value per clan, 0–100, recomputed on the weekly tick:

```
loyalty = 50
        + relation(clanLeader, ruler) / 2        // -50..+50  ->  -25..+25
        - totalGrievanceWeight × 1.5
        + fiefSatisfaction × 10                  // -1..+1: fiefs held vs fiefs deserved by renown
        - kingdomWarExhaustion × 0.2             // the realm's worst ongoing war
        + (crownLegitimacy - 50) × 0.2
```

| Loyalty | Behaviour |
|---|---|
| ≥ 70 | Reliable: votes with the ruler, answers call to arms |
| 40–69 | Transactional: votes its bloc agenda |
| 25–39 | Disaffected: votes against the ruler, will not volunteer troops |
| < 25 | **Defection risk** — see §6 |

Loyalty is derived, not saved: it is a function of saved grievances plus live world state.
That keeps the save small and means a balance change takes effect immediately rather than
only for new campaigns.

## 3. Court blocs

Clans with a shared agenda cluster into a bloc. Agendas are the five in
`Models/CourtAgenda`, and a clan's agenda is picked by whichever pressure on it is strongest:

| Agenda | A clan joins when |
|---|---|
| **Doves** | Its own fiefs are exposed, or kingdom war exhaustion ≥ 40 |
| **Hawks** | It holds fewer fiefs than its renown deserves and a weak neighbour has land |
| **Autonomists** | Crown authority is high and the clan is influential |
| **Centralists** | The clan is the ruling clan, or benefits from crown patronage |
| **Pretenders** | The clan has a claim on the throne and crown legitimacy < 40 |

- A bloc's **leader** is its highest-influence member; its **power** is the sum of member
  influence.
- Blocs vote as a unit in kingdom decisions. This is the point: it makes the political
  arithmetic legible enough for the player to *play against*, rather than a lottery.
- A clan with loyalty ≥ 70 follows the ruler regardless of its bloc. Loyalty beats agenda.

## 4. Crown legitimacy

A per-kingdom pool, 0–100, starting at 60.

| Change | Delta |
|---|---|
| Won a war with legitimacy ≥ 0.7 | **+12** |
| Won a war with legitimacy < 0.3 | **+2** (a win is a win, barely) |
| Lost a war | **−10** |
| Declared a war with no casus belli | **−8** |
| Broke a treaty | **−20** |
| Caught fabricating a claim | **−15** (the Phase 1 hook already logs this) |
| Lost a fief | **−3** |
| Took the throne by irregular succession | **−15** |
| Per year of peace | **+3** |

Effects: legitimacy feeds vassal loyalty (§2), gates pretender bids (§5), and scales the
influence cost of pushing decisions through a hostile court.

## 5. Succession

When a ruler dies, vanilla silently assigns the throne. Instead:

1. Collect claimants: the late ruler's heir, plus any clan leader with a blood claim.
2. Each claimant's support = sum of the influence of clans backing them, where backing is
   driven by loyalty, bloc agenda, and relation to the claimant.
3. Highest support takes the throne. Margin matters:
   - **Clear majority (> 60%)** — orderly succession, no legitimacy loss.
   - **Plurality** — contested: **−15 legitimacy**, and every backer of a losing claimant
     gains a grievance of weight 6.
4. A losing claimant with more than 30% support becomes a standing **pretender**, which is
   what gives a rival kingdom the `SupportClaimant` casus belli from Phase 1.

## 6. Civil war

The end state of unmanaged internal pressure, not a random event. Triggers when **all** hold:

- The pretender bloc's power ≥ **40%** of total kingdom influence, and
- crown legitimacy < **35**, and
- at least two clans have loyalty < **25**.

On trigger the pretender bloc secedes into a new kingdom, taking the fiefs its member clans
hold, and starts at war with the parent kingdom with the `SupportClaimant` casus belli on
both sides. Clans with loyalty < 25 that are *not* in the bloc pick a side by relation.

Everything here routes through existing Phase 1 machinery: the civil war is a war with a
war record, exhaustion, and a peace table. That is deliberate — a civil war that ends by
negotiation rather than annihilation is the interesting case.

## 7. Where this couples to the other pillars

| From | To | Through |
|---|---|---|
| War exhaustion ≥ 40 (Phase 1) | Clans shift to Doves | §3 |
| Casus belli legitimacy (Phase 1) | Grievance weight, crown legitimacy | §1, §4 |
| Broken treaty (Phase 1) | −20 crown legitimacy | §4 |
| Pretender exists (§5) | `SupportClaimant` casus belli for rivals | Phase 1 §2.2 |
| `BribeLord` mission (Phase 3) | Loyalty penalty, defection on civil war | §6 |
| `ForgeLetters` mission (Phase 3) | Manufactured grievance | §1 |

## 8. Implementation order

| Step | Deliverable | Depends on |
|---|---|---|
| 2.1 | Grievance model, sources, decay, ledger in `ModState` | Phase 1.1–1.3 |
| 2.2 | Loyalty calculation + weekly recompute | 2.1 |
| 2.3 | Bloc formation and bloc voting in kingdom decisions | 2.2 |
| 2.4 | Crown legitimacy pool + all its sources | 2.1 |
| 2.5 | Succession with claimant support | 2.3, 2.4 |
| 2.6 | Civil war secession | 2.5 |
| 2.7 | Court UI: blocs, loyalty, grievance ledger, legitimacy | all above |

## 9. Open questions

1. **How much of the court does the player see?** Same fork as enemy war exhaustion
   (spec 01 §8). Exact grievance ledger for your own court is clearly fine; the question is
   whether you see *rival kingdoms'* internal politics without espionage.
2. **Should the player's own clan be subject to this as a vassal?** If yes, serving a king
   becomes a real political position rather than a waiting room. It is more work and touches
   the player's own loyalty number.
3. **Kingdom decisions: extend vanilla or replace?** Extending `KingdomDecision` keeps
   compatibility and less code; replacing gives full control of the vote UI. Recommend
   extending until it visibly constrains us.
4. **Civil war frequency.** The §6 thresholds are a guess. This needs a long AI-only run to
   tune — too frequent and Calradia shatters, too rare and the system never shows up.
