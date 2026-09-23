# Design 07 — Internal politics: war inside a kingdom

Status: **built; verified live on the rebel-win path** (§3d). Phase 2, sitting beside
[02-intrigue.md](02-intrigue.md) rather than replacing it.

The project lead's brief, 2026-09-23: vanilla's internal politics is too simple. Clans should
be able to **declare war on the ruling clan to take the throne** when relations sour, clans
should be able to fight **each other** or take sides, the same contest should be able to break
out **when a ruler dies**, and a clan should be able to have its own **succession dispute** when
its leader dies — including the ruling clan. Explicitly: **without the faction splitting.**

The lead chose the most ambitious of the three routes offered (option C, true intra-kingdom
hostility) and asked for the secession ladder as well.

---

## 1. The spike, and what it found

Written with `tools/CallSites`, built for this question. Metadata only — nothing loads or runs
game code. Every claim below is tagged.

### 1.1 A clan-to-clan war can be written, and is never read — VERIFIED

`Clan` implements `IFaction`, so `FactionManager.DeclareWar(clanA, clanB)` compiles and a
`StanceLink` between two clans is representable.

`Clan.IsAtWarWith` forwards straight to the faction manager, passing **the clan itself**:

```
Clan::IsAtWarWith(IFaction)
  ldarg.0 ; ldarg.1 ; call FactionManager::IsAtWarAgainstFaction ; ret
```

But the engine does not ask the clan. Across the game assemblies there are **120 call sites**
of `FactionManager.IsAtWarAgainstFaction`, and in the instructions immediately preceding those
calls `get_MapFaction` appears **203 times** — against 2 for `get_Kingdom` and 1 for
`get_Clan`. Map-level code resolves hostility through `MapFaction`, essentially without
exception.

`Clan.MapFaction` is exactly this:

```
Clan::get_MapFaction()
  ldarg.0 ; call Clan::get_Kingdom ; brfalse IL_000f
  ldarg.0 ; call Clan::get_Kingdom ; ret      // in a kingdom -> the Kingdom
  IL_000f: ldarg.0 ; ret                      // otherwise -> itself
```

So two clans in one kingdom collapse to **the same object** before any hostility question is
asked. `IsAtWarAgainstFaction(kingdom, kingdom)` is a question about a faction and itself. The
clan-level stance is written and never consulted.

**This is why the naive version of the feature silently does nothing.** The war exists in the
stance table, the log looks correct, and nothing happens on the map.

### 1.2 The GameModel hook exists, and cannot carry this — VERIFIED

`IsAtWarAgainstFaction` is not a plain table lookup. It consults the diplomacy model first:

1. `DiplomacyModel.IsAtConstantWar(f1, f2)` → true short-circuits to "at war"
2. `DiplomacyModel.GetShallowDiplomaticStance(f1, f2)` → **`bool?`**; if it has a value, that
   value **is** the answer
3. otherwise the real `StanceLink`

That is a supported override point, and this mod already overrides `DiplomacyModel`
(`GameModels/ModDiplomacyModel.cs`, Phase 1.11). It is the route CLAUDE.md §3 demands we
exhaust before reaching for Harmony.

**It cannot work here.** By the time the model is consulted, the caller has already reduced
both clans to the same `Kingdom` instance. The model receives `(kingdom, kingdom)` and has no
way to recover which clan asked. The identity we need is destroyed one frame upstream of the
only hook the engine offers.

This is the evidence that the alternatives are exhausted, recorded because CLAUDE.md §3 says a
third Harmony patch may not be added without it.

### 1.3 The lever — VERIFIED live; its first form crashed, see §3c–§3d

> **Superseded in one detail.** Returning *the clan itself* as the map faction crashed the game:
> vanilla casts map factions to `Kingdom`. What shipped returns a kingdom that exists only on
> the map, the rising (§3d). Everything else in this section stands.

Because every consumer reads `MapFaction`, and `Clan.MapFaction` is a two-line property, one
patched getter moves the whole engine:

