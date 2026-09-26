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
    /// WHAT: a backstop on DeclareWarAction.ApplyByKingdomDecision, the path our own evaluation
    /// uses and the one vanilla's AI used. Refuses, in this order:
    ///   1. a war a treaty forbids, by the same answer as
    ///      <see cref="DeclareWarAction_ApplyByDefault_Patch"/>;
    ///   2. any war arriving unsanctioned. War initiation belongs to this mod, so a war that
    ///      reaches here without <see cref="TreatyEnforcement.DeclaringSanctionedWar"/> is either
    ///      a vanilla route <see cref="DeclareWarDecision_IsAllowed_Patch"/> did not catch or a
    ///      third-party mod - either way it is refused and logged loudly rather than silently
    ///      allowed. A decision the player proposed and won is the player's war and goes
    ///      through.
    /// Why only two of DeclareWarAction's eight entry points are patched is in the header of
    /// <see cref="DeclareWarAction_ApplyByDefault_Patch"/>.
    ///
    /// WHY A PATCH: DeclareWarAction is a static action class with no event and no model
    /// behind it, so there is no supported way to refuse a war.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.Actions.
    /// DeclareWarAction::ApplyByKingdomDecision(IFaction faction1, IFaction faction2), one
    /// overload).
    ///
    /// FAILURE MODE: on any exception the war proceeds, i.e. vanilla behaviour. The two
    /// checks fail separately: a fault in the treaty check still leaves the sanction check to
    /// run.
    /// </summary>
    [HarmonyPatch(typeof(DeclareWarAction), nameof(DeclareWarAction.ApplyByKingdomDecision))]
    public static class DeclareWarAction_ApplyByKingdomDecision_Patch
    {
        /// <summary>Returns false to skip the original method, i.e. to refuse the war.</summary>
        private static bool Prefix(IFaction faction1, IFaction faction2)
        {
            if (TreatyRefuses(faction1, faction2)) return false;

            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy) return true;
                if (TreatyEnforcement.DeclaringSanctionedWar) return true;
                if (CoreBehavior.State == null) return true;

                // A player-proposed decision reached a vote and passed; that is the player's
                // war and it goes through.
                if (faction1 is Kingdom proposer && proposer.Leader == Hero.MainHero) return true;

                Log.Info("Enforce", "Refused an unsanctioned kingdom-decision war: "
                                    + faction1?.Name + " -> " + faction2?.Name
                                    + ". War initiation belongs to the mod's evaluation.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("Enforce", "Sanction check failed; allowing the war.", ex);
                return true;
            }
        }

        private static bool TreatyRefuses(IFaction faction1, IFaction faction2)
        {
            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableDiplomacy) return false;

                var state = CoreBehavior.State;
                if (state == null) return false;

                var refusal = TreatyEnforcement.WhyWarActionRefused(state, faction1, faction2);
                if (refusal == null) return false;

                Log.Info("Enforce", "Refused war via kingdom decision: " + faction1.Name + " -> " + faction2.Name
                                    + " because " + refusal
                                    + ". To go to war anyway, the treaty has to be broken first.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Enforce", "War veto check failed; allowing the war.", ex);
                return false;
            }
        }
    }
}
