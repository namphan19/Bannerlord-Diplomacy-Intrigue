# Design 04 — Hegemon and vassal

**Phase 1.9 and 1.10.** Moved into Phase 1 by the project lead (2026-09-15): the mechanism by
which one kingdom rises over others must work by the time Phase 1 closes.

Three directives from the lead shape everything below:

1. **No titles for now.** No Emperor, no Khagan, no proclamation ceremony. Those are flavour
   and they wait.
2. **A hegemon is any kingdom with at least one other kingdom submitting to it.** Nothing
   more is required.
3. **Several kingdoms may be hegemons at the same time.**

Directive 2 is the one that simplifies the design most, so it is worth stating what it means:
**hegemony is not a status a kingdom claims, it is a fact derived from its treaties.** There
is no title record, no founding requirement, no legitimacy gate, no proclamation. A kingdom
with one vassal is a small hegemon; a kingdom with four is a large one; the moment its last
vassal secedes it stops being one, without any event having to fire.

Source material: the lead's `Hegemon–Vassal System.md`, a conceptual framework of 19 sections.
§1 below records what was taken from it and what was not.

---

## 1. Selection from the source document

### 1.1 Taken, because Phase 1 machinery already carries it

| Source | What we use it for |
|---|---|
| §2.3 **Suzerainty** — control of foreign policy with internal autonomy retained | This is exactly our `Vassalage` treaty: `SubordinatesForeignPolicy` plus `CarriesCallToArms`, with the vassal keeping its ruler, fiefs, laws and court. The source's central concept is already built |
| §2.4 **Indirect rule** | Why hegemony is deliberately *not* annexation. Vanilla conquest already annexes; this system is the alternative to it |
| §5 **Mutual but unequal exchange** | The hegemon owes protection, the vassal owes service and tribute. Both directions are enforced (§4.3) |
| §6 **Degrees of dependence** | Collapsed from seven types to the two rungs we have — tributary (pays, keeps everything else) and vassal (pays, fights, gives up foreign policy) — plus the hegemon sphere on top |
| §7 **Multiple hegemons**, §7.4 **hegemonic competition** | Directive 3. Rivals poach each other's vassals (§5.3) rather than only fighting each other |
| §8 **Why vassals obey** — fear, self-interest, dependency, lack of alternatives | The four terms of the Hold formula (§4.1). Legitimacy and personal loyalty are Phase 2 inputs and enter later |
| §9 **Why vassals rebel**, §9.2 the *types* of rebellion | Three of the seven types selected: passive resistance, diplomatic defiance, secession (§6) |
| §10 **Can a vassal become a hegemon** | The centre of what the lead asked for. §7 is the whole loop |
| §10.4 **The strong vassal problem** | Falls out of the Hold formula for free: a vassal stronger than its patron has low Hold and there is nothing the patron can do about it except win wars |
| §15 Scenarios A, B, C, D, G | Used as the acceptance tests (§10). A design that cannot produce the source document's own scenarios is not finished |
| §16 **Stability model** | Simplified into one number per link, `Hold` (§4.1). The source's own equation, restricted to quantities the mod already tracks |

### 1.2 Not taken, and why

| Source | Why not |
|---|---|
| §3 **Multi-layered hierarchy** — a hegemon subordinate to a higher sovereign, vassals of vassals | The engine has two faction tiers and no parent-of-kingdom slot, and `TreatyRegistry` already refuses double subordination. A chain would need a hierarchy the game cannot draw, and would make call-to-arms cascades unbounded. Hegemony stays **flat**: one patron per vassal |
| §4.3 **Political legitimacy** as a foundation of authority | Real, and it is Phase 2.4. Hegemony must work before it, so Hold uses trust and strength now and gains a legitimacy term later |
| §4.5 **Strategic geography** | No terrain model to read. Approximated by the border-adjacency measure the war valuation already uses |
| §13 **Succession**, dynastic marriage, hostages | Phase 2.5. Marriage and hostages are not modelled at all |
| §12.2 **Divide and rule** | Needs court factions inside the vassal to play against each other; Phase 2.3 |
| §4.2 economic subsidies, trade privileges, market access | Our economic channel is tribute, and vanilla's trade agreements are being switched off ([design 05](05-vanilla-override.md)). A subsidy system with no economy behind it would be a number with no meaning |
| §17 **Evolution into empire** (annexation, replacing local rulers) | Vanilla conquest is the annexation path and it works. Hegemony is the branch where you *don't* |
| Religious and cultural authority (§4.3, §9) | Bannerlord has no religion. Culture survives as a small modifier in Hold |
| §11 the control/autonomy dial as a player-set policy | Tempting, but it needs a UI and a per-vassal policy record. Phase 1 gives the hegemon two concrete levers instead — how often it calls, how much tribute it demands — and lets Hold do the rest |