> While a clan is a belligerent in an active internal war, `Clan.MapFaction` returns **the clan
> itself** instead of its kingdom.

`Clan.Kingdom` is untouched. The clan **stays in the kingdom** — it keeps its seat, its votes,
its place in `Kingdom.Clans` — while the map treats it as its own faction for the duration.
That is precisely the lead's "war without splitting the faction", and it is one patch rather
than a campaign against 120 call sites.

**Not yet verified in a game.** What follows from it is reasoning, not observation.

### 1.4 What has not been established

- **Performance.** `get_MapFaction` has **2,216 call sites**, some of them in per-tick paths.
  The patch must be a field read and an integer compare in the common case — when no internal
  war is running, it has to cost nothing. Untested.
- **Fief ownership.** `Settlement.MapFaction` derives from the owning clan, so a rebel clan's
  towns should become hostile ground automatically. Expected, unverified.
- **Vanilla code that assumes kingdom clans are allies.** `Kingdom.Clans` will still contain a
  clan the map considers an enemy. Election, army gathering and policy code all iterate that
  list. **This is the largest unknown in the design** and the most likely source of a crash or
  an absurdity.
- **Clan succession.** `Clan.SetLeader(Hero)` exists. Which vanilla behavior picks a successor
  on a leader's death, and whether it is overridable without a patch, was not reached in this
  spike.

---

## 2. The ladder

The lead liked the proposal to keep secession rather than discard it. Internal war becomes a
rung, not a replacement, mirroring how [04 §13](04-hegemony.md) ordered the vassalage rungs:

```
disaffection  ->  political contest  ->  armed internal contest  ->  secession
  (02 §2)          (influence/votes)      (this document)            (02 §6, rare)
```

Secession stops being the only way a kingdom breaks and becomes **the failure of the three
rungs before it**. Everything built for 02 §6 keeps its value.

---

## 3. Open, for the lead

1. **What does winning an internal war get you?** The throne is the obvious prize, but the
   loser's fate is not: exile, demotion, execution, or a forced oath (which Phase 1's vassalage
   machinery could carry as-is).
2. **Can the player be dragged in unwillingly** as a third-party clan, or only choose a side?
3. **Ruler-clan internal succession** — a dispute inside the ruling clan while it is fighting
   an internal war is two contests at once. Allowed, or mutually exclusive?

---

## 3a. Defaults — adopted by the lead for a first build, 2026-09-23

