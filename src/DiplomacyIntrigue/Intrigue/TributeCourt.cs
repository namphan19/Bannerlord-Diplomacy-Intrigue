using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>One sworn house, as its crown weighs a demand for tribute.</summary>
    public sealed class TributeCourtHouse
    {
        public Clan Clan;

        /// <summary><see cref="LoyaltyModel.Of"/> today.</summary>
        public float Loyalty;

        /// <summary>
        /// <see cref="LoyaltyModel.IfAggrieved"/> with the tribute grievance: what paying
        /// would leave.
        /// </summary>
        public float Projected;

        /// <summary>Influence clamped at 0, the measure bloc power uses (<see cref="CourtBloc"/>).</summary>
        public float Weight;

        /// <summary>Paying would leave this house a defection risk.</summary>
        public bool Breaks => LoyaltyModel.Band(Projected) == LoyaltyBand.DefectionRisk;
    }

    /// <summary>A court's answer to "will you pay", with every house that produced it.</summary>
    public sealed class TributeCourtVerdict
    {
        public Kingdom Payer;

        /// <summary>False when intrigue is switched off: the court then has no voice.</summary>
        public bool Applies;

        public readonly List<TributeCourtHouse> Houses = new List<TributeCourtHouse>();

        /// <summary>True when every house weighs nothing and the share is a head count.</summary>
        public bool CountedHouses;

        /// <summary>Share of the court, 0-1, that paying would leave a defection risk.</summary>
        public float ShareBreaking;

        public bool Refuses => Applies && ShareBreaking >= DiplomacyConstants.AiTributeCourtRefusalShare;

        public int BreakingCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < Houses.Count; i++) if (Houses[i].Breaks) n++;
                return n;
            }
        }

        /// <summary>The houses paying would break, by name, for the payer's own eyes only.</summary>
        public string BreakingNames()
        {
            var names = new List<string>();
            for (var i = 0; i < Houses.Count; i++)
                if (Houses[i].Breaks) names.Add(Houses[i].Clan.Name.ToString());
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }
    }

    /// <summary>
    /// Whether a court would bear paying tribute. The revisit of
    /// <c>AiDiplomacy.CanDemandTribute</c> that 2.2 left owing: until this, a demand was
    /// accepted on strength and trust alone, and a realm whose court was one humiliation from
    /// coming apart paid as readily as a steady one.
    ///
    /// **The cost weighed is the one the realm would really pay.** A tributary's court takes a
    /// <see cref="GrievanceType.HumiliatingTribute"/> grievance every week the pact runs
    /// (<see cref="GrievanceSources"/>), so each sworn house is projected with that grievance
    /// added - through <see cref="LoyaltyModel.IfAggrieved"/>, which moves the same term by the
    /// same factor loyalty itself uses. A house already paying someone else, and so already
    /// holding the grievance, loses nothing more by a second tribute; the projection says so
    /// rather than counting the humiliation twice.
    ///
    /// **Crown legitimacy is not a separate term here.** It is already inside every house's
    /// loyalty (<see cref="LoyaltyModel.Explain"/>, the legitimacy term). Adding it again would
    /// count a weak crown twice - the rejected alternative, since STATUS listed "loyalty and
    /// legitimacy" as the two things this revisit should read.
    ///
    /// Houses are weighed by influence, as bloc power weighs them, because a court is broken by
    /// the houses that can act rather than by a head count. A court whose houses all weigh
    /// nothing is counted by heads instead, so it still has a voice.
    ///
    /// No "is this the player" argument. Whether the verdict gates anything is the caller's
    /// business: it stands in for an AI crown's answer, and a player crown answers for itself.
    /// </summary>
    public static class TributeCourt
    {
        public static TributeCourtVerdict Assess(ModState state, Kingdom payer)
        {
            var verdict = new TributeCourtVerdict
            {
                Payer = payer,
                Applies = Settings.Current.EnableIntrigue,
            };
            if (state == null || payer?.RulingClan == null) return verdict;

            var ruling = payer.RulingClan;
            var total = 0f;
            var breaking = 0f;
            foreach (var clan in Court.MembersOf(payer))
            {
                // The crown is not a house at its own court (LoyaltyModel gives it no loyalty).
                if (clan == ruling) continue;

                var house = new TributeCourtHouse
                {
                    Clan = clan,
                    Loyalty = LoyaltyModel.Of(state, clan),
                    Projected = LoyaltyModel.IfAggrieved(state, clan, GrievanceType.HumiliatingTribute),
                    Weight = clan.Influence > 0f ? clan.Influence : 0f,
                };
                verdict.Houses.Add(house);
                total += house.Weight;
                if (house.Breaks) breaking += house.Weight;
            }

            if (verdict.Houses.Count == 0) return verdict;

            if (total <= 0f)
            {
                verdict.CountedHouses = true;
                verdict.ShareBreaking = verdict.BreakingCount / (float)verdict.Houses.Count;
            }
            else
            {
                verdict.ShareBreaking = breaking / total;
            }

            verdict.Houses.Sort((a, b) => a.Projected.CompareTo(b.Projected));
            return verdict;
        }
    }
}
