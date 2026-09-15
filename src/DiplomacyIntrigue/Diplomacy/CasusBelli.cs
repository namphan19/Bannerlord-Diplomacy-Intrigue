using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Maps and rates war justifications. Legitimacy is the single number the rest of the
    /// mod reads: it scales influence cost, the relation hit with third parties, and how
    /// much internal opposition a war generates.
    /// </summary>
    public static class CasusBelli
    {
        /// <summary>
        /// Translates the reason the base game gives for a war into our taxonomy.
        /// Wars started through Diplomacy & Intrigue carry their justification explicitly and
        /// never take this path.
        /// </summary>
        public static CasusBelliType FromDeclareWarDetail(DeclareWarAction.DeclareWarDetail detail)
        {
            switch (detail)
            {
                // Honouring a pact is the most defensible reason there is.
                case DeclareWarAction.DeclareWarDetail.CausedByCallToWarAgreement:
                    return CasusBelliType.DefendAlly;

                // Someone pressed a dynastic claim, or a clan rose against its liege.
                case DeclareWarAction.DeclareWarDetail.CausedByClaimOnThrone:
                case DeclareWarAction.DeclareWarDetail.CausedByRebellion:
                    return CasusBelliType.SupportClaimant;

                // Retaliation for what the other side did to us.
                case DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility:
                case DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange:
                    return CasusBelliType.AvengeRaid;

                // A court voted for expansion. Legal, and still an act of conquest.
                case DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision:
                    return CasusBelliType.Conquest;

                // A new realm declaring itself has no case to make, and neither does
                // an unexplained war.
                case DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation:
                case DeclareWarAction.DeclareWarDetail.Default:
                default:
                    return CasusBelliType.None;
            }
        }

        /// <summary>
        /// The justification a war is actually fought on: the best claim the aggressor
        /// holds, or the engine's own reason when it holds none.
        ///
        /// This is the single source of truth, and it exists because it was not. The war
        /// ledger and the trust ledger each resolved legitimacy their own way, so a kingdom
        /// that answered an ally's call - recorded as DefendAlly at 1.00 - was
        /// simultaneously punished by every other court as an unjust aggressor at 0.00.
        /// Anything that needs a war's legitimacy must come through here.
        /// </summary>
        public static CasusBelliType Resolve(Core.ModState state, Kingdom aggressor, Kingdom defender,
            DeclareWarAction.DeclareWarDetail detail)
        {
            var fromDetail = FromDeclareWarDetail(detail);

            // An engine reason that is already more defensible than any claim we hold wins:
            // honouring a pact is not made less honourable by also wanting their land.
            var claim = state == null ? null : ClaimRegistry.Best(state, aggressor, defender);
            if (claim == null) return fromDetail;

            return Legitimacy(claim.Type) >= Legitimacy(fromDetail) ? claim.Type : fromDetail;
        }

        /// <summary>Legitimacy of the war a kingdom would actually be fighting.</summary>
        public static float ResolvedLegitimacy(Core.ModState state, Kingdom aggressor, Kingdom defender,
            DeclareWarAction.DeclareWarDetail detail)
            => Legitimacy(Resolve(state, aggressor, defender, detail));

        /// <summary>
        /// Range 0..1. Zero means the war reads as naked aggression; one means it is
        /// broadly accepted as just.
        /// </summary>
        public static float Legitimacy(CasusBelliType type)
        {
            switch (type)
            {
                case CasusBelliType.DefendAlly: return 1.00f;
                case CasusBelliType.BrokenTreaty: return 0.95f;
                case CasusBelliType.EspionageExposed: return 0.85f;
                case CasusBelliType.AvengeRaid: return 0.75f;
                case CasusBelliType.ReclaimAncestralLand: return 0.70f;
                case CasusBelliType.SupportClaimant: return 0.55f;
                case CasusBelliType.TradeDispute: return 0.45f;
                case CasusBelliType.Conquest: return 0.20f;
                default: return 0.00f;
            }
        }
    }
}
