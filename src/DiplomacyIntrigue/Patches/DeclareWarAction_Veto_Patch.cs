using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: a backstop that refuses a war a treaty forbids, for the two entry points the
    /// ordinary AI uses. <see cref="DeclareWarDecision_IsAllowed_Patch"/> is the primary
    /// gate; this catches anything that reaches the action without going through a vote.
    ///
    /// WHY ONLY TWO OF THE EIGHT: the other ApplyBy* overloads represent engine situations
    /// where refusing would leave the campaign inconsistent - a rebellion that cannot
    /// declare war, a newly created kingdom that stays friendly with everyone, a player
    /// hostile act with no consequence. Those are deliberately left alone, which is also
    /// why a *player* attack is never silently swallowed.
    ///
    /// WHY A PATCH: DeclareWarAction is a static action class with no event and no model
    /// behind it, so there is no supported way to refuse a war.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.Actions.
    /// DeclareWarAction::ApplyByKingdomDecision and ::ApplyByDefault).
    ///
    /// FAILURE MODE: on any exception the war proceeds, i.e. vanilla behaviour.
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarAction))]
    public static class DeclareWarAction_Veto_Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(DeclareWarAction.ApplyByKingdomDecision))]
        public static bool ApplyByKingdomDecisionPrefix(IFaction faction1, IFaction faction2)
            => Allow(faction1, faction2, "kingdom decision");

        [HarmonyPrefix]
        [HarmonyPatch(nameof(DeclareWarAction.ApplyByDefault))]
        public static bool ApplyByDefaultPrefix(IFaction faction1, IFaction faction2)
            => Allow(faction1, faction2, "default");

        /// <summary>Returns false to skip the original method, i.e. to refuse the war.</summary>
        private static bool Allow(IFaction faction1, IFaction faction2, string via)
        {
            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy) return true;

                var state = CoreBehavior.State;
                if (state == null) return true;

                var aggressor = faction1 as Kingdom;
                var defender = faction2 as Kingdom;
                if (aggressor == null || defender == null) return true;

                var block = TreatyEnforcement.WhyWarBlocked(state, aggressor, defender);
                if (block == TreatyEnforcement.Block.None) return true;

                Log.Info("Enforce", "Refused war via " + via + ": " + aggressor.Name + " -> " + defender.Name
                                    + " because " + TreatyEnforcement.Explain(state, aggressor, defender, block)
                                    + ". To go to war anyway, the treaty has to be broken first.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("Enforce", "War veto check failed; allowing the war.", ex);
                return true;
            }
        }
    }
}
