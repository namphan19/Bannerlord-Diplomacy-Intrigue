using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: stops vanilla from making peace between kingdoms. Peace becomes ours, the way
    /// war already is.
    ///
    /// WHY: run 02 measured 86.8% of wars ending outside our peace table, median length six
    /// days, 46 of them dying the same day they began. The culprit is the barter path -
    /// <c>DiplomaticBartersBehavior.DailyTickClan</c> offers a <c>PeaceBarterable</c> every
    /// day, valued through <see cref="GetScoreOfDeclaringPeaceForClan"/> - plus the decision
    /// path through <c>IsPeaceSuitable</c>. Both are reached from here.
    ///
    /// WHY A MODEL AND NOT A PATCH: these three methods are virtual on
    /// <c>DiplomacyModel</c>, and CLAUDE.md's order of preference puts a game model ahead of
    /// Harmony. It also composes: every vanilla consumer of these numbers - the barter, the
    /// kingdom decision, the support calculation, the player-facing peace offer - sees the
    /// same answer without us having to find each one.
    ///
    /// DELIBERATELY NOT TOUCHED: <see cref="DefaultDiplomacyModel.GetScoreOfDeclaringWar"/>.
    /// War initiation is already held by two patches, and a second mechanism saying the same
    /// thing would be one more place for the two to disagree.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem.ComponentInterfaces
    /// .DiplomacyModel; base implementation DefaultDiplomacyModel).
    ///
    /// FAILURE MODE: every override falls through to base on exception, so a fault here
    /// restores vanilla diplomacy rather than freezing every war in place.
    /// </summary>
    public sealed class ModDiplomacyModel : DefaultDiplomacyModel
    {
        /// <summary>
        /// Large enough that no vanilla weighting can climb back over it, and finite so that
        /// arithmetic done on it downstream stays sane. A float sentinel like
        /// <c>MinValue</c> would overflow the moment vanilla added a bonus to it.
        /// </summary>
        private const float Forbidden = -100000f;

        public override bool IsPeaceSuitable(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
        {
            try
            {
                // An internal war (Phase 2.6) ends by its own rules, never by a vanilla peace:
                // the kingdom's at-war list holds the rebel banner like any other enemy, so
                // vanilla's decision and barter paths would otherwise offer it one.
                if (Intrigue.InternalWars.IsInternalWarPair(factionDeclaresPeace, factionDeclaredPeace))
                    return false;

                if (VanillaDiplomacy.Active
                    && VanillaDiplomacy.BothKingdoms(factionDeclaresPeace, factionDeclaredPeace))
                {
                    VanillaDiplomacy.NotePeaceRefused();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "IsPeaceSuitable failed; falling back to vanilla.", ex);
            }

            return base.IsPeaceSuitable(factionDeclaresPeace, factionDeclaredPeace);
        }

        public override float GetScoreOfDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
        {
            try
            {
                if (Intrigue.InternalWars.IsInternalWarPair(factionDeclaresPeace, factionDeclaredPeace))
                    return Forbidden;

                if (VanillaDiplomacy.Active
                    && VanillaDiplomacy.BothKingdoms(factionDeclaresPeace, factionDeclaredPeace))
                    return Forbidden;
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfDeclaringPeace failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfDeclaringPeace(factionDeclaresPeace, factionDeclaredPeace);
        }

        /// <summary>
        /// The one that actually stops the same-day peaces: <c>PeaceBarterable</c> values a
        /// peace offer through this, per clan.
        /// </summary>
        public override float GetScoreOfDeclaringPeaceForClan(IFaction factionDeclaresPeace,
            IFaction factionDeclaredPeace, Clan evaluatingClan, out TextObject reason, bool includeReason)
        {
            try
            {
                if (Intrigue.InternalWars.IsInternalWarPair(factionDeclaresPeace, factionDeclaredPeace))
                {
                    reason = includeReason ? new TextObject("A civil war is not ended by a barter.") : null;
                    return Forbidden;
                }

                if (VanillaDiplomacy.Active
                    && VanillaDiplomacy.BothKingdoms(factionDeclaresPeace, factionDeclaredPeace))
                {
                    reason = includeReason
                        ? new TextObject("Peace is negotiated at the diplomacy table.")
                        : null;
                    return Forbidden;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfDeclaringPeaceForClan failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfDeclaringPeaceForClan(factionDeclaresPeace, factionDeclaredPeace,
                evaluatingClan, out reason, includeReason);
        }
    }
}
