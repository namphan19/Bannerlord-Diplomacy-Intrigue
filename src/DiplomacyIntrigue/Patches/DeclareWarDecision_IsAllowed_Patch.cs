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
    /// WHAT: takes war initiation away from the vanilla AI, and stops anyone - the player
    /// included - proposing a war a treaty forbids.
    ///
    /// WHY A PATCH: there is no event or game model that gates whether a kingdom decision
    /// may exist. The alternative - letting the vote happen and vetoing
    /// DeclareWarAction afterwards - leaves the court having voted for a war that then
    /// silently does not occur, which is worse for the player than never seeing it.
    ///
    /// WHY TAKE IT OVER: measured in balance run 01, our own evaluation declared 25 wars
    /// against vanilla's ~220. Everything the mod knows about a war - exhaustion,
    /// weariness, claims, trust, the influence a casus belli costs - had no say in whether
    /// wars happened at all; casus belli was a label applied afterwards rather than a gate.
    ///
    /// WHAT IS DELIBERATELY LEFT ALONE: a decision proposed by the **player's own clan**.
    /// Blocking that would take the Kingdom screen's declare-war option away from the
    /// player, which is not what this change is for. Player proposals are still refused
    /// when a treaty forbids the war - that rule applies to everyone.
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

                // A treaty forbidding the war stops anyone, player included.
                var block = TreatyEnforcement.WhyWarBlocked(state, proposer, target);
                if (block != TreatyEnforcement.Block.None)
                {
                    __result = false;
                    if (Settings.Current.VerboseLogging)
                        Log.Debug("Enforce", "Blocked war proposal " + proposer.Name + " -> " + target.Name
                                             + ": " + TreatyEnforcement.Explain(state, proposer, target, block));
                    return;
                }

                // Past this point the war is legal. Whether it should happen is now our
                // decision, not vanilla's - unless the player proposed it themselves.
                if (__instance.ProposerClan == Clan.PlayerClan) return;
                if (TreatyEnforcement.DeclaringSanctionedWar) return;

                __result = false;
                TreatyEnforcement.NoteVanillaProposalRefused();

                if (Settings.Current.VerboseLogging)
                    Log.Debug("Enforce", "Refused vanilla war proposal " + proposer.Name
                                         + " -> " + target.Name + "; initiation belongs to the mod.");
            }
            catch (Exception ex)
            {
                Log.Error("Enforce", "DeclareWarDecision.IsAllowed postfix failed; leaving the vanilla answer.", ex);
            }
        }
    }
}
