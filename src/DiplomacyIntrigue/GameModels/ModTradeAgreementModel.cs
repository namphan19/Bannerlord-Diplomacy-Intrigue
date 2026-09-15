using System;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace DiplomacyIntrigue.GameModels
{
    /// <summary>
    /// WHAT: switches off vanilla's trade agreements between kingdoms.
    ///
    /// WHY: they are inter-kingdom diplomacy, so by the lead's directive they are ours - and
    /// we have nothing to replace them with. The project's scope is deliberately three
    /// political pillars and **not** the economy, so a stub agreement of our own would be a
    /// treaty that did nothing.
    ///
    /// That is a real subtraction from the player's options and it is recorded as such in
    /// docs/design/05-vanilla-override.md: a diplomacy screen offering an agreement whose
    /// effects nothing in this mod understands is worse than one that does not offer it.
    /// If the economy ever comes into scope, this is the first thing to revisit.
    ///
    /// NOT TOUCHED: <c>GetProfitPerCaravanVisit</c>. Existing agreements signed before the mod
    /// was installed keep paying out; we stop new ones rather than confiscating old ones.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TradeAgreementModel; base
    /// DefaultTradeAgreementModel).
    ///
    /// FAILURE MODE: falls through to base on exception.
    /// </summary>
    public sealed class ModTradeAgreementModel : DefaultTradeAgreementModel
    {
        private const float Forbidden = -100000f;

        public override int GetMaximumTradeAgreementCount(Kingdom kingdom)
            => VanillaDiplomacy.Active ? 0 : base.GetMaximumTradeAgreementCount(kingdom);

        public override bool CanMakeTradeAgreement(Kingdom kingdom, Kingdom other,
            bool checkOtherSideTradeSupport, out TextObject reason, bool includeReason)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(kingdom, other))
                {
                    VanillaDiplomacy.NoteTradeAgreementRefused();
                    reason = includeReason
                        ? new TextObject("Trade agreements are not part of this mod's diplomacy.")
                        : null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "CanMakeTradeAgreement failed; falling back to vanilla.", ex);
            }

            return base.CanMakeTradeAgreement(kingdom, other, checkOtherSideTradeSupport,
                out reason, includeReason);
        }

        public override float GetScoreOfStartingTradeAgreement(Kingdom kingdom, Kingdom targetKingdom,
            Clan clan, out TextObject explanation, bool includeExplanation)
        {
            try
            {
                if (VanillaDiplomacy.Active && VanillaDiplomacy.BothKingdoms(kingdom, targetKingdom))
                {
                    explanation = includeExplanation
                        ? new TextObject("Trade agreements are not part of this mod's diplomacy.")
                        : null;
                    return Forbidden;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Override", "GetScoreOfStartingTradeAgreement failed; falling back to vanilla.", ex);
            }

            return base.GetScoreOfStartingTradeAgreement(kingdom, targetKingdom, clan,
                out explanation, includeExplanation);
        }
    }
}