Proposed on 2026-09-23 and adopted the same day as the basis for a first build ("try it with
the defaults"). They are defaults to test, not settled design: each was written so that 2.6
could be built on it and revisited later without a save break. What building it changed is in
§3b. Before the three questions themselves, reading the code turned up two facts that
constrain every answer, and one correction to this document.

### What the code says before any choice is made

**A. Every clan the patch leaves alone is on the crown's side, by construction.** The patch
makes a rebel clan's `MapFaction` something other than the kingdom. Every clan it does *not*
touch still resolves to the kingdom, and the kingdom is the faction at war with the rebels. So
"the kingdom" *is* the loyalist side. Neutrality is not the default state of an uninvolved
clan. It would be a third map faction, at peace with both sides, which is a separate and larger
thing (see Q2).

**B. Phase 1's war machinery cannot carry a civil war as it stands.** `WarRecord.Aggressor`
and `Defender` are typed `Kingdom` (save ids 1 and 2, frozen), `Treaty.PartyA/PartyB` likewise,
and 27 sites in the mod read a war's sides with `MapFaction as Kingdom`. A rebel side whose map
faction is a `Clan` casts to null at every one of them. Exhaustion, war score, the peace table,
call to arms and the legitimacy verdicts would all skip the civil war **silently**. That is the
same failure mode §1.1 found in the engine, reproduced inside our own code. Design 02 §6's line
"everything here routes through existing Phase 1 machinery" is therefore not true for the
option-C design. See Q4.

**Correction to §3 Q1.** It said a forced oath "Phase 1's vassalage machinery could carry
as-is". It cannot: `Vassalage` is a `Treaty` between two `Kingdom`s, and after an internal war
both sides are clans of the same kingdom.

**One refinement to §1.3.** "`MapFaction` returns the clan itself" makes each rebel clan a
separate faction, so five rebel clans would be five factions needing peace with each other.
Proposed instead: **every rebel clan's `MapFaction` returns the claimant's clan.** The rebellion
is then one faction, one stance against the kingdom, and fiefs held by any rebel clan are
hostile ground to loyalists together. The claimant's clan is the natural banner; it is already
recorded as the `Pretender`.

### Q1. What does winning get you? — Proposed: the throne or the claim, nothing irreversible

| Outcome | Default |
|---|---|
| **Rebels win** | `ChangeRulingClanAction.Apply(kingdom, claimantClan)`. The new crown takes the existing −15 for irregular succession (`SuccessionContestedLegitimacy`). The deposed ruling clan **stays at court** and becomes a standing `Pretender` if its side held ≥ 30% (`SuccessionPretenderShare`), so the ladder can turn again |
| **Crown wins** | The claim is retired (`Pretender` removed). Legitimacy +12, the figure for a just war won. Rebel clans stay in the kingdom and keep their fiefs **as the war left them** — what was taken stays taken |
| **Negotiated / stalemate** | Nothing moves, as with a white peace at 2.4. The claimant keeps the claim, and the war simply ends |
| **Loser's fate** | **No exile, execution or demotion in v1.** Exile expels a clan into a rival's arms, and execution is irreversible and has vanilla consequences of its own. Both are good choices for a *ruler* to make later, as a decision with costs, not an automatic outcome |

What ends it (a question §3 did not ask but a build needs), un-tuned: a side loses when its
leader is **held captive 30 days** by the other side, or when that side's internal exhaustion
reaches **100**. The claimant's **death** ends it as a crown win. The ruler's death does not:
vanilla's heir takes the throne and the war goes on against them (Q3), unless the heir is a
rebel, which is a rebel win. Once both sides pass 40 (`ExhaustionCourtPressure`, the dove
threshold) the war ends in a stalemate. Capture alone is too common in vanilla battles to end
a war on the day it happens.

### Q2. Can the player be dragged in? — Proposed: yes, exactly as by any war their kingdom fights

- **A sworn clan is loyalist by default** (fact A). That is what vanilla already does to a vassal
  whose kingdom declares war, so nothing new is imposed on the player.
- **Changing sides uses one rule for everyone.** Design 02 §6 has clans below loyalty 25 that are
  not in the bloc pick a side by relation. The AI does so by that rule. The player's clan meets
  the same condition and gets a **prompt** instead of a roll, which is the UI-layer difference
  decision 02 §9.2 allows. No argument in the resolver says "is this the player".
- **If the player is the claimant**, the trigger is the same as for an AI claimant. The player is
  asked before the banner is raised, and can decline. An AI claimant's "decision" is the trigger
  rule itself.
- **No neutrality in v1.** A neutral clan is a third map faction with two peace stances and a
  third place in every vanilla system that assumes two sides. Deferred until the two-sided war is
  verified.

### Q3. A ruling-clan dispute during an internal war? — Proposed: mutually exclusive in v1

- **One internal war per kingdom at a time.**
- **No clan succession dispute in either belligerent clan** while it runs. If a leader dies
  mid-war, vanilla's heir takes the clan uncontested and inherits its side. If the claimant dies,
  the war ends as a crown win unless the heir holds a claim of their own. That case is a Q1
  outcome, not a second contest.
- **Intra-clan succession disputes become 2.6b**, after the internal war is verified. §1.4 notes
  that the spike never reached how vanilla picks a clan's successor, so there is nothing yet to
  build it on.

### Q4. How is a civil war recorded? — Proposed: its own record, not `WarRecord`

Follows from fact B. Options:

1. **A new `InternalWar` type** (definer class id **13**), holding the kingdom, the claimant clan,
   the start date and a per-side exhaustion. Readers that need to know about it ask for it by
   name. **Recommended.** It is additive, touches no frozen id, and cannot be misread by Phase 1
   code that was never written for it.
2. Bolt a `RebelClan` (id 15) onto `WarRecord`, with Aggressor = Defender = the kingdom. This
   reuses the peace table, but every one of the 27 `as Kingdom` sites, plus every "the other
   side" computation, would read a war against itself. Rejected.

The cost of option 1 is that a civil war gets **a smaller settlement than the peace table**: the
three outcomes in Q1, not a budget of terms. That matches Q1's scope and can grow later.

### Still unknown, carried from §1.4, and one more

- **`Army` requires a `Kingdom`** (`.ctor(Kingdom, MobileParty, ArmyTypes)`, from ApiDump). A
  rebel army would be an army *of the parent kingdom*, and what the map makes of that is untested.
  Expected default for v1: **rebels fight as separate parties and form no armies**, unless the
  first live test shows vanilla's army code tolerates it.
- The trigger thresholds in 02 §6 (bloc ≥ 40% of influence, legitimacy < 35, two clans < 25)
  stay as the defaults. Decision 02 §9.4 already marks them as guesses.

---

## 3b. What building it found, 2026-09-23

The first build ([Intrigue/InternalWars.cs](../../src/DiplomacyIntrigue/Intrigue/InternalWars.cs))
read the IL of every map-faction getter before writing a patch. Three findings changed the
design. None has been seen in a running game yet.

**§1.3 was wrong: one getter does not move the whole engine.** `Town`, `SettlementComponent`
and `MobileParty` all call `Clan.get_MapFaction`, and `Village` reads its bound town, so parties
and fiefs do follow the clan getter. **`Hero.get_MapFaction` does not.** It inlines
`Clan.Kingdom ?? Clan` and never calls the clan's getter, so a rebel lord's party would have
been hostile to the crown while the lord in person still read as the crown's own. It takes a
second patch, `Patches/Hero_MapFaction_Patch.cs`. The project now has **five** Harmony patches,
not four.

**The kingdom would have offered the rebels a vanilla peace.** `Kingdom.UpdateFactionsAtWarWith`
walks every clan as well as every kingdom, so the rebel banner lands in the kingdom's at-war list
like any foreign enemy, and vanilla's barter and decision paths would value a peace with it.
`ModDiplomacyModel` now refuses any peace between the two sides of an internal war. The war
ends only by §3a's rules. That is a model override, not a patch.

**A loyalist marshal would have summoned the rebels.** An army belongs to the kingdom and the
rebels are still in it. `GameModels/ModArmyManagementModel.cs` (a model, not a patch) keeps them
apart: a rebel cannot raise an army, cannot be called into one, and is filtered out of a loyalist
lord's call. When the war starts, an army led by a rebel is disbanded and rebel parties are sent
out of loyalist armies.

Two consequences of the mechanism, now written down as design:

- **The rebellion is at war with the crown and nobody else.** It inherits none of the kingdom's
  foreign wars. Its stance toward other kingdoms is the banner clan's own clan-level links, which
  are normally unused, so they are logged at the start of each war for the live test to read.
- **Exhaustion is the internal war's own**, not Phase 1's. Casualties between the two sides use
  Phase 1's formula (losses against the side's strength). They do not feed
  `WarExhaustion.Worst`, so a civil war does not push the court toward the Doves. That is a gap
  to revisit, not a decision.

