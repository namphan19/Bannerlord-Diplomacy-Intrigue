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
    /// WHAT: a backstop on DeclareWarAction.ApplyByDefault, one of the two war entry points the
    /// ordinary AI uses. Refuses a war a treaty forbids, or one declared by a kingdom that has
    /// handed its foreign policy to a patron (<see cref="TreatyEnforcement.WhyWarBlocked"/>).
    /// <see cref="DeclareWarDecision_IsAllowed_Patch"/> is the primary gate; this catches
    /// anything that reaches the action without going through a vote. Its sibling on the
    /// other entry point is <see cref="DeclareWarAction_ApplyByKingdomDecision_Patch"/>.
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
    /// DeclareWarAction::ApplyByDefault(IFaction faction1, IFaction faction2), one overload).
    ///
    /// FAILURE MODE: on any exception the war proceeds, i.e. vanilla behaviour.
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarAction), nameof(DeclareWarAction.ApplyByDefault))]
    public static class DeclareWarAction_ApplyByDefault_Patch
    {
        /// <summary>Returns false to skip the original method, i.e. to refuse the war.</summary>
        private static bool Prefix(IFaction faction1, IFaction faction2)
        {
            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy) return true;

                var state = CoreBehavior.State;
                if (state == null) return true;

                var refusal = TreatyEnforcement.WhyWarActionRefused(state, faction1, faction2);
                if (refusal == null) return true;

                Log.Info("Enforce", "Refused war via default: " + faction1.Name + " -> " + faction2.Name
                                    + " because " + refusal
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