---

## 2. What exists today, and the one thing that does not

Everything the vassal side of this needs is built and was verified in the live game:
`Treaty.SubordinateParty`, `CarriesCallToArms`, `SubordinatesForeignPolicy`, tribute
scheduling, the refusal-breaks-vassalage rule, and the refusal to let a kingdom serve two
patrons.

**Vassalage had no route into play, and that was the whole of 1.9.** When this document was
written, every treaty-creating call site had been traced and only `DebugCommands` could create
a `Vassalage` treaty: `PeaceTerms` had no vassalage field, the AI never proposed subordination,
and the player's menu did not offer it. Balance run 02 confirmed it from the other end —
`vassalage=0` in all 157 weekly snapshots of a 13.1-year campaign.

So 1.9 was mostly about **routes in** and **one new number**, not new machinery.

**Built and verified on 2026-09-16.** All three routes are live, and the first hegemon was
made through the real peace table rather than a debug command: Southern Empire, holding Aserai
at war score 99.2. Three more links followed inside three simulated weeks by the AI's own
voluntary route. §10a records where the code departs from what is written below.

---

## 3. Becoming a vassal

Three routes. All three produce the same `Vassalage` treaty, and they differ in the state the
relationship starts in.

### 3.1 Imposed at the peace table

A new rung at the top of the existing concession ladder:

| Demand | War-score cost |
|---|---|
| Release prisoners | 5 |
| Castle | 25 |
| Town | 45 |
| Tributary pact | 60 |
| **Vassalage** | **90** |

Run 02 measured what a war score actually reaches: wars lasting 61+ days averaged `|score|`
47.4 with a maximum of 96.3. So 90 is deliberately near the top of the observed range —
submission is what happens after a war someone has comprehensively lost, not after a good
season. Starting state: **Hold 35**, one grievance, trust unchanged (losing a war is not a
betrayal).

**Run 03 confirms 90 is reachable, twice in 6.2 years.** Two wars ended at war score **exactly
100** — `Battania / Northern Empire` and `Khuzait / Sturgia` — and both settled for a tributary
pact because that is the highest rung the ladder currently has. Had this rung existed, both
would have produced a vassal, and therefore a hegemon. So the threshold is measured rather than
guessed, and at roughly **0.3 submissions per year** a hegemony would form every three years or
so, which is the right order of magnitude for something meant to feel like a turn in history.

The same run also shows who would have risen. Fifteen tributary pacts were imposed in 6.2
years, every one of them at a peace table — i.e. won:

| Patron | Clients |
|---|---|
| **Khuzait** | Northern Empire ×3, Southern Empire, Aserai, Sturgia |
| **Vlandia** | Western Empire ×3, Battania, Aserai |
| Western Empire, Southern Empire, Sturgia, Battania | one each |

Two proto-hegemons, and one chronic client: **Northern Empire paid six pacts to four different
patrons**. That is possible only because tribute is not exclusive — `TreatyRegistry` already
refuses a second subordination, so under vassalage Northern Empire would have had to belong to
*one* of them, and the other three would have had a reason to fight over it. The contested
vassal in §5.3 is not hypothetical; the world is already producing the candidate.

One mechanical note for 1.9: every tributary pact in run 03 **expired honoured in full** after
its two years (`TributaryPactYears = 2`) with **zero defaults**, and was then sometimes
re-imposed on the same pair after another war. Vassalage runs five years and needs renewal to
be a decision rather than an expiry — which is what `Hold` ≥ 70 in §4.2 is for.

### 3.2 Offered voluntarily

A cornered kingdom asks for protection. Evaluated weekly alongside the other AI moves, using
the same shape as the existing treaty valuation so the number shown to a player is the number
the AI used:

```
submissionValue =  70 * (threatFromOthers / ownStrength)     // who is about to eat me
                 + 40 * protectionReach(patron)              // can they actually reach my border
                 + 30 * weariness(me) / 100                  // I cannot fight another war
                 + 20 * trust(me -> patron) / 100
                 - 50 * ownStrength / strongestNeighbour      // a strong realm does not kneel
                 - 20 * cultureMismatch
```

Submits at **≥ 55**. Starting state: **Hold 60**, no grievance. A volunteer is a much steadier
vassal than a defeated one, and that difference should be visible in play.