The riskiest unknown was expected to be JIT inlining: `Clan.get_MapFaction` is a 16-byte
getter, and a Harmony patch cannot reach an inlined copy. The live test settled it, below, and
the real problem turned out to be somewhere else.

## 3c. The first live test, and why the first build failed, 2026-09-23

On `di_pretender_test`, the first live run:

| Check | Result |
|---|---|
| Natural trigger | Battania met all three conditions (bloc 61%, legitimacy 25, 4 clans below 25). The campaign's own daily tick started the war, with no test command: Aradwyr against Muinser, 5 rebel clans, the crown side holding 39% of the court |
| **The engine sees two sides** | Two real battles between rebels and crown within 2 seconds, each counted into the internal war's exhaustion. A battle only counts when the engine's own `MapEventSide.MapFaction` returns the banner on one side and the kingdom on the other, so **the getter was not inlined out of the patch's reach**, and the AI attacked across the line |
| **The game crashed 2 seconds later** | `InvalidCastException: Unable to cast Clan to Kingdom` in vanilla `KingdomDecisionProposalBehavior.DailyTickClan`, which does `(Kingdom)clan.MapFaction` for a clan it knows is in a kingdom |

The crash is the design's largest unknown from §1.4 ("vanilla code that assumes kingdom clans
are allies"), and it is a class of fault, not one bug. The mechanism breaks an invariant
vanilla relies on: **a clan in a kingdom has a `Kingdom` for its map faction.** A Cecil scan
of every game assembly found **46** places that read a map faction and `castclass Kingdom`
without `isinst`. About **25** have no `IsKingdomFaction` guard nearby. The guard check is a
heuristic over the 12 instructions before the read, so the true count may be higher, and a cast
through a stored local is not caught at all. The unguarded ones include paths that run all the
time:

