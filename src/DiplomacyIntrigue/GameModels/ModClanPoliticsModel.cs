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
    /// **NOT VERIFIED** that vanilla's AI governor assignment asks this method. The name says it
    /// should; the reference assemblies carry no method bodies to confirm it. If a handler is
    /// still made a governor in game, the daily check in <c>SpyNetworks</c> still releases them,
    /// and the next step is to find what vanilla calls instead - with evidence, before any patch.
    ///
    /// DELIBERATELY NOT TOUCHED: raising a party from a handler. No model in v1.4.8 answers "may
    /// this hero lead a party"; <c>DiplomacyModel.GetHeroCommandingStrengthForClan</c> is the
    /// nearest, but what vanilla uses it for is unknown here, and lowering it on a guess could move
    /// clan strength everywhere it is read.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 reference assemblies (signature and virtual only).
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
