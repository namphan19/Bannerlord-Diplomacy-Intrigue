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

> **Read §12 and §13 before §3.** As measured on 2026-09-19 this system produced **no vassals at
> all**: the peace table had never imposed one in any run, and voluntary submission had lost the
> saturated trust it was riding on.
> [§12](#12-the-vassalage-drought-and-the-design-that-answers-it-2026-09-20) holds the diagnosis
> and the design calls that answered it, and supersedes §3.1's ladder ordering and §3.2's patron
> selection. [§13](#13-one-subjugation-rung-and-a-cliff-2026-09-20-after-run-07) then supersedes
> parts of §12 itself, after [run 07](../balance/run-07.md) measured it: the two top rungs became
> one at a single price, the winner's demand became a cliff at that rung, and §12.4.1's ceiling
> turned out to be wrong.

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
| §10.4 **The strong vassal problem** | Was claimed here to fall out of the Hold formula for free. **It did not**: the run-04 world ended with the map's strongest kingdom, at 2.2× its patron's strength, still a vassal - the linear fear term charged a weak patron at most half what it rewarded a strong one, and revolt ignored strength entirely. It is now carried by a symmetric fear term and a revolt line that rises with the vassal's strength (§10a) |
| §15 Scenarios A, B, C, D, G | Used as the acceptance tests (§10). A design that cannot produce the source document's own scenarios is not finished |
| §16 **Stability model** | Simplified into one number per link, `Hold` (§4.1). The source's own equation, restricted to quantities the mod already tracks |

### 1.2 Not taken, and why

| Source | Why not |
|---|---|
| §3 **Multi-layered hierarchy** — a hegemon subordinate to a higher sovereign, vassals of vassals | The engine has two faction tiers and no parent-of-kingdom slot, and `TreatyRegistry` already refuses double subordination. A chain would need a hierarchy the game cannot draw, and would make call-to-arms cascades unbounded. Hegemony stays **flat**: one patron per vassal, and a vassal holds no vassals. `CanSign` enforced only the first half until the run-06 review (2026-09-19); both halves are refused there now, and a chain found in a save is cut at load by dissolving the lower link, penalty-free |
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
first four departures were made while building 1.9 on 2026-09-16; the next six came from the
design review of run 04 the same day, and the last three from reading strength properly (docs/STATUS.md, "What to do next" §3). Several of those
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
| §4.1 fear: `25 * clamp(strength(patron)/strength(vassal) - 1, -1, +1)` | `25 * clamp(log2(ratio), -1, +1)` | The linear form is lopsided - twice as strong scored +25, half as strong only -12.5 - and the run-04 world was the case it hid: a patron weaker than six of its seven vassals |
| §4.2 / §6.3: secession below a fixed Hold 15 | The line is `15 + 15 * clamp(log2(vassal/patron), -1, +1)`, from 0 to 30 | Resentment is the motive, strength is whether revolt is anything but suicide. A fixed line sent weak vassals to certain defeat and left strong ones under a patron they could throw off |
| §3.1: submission imposed at war score 90 | Also requires the winner to be the stronger of the two | Every route into vassalage now asks the same question, `Hegemony.IsStrongEnoughToHold` |
| (Not in the spec — run 06, F3) | A neglected vassal may **defect to the kingdom attacking it**: Hold under 40, defender in a war it is losing by 20+ score, patron not fighting the aggressor. The submission is the peace; the old bond is broken *by the patron* (its breach in every court), siblings take the contagion hit, and the new patron is called into the vassal's other defensive wars at once. The attacker must not itself be a vassal. A player attacker is asked; a refusal holds for 42 days | Run 06 showed a patron barred by truce or the cascade guard simply watching a vassal die. Rather than override those rules, the vassal gets an exit and the patron's name pays for it |

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

---

## 12. The vassalage drought, and the design that answers it (2026-09-20)

Everything above describes a system that, as of the `feature/run-06-fixes` branch, **produces no
vassals at all**. This section records the diagnosis, the lead's design calls of 2026-09-20, and
what was deliberately *not* changed. It supersedes §3.1's ladder ordering and §3.2's patron
selection; §4 through §9 are untouched.

### 12.1 The measurement

Two live runs (`di_review_0919`, then `di_review_0919_b`; Winter 1156 → Winter 1162, ~6 in-game
years) produced **zero vassal links**. Full evidence, with the same tables:
[docs/balance/vassalage-absence-2026-09-19.md](../balance/vassalage-absence-2026-09-19.md).

Every vassal link in project history came from voluntary submission. Peace-table vassalage has
**never fired in any run**:

| Log | Voluntary | Imposed at the table |
|---|---|---|
| run-04 | 9 | 0 |
| run-05 | 3 | 0 |
| run-06 parts 1-3 | 5 | 0 |
| live 2026-09-19 (both runs) | 0 | 0 |

Three of the four doors into vassalage (§3.1, §5.3 poaching, §10a defection) need a link to
already exist or a peace table that has never delivered one. With no link on the map, the system
locks itself shut.

### 12.2 Three causes

**Cause 1 — the ladder cannot reach vassalage. Structural, and it is arithmetic, not balance.**

The loser walks `AiDiplomacy.ConcessionLadder` cheapest-first, and the winner accepts any package
worth at least `PeaceWinnerMinimumShare` (0.5) of the war score. War score was clamped to ±100
(`WarRecord.AddWarScore`), so the winner can never want more than 50. Tribute plus prisoners
costs 65. **65 > 50 always**, so tribute always settles and the loser never reaches the vassalage
rung at 95. Not "rarely" — never, for every value of the war score.

The winner collecting (`TryCollectPeace`) walks the same list in reverse, but the list is not
sorted by cost: single fiefs (castle 25, town 45) are yielded *after* vassalage (90), so the
reverse walk asks for land first. §3.1's claim that vassalage is "offered ahead of land" is true
only of the loser's direction — the direction that cannot reach it.

**Cause 2 — voluntary submission lost its trust term.** Run 06's submissions carried a
near-maximal trust term (+20 of a 55 threshold) because trust was saturated near +100. F2's decay
settles mean trust near 14, worth +2.8. That is ~17 points gone from every submission value, and
the threshold was calibrated in the saturated world. *This is arithmetic on logged values, not a
measurement.*

**Cause 3 — the threat term rarely pays.** `Hegemony.SubmissionValue` scales threat by `cover`:
the share of the danger the patron is legally free to fight, times how much of it the patron could
match. On a map dense with tributary, defensive and non-aggression pacts, would-be patrons are
bound to the very kingdoms doing the attacking, so `cover` sits near zero. Measured on
`di_review_0919_b`: Western Empire, losing to Sturgia at +92 and the most desperate kingdom on the
map, valued its best available submission at **4.0** against a threshold of 55.

The logic is inverted: **the more surrounded a kingdom is, the less submission is worth to it.**

### 12.3 The lead's design intent

Stated 2026-09-20, and the changes below follow from it:

- Kingdoms start near parity; war creates the disparity; the losing side is **forced** to submit.
- A weak kingdom may also **choose** a protector and kneel voluntarily.
- A surrounded kingdom may kneel to **the strongest of those attacking it**.
- If the attackers include a hegemon, it kneels to the **hegemon**, never to a hegemon's vassal —
  chains remain forbidden. Among several attacking hegemons, the strongest.
- Kneeling to a party **outside** the war is preferred; kneeling to an attacker is the fallback.

### 12.4 The changes

#### 12.4.1 War score is unbounded; the *demand* is capped

`WarScore` is no longer clamped to ±100. The lead's reasoning: the resources a kingdom can pour
into a war are not bounded, so the score that measures the effort should not be either.

The bound moves to the demand side, on this principle:

> What is unbounded is the **effort**. What is bounded is **what a kingdom can give** — at most,
> itself.

So `WinnerWouldAccept` wants `min(score * PeaceWinnerMinimumShare, PeaceCostVassalage +
PeaceCostPrisoners)`. The ceiling is **derived from the ladder's top rung**, not invented, so
retuning the cost of vassalage moves the ceiling with it and there is no second constant to
forget.

> **Superseded by [§13.3](#133-the-ceiling-must-describe-what-is-reachable).** That ceiling is
> wrong: the top rung is not always reachable. Against a loser that already answers to another
> patron, `CanSign` bars both subjugation and tribute, and a demand pinned to the rung's cost
> refuses everything on the table. Run 07 produced exactly that — a 225-point victory that ended
> in a white peace. The ceiling is now the dearest package the war could actually produce.

The consequence is that submission now arises from the economics rather than from a priority rule:

| War score | Winner wants | Tribute (65) enough? | Settles as |
|---|---|---|---|
| < 130 | < 65 | yes | tribute — unchanged from today |
| **130 – 190** | **65 – 95** | no | **vassalage is the only satisfying package** |
| > 190 | 95 (capped) | no | vassalage |

This is graduated rather than a cliff, which matches the lead's own narrative: a decisive victory
takes a tributary, an overwhelming one takes a vassal.

**Two consequences to watch.** `WouldAccept`'s willingness threshold is
`ExhaustionSeekPeace - score/2`, so at a large score it goes deeply negative and exhaustion stops
being a brake in one-sided wars — directionally right, but it means the gate is gone. And
`WarScoreDriftPerDay` (0.05) becomes negligible at a high score, so such a war never drifts back
toward a white peace. Neither is addressed here.

`WarScoreMin`/`WarScoreMax` exist as constants but `AddWarScore` hardcoded ±100; the clamp removal
fixes that inconsistency at the same time. `WarScore` is `[SaveableProperty(7)]` on `WarRecord`
and older saves hold values within ±100, so **no schema bump is needed** — existing values stay
valid, they simply stop being clipped.

#### 12.4.2 The ladder's order, on the winner's side

`ConcessionLadder` yields fiefs after vassalage, so the winner's reverse walk reaches land first.
Vassalage moves after the fiefs so that the reverse walk asks for submission before territory.
The loser's direction is unaffected — 12.4.1 governs that one.

#### 12.4.3 New rung — dissolution of a hegemony

**The problem this solves.** On the day a peace is signed, a defeated hegemon still holds its
vassals, so `TreatyRegistry.CanSign` refuses to let it submit (§5.1's flat-hegemony rule). In the
130–190 band the winner therefore has *no satisfying package* against a hegemon: tribute is too
cheap, vassalage is forbidden, a fief is worth 45. Cause 1's dead zone, relocated.

**The rung.** *"Keep your throne; your vassals are free."* Cost **75**, between tribute (65) and
vassalage (90) — **untuned**. It ends every active Vassalage treaty in which the loser is the
patron. The ex-patron and every freed vassal become independent kingdoms. Nobody inherits the
sphere.

Why this is the right shape:

- It is the only thing a hegemon has to give that is neither territory nor itself.
- It writes the lead's arc explicitly: lose → lose the empire → become an ordinary kingdom →
  lose again → kneel. Two acts, each earned.
- It does not violate "derived, never declared" (§5.1). The rung ends treaties; hegemony ends
  because the treaties did, with no flag cleared and no event fired.

**The freed vassals stay at war.** Ending the link does not end the wars those vassals were called
into — each has its own `WarRecord` with the winner. So the moment after dissolution there is a
cluster of newly independent, patronless kingdoms at war with the strongest power on the map:
exactly the input 12.4.4 is built for. The winner does not inherit the sphere at the table; it
picks the pieces up one at a time, over months, through the same valuation everyone else uses.
That was the lead's call and it is the reason no blanket white peace is granted here.

**Implementation note.** Links ended this way must **not** be recorded as the patron breaching a
treaty — `ClaimRegistry` would otherwise manufacture a grievance for an act the peace compelled.

`PeaceTerms` is not savable and is absent from `ModSaveDefiner`, so the new field costs nothing
in save terms.

#### 12.4.4 Submitting to an attacker

`AiDiplomacy.TrySubmit` skips every patron at war with the candidate, which is why Cause 3 has no
escape: the kingdoms with the most to gain from a protector are the ones whose neighbours are all
already fighting them.

The new branch lets a cornered kingdom kneel to one of its attackers. **It needs its own
valuation, not the existing one with different weights**, because `cover` is the wrong question
here:

> Submitting to a third party asks how much of the danger that party could **cover**. Submitting
> to an attacker **removes** the danger outright — the submission is the peace. `cover` would
> report near zero and veto precisely the case that works.

Selection rules, from §12.3:

- A patron outside the war is **preferred**, by a weight rather than by absolute precedence.
  Lexicographic priority would let a worthless outsider beat an ideal attacker-patron; a weight
  keeps one comparable number, which keeps §5's rule that the figure shown to a player is the
  figure the AI used.
- Among attackers, a hegemon is preferred over its own vassal — chains stay forbidden, and
  `CanSign` already refuses a vassal taking one.
- Among several attacking hegemons, the **strongest that would still take vassals**. "Strongest"
  alone would usually name the greediest kingdom on the map, and `WouldTakeVassals` has it
  refusing vassals above `GreedRefusesVassals` — a rule that would routinely point at a closed
  door.

**As built (2026-09-20).** Three details differ from the paragraphs above, and the code is right:

- **One valuation, not two.** `SubmissionValue` now splits the threat into the patron's own war,
  which the oath *ends* and which therefore counts in full, and everybody else's, which is
  covered as before. `relief = (fromPatron + cover · otherThreat) / threat` reduces to the old
  cover-only figure exactly whenever the patron is not an attacker, so every route that worked
  before keeps the numbers it was measured with. A separate function would have been a second
  resolver for one concept. It also closes a latent inconsistency: `SubmissionValue` was already
  reachable with an attacking patron (from `TryPoach` and the console), and the old `cover`
  counted such a patron in the threat but never in the coverable share.
- **A charge on the enemy, not a bonus to the outsider** — `SubmissionToEnemyPenalty`, 10,
  untuned. The preference is identical either way, but a bonus would have shifted every existing
  path's numbers (voluntary submission and the whole poaching valuation, neither of which can
  involve a patron at war), and those are the numbers runs 04–06 were measured with.
- **"Strongest" is not a separate tiebreak.** The relief term is proportional to the attacker's
  strength, so the strongest attacking hegemon already scores highest; selection stays a single
  comparison of value, as everywhere else in this file.

The oath starts at `HoldOnDesperateSubmission` (45) — the same footing F3's defection uses,
because it is the same act by a kingdom that has no patron to walk out on. It is tagged
`PeaceCause.Submission` so a balance run can tell it from a defection.

#### 12.4.5 The patron imposes peace between its vassals

When a kingdom kneels to a hegemon whose *vassal* was among its attackers, the two become siblings
under one patron while still at war. The lead's call: **the patron imposes a white peace**,
because the war belonged to the coalition rather than to that vassal privately.

The aggrieved vassal — which was winning, and is ordered to stop with nothing to show — takes a
**Hold penalty**. This is deliberate and is not damage control:

> Every vassal a hegemon gains this way costs it some of the loyalty it already holds. Expansion
> is paid for out of the sphere's cohesion, so the snowball brakes itself from the inside.

#### 12.4.6 A defeated hegemon — no special rule

An earlier draft of this design had a defeated hegemon dissolve its sphere automatically. **That
was wrong and is rejected.** `HoldTarget` already does the work: when a hegemon loses a war, four
of its terms move together —

| Term | Weight | Why it falls |
|---|---|---|
| `fear` | 25 | `Power.Balance(patron, vassal)` drops and can go negative |
| `protection` | 20 | A patron saving itself cannot answer its vassals' calls; the term is signed −1..+1 |
| `wars` | 15 | It drags its vassals into wars it is losing |
| `rival` | 25 | The victor is now visibly the better umbrella |

Roughly 85 points of swing on a base of 40. Hold falls through poaching (40), defiance (30) and
secession (15) on its own, and when the last link ends the hegemony ends with it — §5.1, already
built. Adding a dissolve-on-defeat rule would insert an event at exactly the place this design
deliberately has none, and would be a second source of truth for something already derivable.

The slow path (Hold erosion, months) and the fast path (12.4.3, demanded at the table) are
complements: one is what time does, the other is what a victor chooses.

#### 12.4.7 Rejected

| Proposed | Rejected because |
|---|---|
| Make vassalage the first rung tried by both sides | 12.4.1 already produces it, and gradually. A hardcoded priority puts a cliff at score 95 and imposes submission where tribute would have satisfied the winner |
| Drop the "already a hegemon" gate on `Hegemony.TryPoach` | The gate is the lead's post-run-04 decision, and poaching means war with the patron. It is a contest *between established spheres*, not a bootstrap route. Its silence in a world with no links is correct, and it revives on its own once the other doors open |
| Instant dissolution of a defeated hegemon's sphere | See 12.4.6 |

### 12.5 Balance of power, and what this design costs it

The counterweight to a rising hegemon is not a coalition object; there is none in the code. It is
`AiDiplomacy.BalancingPull`, a term worth up to `PactWeightBalancing` (40) in the pact valuation:

```
pull = SmoothedSphere(strongest outside sphere) / (us + them) - 1     clamped 0..1
```

Nobody declares an alliance. Kingdoms simply find defensive pacts with each other more attractive
while a third sphere outweighs them both, and the resulting web of pacts *is* the coalition. It
works: run 06 saw a coalition answer Southern Empire against Khuzait, and the max-speed run of
2026-09-19 saw one bring Vlandia down (greed 0.58 → 0.1).

Three things about it matter here.

**It shrinks as this design succeeds.** `IsForbiddenByPatron` bars a vassal from signing with
outsiders, and `BalancingPull` deliberately never draws a vassal to balance against its own
patron. The pool of kingdoms able to coalesce is the *free* kingdoms only. Making vassalage
reachable therefore erodes the very brake that is supposed to contain it. **This is a cost
introduced by §12.4, not a pre-existing flaw**, and it is the thing most worth watching in the
first run.

The dissolution rung (12.4.3) is a partial answer, and was not designed as one: breaking a sphere
returns a handful of kingdoms to the pool that may legally sign.

**The trust floor may veto it.** `TrustRegistry.WillConsiderPacts` is a hard gate at
`TrustFloorForPacts` (−20) — not a term to be outweighed. Before F2 it never bound, because trust
saturated at 100. Now mean trust settles near 13.7 (safe), but a war drags the pair to the −35 war
floor, and the 2026-09-19 review observed **many pairs below −20**. In the case that matters
most — the map just after a great war between two spheres — most surviving pairs have a war
history with each other. A coalition against an existential threat could be blocked not because
nobody wants it but because the members recently fought.

Open design call: should a high `pull` relax the pact trust floor, in the same spirit as the
`CanSign(settlesWar:)` exemption already granted to the peace table? **Not decided, not
implemented, and not measured** — this is inference from two verified facts, not an observation.

**One term is inconsistent, and it is a bug.** The threat side reads `SmoothedSphere` (head plus
vassals); the counterweight side reads `Power.Smoothed(us) + Power.Smoothed(them)` — the two
kingdoms alone, without their spheres. Two measures of "how strong is a side" that disagree, which
is what CLAUDE.md §3 names as the bug rather than the symptom. A hegemon weighing a rival does not
count its own vassals on its own side and so overstates the pull. Both sides should read
`SmoothedSphere`.

### 12.6 What is unverified

- The 130 and 190 boundaries in 12.4.1 are arithmetic on constants, not observation. Nobody has
  counted the distribution of peak war scores in the existing logs; at 12 points per town and 6
  per castle, crossing 130 needs roughly eleven towns' worth of victory.
- The cost 75 (12.4.3) and the outsider bonus (12.4.4) are guesses to be tuned in a run.
- Cause 2's counterfactual table is arithmetic on logged values; Cause 3's figures come from a
  single world state.
- 12.5's trust-floor risk is inference. The coalition mechanism itself is verified; its behaviour
  in a post-total-war, high-vassalage map is not.

### 12.7 Implementation order

| Step | Work | State |
|---|---|---|
| **12a** | Remove the war-score clamp; cap the winner's demand at the top rung | **built** — `WarRecord.AddWarScore`, `PeaceTable.MinimumAcceptable` |
| **12b** | Reorder `ConcessionLadder` so the reverse walk reaches vassalage before land | **built** |
| **12c** | `BalancingPull`: `SmoothedSphere` on both sides | **built** — a bug fix |
| **12d** | Dissolution rung (12.4.3), including the no-breach rule for the ended links | **built** — `PeaceTable.DissolveSphere` |
| **12e** | Submission to an attacker (12.4.4) | **built** — `SubmissionValue` relief, `TrySubmit` |
| **12f** | Patron-imposed peace between siblings, with the Hold penalty (12.4.5) | **built** — `Hegemony.ReconcileWithSiblings` |
| **12g** | First measured run; then tune 75, the enemy penalty, and the submission threshold | **not started** |

12a and 12e are the two that matter: the first opens the coerced route, the second the voluntary
one. Everything else is either a consequence of them or a cleanup.

**Carried along, because the change made them wrong or asymmetric:**

- `DiplomacyMenu.ShowSueForPeace` recomputed `budget * PeaceWinnerMinimumShare` inline. Harmless
  while the share was the whole rule; with the cap in place it would have quoted the player a
  figure the AI does not use. It reads `PeaceTable.MinimumAcceptable` now — the same rule the
  peace table has followed since run 02.
- Both player peace menus gained the dissolution rung (demand it, and offer it while losing).
  Without that, the AI would have had a move on the table that the player could not reach, which
  is the one asymmetry this project does not allow.
- `WarScoreMin`/`WarScoreMax` are gone. They were never read; `AddWarScore` hardcoded ±100.

### 12.8 Live check, 2026-09-20

Driven through the GABS bridge on `di_review_0919_b` (Winter 15, 1162). **Mechanisms only** —
this was not a measured run, and nothing here says the balance is right.

| Checked | Evidence |
|---|---|
| Save compatibility | The save loads clean with the clamp gone: schema v4, 974 treaties, `healthy: True`. Old `WarScore` values need no migration, as predicted |
| 12a, the figure | `diplomacy.peace_allowance` at score 92.2: *"gives a budget of 92, and we will not settle for less than 46"* |
| 12a, the band | At final score **85.5** the loser bought peace with a **tributary pact**, not submission — `endedBy=PeaceTable terms=tributary_pact_at_500_per_period`. Exactly what 12.4.1's table predicts below 130 |
| 12d, the rung | Renders and gates on the loser's sphere: `break up sphere 75 (blocked: they hold no vassals)`, and `(they free every vassal)` once the loser held one |
| 12d, end to end | Executed against a hegemon: `treaty_dissolved type=Vassalage`, `hegemony_dissolved_at_table patron=Western_Empire vassal=Aserai winner=Sturgia`. Closed as **Dissolved**, no breach claim, no trust penalty, and `diplomacy.hegemony` then read *"Nobody holds a vassal"* with no event having fired |
| 12e, the formula | Western Empire → Sturgia: `relief 0.38` is exactly 16572/43565, and `threat +19.0` is exactly `clamp(5.03, 0, 2) · 0.38 · 25`. The `enemy -10.0` charge applies only to an attacker-patron |

**Still unverified.** 12f (`ReconcileWithSiblings`) has not executed a single line: it lives
inside `Hegemony.Submit`, and reaching that needs a real vassalage, which needs war score ≥ 95.
`diplomacy.sign_treaty` is **not** a substitute — it calls `TreatyRegistry.Sign` directly and
skips Submit's starting Hold, its call to arms and this reconciliation, so a link made that way
is not a vassalage in any sense the system recognises. 12b and 12c had no situation that would
distinguish them.

**The number that matters, and it is not good enough.** Western Empire, losing at −92 and the
most desperate kingdom on the map, valued kneeling to its attackers at **11.3** (Sturgia) and
**5.4** (Khuzait) against a threshold of **55**. Up from the ~4 measured before §12 — the relief
term is doing real work — but nowhere near. The largest negatives are culture (−20), pride (−16)
and the enemy charge (−10). **12.4.4 opens the door; nothing yet walks through it.** Lowering
`AiSubmissionThreshold` is the obvious lever and is the lead's call, not a fix to make quietly.

**A structural point the drift makes, now measured.** War score bleeds 0.05/day while exhaustion
builds 0.08/day, and the peace table only opens at exhaustion 60. Measured here: 120 days took
the score from 92.16 to 85.81 while waiting for a side to tire. So to still hold ≥ 95 when the
table finally opens, a war has to have **peaked well above 100** — which the old clamp made
impossible. That is a stronger argument for 12a than 12.4.1 makes, and it means the 130 band is
reachable only by a genuinely enormous victory.

**Two diagnostics were lying, both fixed here.** `diplomacy.submission_value` called `CanSign`
without `settlesWar`, so it answered *"Make peace first"* for every attacker-patron — the one
command pointed at 12.4.4 reported that route as impossible. And `diplomacy.offer_peace` had no
term for the dissolution rung, so the rung could not be exercised at all; adding `dissolve` is
what made the row above possible. Both are the same failure the project has hit before: a
diagnostic that asks a weaker question than the system it tests.

**Not caused by the mod:** GABS crashed the game once during this session — `CLR20r3`, P4 =
`Lib.GAB` — on the `started_bridge_pending` path, matching the signature already in CLAUDE.md.
A restart that returned `started_connected` was stable.

---

## 13. One subjugation rung, and a cliff (2026-09-20, after run 07)

The lead's calls after reading [run 07](../balance/run-07.md). This supersedes §12.4.1's ceiling,
§12.4.2's ordering rationale and §12.4.3's separate pricing, and closes §12.4.7's open 12b
question.

### 13.1 The two top rungs become one

Vassalage (90) and dissolution of a hegemony (75) were separate rungs. They can never both be
offered in the same war — a hegemon cannot submit while it holds vassals (`CanSign` keeps
hegemony flat), and a free kingdom has no sphere to give up — so they are now **one rung, one
constant**: `PeaceCostSubjugation = 70`, bundled with prisoners into a package costing **75**.

The rung asks for the loser's political standing, in whichever form it still has one:

| Loser | Gives up |
|---|---|
| A free kingdom | its independence — becomes a vassal |
| A hegemon | its sphere — every vassal goes free |
| A kingdom that already answers to somebody | nothing; the rung is unavailable |

One constant rather than two is the project's "one resolver per concept" rule applied to a
number: there is no case where the two could disagree, so there is no reason for them to be able
to.

### 13.2 The demand is a cliff at that rung, not a slope

`PeaceTable.MinimumAcceptable` now returns the subjugation package's cost whenever this war could
actually produce it. Below that it is half the war score as before, capped by §13.3.

The lead's intent: **a victory that can take the loser's standing asks for it**, rather than
settling for the tribute that half the score would have bought.

The consequence is a sharp change in the shape of the ladder's output:

| War score | Winner wants | Settles as |
|---|---|---|
| under 65 | score × 0.5 | prisoners, money or land |
| 65 – 75 | score × 0.5 | tributary pact |
| **75 and above** | **75** | **subjugation** |

**Tribute is now a ten-point band.** Run 07 settled 18 tributary pacts and 13 of them were at a
score of 75 or more, so most of those become subjugations. That was the lead's intent and it is
the single biggest thing the next run has to check — both the rate of new links and whether the
Hold machinery can absorb it.

**This also closes the 12b question**, which §12.4.7 left open and run 07 turned into a live
example (Vlandia vs Aserai at 98.1, settled for tribute only because Aserai reached the table
first). A cliff is symmetric: the loser walking the ladder up and the winner walking it down
stop on the same rung at the same score. Which side tires first no longer decides whether a
kingdom loses its independence, so the ladder's ordering no longer carries that weight.

### 13.3 The ceiling must describe what is reachable

§12.4.1 capped the winner's demand at a constant — the cost of the ladder's top rung — and
justified it as "a kingdom cannot concede more than itself". **That was wrong**, and run 07
produced the counterexample:

> Spring 1099. Khuzait beat Sturgia to a war score of **225.6** over 126 days, and took a
> **white peace**. Sturgia already answered to Vlandia, so `CanSign` barred subjugation *and*
> tribute. The demand stayed pinned at 95 with nothing on the table able to reach it, and the
> war ended only when the **winner** tired past `ExhaustionAcceptWhitePeaceWhenWinning`.

Sturgia spent its last 34 days wanting peace and unable to buy it. The ceiling is now
`PeaceTable.DearestDemandable`: the cost of the dearest package `IsDemandable` would actually
allow in *this* war, asked through the real resolver rather than re-derived. Against a loser that
already has a patron it collapses to land, money or prisoners, and the winner takes what is
there.

### 13.4 The indemnity rung was dead, and is now sized by the war

Run 07 settled 100 wars with **zero indemnities**. The rung offered half the ruler's treasury,
which at `PeaceCostPerThousandIndemnity` = 8 priced Sturgia's 740,000 denars at roughly 2,950
concession points against a budget that never passed 226. It could never be demanded.

`PeaceTable.LargestIndemnity` now sizes the offer from the **war score**, capped at what a
tributary pact costs so that money stays a mid-ladder option — without that cap an indemnity
sized to the whole budget would be the dearest available package in nearly every war and would
crowd out both land and tribute. The AI ladder and the player's menu both read it, so they quote
the same number.

**The price itself is still suspect and is left to the lead.** At 8 points per 1,000 denars a
60-point indemnity is 7,500 denars: real on the ladder, trivial to a ruler holding several
hundred thousand. Making money bite is a balance decision, not a fix.

### 13.5 Voluntary submission drops to 50

`AiSubmissionThreshold` 55 → 50. Run 07's two submissions cleared 55 at **58.4** and **58.9**, so
the bar was clearly reachable; the lead lowered it to admit the band just below.

**Nothing is known about that band.** `SubmissionValue` was only ever logged when a link formed,
so every run recorded its successes and none of its near-misses, and this change is a guess about
an unmeasured distribution. The new weekly `[SUBMIT]` telemetry records, for every kingdom that
could submit at all, the best offer available to it and whether it cleared the bar. The next run
will have the distribution this one should have had.

### 13.6 Deliberately not done

**The strength margin.** Run 07 §7.1 found `IsStrongEnoughToHold` admits a patron only 1.2%
stronger than its vassal (Northern Empire 9700 over Aserai 9584), producing a link with a Hold
target of **0 on the day it was signed**. The lead's call is to leave it for now.

This is worth watching precisely because §13.2 raises the rate of imposed links. Every extra
subjugation is another chance to create a doomed one, and the churn — a war ending in submission
that unravels weeks later — is the failure mode to look for in the next run.

### 13.7 What the next run must answer

1. How many links form, and how many survive their first year? §13.2 is expected to multiply the
   imposed route several times over.
2. Does tribute survive in its 65–75 band, or vanish?
3. What does `[SUBMIT]` show about the 50–55 band — was lowering the threshold worth anything?
4. How often does §13.6's doomed-link case appear now?
5. Does `ReconcileWithSiblings` (§12.4.5) finally execute? With more links per hegemon the
   coincidence it needs becomes likelier.

**Verified so far:** compiles with no warnings, `tools/LoadProbe` finds no load blockers, and a
**two-year smoke test** on `di_fresh_1084` (Summer 1084 → Spring 1086, 8 wars settled) ran with
**0 errors and 0 warnings**. From that test:

- The indemnity rung is alive: **4 of the 9 settlements used one** (2,000–4,000 denars, at war
  scores 22–41), against 0 of 100 in run 07. It confirms the fix works and, in the same breath,
  the caveat in §13.4 — 2,000–4,000 denars is nothing to a ruler holding ~10⁶.
- `[SUBMIT]` records 184 weekly lines. Median value 0.6, max **51.2**, and only **one** value at or
  above the new threshold of 50; 11 lines sit at 30 or above. Two years of a young world says
  little about the near-miss band, but the record is producing the distribution it was written to.

**This was a smoke test, not a measured run.** It shows the code runs and does not crash. It does
not answer any of §13.7's questions: no link formed in two years, the world had not yet
diverged enough to reach the cliff, and nothing here says whether the rates are right.