- `GainKingdomInfluenceAction.ApplyInternal`: every influence gain of a rebel clan.
- `AiPartyThinkBehavior.PartyHourlyAiTick`: hourly, per party.
- `KingdomDecisionProposalBehavior.DailyTickClan`: the crash above.
- `SettlementClaimantDecision` and `SettlementClaimantPreliminaryDecision`: after any fief changes hands.
- `TradeAgreementsCampaignBehavior.SettlementEntered`.
- About fifteen lord-conversation conditions: talking to a rebel lord.

Guarding them one by one would mean roughly 25 more Harmony patches, against CLAUDE.md §3's
rule that Harmony is the last resort. Two other routes were looked at and rejected, because
each gives up part of the agreed brief. **Temporary departure**: rebels leave through vanilla's
rebellion path and rejoin afterwards. They would lose their seats and votes during the war, and
that is the faction split the lead turned down. **An empty shadow kingdom** only for the map:
`AiMilitaryBehavior.CalculateMilitaryBehaviorForFactionSettlements` picks siege targets from
the *enemy faction's* settlement list. A shadow kingdom with an empty list would leave every
rebel castle untouchable, so the war would have no sieges.

Nothing was saved during the run, and `di_pretender_test` is unchanged. The deployed module
was put back to `development` straight after, so no playable build carried that code.

## 3d. The rising: what shipped, 2026-09-23

The lead's call, on the same day: "the decision is yours, I only need the result as agreed".
The agreed result is fixed: rebels stay in the kingdom and keep their seats and votes, and the
kingdom does not split. The mechanism keeps the §1.3 idea and changes one thing. **The rebels'
map faction is a real `Kingdom`, the rising**, created when the war starts and destroyed when it
ends, instead of the claimant's clan. Every vanilla `(Kingdom)MapFaction` cast then holds.

What makes that work, each piece established from IL before it was written:

