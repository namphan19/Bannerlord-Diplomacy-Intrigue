using DiplomacyIntrigue.Models;
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
                case DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility:
                    return CasusBelliType.AvengeRaid;
                case DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision:
                    return CasusBelliType.Conquest;
                default:
                    return CasusBelliType.None;
            }
        }

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