### 3.3 Player routes

Both directions, same numbers, no exceptions: the player may demand vassalage at their own
peace table when the score allows it, may accept or refuse a voluntary submission, may submit
to an AI hegemon, and may refuse service or secede as a vassal.

---

## 4. Hold — the one new number

`Hold` is a 0–100 value **per vassalage link**, and it is the whole system's load-bearing
number. It answers: how firmly does this patron hold this vassal?

### 4.1 The formula

The source document's §16 stability model, restricted to quantities we already track:

```
target =  40
        + 25 * clamp(strength(patron) / strength(vassal) - 1, -1, +1)   // fear
        + 20 * protectionScore                                         // did they defend me
        + 15 * trust(vassal -> patron) / 100                           // and can I trust them
        - 20 * tributeBurden                                           // tribute vs my income
        - 15 * warBurden                                               // obligation wars in the last year, capped at 3
        - 25 * bestRivalOffer                                          // someone else offers protection
        - 10 * cultureMismatch
```

`Hold` moves toward `target` by **1 per day**. That lag is the point: a hegemon that loses a
war does not lose its vassals that afternoon, and a vassal that has been neglected for a
season does not forgive it overnight. Everything in the formula is a slow pressure.

### 4.2 Thresholds

| Hold | Behaviour |
|---|---|
| ≥ 70 | Renews the vassalage willingly when it expires |
| 40–69 | Serves, but lets the treaty lapse at expiry rather than renewing |
| 30–39 | **Passive resistance** — tribute paid late, calls to arms refused (§6) |
| 15–29 | **Diplomatic defiance** — signs treaties with outsiders despite `SubordinatesForeignPolicy`, and will listen to a rival patron |
| < 15 for 30 days | **Secession** — declares an independence war (§6.3) |

`VassalageYears = 5` already exists, so a link that drifts into the 40s quietly dies at its
term instead of exploding. Not every hegemony should end in a war.

### 4.3 The hegemon's duty, enforced

From the source's §5.1 and Scenario B. A patron that does not protect its vassal loses it:

- A vassal is attacked and the patron **joins the war within 10 days** → `protectionScore` up,
  worth up to +20 Hold.
- The patron **does not join at all** → **−25 Hold immediately**, and a grievance.
- The vassal **loses a fief** while fighting one of the patron's wars → **−10 Hold**.

This is what stops hegemony from being a free income stream. Holding vassals costs wars.

---

## 5. Being a hegemon

### 5.1 Derived, never declared

```
IsHegemon(k)  = any active Vassalage treaty where k is the dominant party
Sphere(k)     = { k } + its vassals
```

No title state, no save field, nothing to keep in sync — which also means it cannot desync.
Several hegemons coexist by construction, satisfying directive 3 at zero cost.

### 5.2 What the hegemon gets

