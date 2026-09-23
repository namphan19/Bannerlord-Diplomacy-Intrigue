using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: while a clan is a rebel in an internal war, <c>Clan.MapFaction</c> returns the
    /// war's rising - a kingdom that exists only on the map - instead of the clan's realm.
    /// <c>Clan.Kingdom</c> is untouched: the clan stays in the realm, keeps its seat and its
    /// votes, while the map treats the rebellion as a faction of its own at war with the
    /// crown. Design 07, option C; Phase 2.6.
    ///
    /// WHY A KINGDOM AND NOT THE CLAIMANT'S CLAN: the first build returned the clan, and the
    /// game crashed two seconds into the first war - vanilla casts a map faction to `Kingdom`
    /// without checking at ~25 places (design 07 §3c). The rising is a real kingdom so that
    /// every one of those casts holds.
    ///
    /// WHY A PATCH - the evidence CLAUDE.md §3 requires, gathered with tools/CallSites before
    /// this file was written (design 07 §1):
    ///
    ///   - A clan-to-clan war can be *written* (`Clan` is an `IFaction`, and the stance table
    ///     accepts one) and is never *read*. Near the 120 call sites of
    ///     `FactionManager.IsAtWarAgainstFaction`, `get_MapFaction` appears 203 times against
    ///     2 for `get_Kingdom`: the engine asks the map faction, essentially without exception,
    ///     and two clans of one kingdom are the same object by then.
    ///   - The supported hook, `DiplomacyModel.GetShallowDiplomaticStance`, is consulted inside
    ///     `IsAtWarAgainstFaction` - after the caller has already reduced both clans to their
    ///     kingdom. It receives `(kingdom, kingdom)` and cannot recover which clan asked. This
    ///     mod already overrides `DiplomacyModel` (Phase 1.11), so the model route was tried
    ///     first and is closed, not overlooked.
    ///   - Every other map faction derives from this getter, verified by IL: `Town`,
    ///     `SettlementComponent` and `MobileParty` all call `Clan.get_MapFaction`, and
    ///     `Village` reads its bound town's. One getter moves parties, towns and villages
    ///     together. The one that does **not** is `Hero.MapFaction`, which reads
    ///     `Clan.Kingdom` directly - see <see cref="Hero_MapFaction_Patch"/>.
    ///
    /// COST: 2,216 call sites, some in per-frame party AI. The postfix reads one static bool
    /// and returns when no internal war is running anywhere, which is the common case; with
    /// one running, a reference-keyed dictionary lookup. No allocation on either path.
    ///
    /// NOT RE-ENTRANT BY CONSTRUCTION: <see cref="InternalWars.TryFaction"/> reads only its own
    /// index and <c>Clan.Kingdom</c>, a field read. Nothing it touches asks for a map faction.
    ///
    /// INLINING, settled live on 2026-09-23: a getter this small could have been inlined into
    /// its callers, out of a patch's reach. It was not - rebel and crown parties fought two
    /// battles within seconds of the first war starting, which only happens when the engine's
    /// own `MapEventSide.MapFaction` (through `MobileParty.MapFaction`) sees this postfix.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.Clan::get_MapFaction:
    /// `Kingdom ?? this`).
    ///
    /// FAILURE MODE: on any exception the vanilla result stands - the clan answers with its
    /// kingdom, i.e. the internal war stops showing on the map rather than the map breaking.
    /// </summary>
    [HarmonyPatch(typeof(Clan), nameof(Clan.MapFaction), MethodType.Getter)]
    public static class Clan_MapFaction_Patch
    {
        private static void Postfix(Clan __instance, ref IFaction __result)
        {
            if (!InternalWars.Any) return;

            try
            {
                if (InternalWars.TryFaction(__instance, out var faction)) __result = faction;
            }
            catch (Exception ex)
            {
                Log.Error("InternalWar", "Clan map faction redirect failed; vanilla stands.", ex);
            }
        }
    }
}
