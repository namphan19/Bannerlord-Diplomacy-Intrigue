using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// What a succession costs the crown, and who is left holding a claim.
    ///
    /// **Who takes the throne is still vanilla's decision**, and that is deliberate. Design 02
    /// §9.3 - the lead's call on 2026-09-23 - is to extend `KingdomDecision` rather than
    /// replace it, and `KingSelectionKingdomDecision` already runs a real election with real
    /// clan support. Overriding its outcome would mean owning succession end to end, including
    /// every path that reaches it: a ruler killed in battle, a clan destroyed, a kingdom
    /// absorbed. What this file owns instead is the *politics afterwards*, which vanilla has
    /// none of: how divided the court was, what that division cost the new ruler's standing,
    /// and who walks away still calling themselves the rightful heir.
    ///
    /// That split keeps the risky half in the engine's hands and puts the interesting half in
    /// ours. If the lead later wants the throne itself decided here, this is the file to widen
    /// and `KingSelectionKingdomDecision` is the class to extend.
    /// </summary>
    public static class SuccessionModel
    {
        /// <summary>
        /// A ruling clan changed. Work out how contested it was and apply the consequences in
        /// design 02 §5: a contested succession drains the crown's standing, hands every
        /// backer of a losing claimant a grievance, and leaves a strong loser as a pretender.
        /// </summary>
        public static void OnRulingClanChanged(ModState state, Kingdom kingdom, Clan oldRulingClan)
        {
            if (state == null || kingdom == null || kingdom.IsEliminated) return;

            var incumbent = kingdom.RulingClan?.Leader;
            if (incumbent == null) return;

            // A claim against a throne nobody holds any more is not a claim.
            RetireSpentClaims(state);

            var claimants = Claimants(state, kingdom, incumbent, oldRulingClan);
            if (claimants.Count < 2)
            {
                Log.Info("Succession", kingdom.Name + ": " + incumbent.Name
                                       + " took the throne unopposed - no rival claimant stood.");
                return;
            }

            var support = Support(state, kingdom, claimants);
            var total = 0f;
            foreach (var pair in support) total += pair.Value;
            if (total <= 0f) return;

            var incumbentShare = support.TryGetValue(incumbent, out var own) ? own / total : 0f;

            if (incumbentShare >= IntrigueConstants.SuccessionClearMajority)
            {
                Log.Info("Succession", kingdom.Name + ": " + incumbent.Name + " took the throne with "
                                       + (incumbentShare * 100f).ToString("0") + "% of the court - orderly.");
                return;
            }

            // Contested. The crown starts weaker than it would have.
            LegitimacyRegistry.Adjust(state, kingdom, -IntrigueConstants.SuccessionContestedLegitimacy,
                "a contested succession at " + (incumbentShare * 100f).ToString("0") + "% support");

            foreach (var pair in support)
            {
                var claimant = pair.Key;
                if (claimant == incumbent) continue;

                var share = pair.Value / total;

                // Everyone who backed a loser is left with a grievance against the winner.
                GrieveBackers(state, kingdom, claimant, incumbent);

                if (share < IntrigueConstants.SuccessionPretenderShare) continue;
                if (claimant.Clan == null || claimant.Clan == kingdom.RulingClan) continue;

                state.Pretenders.Add(new Pretender(kingdom, claimant, share));
                BlocModel.Invalidate();

                Log.Info("Succession", kingdom.Name + ": " + claimant.Name
                                       + " kept " + (share * 100f).ToString("0")
                                       + "% of the court and remains a pretender.");
            }
        }

        // ----- Reading --------------------------------------------------------

        /// <summary>Standing claims against a throne. The Pretenders bloc and design 07 read this.</summary>
        public static List<Pretender> PretendersTo(ModState state, Kingdom kingdom)
        {
            var list = new List<Pretender>();
            if (state == null || kingdom == null) return list;

            for (var i = 0; i < state.Pretenders.Count; i++)
            {
                var p = state.Pretenders[i];
                if (p.Kingdom == kingdom && p.IsStillStanding) list.Add(p);
            }
            return list;
        }

        /// <summary>The claim this clan's own leader holds, if any.</summary>
        public static Pretender ClaimOf(ModState state, Clan clan)
        {
            if (state == null || clan?.Leader == null) return null;

            for (var i = 0; i < state.Pretenders.Count; i++)
            {
                var p = state.Pretenders[i];
                if (p.Claimant == clan.Leader && p.IsStillStanding) return p;
            }
            return null;
        }

        /// <summary>Drops claims whose claimant died, took the throne, or left the realm.</summary>
        public static void RetireSpentClaims(ModState state)
        {
            if (state == null || state.Pretenders.Count == 0) return;

            var removed = state.Pretenders.RemoveAll(p => !p.IsStillStanding);
            if (removed > 0)
            {
                BlocModel.Invalidate();
                Log.Info("Succession", removed + " claim(s) lapsed - the claimant died, took the"
                                       + " throne, or left the kingdom.");
            }
        }

        // ----- The arithmetic -------------------------------------------------

        /// <summary>
        /// Who could plausibly have taken the throne. Design 02 §5: "the late ruler's heir,
        /// plus any clan leader with a blood claim."
        ///
        /// **This pool is narrow, by the spec's own rule.** Bannerlord's kingdom clans are
        /// separate families at campaign start, so on a fresh map almost nobody outside the
        /// ruling clan is related to the ruler and almost every succession will be unopposed.
        /// That is faithful to design 02 §5 and it may well be too faithful - it would make
        /// the Pretenders bloc, and with it design 07's armed contest, nearly unreachable. The
        /// measurement is worth having before the rule is widened, so the narrow version ships
        /// first and says so.
        /// </summary>
        private static List<Hero> Claimants(ModState state, Kingdom kingdom, Hero incumbent, Clan oldRulingClan)
        {
            var claimants = new List<Hero> { incumbent };
            var lateRuler = oldRulingClan?.Leader;

            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                var leader = clan?.Leader;
                if (leader == null || clan.IsEliminated || leader == incumbent) continue;
                if (claimants.Contains(leader)) continue;

                if (HasBloodClaim(leader, lateRuler, oldRulingClan)) claimants.Add(leader);
            }
            return claimants;
        }

        /// <summary>
        /// Blood ties the game actually models: parents, children, siblings, and membership of
        /// the late ruling clan itself. Deliberately not "related somewhere up the tree" -
        /// Bannerlord's genealogy is shallow and a looser rule would make half a kingdom a
        /// claimant, which is a different design rather than a more generous one.
        /// </summary>
        private static bool HasBloodClaim(Hero candidate, Hero lateRuler, Clan oldRulingClan)
        {
            if (candidate == null) return false;
            if (oldRulingClan != null && candidate.Clan == oldRulingClan) return true;
            if (lateRuler == null) return false;

            if (candidate.Father == lateRuler || candidate.Mother == lateRuler) return true;
            if (lateRuler.Father == candidate || lateRuler.Mother == candidate) return true;

            foreach (var sibling in candidate.Siblings)
                if (sibling == lateRuler) return true;

            return false;
        }

        /// <summary>
        /// How the court divides. Every clan backs one claimant and brings its influence.
        ///
        /// Backing follows design 02 §5 - "loyalty, bloc agenda, and relation to the claimant".
        /// Relation carries it, with the incumbent given the clan's loyalty as a bonus, because
        /// loyalty to the crown is exactly the disposition to accept whoever now wears it.
        /// Bloc agenda enters through loyalty rather than as a separate term: a clan's bloc is
        /// already a function of the same pressures.
        /// </summary>
        private static Dictionary<Hero, float> Support(ModState state, Kingdom kingdom, List<Hero> claimants)
        {
            var support = new Dictionary<Hero, float>();
            for (var i = 0; i < claimants.Count; i++) support[claimants[i]] = 0f;

            var incumbent = kingdom.RulingClan?.Leader;

            for (var c = 0; c < kingdom.Clans.Count; c++)
            {
                var clan = kingdom.Clans[c];
                if (clan?.Leader == null || clan.IsEliminated) continue;

                var influence = clan.Influence > 0f ? clan.Influence : 0f;
                if (influence <= 0f) continue;

                Hero backed = null;
                var best = float.MinValue;

                for (var i = 0; i < claimants.Count; i++)
                {
                    var claimant = claimants[i];
                    var score = clan.Leader == claimant
                        ? IntrigueConstants.SuccessionSelfBacking
                        : clan.Leader.GetRelation(claimant);

                    if (claimant == incumbent)
                        score += LoyaltyModel.Of(state, clan) * IntrigueConstants.SuccessionLoyaltyWeight;

                    if (score <= best) continue;
                    best = score;
                    backed = claimant;
                }

                if (backed != null) support[backed] += influence;
            }
            return support;
        }

        /// <summary>
        /// Every clan that backed a losing claimant resents the winner for it. Design 02 §5
        /// puts this at weight 6; the grievance is against the new ruling clan, which is the
        /// crown now.
        /// </summary>
        private static void GrieveBackers(ModState state, Kingdom kingdom, Hero loser, Hero incumbent)
        {
            var ruling = kingdom.RulingClan;
            if (ruling == null) return;

            for (var c = 0; c < kingdom.Clans.Count; c++)
            {
                var clan = kingdom.Clans[c];
                if (clan?.Leader == null || clan.IsEliminated || clan == ruling) continue;

                // Backed the loser if they simply like them better than the winner.
                if (clan.Leader.GetRelation(loser) <= clan.Leader.GetRelation(incumbent)) continue;

                GrievanceRegistry.Add(state, clan, ruling, GrievanceType.SuccessionPassedOver,
                    reason: "backed " + loser.Name + " for the throne");
            }
        }
    }
}