- **Troops.** Vassals answer calls to arms in offensive wars too, subject to §5.4.
- **Tribute**, on the existing schedule.
- **Foreign policy.** A vassal cannot sign with outsiders or declare its own wars.
- **Summons** (the lead's earlier proposal, ruler only): the patron's ruler may order a
  vassal's parties to escort it — `MobilePartyAi.SetDoNotMakeNewDecisions` plus
  `MobileParty.SetMoveEscortParty`, both public in v1.4.8, no Army surgery. Influence cost per
  summon, at most half the vassal's parties, fixed duration then a cooldown. Refusable at a
  Hold cost.

### 5.3 Rival hegemons compete for vassals

The source's §7.4, and the cheapest interesting thing in this whole design. A hegemon
evaluating its weekly moves may **offer protection to another hegemon's vassal** whose Hold is
below 40. The offer is the `bestRivalOffer` term in §4.1, so it pushes Hold down before it
ever succeeds — courting someone else's vassal destabilises them even when it fails.

If the vassal accepts, its vassalage transfers: the old patron takes **−30 trust** with the
poacher, a `BrokenTreaty`-grade grievance, and a standing casus belli against them. Poaching
is meant to start wars.

### 5.4 The cascade cap

Run 02 measured `DefendAlly` at **27 % of 167 wars** with nothing but alliances on the map. A
hegemon with four vassals owing offensive service would turn each of its wars into five.

| Cap | Value |
|---|---|
| Vassals called per war | ⌈N/2⌉, nearest to the target first |
| Obligation wars per vassal | 1 at a time |
| Excused | exhaustion > 50, or already in an obligation war, or Hold < 30 |

Nearest-first is also the rule that reads correctly: a patron marching west calls the vassals
whose borders face west.

---

## 6. Defiance, in three escalating forms

Selected from the source's §9.2. The other four types there — limited revolt, civil war,
dynastic rebellion, proxy rebellion — need Phase 2 court politics and wait for it.

### 6.1 Passive resistance (Hold 30–39)

Refuses a call to arms; pays tribute late. Refusal costs the vassal **−10 trust** with the
patron and earns a **defiance mark**. Two marks inside a year and the vassalage lapses at its
next expiry. Consistent with the Phase 1 alliance rule: refusal has to be a real option, or
submission is a suicide pact no AI would ever accept.

### 6.2 Diplomatic defiance (Hold 15–29)

Signs a pact with an outsider in defiance of `SubordinatesForeignPolicy`. The patron may
respond: overlook it (nothing happens, Hold unchanged), or punish it — which means declaring
war on its own vassal, with everything that implies for the other vassals watching.

### 6.3 Secession (Hold < 15 for 30 days)

An independence war, declared by the vassal against its patron. It is a real war using the
existing machinery, with two differences:

- Other vassals of the same patron take **−10 Hold** — the source's contagion effect. One
  successful defection makes the next one likelier, which is how these systems historically
  come apart.
- If the seceding vassal **wins** (peace at a positive war score), the vassalage ends as
  `Broken` against the patron and the ex-vassal gains a lasting grievance and a claim.
- If it **loses**, the vassalage is re-imposed at **Hold 20** with no term reset. Crushing a
  revolt buys time, not loyalty.

---

## 7. The rise: how a kingdom becomes a hegemon

This is what the lead asked for, and it needs no new mechanism — it is what the parts above do
when they run. Five routes, each already present:

1. **Win a decisive war.** Score ≥ 90 at the peace table imposes vassalage (§3.1). One war,
   one vassal, and the winner is a hegemon.
2. **Be the obvious protector.** A cornered neighbour submits voluntarily (§3.2). Frequently
   the *first* vassal a rising kingdom gets, since it costs no war at all.
3. **Poach.** Court a rival's neglected vassal (§5.3).
4. **Secede and inherit.** A vassal that wins its independence war keeps whatever vassals it
   had acquired, and is now a hegemon in its own right — the source's §10.1 rise, reached
   without any special case.
5. **Outlive a collapse.** When a hegemon falls (§8), its ex-vassals are loose and courtable,
   and the strongest of them is usually the one who courts them.

Because hegemony is derived from treaties (§5.1), all five routes converge on the same fact:
hold one vassalage and you are a hegemon. There is no gate to pass, which is exactly why a
kingdom can *rise*.

## 8. When a hegemon falls

No inheritance, no automatic transfer:

- Patron **eliminated** → every vassalage ends as `Dissolved`, not `Broken`. Nobody chose
  this, so nobody eats a trust penalty.
- Ex-vassals get a **two-year non-aggression grace** with each other, so a collapse does not
  instantly become a free-for-all of eight simultaneous wars.
- The strongest ex-vassal has no special right to the sphere. It has §7's five routes like
  everyone else, and courting frightened neighbours is the fast one.

## 9. Save data

New, and the plan matters because save format is frozen once shipped:

| Data | Shape |
|---|---|
| `Hold` per link | a field on `Treaty` — a new `[SaveableProperty]` id, defaulting to 40 for links loaded from an older save |
| defiance marks | a small record: patron, vassal, when |
| `protectionScore`, `warBurden` | derived daily from existing `WarRecord` data; **not saved** |
| hegemon status | derived; **not saved** |

Adding a field that defaults sensibly does not require a schema bump; the defiance-mark list
needs both a class definition **and** a container definition in `ModSaveDefiner`.

## 10. Acceptance — the source document's own scenarios

A measured run must produce these, or the design is not done. Named after the lead's §15:

| Scenario | What the log must show |
|---|---|
| **A — loyal vassal** | a link held above Hold 70 for a year, answering calls and paying tribute |
| **B — neglected vassal** | a patron that failed to defend, losing the vassal to the −25 and its lapse |
| **C — ambitious vassal** | a vassal stronger than its patron, Hold decaying with nothing the patron can do |
| **D — rival intervention** | a poaching offer turning into a war between hegemons |
| **G — vassal becomes hegemon** | a secession war won, and the ex-vassal holding a vassal of its own |

Plus the volume checks: at least one vassalage forms **without a debug command** (run 02:
zero), spheres stay bounded — no single hegemon holding more than half the map — and
`DefendAlly` plus obligation wars stay under ~35 % of all wars with the §5.4 caps on.

## 10a. Where the implementation departs from this spec

Written down because a spec that quietly disagrees with the code is worse than no spec. The
first four departures were made while building 1.9 on 2026-09-16; the last six came from the
design review of run 04 the same day (docs/STATUS.md, "What to do next" §3). Several of those
six are the code finally doing what this spec already said.

| Spec said | Code does | Why |
|---|---|---|
| Protection: a discrete **-25 Hold** when a patron fails to join within 10 days | A continuous term from -20 to +20, recomputed daily from the vassal's current wars | No event bookkeeping and no saved flags, it cannot get stuck, and it reads better: ignoring a war for three weeks costs three weeks of Hold, and joining late starts earning it back the same day |
| Contagion: other vassals lose Hold when a revolt **succeeds** | They lose it when the revolt is **declared** | Detecting victory means hooking the peace that ends a war whose treaty is already broken. And the demonstration effect starts when somebody defies, not when they win |
| Defiance marks: a separate saved record of patron, vassal, when | Three fields on the `Treaty` itself (ids 15-17) | The link *is* the treaty. A new savable type needs a class definition **and** a container definition in `ModSaveDefiner`, and a missing container is the most common way to break a Bannerlord mod |
| (Not in the spec) | A vassal is now barred from signing **any** treaty with an outsider, and defies that below Hold 30 | The prohibition existed for war only. Enforcing it for treaties is what gives the middle tier of defiance something to defy |
| §4.3: the patron "joins the war within 10 days" | Until the run-04 review, nothing ever asked it to: the call to arms refused every call from vassal to patron. Now the patron is **called** when its vassal is attacked, and at signing into the wars the vassal is already defending, under the ally rules (trust floor, exhaustion, hopeless odds). Refusing costs trust and Hold, not a mark | Hold measured a duty no code could fulfil. A patron that distrusts its vassal staying out is intended: protection answers service |
| §4.1 `protectionScore`: "did they defend me" | Counts only wars the vassal is **defending**, and only against attackers the patron may be called against | Otherwise a war between two vassals of the same patron - which the vassalage itself forbids the patron to join - scored as neglect |
| §3.2: a cornered kingdom asks a stronger one | The submission value reads the patron at last: a patron no stronger than the candidate scores 0, and the threat term is scaled by the share of the danger the patron could actually take on | Nothing in the valuation depended on the patron's strength, so a kingdom knelt to its nearest neighbour whatever that neighbour could do for it |
| §6.1: passive resistance withholds tribute | And earns a defiance mark for it, at most one per 28 days | Withholding was free, so it was simply what every link below 40 did |
| §6.3: secession is one vassal's war | Siblings under Hold 25 after the contagion **rise with it**, all renouncing before anyone declares | A lone rebel faced the patron plus half the other vassals and lost every time; run 04's rebels knelt again within the run |
| §5.3: the poached vassal's old link | Closed without charging the client; the poacher pays. Checked with `CanSign(replacing:)` before anything is torn up | Charging both parties priced one act twice, and the client's lost trust could make the new signing fail after the old link was already gone |

Also worth recording: a coerced submission was measured starting at Hold 35 with a target of
48.9 (base 40, fear +7.8, trust +15.0, tribute -3.8, culture -10.0). It spends about a
fortnight below the resistance threshold of 40 - withholding tribute, refusing summons - and
then settles into service. That is the intended shape of a submission at swordpoint, and it
happens without any special case for it.

## 11. Implementation order

Gated on the run-02 fixes: submission needs war scores near 90, and no war currently survives
long enough to earn one. See [STATUS.md](../STATUS.md).

| Step | Work |
|---|---|
| **1.9a** | `PeaceTerms.ImposeVassalage` + `PeaceCostVassalage = 90` + the ladder rung; player menu option |
| **1.9b** | Voluntary submission (§3.2) as an AI weekly move, and as a player option both ways |
| **1.9c** | `Hold`: the field, the daily drift, the §4.3 duty effects, telemetry per link |
| **1.9d** | Defiance: marks, the three tiers, secession wars, contagion |
| **1.10a** | Rival poaching (§5.3) and the cascade cap (§5.4) |
| **1.10b** | Collapse rules (§8); hegemony section in the Ctrl+D menu — sphere, Hold, marks, who owes what |
| **1.10c** | Summons (§5.2), last because it is the most intrusive and the least load-bearing |

Titles (Emperor, Khagan) sit after Phase 2.4 legitimacy, as flavour over a mechanism that by
then will have been measured.
