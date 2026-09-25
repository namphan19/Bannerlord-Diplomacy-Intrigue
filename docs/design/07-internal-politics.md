# Design 07 — Internal politics: war inside a kingdom

Status: **built and verified live**: all three outcomes, sieges, save/reload (§3d), and a house divided (§5).
**2.6c, conceding and changing sides for gold, built and run live (§6).** Phase 2, sitting beside
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
  first live test shows vanilla's army code tolerates it. *Superseded (§3d): once the rising
  became a kingdom, rebel armies are raised under it, and allowing them is what gave the war
  its sieges.*
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
apart: neither side's call to arms can reach the other's parties. When the war starts, a
realm army led by a rebel is disbanded and rebel parties are sent out of loyalist armies. (The
first build also forbade rebel armies outright; the live test showed that cost the war every
siege, and it was lifted - §3d.)

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

### Second round of live tests, 2026-09-23/24: six sessions, five bugs, all fixed

| Check | Result |
|---|---|
| **Crown wins** (`test_end_internal_war ... | crown`) | All 8 clans still in Battania, every map faction back to Battania, at peace. Legitimacy 25 → 37 (+12). Aradwyr's claim retired. The rising `isEliminated`, with its clan list emptied first |
| **Stalemate** | Legitimacy unchanged, the claim still standing, the rising eliminated. **The cooldown held**: Battania still met all three conditions the next day and did not rise again ("365 days to go") |
| **Rebels win, naturally, a second time** | Crown exhausted first. fen Eingal took the throne, the rising was eliminated with no armies and no clans left, and all 8 clans were still in Battania. The castles the rebels took stayed with them |
| **Sieges and fiefs changing sides** | 0 in the first two runs, because the AI besieges only with armies and v1 barred rebel armies. With the ban lifted: 3 sieges in one session. Llanoc Hen and Rhemtoil castles and the town of Pen Cannoc went to the rebels, and the rising's fief list followed each capture at once (8 → 11) |
| **Save and reload with captured fiefs and cadet houses** | The rising came back holding all 11 fiefs, including the three it took; both cadet branches were still at their courts |

Bugs found this way, each fixed and re-run: rebel armies banned (no sieges); a null skill seed
in heir scoring; the death check reading `IsAlive` inside `KillCharacterAction`; a dead head
moved into the new house; a landless cadet's null mid-settlement, which **crashed the game**
at the next fief vote in its realm. That last one was read out of the live process with
`tools/DumpProbe --pid`, because the crash dialog holds the exception.

**Still not verified:**

- Either player prompt: rising as the claimant, or choosing a side. The test saves have the
  player in Khuzait.
- The captivity end condition. Every war so far ended on exhaustion.
- A long AI-only run. How often internal wars start, and whether a realm recovers from one, is
  unmeasured. A signal from the first run is recorded and not acted on: the day the war
  ended, Sturgia and Vlandia **both** declared war on Battania, which was at legitimacy 0 and
  worn out.

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
(`ClanSuccessionContestMargin`) **and** their relation with the successor is below **−10**
(`ClanSuccessionDisputeRelation`; first shipped at 10, see the live test below), the house divides. The runner-up leaves with their spouse
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
- `diplomacy.test_divide_clan <clan> [| <hero>]` splits a house now, skipping the thresholds
  but not the mechanics, optionally with a named founder. It is for testing the split itself.

### Live test, 2026-09-23/24

| Check | Result |
|---|---|
| **A real death divides a house** | Gusukan, head of Oburit (Khuzait), killed: Altu succeeded, and Sevin (15 points to 20, relation −24) founded **Oburit of Sevin**, tier 1, at the Khuzait court |
| **A ruling house divides** | Queen Rhagaea of Southern Empire killed: Ulbos took the throne, and Patyr (20 to 25, relation −12) founded **Pethros of Patyr**, tier 4, with his spouse Verina |
| **The founder is a claimant** | In Sturgia, Lilizha, who is *not* the late king's child or sibling, split from the ruling house, then King Raganvad died. The tally read "Vidar 85% (7 clans), **Lilizha 15%** (3 clans)". She counted, and at 15% did not become a pretender, which is the rule |
| Save and reload | Both cadet branches still at their courts after a reload |
| **How often it fires** | Measured on `di_civilwar_test`: at the first threshold (relation < 10), **22 of ~70** houses would divide at their head's death, because most heirs sit at relation 0 ("never met"). Moved to < −10, about 6 (~8%) |

