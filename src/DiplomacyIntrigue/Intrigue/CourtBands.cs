using DiplomacyIntrigue.Core;
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
    /// tab's legitimacy colour both read it, and <c>diplomacy.court_bands</c> prints it. Since
    /// R-1 (2026-09-27) the AI's war valuation reads a rival through it too
    /// (<see cref="SignsOf"/>), so the AI sees a rival court exactly as the player does.
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

        // ----- what a rival can see of a court's trouble -------------------------------

        /// <summary>
        /// The signs of a divided realm that anyone outside it can read. What a rival's war
        /// valuation weighs (design 02 §7.2, R-1), and nothing more.
        /// </summary>
        public struct CourtSigns
        {
            /// <summary>The crown band the Encyclopedia shows.</summary>
            public CrownStanding Crown;
            /// <summary>Somebody presses a claim to the throne - named on the Encyclopedia page.</summary>
            public bool PretenderStands;
            /// <summary>The realm is fighting its own rising - a war on the map, which nobody can hide.</summary>
            public bool AtWarWithItself;

            /// <summary>No sign of trouble at all.</summary>
            public bool Quiet => Crown == CrownStanding.Secure && !PretenderStands && !AtWarWithItself;
        }

        /// <summary>
        /// A court's trouble as an outsider sees it: bands and public facts, never a figure.
        ///
        /// Read by the AI weighing a war on this realm, and built from exactly what the player
        /// weighing the same realm is shown - the crown's band, the claimants the Encyclopedia
        /// names, a rising in the field. Design 02 §9.1 shows a rival court only as bands; an AI
        /// acting on the exact legitimacy or bloc shares would be acting on what the player is
        /// never allowed to see, which is a hidden advantage by another name (CLAUDE.md §3).
        /// </summary>
        public static CourtSigns SignsOf(ModState state, Kingdom kingdom)
        {
            var signs = new CourtSigns { Crown = CrownStanding.Secure };
            if (state == null || kingdom == null) return signs;

            signs.Crown = CrownOf(LegitimacyRegistry.Of(state, kingdom));
            signs.PretenderStands = SuccessionModel.PretendersTo(state, kingdom).Count > 0;
            signs.AtWarWithItself = InternalWars.OngoingIn(state, kingdom) != null;
            return signs;
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
