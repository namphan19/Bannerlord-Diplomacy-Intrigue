using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>How secure a crown looks from outside its court.</summary>
    public enum CrownStanding
    {
        Failing = 0,
        Questioned = 1,
        Secure = 2,
    }

    /// <summary>How much a house weighs in its own court, as an outsider hears it.</summary>
    public enum CourtWeight
    {
        NoWeight = 0,
        Middling = 1,
        GreatHouse = 2,
    }

    /// <summary>
    /// A rival court as your envoys can describe it: bands, never figures. Design 02 §9.1,
    /// the lead's call - your own court is shown exactly (the Court tab), a rival's only as
    /// bands, and the figures are what Phase 3's <c>ReadCourt</c> mission sells.
    ///
    /// The same rule as <see cref="Diplomacy.ExhaustionBands"/>, for the same reason: **every
    /// edge is a behavioural threshold read from the constant the AI uses**, so a band is a
    /// statement about what that court will do, never a decorative fifth, and it cannot drift
    /// away from the behaviour. Each edge below names the code that acts on it.
    ///
    /// This is the one place those edges are drawn. The Encyclopedia section and the Court
    /// tab's legitimacy colour both read it, and <c>diplomacy.court_bands</c> prints it.
    /// </summary>
    public static class CourtBands
    {
        // ----- the crown ------------------------------------------------------------

        /// <summary>
        /// Two edges, both live: below <see cref="IntrigueConstants.LegitimacyPretenderThreshold"/>
        /// a claimant's party may gather openly (<see cref="LegitimacyRegistry.IsWeak"/>, read by
        /// the Pretenders bloc); below <see cref="IntrigueConstants.LegitimacyNeutral"/> the crown
        /// costs every clan loyalty rather than adding to it (<see cref="LoyaltyModel.Explain"/>).
        ///
        /// Not <see cref="IntrigueConstants.LegitimacyStart"/>: 60 is where a crown begins, which
        /// changes nothing about how its court behaves. The Court tab coloured on 60 until 2.7's
        /// rival view made the two disagree.
        /// </summary>
        public static CrownStanding CrownOf(float legitimacy)
        {
            if (legitimacy < IntrigueConstants.LegitimacyPretenderThreshold) return CrownStanding.Failing;
            if (legitimacy < IntrigueConstants.LegitimacyNeutral) return CrownStanding.Questioned;
            return CrownStanding.Secure;
        }

        public static string Name(CrownStanding standing)
        {
            switch (standing)
            {
                case CrownStanding.Failing: return "Failing";
                case CrownStanding.Questioned: return "Questioned";
                default: return "Secure";
            }
        }

        /// <summary>What the band means for that court, in behavioural terms.</summary>
        public static string Meaning(CrownStanding standing, string his)
        {
            switch (standing)
            {
                case CrownStanding.Failing:
                    return "Low enough that a claimant's party may gather openly.";
                case CrownStanding.Questioned:
                    return "Doubted enough to cost " + his + " the loyalty of every house.";
                default:
                    return "Firm enough to steady " + his + " court.";
            }
        }

        // ----- a house's mood ---------------------------------------------------------

        /// <summary>
        /// A loyalty band in an envoy's words. The bands themselves are
        /// <see cref="LoyaltyModel.Band"/> - 70 votes with the ruler, 40 votes its bloc, 25
        /// votes against - so nothing new is decided here, only how it is said.
        /// </summary>
        public static string MoodName(LoyaltyBand band)
        {
            switch (band)
            {
                case LoyaltyBand.Reliable: return "Steadfast";
                case LoyaltyBand.Transactional: return "Self-interested";
                case LoyaltyBand.Disaffected: return "Sullen";
                default: return "Ready to break";
            }
        }

        // ----- a house's weight ---------------------------------------------------------

        /// <summary>
        /// Two edges, both live. A great house is at or above
        /// <see cref="IntrigueConstants.SuccessionClaimantInfluenceRatio"/> times its court's
        /// average (<see cref="SuccessionModel.IsMagnate"/>): strong enough to press a claim of
        /// its own, if it were ever disaffected enough to want one. A house at or below zero
        /// influence adds nothing to a bloc's power (<c>CourtBloc.Add</c>) or to a claimant's
        /// support at a succession, so it carries no weight at all.
        /// </summary>
        public static CourtWeight WeightOf(Clan clan, Kingdom kingdom)
        {
            if (clan == null || clan.Influence <= 0f) return CourtWeight.NoWeight;
            if (SuccessionModel.IsMagnate(clan, kingdom)) return CourtWeight.GreatHouse;
            return CourtWeight.Middling;
        }

        public static string Name(CourtWeight weight)
        {
            switch (weight)
            {
                case CourtWeight.GreatHouse: return "Among the great houses of the realm";
                case CourtWeight.Middling: return "Of middling account";
                default: return "Carries no weight at court";
            }
        }
    }
}
