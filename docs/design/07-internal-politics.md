# Design 07 — Internal politics: war inside a kingdom

Status: **spike complete, design proposed**. Phase 2, sitting beside
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

### 1.3 The lever — VERIFIED as a mechanism, UNVERIFIED in a running game

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
