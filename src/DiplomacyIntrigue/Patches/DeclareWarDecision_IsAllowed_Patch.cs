using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: stops a kingdom from even proposing a war that one of its treaties forbids,
    /// and stops a vassal from proposing any war on its own account.
    ///
    /// WHY A PATCH: there is no event or game model that gates whether a kingdom decision
    /// may exist. The alternative - letting the vote happen and vetoing
    /// DeclareWarAction afterwards - leaves the court having voted for a war that then
    /// silently does not occur, which is worse for the player than never seeing it.
    ///
    /// WHY A POSTFIX: IsAllowed() is the game's own gate. Narrowing its answer composes
    /// with whatever else it checks, instead of replacing that reasoning.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem,
    /// TaleWorlds.CampaignSystem.Election.DeclareWarDecision::IsAllowed).
    ///
    /// FAILURE MODE: on any exception the original answer is left untouched, so the game
    /// falls back to vanilla behaviour rather than breaking kingdom decisions.
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarDecision), nameof(DeclareWarDecision.IsAllowed))]
    public static class DeclareWarDecision_IsAllowed_Patch
    {
        private static void Postfix(DeclareWarDecision __instance, ref bool __result)
        {
            // Nothing to narrow if the game already said no.
            if (!__result) return;

            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy) return;

                var state = CoreBehavior.State;
                if (state == null) return;

                var proposer = __instance.Kingdom;
                var target = __instance.FactionToDeclareWarOn as Kingdom;
                if (proposer == null || target == null) return;

                var block = TreatyEnforcement.WhyWarBlocked(state, proposer, target);
                if (block == TreatyEnforcement.Block.None) return;

                __result = false;

                if (Settings.Current.VerboseLogging)
                    Log.Debug("Enforce", "Blocked war proposal " + proposer.Name + " -> " + target.Name
                                         + ": " + TreatyEnforcement.Explain(state, proposer, target, block));
            }
            catch (Exception ex)
            {
                Log.Error("Enforce", "DeclareWarDecision.IsAllowed postfix failed; leaving the vanilla answer.", ex);
            }
        }
    }
}
