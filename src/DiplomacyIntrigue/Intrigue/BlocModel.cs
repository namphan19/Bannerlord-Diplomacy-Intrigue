using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// Which faction of the court a clan belongs to, and how much the factions weigh.
    ///
    /// Design 02 §3's purpose is worth keeping in front of anyone editing this file: blocs
    /// exist to make the political arithmetic **legible enough to play against**. A court
    /// that votes by a hidden lottery is noise; a court that votes in three visible blocs is
    /// a problem the player can solve. Any change that makes bloc membership harder to
    /// predict is a change in the wrong direction.
    ///
    /// **Derived, never saved**, like <see cref="LoyaltyModel"/>. A clan's agenda is whatever
    /// pressure on it is strongest right now, so it follows the world without a migration.
    /// </summary>
    public static class BlocModel
    {
        /// <summary>
        /// The agenda pressing hardest on this clan. <see cref="CourtAgenda.None"/> for the
        /// ruling clan - it does not lobby itself - and for a clan with no kingdom.
        /// </summary>
        public static CourtAgenda AgendaOf(ModState state, Clan clan)
        {
            var scores = Pressures(state, clan);
            var best = CourtAgenda.None;
            var bestScore = 0f;

            foreach (var pair in scores)
            {
                if (pair.Value <= bestScore) continue;
                best = pair.Key;
                bestScore = pair.Value;
            }
            return best;
        }

        /// <summary>
        /// Every agenda's pull on this clan, so a diagnostic can show why a clan sits where it
        /// does. Values are not comparable between clans - only within one.
        /// </summary>
        public static Dictionary<CourtAgenda, float> Pressures(ModState state, Clan clan)
        {
            var result = new Dictionary<CourtAgenda, float>();

            var kingdom = clan?.Kingdom;
            var ruling = kingdom?.RulingClan;
            if (state == null || ruling == null) return result;

            // The ruling clan is the crown, not a faction at its own court. Design 02 §3 lists
            // it under Centralists, but a bloc of one that always agrees with itself adds
            // nothing to read and would distort every power share.
            if (clan == ruling) return result;

            var exhaustion = WarExhaustion.Worst(state, kingdom);
            var hunger = FiefStanding.Hunger(clan);
            var authority = CrownAuthority.Of(kingdom);
            var influenceShare = InfluenceShare(clan, kingdom);

            // Doves: the realm is bleeding. The threshold is DiplomacyConstants'
            // ExhaustionCourtPressure, which Phase 1 wrote and left waiting for exactly this;
            // a second copy here would be the duplication CLAUDE.md §3 forbids.
            //
            // Design 02 §3 also says "its own fiefs are exposed", which needs a notion of a
            // frontier this mod does not have. The war the realm is actually losing stands in
            // for it - a PROXY, not the specified rule.
            result[CourtAgenda.Doves] = exhaustion > DiplomacyConstants.ExhaustionCourtPressure
                ? (exhaustion - DiplomacyConstants.ExhaustionCourtPressure)
                  * IntrigueConstants.DovePressurePerExhaustion
                : 0f;

            // Hawks: short of land, and somebody weaker has some.
            result[CourtAgenda.Hawks] = hunger > 0f && AWeakerNeighbourHasLand(state, kingdom)
                ? hunger * IntrigueConstants.HawkPressureFromHunger
                : 0f;

            // Autonomists: the crown has been taking, and this clan has the influence to
            // object. Both halves are required - a weak clan resenting a strong crown is
            // disaffection, not a political programme.
            result[CourtAgenda.Autonomists] = authority > 0f
                ? authority * influenceShare * IntrigueConstants.AutonomistPressure
                : 0f;

            // Centralists: this court's business is going the clan's way. Patronage is read as
            // holding more land than its standing demands, which is what a favoured clan looks
            // like from outside.
            var patronage = FiefStanding.Satisfaction(clan);
            result[CourtAgenda.Centralists] = patronage > 0f
                ? patronage * IntrigueConstants.CentralistPressureFromPatronage
                : 0f;

            // Pretenders need a claim on the throne AND crown legitimacy below 40 (design 02
            // §3). Neither exists yet: legitimacy is 2.4 and standing claimants come out of
            // succession, 2.5. The entry is present and always zero so that the bloc cannot
            // quietly form on half its conditions - design 07's armed contest is the thing
            // waiting on it, and it must not start firing early on an accident.
            result[CourtAgenda.Pretenders] = 0f;

            return result;
        }

        /// <summary>
        /// The court grouped into blocs, strongest first. A bloc's power is the sum of its
        /// members' influence and its leader is the most influential of them, both straight
        /// from design 02 §3.
        /// </summary>
        public static List<CourtBloc> BlocsOf(ModState state, Kingdom kingdom)
        {
            var blocs = new List<CourtBloc>();
            if (state == null || kingdom?.Clans == null) return blocs;

            var byAgenda = new Dictionary<CourtAgenda, CourtBloc>();
            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                if (clan == null || clan.IsEliminated) continue;

                var agenda = AgendaOf(state, clan);
                if (agenda == CourtAgenda.None) continue;

                if (!byAgenda.TryGetValue(agenda, out var bloc))
                {
                    bloc = new CourtBloc(kingdom, agenda);
                    byAgenda[agenda] = bloc;
                    blocs.Add(bloc);
                }
                bloc.Add(clan, LoyaltyModel.Of(state, clan));
            }

            blocs.Sort((a, b) => b.Power.CompareTo(a.Power));
            return blocs;
        }

        private static float InfluenceShare(Clan clan, Kingdom kingdom)
        {
            var total = 0f;
            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var other = kingdom.Clans[i];
                if (other == null || other.IsEliminated) continue;
                if (other.Influence > 0f) total += other.Influence;
            }
            if (total <= 0f) return 0f;
            return clan.Influence > 0f ? clan.Influence / total : 0f;
        }

        /// <summary>
        /// Is there anybody worth attacking? Reads the same smoothed strength the Phase 1 war
        /// valuation reads (<see cref="Power"/>), so the hawks want wars the kingdom's own AI
        /// would also consider - a hawk bloc pushing for a war nobody could ever declare would
        /// be theatre.
        /// </summary>
        private static bool AWeakerNeighbourHasLand(ModState state, Kingdom kingdom)
        {
            var ours = Power.Smoothed(state, kingdom);
            if (ours <= 0f) return false;

            foreach (var other in Kingdom.All)
            {
                if (other == null || other == kingdom || other.IsEliminated) continue;
                if (other.Settlements == null || other.Settlements.Count == 0) continue;

                if (Power.Smoothed(state, other) < ours * IntrigueConstants.HawkWeakNeighbourRatio)
                    return true;
            }
            return false;
        }
    }
}