Four things running it showed that reading the code did not:

- **Vanilla moves relations at the succession.** Every hero's relation with the new head is
  adjusted inside `ChangeClanLeaderAction`, and in all three cases it warmed: Sevin −38 → −24,
  Patyr −48 → −12, Simir −9 → 2. `diplomacy.heirs` predicts from today's relations and says so.
- **The head is replaced before the head dies.** `KillCharacterAction` marks the hero, changes
  the clan head, and only then kills them, so the death is read from the death mark.
- **A runner-up is often not the late ruler's child or sibling.** Vanilla's heirs include
  nephews and in-laws. In Sturgia the heir apparent himself is neither. The succession model
  now counts anyone who left the ruling house at this succession (`NoteBranchedHeir`).
- **The throne watch missed a death straight after a load.** It seeded itself on the first
  daily tick, so a ruler who died before that tick was recorded as their heir's first
  sighting. It now seeds at session start.

**Still not verified:** the player's own house dividing, and a cadet branch going on to become
a standing pretender and start an internal war. Both follow from verified pieces, but the full
chain has not been seen end to end.

---

## 6. Conceding, and changing sides for gold (2.6c), decided 2026-09-24

2.6 as shipped gives nobody anything to do in a civil war except fight it. The player sees a
notice when it starts and another when it ends, and no screen shows the sides, the exhaustion or
what each ending does. This section is the answer: two new acts, and where the war is shown.
Mockup: the "Civil war — Phase 2.6 UI" row of the court canvas
(https://claude.ai/artifact/1FrpG5in328WYfNi6sP8Pf, boards `CivilWar`, `ChangeSides`,
`RealmCivilWar`). Its figures are sample, not live. **Built on 2026-09-24 on branch
`feature/phase-2.6c-civil-war-ui`, and run live the same day** - see "The first live run" at the
end of this section.

### What the lead decided

| # | Decision |
|---|---|
| 1 | **Either side's leader can concede.** A concession ends the war exactly as if that side had reached exhaustion 100. There are no new outcomes: it reuses §3a Q1 as it stands |
| 2 | **A house can change sides mid-war, paid in gold.** Gold only for now. Influence and fiefs as currency are not in scope |
| 3 | **The player's house can be bought** like any other house, and paid, not charged |
| 4 | The price scale is Claude's call, and may rest on clan strength |
| 5 | A mockup before any code |

### Conceding

- Only the **ruler** (for the crown) and the **claimant** (for the rising) can concede. A house
  that merely sides with one of them cannot end the war for it.
- A concession calls the existing ending with the existing outcome: the crown conceding is a
  rebel win, and the rising conceding is a crown win. Legitimacy, the pretender left behind, the
  365-day cooldown: all as §3a Q1.
- **The AI concedes by rule**, so the act is not the player's alone: a leader concedes when its
  own side's exhaustion is at **75** or more while the other side's is **under 40**
  (`InternalWarConcedeExhaustion`, `InternalWarConcedeOtherBelow`, both UN-TUNED). The condition
  cannot overlap the stalemate, which needs both sides past 40. Checked on the daily tick,
  beside the existing endings.
- A player leader is never conceded for automatically. The button needs a second click to
  confirm, inside the panel rather than in an inquiry, so the test bridge can drive it.

### Changing sides: who

A house can be bought if it is a sworn clan of the kingdom at war with itself, and none of:
the ruling clan, the claimant's clan, a mercenary, a house that has already changed sides in
this war, a house whose head is a prisoner or in a battle. **Once per war per house.**

### Changing sides: the price

The side that gains the house pays its head. The payment comes from the leader's own purse (the
ruler's or the claimant's), not from any treasury. One price, whichever direction. It is what
the crown pays to win a house back, and what the claimant pays to take one.

