using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: gates vanilla's kingdom decisions on war, peace and alliance.
    ///
    /// - **War**: refused when one of our treaties forbids it. This applies to everyone, the
    ///   player included, and it is the good half of this model: <c>KingdomDiplomacyVM
    ///   .GetIsProposingWarEnabledWithReason</c> reads it, so the vanilla button now greys out
    ///   **and says why**. The Harmony postfix could only answer yes or no.
    /// - **Peace**: refused between kingdoms outright. Peace is negotiated at our table.
    /// - **Alliance**: refused between kingdoms. We have our own alliances with trust
    ///   thresholds and refusable calls to arms; vanilla's would be a second, invisible system
    ///   with different rules.
    ///
    /// WHY THE WAR PATCH STILL EXISTS: this model receives only the two kingdoms. It has no
    /// proposer argument, so it cannot express "the AI may not start this war but the player
    /// may" - and that distinction is the entire basis of the war takeover. The patch keeps
    /// the proposer-aware rule; this model keeps the universal one.
    ///
    /// LEFT ALONE ON PURPOSE: policy votes, annexation, clan expulsion and king selection. A
    /// kingdom's internal politics is Phase 2's subject, and Phase 2 will extend those rather
    /// than replace them.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (KingdomDecisionPermissionModel; consumers
    /// DeclareWarDecision.IsAllowed, MakePeaceKingdomDecision.IsAllowed,
    /// StartAllianceDecision.IsAllowed, KingdomDiplomacyVM).
    ///
    /// FAILURE MODE: falls through to base on exception - vanilla rules rather than no rules.
    /// </summary>
    public sealed class ModKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
    {
        public override bool IsWarDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2,
            out TextObject reason)
        {
            try
            {
                if (VanillaDiplomacy.Active && kingdom1 != null && kingdom2 != null)
                {
                    var state = CoreBehavior.State;
                    var block = TreatyEnforcement.WhyWarBlocked(state, kingdom1, kingdom2);
                    if (block != TreatyEnforcement.Block.None)
                    {
                        reason = new TextObject(TreatyEnforcement.Explain(state, kingdom1, kingdom2, block));
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "IsWarDecisionAllowedBetweenKingdoms failed; falling back to vanilla.", ex);
            }

            return base.IsWarDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
        }

        public override bool IsPeaceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2,
            out TextObject reason)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(kingdom1, kingdom2))
                {
                    VanillaDiplomacy.NotePeaceRefused();
                    reason = new TextObject("Peace is negotiated at the diplomacy table (Ctrl+D), "
                                            + "where terms can be demanded or offered.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "IsPeaceDecisionAllowedBetweenKingdoms failed; falling back to vanilla.", ex);
            }

            return base.IsPeaceDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
        }

        public override bool IsStartAllianceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2,
            out TextObject reason)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(kingdom1, kingdom2))
                {
                    VanillaDiplomacy.NoteAllianceRefused();
                    reason = new TextObject("Alliances are signed at the diplomacy table (Ctrl+D).");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "IsStartAllianceDecisionAllowedBetweenKingdoms failed; "
                                      + "falling back to vanilla.", ex);
            }

            return base.IsStartAllianceDecisionAllowedBetweenKingdoms(kingdom1, kingdom2, out reason);
        }
    }
}
