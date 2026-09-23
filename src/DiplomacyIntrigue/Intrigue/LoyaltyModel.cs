using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// How loyal a clan is to its ruler, 0-100. The number that turns the court's memory
    /// (<see cref="GrievanceRegistry"/>) into something that changes behaviour.
    ///
    /// **Derived, never saved.** It is a function of stored grievances plus live world state,
    /// so the save stays small and a balance change takes effect on campaigns already running
    /// rather than only on new ones. Same discipline as <c>Hegemony.IsHegemon</c>: one source
    /// of truth, computed where it is read.
    ///
    /// The formula is design 02 §2 term for term. Every coefficient is un-tuned and says so
    /// in <see cref="IntrigueConstants"/>.
    /// </summary>
    public static class LoyaltyModel
    {
        /// <summary>
        /// The clan's loyalty to its own ruler. 50 for a clan with no kingdom and for the
        /// ruling clan itself - a clan cannot be disloyal to itself, and a dispute *inside*
        /// the ruling clan is a different mechanism entirely (design 07).
        /// </summary>
        public static float Of(ModState state, Clan clan)
        {
            var explained = Explain(state, clan);
            return explained.Total;
        }

        public static LoyaltyBand BandOf(ModState state, Clan clan) => Band(Of(state, clan));

        public static LoyaltyBand Band(float loyalty)
        {
            if (loyalty >= IntrigueConstants.LoyaltyReliable) return LoyaltyBand.Reliable;
            if (loyalty >= IntrigueConstants.LoyaltyTransactional) return LoyaltyBand.Transactional;
            if (loyalty >= IntrigueConstants.LoyaltyDisaffected) return LoyaltyBand.Disaffected;
            return LoyaltyBand.DefectionRisk;
        }

        public static string Describe(LoyaltyBand band)
        {
            switch (band)
            {
                case LoyaltyBand.Reliable: return "reliable - votes with the ruler, answers the call";
                case LoyaltyBand.Transactional: return "transactional - votes its own interest";
                case LoyaltyBand.Disaffected: return "disaffected - votes against the ruler, volunteers nothing";
                default: return "defection risk";
            }
        }

        /// <summary>
        /// The same number, term by term, so the court UI and the diagnostics can explain
        /// themselves. Written after Phase 1 learned twice that a single opaque figure is
        /// impossible to debug and impossible to present - <c>diplomacy.war_value</c> exists
        /// for exactly this reason, and it was written after guessing wrong twice.
        /// </summary>
        public static LoyaltyBreakdown Explain(ModState state, Clan clan)
        {
            var result = new LoyaltyBreakdown { Base = IntrigueConstants.LoyaltyBase };

            var kingdom = clan?.Kingdom;
            var ruling = kingdom?.RulingClan;
            // A mercenary is paid, not sworn: it has no loyalty to the crown to measure.
            if (state == null || ruling == null || ruling == clan || !Court.IsMember(clan)) return result;

            result.Applies = true;

            // Relation is how the two leaders feel about each other; grievances are what the
            // clan has not forgotten. Both terms are present on purpose - design 02 §2 keeps
            // them separate because a ruler can be liked and still resented.
            var ruler = kingdom.Leader;
            if (ruler != null && clan.Leader != null)
                result.Relation = clan.Leader.GetRelation(ruler) * IntrigueConstants.LoyaltyRelationFactor;

            result.Grievances = -GrievanceRegistry.TotalAgainst(state, clan, ruling)
                                * IntrigueConstants.LoyaltyGrievanceFactor;

            result.Fiefs = FiefStanding.Satisfaction(clan) * IntrigueConstants.LoyaltyFiefFactor;

            result.WarExhaustion = -WarExhaustion.Worst(state, kingdom)
                                   * IntrigueConstants.LoyaltyWarExhaustionFactor;

            // Live as of 2.4. A crown above the midpoint of the scale steadies its court and
            // one below it drains loyalty everywhere at once - which is what makes legitimacy
            // worth defending rather than a number on a screen.
            var crownLegitimacy = LegitimacyRegistry.Of(state, kingdom);
            result.Legitimacy = (crownLegitimacy - IntrigueConstants.LegitimacyNeutral)
                                * IntrigueConstants.LoyaltyLegitimacyFactor;

            return result;
        }
    }

    /// <summary>
    /// Loyalty split into the terms that produced it. A struct-like carrier, not saved.
    /// </summary>
    public sealed class LoyaltyBreakdown
    {
        /// <summary>False for a clan with no kingdom, and for the ruling clan itself.</summary>
        public bool Applies;

        public float Base;
        public float Relation;
        public float Grievances;
        public float Fiefs;
        public float WarExhaustion;
        public float Legitimacy;

        public float Raw => Base + Relation + Grievances + Fiefs + WarExhaustion + Legitimacy;

        /// <summary>Clamped to 0-100. Bands read this.</summary>
        public float Total
        {
            get
            {
                var raw = Raw;
                return raw < 0f ? 0f : (raw > 100f ? 100f : raw);
            }
        }

        public override string ToString()
            => Total.ToString("0.0") + "  (base " + Base.ToString("0.0")
               + ", relation " + Relation.ToString("+0.0;-0.0;0.0")
               + ", grievances " + Grievances.ToString("+0.0;-0.0;0.0")
               + ", fiefs " + Fiefs.ToString("+0.0;-0.0;0.0")
               + ", war " + WarExhaustion.ToString("+0.0;-0.0;0.0")
               + ", legitimacy " + Legitimacy.ToString("+0.0;-0.0;0.0") + ")";
    }
}