| Piece | Why | Evidence |
|---|---|---|
| The rising's clan, fief, hero and war-party lists are filled with the rebels **without moving them**. `AddClanInternal` (by reflection), `OnHeroAdded`, `OnFortificationAdded` and `OnWarPartyAdded` are called, and `Clan._kingdom` is never touched | So the AI finds rebel castles to besiege and its own to defend | `Clan.EnterKingdomInternal` makes exactly these four calls; each only edits a list |
| The lists are re-synced daily, when a fief changes hands, when a war party is raised or destroyed, when a hero dies, and after every load | The engine updates only the clan's own kingdom, and never saves these lists | All of them are `[CachedData]` (Cecil). `Kingdom.InitializeCachedLists` rebuilds them on load from `Clan.Kingdom` |
| The lists are **emptied before** the rising is destroyed | `DestroyKingdomAction` runs `DestroyClanAction` on every clan still listed in the kingdom. Destroying the rising with its lists intact would wipe the rebels out of the game | IL of `DestroyKingdomAction.ApplyInternal` |
| The map-faction index is rebuilt in `CoreBehavior.SyncData` | `ClanVariablesCampaignBehavior.OnSessionLaunched` destroys any kingdom whose `Leader.MapFaction` is not itself. The rising's leader passes that check only through the hero patch | IL. It is the only vanilla load-time path that reaches a rising; the other three that destroy kingdoms key on `Clan.Kingdom` or a game-version upgrade |
| `Realms.IsRealm()` is read by every loop over `Kingdom.All` in the mod, and every war, peace, raid and destruction handler ignores a rising | A rising is a `Kingdom` object but not a realm: no treaties, claims, trust, court or peace table | 50 loops moved onto one helper |
| `ModKingdomDecisionPermissionModel` refuses war, peace and alliance decisions naming a rising, king selection in one, annexation under its banner, and expelling a rebel mid-war | A decision voted *inside* the rising by rebels who are still members of their realm could otherwise expel one of them from it | Model hooks, no patch |

Still exactly two patches (`Clan_MapFaction_Patch`, `Hero_MapFaction_Patch`), plus two model
overrides (`ModArmyManagementModel`, the peace guard in `ModDiplomacyModel`).

### The live test, `di_pretender_test`, 0 errors after one fix

| Check | Result |
|---|---|
| Natural trigger | Battania: Aradwyr and 5 clans against Muinser, on the campaign's own daily tick |
| The rising holds the rebels | `di_rising`: 5 clans, 8 fiefs (3 towns, 5 castles), 13 war parties; at war with Battania and nobody else |
| **The rebels are still in the kingdom** | Still in `Battania.Clans`; `diplomacy.loyalty Battania` still lists all seven clans, the rebels included |
| The engine sees two sides | Clan, leader, party and town of every rebel resolve to the rising through the engine's own getters; the stance is WAR against the crown. Loyalists resolve to Battania, at peace |
| Real fighting | 183 battles and 41 raids between the two sides, each counted into the war's exhaustion |
| **Save and reload mid-war** | Saved as `di_civilwar_test`, new process, reloaded: `1 internal wars`, the rising **not destroyed** by vanilla's load-time checks, its lists rebuilt in full (5 clans, 8 fiefs), the map-faction table identical |
| Natural end | The crown's side reached exhaustion 100 first (rebels 35.6): **rebels won**. Aradwyr's clan took the throne, legitimacy −15 (12 → 0), and Muinser was deposed but kept as a pretender at the 39% his side held. Aradwyr's own claim lapsed on taking the throne |
| Bug found and fixed | `Collection was modified` from iterating `Kingdom.All` while the war created the rising in it. Caught by the handler's `try/catch`, not a crash; fixed by iterating a copy and verified on the second run |

**Not yet verified:**

- The crown-win and stalemate outcomes. Only the rebel win happened.
- A fief changing hands between the two sides. It happened 0 times, so the settlement-change sync never ran live.
- That the 5 rebel clans still exist after the rising was destroyed, and that the rising is eliminated. The log shows no error, but the game was not asked.
- Either player prompt: rising as the claimant, or choosing a side.
- The captivity end condition.
- A balance signal, recorded and not acted on: the day the war ended, Sturgia and Vlandia **both** declared war on Battania, which was now at legitimacy 0 and worn out. Plausible, and a civil war probably should invite this, but a realm that loses a civil war and two foreign wars in a week may not recover.

---

## 4. Tooling this produced

`tools/CallSites` — the reverse of `tools/ApiDump`. ApiDump answers "what is the public
surface"; this answers "who calls this method, and what did they push onto the stack first",
which is the only way to tell whether the engine reads a `Clan` or its `MapFaction`.

