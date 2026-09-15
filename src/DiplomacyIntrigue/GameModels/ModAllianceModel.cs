using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: switches off vanilla's own alliances and its own call-to-war agreements.
    ///
    /// WHY THIS WAS A SURPRISE: v1.4.8 has more inter-kingdom diplomacy than the war/peace
    /// pair. <c>StartAllianceDecision</c>, <c>ProposeCallToWarAgreementDecision</c> and
    /// <c>AcceptCallToWarAgreementDecision</c> are vanilla's - and the
    /// <c>CausedByCallToWarAgreement</c> detail that shows up in our own war log is this
    /// system, not ours. Leaving it running means two alliance systems on the same map with
    /// different rules, different durations and different refusal costs, only one of which
    /// the player can see in the diplomacy screen.
    ///
    /// WHAT REPLACES IT: our <c>Alliance</c> treaty (trust-gated, refusable calls to arms at
    /// a trust cost rather than a breach) and <c>CallToArms</c>.
    ///
    /// NOT TOUCHED: <c>GetAllianceFactorForDeclaringWar</c> and
    /// <c>…ForDeclaringPeace</c>. They are multipliers applied to vanilla scores that are
    /// already forbidden elsewhere; with no vanilla alliances left they have nothing to
    /// multiply, and overriding them would be a second lever doing the first one's job.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (AllianceModel; base DefaultAllianceModel).
    ///
    /// FAILURE MODE: falls through to base on exception.
    /// </summary>
    public sealed class ModAllianceModel : DefaultAllianceModel
    {
        private const float Forbidden = -100000f;

        /// <summary>
        /// Zero is the cheapest possible block: every vanilla path that offers an alliance
        /// checks the cap before anything else, so this alone stops the proposals being
        /// generated rather than refusing them one at a time.
        /// </summary>
        public override int MaxNumberOfAlliances => VanillaDiplomacy.Active ? 0 : base.MaxNumberOfAlliances;

        public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction,
            out TextObject reason, bool includeReason)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(kingdom, targetKingdom))
                {
                    VanillaDiplomacy.NoteAllianceRefused();
                    reason = includeReason
                        ? new TextObject("Alliances are signed at the diplomacy table (Ctrl+D).")
                        : null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CanMakeAlliance failed; falling back to vanilla.", ex);
            }

            return base.CanMakeAlliance(kingdom, targetKingdom, evaluatingFaction, out reason, includeReason);
        }

        public override ExplainedNumber GetScoreOfStartingAlliance(Kingdom kingdomDeclaresAlliance,
            Kingdom kingdomDeclaredAlliance, out TextObject explanation, bool includeDescription)
        {
            try
            {
                if (VanillaDiplomacy.Active
                    && VanillaDiplomacy.BothKingdoms(kingdomDeclaresAlliance, kingdomDeclaredAlliance))
                {
                    explanation = includeDescription
                        ? new TextObject("Alliances are signed at the diplomacy table (Ctrl+D).")
                        : null;
                    return new ExplainedNumber(Forbidden, includeDescription, null);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfStartingAlliance failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfStartingAlliance(kingdomDeclaresAlliance, kingdomDeclaredAlliance,
                out explanation, includeDescription);
        }

        public override float GetScoreOfCallingToWar(Kingdom callingKingdom, Kingdom calledKingdom,
            Kingdom kingdomToCallToWarAgainst, IFaction evaluatingFaction, out TextObject reason)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(callingKingdom, calledKingdom))
                {
                    VanillaDiplomacy.NoteCallToWarRefused();
                    reason = new TextObject("Obligations to join a war come from treaties, not agreements.");
                    return Forbidden;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfCallingToWar failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfCallingToWar(callingKingdom, calledKingdom, kingdomToCallToWarAgainst,
                evaluatingFaction, out reason);
        }

        public override float GetScoreOfJoiningWar(Kingdom offeringKingdom, Kingdom kingdomToOfferToJoinWarWith,
            Kingdom kingdomToOfferToJoinWarAgainst, IFaction evaluatingFaction, out TextObject reason)
        {
            try
            {
                if (VanillaDiplomacy.Active
                    && VanillaDiplomacy.BothKingdoms(offeringKingdom, kingdomToOfferToJoinWarWith))
                {
                    VanillaDiplomacy.NoteCallToWarRefused();
                    reason = new TextObject("Obligations to join a war come from treaties, not agreements.");
                    return Forbidden;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfJoiningWar failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfJoiningWar(offeringKingdom, kingdomToOfferToJoinWarWith,
                kingdomToOfferToJoinWarAgainst, evaluatingFaction, out reason);
        }
    }
}
