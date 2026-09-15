# Balance run 01 — 28 in-game years

First real campaign measurement. Log: `diplomacy-intrigue-20260915-170243.log`.

| | |
|---|---|
| Span | Summer 1, **1084** → Autumn 1, **1112** — 340 weekly snapshots, **28.3 in-game years** |
| Wars ended | **247** |
| Errors | **0** |
| Kingdoms alive | 8 at the start, 8 at the end, never fewer |

---

## Acceptance criteria: all four met

| Criterion | Result |
|---|---|
| Wars average under ~3 years (252 days) | **18.4 days mean**, 18 median, 78 max — passes by a wide margin |
| At least one alliance forms and holds | **up to 8 at once**, present in **290 of 340 weeks** (85%) |
| No kingdom at permanent total war | every kingdom at war in only **3 of 340 weeks** (0.9%); mean 38.8% of kingdoms at war |
| Save/load stable | 28 years, zero errors, zero tribute defaults |

**A prediction of mine was wrong, and the data corrected it.** Before this run I expected no
alliance to ever form, reasoning that `SharedThreat` carries weight 60 while being zero for
a kingdom with no enemies. Alliances formed abundantly. The other terms —
proximity, trust and ruler relation — clear the threshold of 70 on their own once trust has
had time to build, and 373 treaties expiring honoured over 28 years builds a great deal of
trust. The hypothesis was reasonable and simply false.

---

## The real finding: wars are far too short, and it is one constant

18.4 days overall, but that number is two populations averaged together:

| Population | n | Mean duration | Mean peak exhaustion |
|---|---|---|---|
| Wars a kingdom **chose** | 155 | **23.6 days** | **51.6** |
| Wars **joined by obligation** | 92 | 9.6 days | 13.9 |

Exhaustion at the moment a war ended is bimodal, which is the giveaway:

```
   under 5    78  ###############      obligation wars: begin and end with the principal
      5-20    20  ####
     20-40    31  ######
     40-60    23  ####
     60-80    79  ###############      real wars: ended by the exhaustion system
       80+    16  ###
```

**38.5% of wars reached 60+**, and the mod's AI sued for peace **79 times** — those line up.
So the exhaustion system *is* the primary driver for wars that matter. The problem is how
fast it gets there.

A chosen war reaches exhaustion 51.6 in 23.6 days: **2.19 exhaustion per day**. The time
term contributes 0.08/day — under **4%** of it. Casualties are essentially the whole thing,
and they are roughly **30× stronger than the design intended**, which assumed elapsed time
would set the floor and casualties would merely accelerate.

**Action taken:** `ExhaustionCasualtyStrengthDivisor` 100 → **20**, making casualty
exhaustion 5× weaker. Arithmetic: to land chosen wars near 1.5 years (126 days) the
non-time terms need to fall from 2.11/day to about 0.40/day — a factor of 5.3. This is an
estimate, not a derivation: casualties do not scale linearly with war length, so it needs a
second run to confirm.

---

## Consequences of the short wars

**The peace table never negotiates.** All **79 of 79** AI peaces were white peaces. The
concession ladder never fired once — "bought peace" appears zero times in 28 years, and no
fief ever changed hands through peace *terms*. Wars end before war score accumulates: final
score averages |14.7|, and 106 of 247 ended essentially level. So the demand budget, the
casus-belli land gate and the concession ladder are all correct in isolation and **dead code
in practice**. Longer wars should bring them to life on their own; if they do not, that is
the next thing to look at.

**Obligation wars are mostly noise.** 92 of 247 wars (37%) exist only because an ally was
called: mean 9.6 days, peak exhaustion 13.9, and **26 of them lasted zero days** — declared
and ended on the same day, because the principal made peace immediately and
`ReleaseFollowers` let them out. They inflate the war count and drag the headline average
down. Allies refused only **once in 93 calls**, so every war involving an alliance becomes
two to four wars in the ledger.

Open question for the lead: should a follower's war get its own `WarRecord` at all, or
should it be recorded as participation in the principal's war? The current shape is honest
about the game state — they really are separate `StanceLink`s — but it makes every
statistic harder to read.

**Claims over-accumulate.** Live claims climb steadily and settle at **83–93**. With 165
fiefs changing hands over 28 years and a 20-year ancestral memory window, every kingdom ends
up holding a claim on nearly every other. `ReclaimAncestralLand` is 39.7% of all wars and
`Conquest` — the expensive fallback that is supposed to price naked aggression — only 18.6%.
Cheap justification everywhere means war is cheap everywhere. `AncestralClaimMemoryYears`
(20) is the lever.

**Vanilla still starts most wars.** The mod's AI declared **25** wars in 28 years; 247
happened. Treaty enforcement is working — the action-level backstop logged **zero** refusals,
meaning the decision-level patch stops forbidden proposals before they reach a vote — but
initiative still belongs to vanilla's war AI. Whether to take that over is a scope decision,
and an invasive one.

---

## What worked without needing attention

| | |
|---|---|
| Treaties honoured to expiry | **373** — the trust engine has plenty to reward |
| Treaties broken | **4** in 28 years — and trust stays high, which is why alliances form |
| Tribute defaults | **0** |
| Truces recorded on peace | **247** — every single peace, which is what stops same-day re-declaration |
| Followers released on peace | **28** |
| Call to arms answered / refused | 92 / 1 |
| Errors | **0** |

## Steady state at the end

8 kingdoms, 2 at war, 1 ongoing war, average exhaustion 8.9, 83 live claims, and a standing
web of 10 truces, 11 defensive pacts, 5 alliances and 1 non-aggression pact. Calradia is
politically dense and militarily quiet — the opposite of vanilla, and currently a little too
far in that direction.
