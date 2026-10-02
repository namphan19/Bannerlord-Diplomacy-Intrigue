using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Espionage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: a hero who runs a spy network cannot be made a governor.
    ///
    /// WHY: found live on 2026-09-25 (design 03 §10): vanilla made both AI handlers governors
    /// within days of their posting, and the daily check then took them off their networks. A
    /// handler is stationed in a foreign town; governing one of the clan's own is the one thing
    /// they cannot also be doing. Before the AI could run networks (3.6), its handlers had to stay.
    ///
    /// WHY A MODEL AND NOT A PATCH: <c>ClanPoliticsModel.CanHeroBeGovernor</c> is virtual on
    /// <c>DefaultClanPoliticsModel</c> in v1.4.8 (checked on the reference assemblies), and a
    /// model is ahead of Harmony in CLAUDE.md's order of preference. It also covers the player:
    /// the town screen reads the same answer, so a player's own handler is not offered as a
    /// governor either - the same rule, not an AI exemption.
    ///
    /// VANILLA DOES ASK THIS - settled by run 09 (§4, 2026-10-01, IL on v1.5.3 and two live
    /// cases). An earlier version of this header said it was not verified, and design 03 §10 said
    /// it was not asked; both were wrong. The governorships that were lost went through a hole in
    /// our own <c>SpyNetworks.CanHandle</c>: a hero already travelling to a governorship has a null
    /// <c>GovernorOf</c> until the teleport lands, so the mod posted them abroad and vanilla's queued
    /// teleport completed 3-5 real seconds later. Story 3.8 closed it by refusing a hero who
    /// <c>IsTraveling</c>.
    ///
    /// A PARTY IS NOT THIS CLASS'S BUSINESS: no model in v1.4.8 answers "may this hero lead a
    /// party", and story 3.8 answers it on <c>CampaignEvents.CanHeroLeadPartyEvent</c> instead
    /// (<c>Behaviors/HandlerPostBehavior</c>). <c>DiplomacyModel.GetHeroCommandingStrengthForClan</c>
    /// is still left alone: lowering it on a guess could move clan strength everywhere it is read.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 reference assemblies (signature and virtual); that
    /// vanilla calls it, on v1.5.3 by IL (run 09 §4).
    ///
    /// FAILURE MODE: falls through to vanilla on exception, so a fault here costs a handler, not a
    /// governorship.
    /// </summary>
    public sealed class ModClanPoliticsModel : DefaultClanPoliticsModel
    {
        public override bool CanHeroBeGovernor(Hero hero)
        {
            try
            {
                var state = CoreBehavior.State;
                if (state != null && SubModule.Healthy && Settings.Current.EnableEspionage
                    && SpyNetworks.HandledBy(state, hero) != null)
                    return false;
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Checking whether " + hero?.Name + " may govern failed; vanilla decides.", ex);
            }
            return base.CanHeroBeGovernor(hero);
        }
    }
}