```
price = (2,000 + 15 x strength + 4,000 per town + 2,000 per castle)
        x relation  x bond  x momentum          rounded to 100, never below 1,000

strength  = Clan.CurrentTotalStrength (the engine's own figure; 300-650 for most houses in the
            balance runs, 92-975 across Battania's houses on 2026-09-24)
relation  = 1 - rel(buyer, house head) / 200                          0.5 - 1.5
bond      = 0.5 + tie / 100, where tie is how firmly the house holds  0.5 - 1.5
            to its current side: on the crown's side its loyalty
            (LoyaltyModel.Of, the court's resolver), on the rising's
            side 50 + rel(house head, claimant) / 2
momentum  = clamp(1 + (buyer's exhaustion - other side's) / 100,      0.7 - 1.5
            0.7, 1.5): joining the side that is losing costs more
```

A typical house (strength 450, one castle, neutral on every factor) costs **10,800**. With
each factor between 0.8 and 1.3, most houses land between about 6,000 and 25,000. With every
factor at its limit, the extremes run from the 1,000 floor to about 60,000 (strength 650, a
town and a castle, all three factors at 1.5). How those prices compare with lords' purses has
not been measured. That is the first thing to check once it runs. Every constant goes into `IntrigueConstants`, marked UN-TUNED.
Worked through, the mockup's example (strength 400, one castle, relation −12 with the buyer, +8
with the claimant, exhaustion 62.4 against 35.6) comes to 10,000 × 1.06 × 1.04 × 1.27 ≈ **14,000**.
The mockup shows 13,800, a sample figure drawn before the formula was fixed.

The panel lists the three base parts, then each factor as the gold it adds or removes, so the
lines sum to the price. **One resolver** computes it: `SideChange.QuoteFor(state, war, clan)`,
which also answers whether the house can change at all. The panel, the AI and the player's own
offer read the same figure.

### Changing sides: when the AI buys

Weekly, each AI leader considers the houses on the other side that it can buy, and buys the one
with the most strength per denar, if **its own side is not ahead** (its exhaustion is not below
the other side's) and the price is at most **half its purse** (`AiSideChangeBudgetShare = 0.5`,
UN-TUNED). One house per leader per week. The house accepts: the price is its asking price.
One resolver, `SideChange.AiWouldPay`, holds both conditions.

"Not ahead" was added by Claude while building, after the lead had seen the rule without it.
Without it, a rich ruler who is already winning buys a house a week, at the discount the momentum
factor gives the winning side, and a civil war is settled by purse rather than by arms. With it,
buying is the losing side's way back, and it pays the losing side's premium for it. It is the
same test for both sides and for the player's house, and it is the first rule to revisit if
houses change sides too rarely.

**The player's house under the same rule.** When an AI leader's pick is the player's house, the
player gets the offer and can accept (and is paid) or refuse. A refusal stops that leader
offering again for 30 days. That cooldown is transient and not saved. The player can also go
over unasked, from the Court tab, whenever the other leader would pay by the same budget test.
A player who leads a side buys houses from the Court tab, with the same price and eligibility.

### What changing sides does

- The gold moves from the buyer to the house's head (`GiveGoldAction`).
- The house joins or leaves `InternalWar.Rebels`, and then the path the side-choice prompt
  already uses runs: `RebuildIndex`, `SyncFaction`, `SeparateArmies`. Its fiefs and parties go
  with it, because they follow the clan's map faction.
- The head's relation with the leader they left drops by **20** (UN-TUNED).
- The house stays a sworn house of the kingdom throughout. Nothing here moves `Clan.Kingdom`.

**Save data.** "Has changed sides in this war" must survive a reload: `InternalWar` property
**14**, `SideChanges`, a `List<InternalWarMember>` of houses that changed sides. That reuses a
class and container already defined (class id 14), so there is no new definer entry. A save
written before 2.6c has no property 14 and gets an empty list on load.

### Where it is shown

The rule of one surface per scope decides this. A civil war is a matter inside one realm, so
it lives on the **Court tab**:

- **The Court tab in civil-war mode**, above the court:
  - both sides, with their houses, fiefs, share of the court at the start, exhaustion bars
    marked at 40 and 100, and each leader's days held against 30;
  - the three endings, each with what triggers it;
  - the roster split by side, with each house's price;
  - the selected house's price, line by line;
  - Concede, for the two leaders only.