```bash
dotnet run --project tools/CallSites -- --il      "Clan::IsAtWarWith"
dotnet run --project tools/CallSites -- --callers "FactionManager::IsAtWarAgainstFaction"
dotnet run --project tools/CallSites -- --callers "::get_MapFaction"
dotnet run --project tools/CallSites -- --members "Clan"
```

Output lands in `artifacts/callsites/`.

## 5. A house divided: succession disputes inside a clan (2.6b), 2026-09-23

The rest of the lead's brief: "a clan should be able to have its own **succession dispute**
when its leader dies, including the ruling clan". Built on the same rule as 2.5: **vanilla
still picks the heir, and this pillar handles the politics afterwards.**

### What the spike found (IL, v1.4.8)

| Question | Answer |
|---|---|
| Who picks a clan's new head? | `ChangeClanLeaderAction.ApplyInternal` calls `clan.GetHeirApparents()`, which scores every eligible clan hero through **`HeirSelectionCalculationModel`**, a GameModel. The highest score wins and ties are broken at random. Then `Clan.SetLeader`, then **`OnClanLeaderChanged(oldLeader, newLeader)`** |
| How are heirs scored? | Male +10, direct line +10, older or younger ±5, and the family's most skilled hero gets `HighestSkillPoint` on top. So "close" is measurable on vanilla's own scale |
| Can a hero found a new clan at runtime? | Yes, by vanilla's own recipe: `Clan.CreateCompanionToLordClan` (create, name, culture, banner, kingdom, home, `hero.Clan`, `SetLeader`, `IsNoble`, `OnClanCreated`). It is the path used every time a player grants a fief to a companion |
| What happens to the party a hero is leading when they change clan? | Nothing. `Hero.set_Clan` only moves them between lord lists. So the hero is first taken out of the party with `TeleportHeroAction.ApplyImmediateTeleportToSettlement`, which removes them from the roster |

No Harmony patch, no model override and no new save data. The cadet branch is an ordinary
clan that vanilla saves.

### The rule

When a head dies, if the runner-up heir came within **5 points** of the successor
(`ClanSuccessionContestMargin`) **and** their relation with the successor is below **10**
(`ClanSuccessionDisputeRelation`), the house divides. The runner-up leaves with their spouse
and their children who have not come of age, and founds **a cadet branch in the same realm**.
It has no fief, starts with a quarter of the parent house's renown, and flies the parent's icon
in its colours. Relation between the two heads drops by 20. All four numbers are UN-TUNED.

The rule does not apply while the house is fighting an internal war (§3a Q3), or while the
runner-up is held prisoner or is in battle.

**The ruling house needs nothing extra.** A runner-up who splits from the ruling clan is now a
clan leader, and is a child or sibling of the late ruler. That is exactly who
`SuccessionModel`'s blood claim admits. At the next daily succession watch they stand as a
claimant, the court is tallied, and at 30% support they become a standing pretender. From
there the Pretenders bloc and the internal war (2.6) follow by their own rules. A second
claimant path here would be a second resolver for the same question.

**The player's house is under the same rule.** Vanilla lets the player choose their heir.
Passing over a higher-scoring heir who dislikes the choice can split the player's own house.

### Tools

- `diplomacy.heirs [clan]` shows, for every house or one, what would happen if its head died
  today. It prints `ClanSuccession.Predict`, the resolver the event uses.
- `diplomacy.test_divide_clan <clan>` splits a house now, skipping the thresholds but not the
  mechanics. It is for testing the split itself.

### Not yet verified — nothing here has run in a game

- The split mechanics: the new clan exists, is in the realm, is noble, has the household, the
  founder is out of their old party, and the clan survives a save and reload.
- A real death: the event path (`bannerlord.hero.kill_hero` on a head that `diplomacy.heirs`
  marks WOULD DIVIDE).
- The ruling-house path: that the next succession watch counts the cadet leader as a claimant.
- How often it fires. The relation threshold in particular is a guess until warm family
  relations in a real campaign are measured.
