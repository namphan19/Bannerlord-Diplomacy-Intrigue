using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: while a hero's clan is a rebel in an internal war, <c>Hero.MapFaction</c> returns
    /// the war's rising - the same answer <see cref="Clan_MapFaction_Patch"/> gives for the
    /// clan itself.
    ///
    /// ALSO LOAD-BEARING FOR THE RISING'S SURVIVAL: vanilla's
    /// `ClanVariablesCampaignBehavior.OnSessionLaunched` destroys any kingdom whose
    /// `Leader.MapFaction` is not itself. The rising's leader is the claimant, whose clan is in
    /// the parent realm; only this postfix makes the check pass, so the index it reads is
    /// rebuilt in `CoreBehavior.SyncData`, before session launch. The partner of that patch; neither is complete without the other.
    ///
    /// WHY A SECOND PATCH: design 07 §1.3 expected one getter to move the whole engine. Reading
    /// the IL of every map-faction getter before writing the first one showed it does not:
    ///
    /// ```
    /// Hero::get_MapFaction()
    ///   ldarg.0 ; call Hero::get_Clan ; brfalse ...
    ///   ldarg.0 ; call Hero::get_Clan ; callvirt Clan::get_Kingdom ; dup ; brtrue ret
    ///   pop ; ldarg.0 ; call Hero::get_Clan ; ret
    /// ```
    ///
    /// It inlines `Clan.Kingdom ?? Clan` rather than calling `Clan.MapFaction`, so a rebel
    /// lord's *party* would be hostile to the crown while the lord in person - in a
    /// conversation, as a prisoner, in any check written against the hero - would still read
    /// as the crown's own. Two answers to "whose side is this lord on" is the absurdity the
    /// feature cannot survive. The evidence against a model or an event is the same as for the
    /// clan getter, recorded in that file's header and design 07 §1.
    ///
    /// Heroes without a clan (notables, wanderers) fall through to their home settlement or
    /// party, both of which already reach the clan getter - verified by the same IL.
    ///
    /// COST AND RE-ENTRANCY: as for the clan getter - one static bool when no internal war is
    /// running. `Hero.Clan` is `CompanionOf ?? _clan` and `Clan.Kingdom` a field read (both by
    /// IL); neither asks for a map faction.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.Hero::get_MapFaction).
    ///
    /// FAILURE MODE: on any exception the vanilla result stands.
    /// </summary>
    [HarmonyPatch(typeof(Hero), nameof(Hero.MapFaction), MethodType.Getter)]
    public static class Hero_MapFaction_Patch
    {
        private static void Postfix(Hero __instance, ref IFaction __result)
        {
            if (!InternalWars.Any) return;

            try
            {
                var clan = __instance?.Clan;
                if (clan != null && InternalWars.TryFaction(clan, out var faction)) __result = faction;
            }
            catch (Exception ex)
            {
                Log.Error("InternalWar", "Hero map faction redirect failed; vanilla stands.", ex);
            }
        }
    }
}