- **Changing sides**, when the player's house is not a leader: where it stands, what the other
  leader would pay and whether their purse allows it, and what going over means under each ending.
- **The Realm tab only points there.** The civil war heads the wars strip with "Open the court",
  and the standing strip reads "Divided". When the player's house is with the rising, the
  strip says so, and marks the realm's foreign wars as the crown's.
- **The Diplomacy tab does not list the rising.** It is a `Kingdom` at war with the realm, so
  vanilla's list would show it as an enemy to negotiate with. There is nothing to negotiate
  there. Not yet checked in game whether vanilla lists it today.
- No action is needed anywhere else. Every act here is internal, so all of them sit on the Court
  tab, which the test bridge can click. Lord dialogue and inquiries would put them where it
  cannot.

### What was built, 2026-09-24

| Piece | Where |
|---|---|
| The price, eligibility and the AI's paying rule | `Intrigue/SideChange.cs`: `QuoteFor`, `AiWouldPay`, `Execute`, `WeeklyTick` |
| Moving a house, conceding, the AI's concession | `InternalWars.ChangeSide`, `Concede`, and the rule in `Advance` |
| Armies split on both kingdoms, not only the realm | `InternalWars.SeparateArmies` (a house bought back can stand in a rising's army) |
| Court tab in civil-war mode | `UI/KingdomScreen/CivilWarVM.cs`, and the civil-war block of `DiCourtPanel.xml` |
| Realm tab: "Divided", and the civil war first in the wars strip | `RealmVM.ComposeStanding`, `ComposeWars` |
| The rising kept off the Diplomacy tab | `KingdomDiplomacyVMMixin`, now hooked on `RefreshDiplomacyList` |

**Checked in the IL before writing, v1.4.8:** vanilla builds the Diplomacy tab's war list from
`_playerKingdom.FactionsAtWarWith`, keeping any entry whose two sides are kingdoms, so the rising
*was* listed there as an enemy. `RefreshValues` does not rebuild that list, which is why the mixin
moved to `RefreshDiplomacyList`.

**Test levers:**
- `diplomacy.civil_war_prices <kingdom>` prints every house's quote, line by line, and whether
  the other leader would pay it.
- `diplomacy.test_change_side <clan> [| unpaid]` moves a house through `Execute`.
- `diplomacy.test_concede <kingdom> | crown|rising` concedes for a side.
- `diplomacy.test_player_side <kingdom> | crown|rising|ruler` puts the player's house where the
  Court tab can be seen from each place a player can stand.
- `diplomacy.test_court_select <clan>` selects a row in civil-war mode as well.

### The first live run, 2026-09-24, `di_civilwar_test`, 0 errors

Four sessions, after the fixes listed below. The player's house was put on each side with
`test_player_side`.

| Check | Result |
|---|---|
| Court tab, civil-war mode, 1920x1080 | Renders as the mockup: both cards with the 40 mark, the three endings, the court split by side with prices, the price column line by line |
| The rising off the Diplomacy tab | "At War (1)": Western Empire only, the count corrected |
| Realm tab | "Divided", the civil war first with "Open the court", which opens the Court tab; no "war score" caption on it |
| **The player buys a house (as ruler)** | Paid 16,900 for fen Caernacht; its clan, head, party and castle all answer to Battania afterwards (`test_map_faction`) |
| **The player's house goes over (as a rebel)** | Received 33,700 from Muinser; relation with the claimant's house -20; the house marked "changed sides" |
| **The AI buys** | `ai_week 1`: Muinser, losing, bought fen Uvain for 37,100, the best strength per denar on offer |
| "Not ahead" holds | Aradwyr, winning, bought nobody; the player's "Go over" to him was disabled with that reason |
| **The player concedes (as ruler)** | Peace, the rising destroyed, Aradwyr on the throne, legitimacy 25 -> 10, the player kept as a pretender at 39% |
| **The AI concedes** | Left running at speed: Muinser conceded when the crown reached 75.8 with the rising at 34.8 |
| Save and reload | `SideChanges` survived: the bought house was refused a second change after a reload |

**What the purses showed.** Muinser held 452,986 and Aradwyr 144,075, against prices from 2,100
to 45,900. The half-purse limit almost never binds between two rulers of this size: the "not
ahead" rule is the one doing the work. Worth knowing before tuning either.

**Fixed during the run:**
- The Kingdom screen's own header - kingdom name, leader portrait, "Abdicate Leadership" - is
  built from the player's map faction when the screen opens, and nothing of ours rebuilds it.
  After the player conceded the throne it still offered abdication; after the player's house went
  over it still read "Aradwyr's Rising". Both acts now close the screen, as vanilla's Done does.
- The succession footer said "Okhon claims your throne" to Okhon. It now words itself for the
  ruler, a vassal or the claimant.
- A leader's house showed an empty price card; the card is hidden.
- A leader held by a foreign enemy read as "free".
- A purchase lost the selection; the bought house stays selected.

**Found, not fixed - for the lead:** a player among the rebels sees the **vanilla** parts of the
Kingdom screen (header, Clans, Fiefs, Policies, Armies) as the rising, because vanilla reads
the player's map faction. The Realm and Court tabs show the realm. This is 2.6's behaviour, not
2.6c's; whether the rising is the right thing for those tabs to show is a design question.

**Still not seen live** after the first run: an AI leader's offer to the player's house (the
inquiry), and the player as the claimant. Both were run on 2026-09-25, below.

### The second live run, 2026-09-25, 0 errors

| Check | Result |
|---|---|
| **An AI leader's offer, refused** (`di_civilwar_test`, the player's house put with the rising) | `ai_week 1`: Muinser, losing, picked Airit as the best strength per denar (1,375 for 33,700 against fen Uvain's 736 for 37,100) and put the inquiry. Refused: the next `ai_week 1` did not ask again and bought fen Uvain instead |
| **The same offer, accepted** (reloaded) | Player's purse 0 -> 33,700, Muinser's 452,986 -> 419,286, the house with the crown, relation with Aradwyr -20, and "Has already changed sides once in this war" on its quote |
| **The player as claimant, by the real path** (`di_civilwar_2_6c`) | The player, ruling, conceded: Aradwyr crowned, legitimacy 10, and the player kept as a pretender at 39% - the deposition rule of §3a Q1. `test_start_internal_war` then raised "Okhon's Rising" with 4 houses |
| **Buying a house as the claimant** | Paid 9,600 for fen Caernacht from the Court tab: purse 83.1k -> 73.5k, the rising 5 clans and 4 fiefs, the house marked "changed sides" and still selected |
| **Conceding as the claimant** | Two clicks on "Give up your claim": CrownWon, legitimacy 10 -> 22 (+12), the claim retired, the rising destroyed |

**Fixed during the run:** conceding as the claimant left the Kingdom screen open under a vanilla
header still reading "Okhon's Rising"; it now closes for either concession. The court line read
"you serve Aradwyr" to a house in arms against him, and the price headings named the player
where the rest of the panel says "you".

**Still not seen live:** the "Raise your banner?" prompt for a player claimant (it needs the
trigger met, which the 365-day cooldown after the first war kept out of reach; the lever starts
the war without it), the "choose a side" prompt, a claimant's 30 days of captivity, and a cadet
branch going on to start a war. The price lines still name the player ("How they feel about
Okhon"): they are built in `SideChange`, which the Intrigue layer keeps free of any "is this the
player" argument, and are printed by `civil_war_prices` too.

**What the first run had to check** (kept as it was written before the run):
- The Court tab renders in both modes at 1920x1080.
- The rising is gone from the Diplomacy tab.
- A purchase moves the house's parties and fiefs to the other side on the map.
- A concession by the player as ruler, with the Kingdom screen still open, changes the ruler
  cleanly.
- A save and reload keeps `SideChanges`.
- A week of `ai_week`, and how the prices compare with real purses.

### Not decided yet

- Whether the price should also carry what the house has **won in this war** (fiefs taken). It
  is left out because "fiefs they would bring" already counts what the house holds now.
- Influence or fiefs as a currency (lead: later).
