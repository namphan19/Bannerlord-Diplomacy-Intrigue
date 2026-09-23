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
            // §3). As of 2.4 the crown's half is real - LegitimacyRegistry.IsWeak answers it -
            // but there are still no standing claimants, which is succession's job at 2.5.
            // Deliberately still zero: letting the bloc form on half its conditions would let
            // design 07's armed contest start firing on an accident, and a bloc that exists
            // with no claimant to rally to has nobody to put on the throne.
            result[CourtAgenda.Pretenders] = 0f;

            return result;
        }

        // ----- Caching --------------------------------------------------------
        //
        // Bloc membership is read once per clan per outcome while a kingdom votes, and each
        // read walks every clan and every grievance. Recomputing it each time is affordable
        // for a diagnostic and not for a vote, so the result is memoised per kingdom.
        //
        // The key is the campaign day AND a generation counter, not the day alone. A day
        // stamp on its own would be a lie under `diplomacy.tick_days`, where the clock never
        // moves: grievances would decay, legitimacy would shift, and the cache would keep
        // serving the state of the world as it was before the command ran. That is exactly
        // the trap CLAUDE.md §1 records about diagnostics that drive part of a tick, and it
        // would have been reintroduced here by a one-line cache.

        private sealed class CachedBlocs
        {
            public int Day;
            public int Generation;
            public List<CourtBloc> Blocs;
        }

        private static readonly Dictionary<Kingdom, CachedBlocs> Cache =
            new Dictionary<Kingdom, CachedBlocs>();

        private static int _generation;

        /// <summary>
        /// Called by anything that moves a number loyalty reads - a grievance recorded or
        /// decayed, a legitimacy adjustment. Cheap on purpose: one integer, no allocation.
        /// </summary>
        public static void Invalidate() => _generation++;

        /// <summary>Drops everything. For session start, where the old kingdoms are gone.</summary>
        public static void Reset()
        {
            Cache.Clear();
            _generation++;
        }

        /// <summary>
        /// The bloc a clan belongs to, or null. The vote path's entry point, so it reads the
        /// cache rather than rebuilding the court.
        /// </summary>
        public static CourtBloc BlocOf(ModState state, Clan clan)
        {
            var kingdom = clan?.Kingdom;
            if (kingdom == null) return null;

            var blocs = BlocsOf(state, kingdom);
            for (var i = 0; i < blocs.Count; i++)
                if (blocs[i].Members.Contains(clan)) return blocs[i];
            return null;
        }

        /// <summary>
        /// The court grouped into blocs, strongest first. A bloc's power is the sum of its
        /// members' influence and its leader is the most influential of them, both straight
        /// from design 02 §3.
        /// </summary>
        public static List<CourtBloc> BlocsOf(ModState state, Kingdom kingdom)
        {
            if (state != null && kingdom != null)
            {
                var today = (int)CampaignTime.Now.ToDays;
                if (Cache.TryGetValue(kingdom, out var cached)
                    && cached.Day == today && cached.Generation == _generation)
                    return cached.Blocs;

                var fresh = BuildBlocs(state, kingdom);
                Cache[kingdom] = new CachedBlocs { Day = today, Generation = _generation, Blocs = fresh };
                return fresh;
            }

            return new List<CourtBloc>();
        }

        private static List<CourtBloc> BuildBlocs(ModState state, Kingdom kingdom)
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
