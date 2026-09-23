using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Diplomacy;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// Turns things that happen in the campaign into entries in the court's ledger. The only
    /// caller of <see cref="GrievanceRegistry.Add"/> outside diagnostics.
    ///
    /// Kept apart from <see cref="GrievanceRegistry"/> so the ledger stays a pure store with
    /// one way in, and every question of "does this event offend anybody, and how much" is
    /// answerable by reading one file.
    ///
    /// **Three of design 02 §1's eight sources are not wired yet**, and deliberately so:
    /// `PolicyAgainstAgenda` and `PeaceWhileWinning` both need a clan to *have* an agenda,
    /// which is 2.3, and `RequestRefused` needs a request mechanism that does not exist. They
    /// are listed here rather than silently missing, because a half-wired source that nobody
    /// records is exactly how a system comes to look balanced while running on a third of its
    /// inputs.
    /// </summary>
    public static class GrievanceSources
    {
        /// <summary>
        /// A fief changed hands. Two different slights live here, and they are not symmetric:
        /// a fief *granted* to a rival offends the clans that wanted it, and a fief *taken* by
        /// an enemy offends the clan that lost it.
        /// </summary>
        public static void OnSettlementOwnerChanged(ModState state, Settlement settlement,
            Hero newOwner, Hero oldOwner,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (state == null || settlement == null) return;

            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision)
                GrantedToARival(state, newOwner?.Clan);
            else if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)
                LostToTheEnemy(state, oldOwner?.Clan, newOwner?.Clan);
        }

        /// <summary>
        /// The crown handed a fief to somebody. Every *other* clan of that kingdom that is
        /// short of land resents it - and resents **the crown**, not the clan that received
        /// it. Blaming the recipient was the obvious alternative and it lost: it would make
        /// the ruler's decision free, and the whole point of this mechanism is that handing a
        /// town to a favourite costs the ruler something.
        ///
        /// "How strongly they bid" (design 02 §1) is not available to us - vanilla's fief
        /// decision does not expose the bids - so land hunger stands in for it. That is a
        /// **proxy, not the specified rule**, and it is the first thing to revisit if the
        /// source turns out to fire too widely. Land hunger itself lives in
        /// <see cref="FiefStanding"/>, because loyalty asks the same question.
        /// </summary>
        private static void GrantedToARival(ModState state, Clan receiver)
        {
            var kingdom = receiver?.Kingdom;
            var ruling = kingdom?.RulingClan;
            if (ruling == null) return;

            for (var i = 0; i < kingdom.Clans.Count; i++)
            {
                var clan = kingdom.Clans[i];
                if (clan == receiver || clan == ruling || clan.IsEliminated) continue;

                var hunger = FiefStanding.Hunger(clan);
                if (hunger <= 0f) continue;   // a clan already well provided for does not begrudge it

                GrievanceRegistry.Add(state, clan, ruling, GrievanceType.FiefToRival,
                    IntrigueConstants.GrievanceFiefToRival * hunger,
                    "fief granted to " + (receiver == null ? "another clan" : receiver.Name.ToString()));
            }
        }

        /// <summary>
        /// A fief fell to an enemy. The clan that held it blames its own crown for failing to
        /// defend it - which is why the target is the ruling clan and not the besieger. A
        /// grievance is an internal political fact; anger at the enemy is what casus belli
        /// and claims already carry (Phase 1.2).
        /// </summary>
        private static void LostToTheEnemy(ModState state, Clan loser, Clan taker)
        {
            var kingdom = loser?.Kingdom;
            var ruling = kingdom?.RulingClan;
            if (ruling == null || loser == ruling) return;

            // A fief changing hands inside one kingdom is not a defeat; only a real loss to
            // an outside power is.
            if (taker != null && taker.Kingdom == kingdom) return;

            GrievanceRegistry.Add(state, loser, ruling, GrievanceType.FiefLostToEnemy,
                reason: "the crown did not defend it");

            // The court blames the crown, and so does the wider world (design 02 §4).
            LegitimacyRegistry.OnFiefLost(state, kingdom);
        }

        /// <summary>
        /// A war was declared. The weight is <c>(1 - legitimacy)</c> of the ceiling, straight
        /// out of design 02 §1 - so a fully justified war offends nobody and a naked land grab
        /// offends the whole court. This is the hinge where Phase 1's casus belli work starts
        /// paying into Phase 2.
        ///
        /// Legitimacy comes from <see cref="CasusBelli.ResolvedLegitimacy"/> rather than from
        /// the war record, on purpose. The record may not exist yet - campaign listeners do
        /// not fire in registration order, and this project has a live trace proving it - and
        /// CasusBelli is the single resolver precisely so that every caller gets the same
        /// answer regardless of when it asks.
        ///
        /// Design 02 §1 says "a war their bloc opposed". Blocs are 2.3. Until then every
        /// non-ruling clan of the declaring kingdom is offended, which is **wider than the
        /// specified rule** and will need narrowing when agendas exist.
        /// </summary>
        public static void OnWarDeclared(ModState state, Kingdom aggressor, Kingdom defender,
            DeclareWarAction.DeclareWarDetail detail)
        {
            if (state == null || aggressor == null || defender == null) return;

            var ruling = aggressor.RulingClan;
            if (ruling == null) return;

            var legitimacy = CasusBelli.ResolvedLegitimacy(state, aggressor, defender, detail);

            // A war with nothing at all to point at costs the crown its own standing, on top
            // of what it costs with the court (design 02 §4).
            if (CasusBelli.Resolve(state, aggressor, defender, detail) == CasusBelliType.None)
                LegitimacyRegistry.OnWarDeclaredWithoutCause(state, aggressor);

            var weight = IntrigueConstants.GrievanceUnjustWarMax * (1f - legitimacy);
            if (weight <= 0f) return;

            for (var i = 0; i < aggressor.Clans.Count; i++)
            {
                var clan = aggressor.Clans[i];
                if (clan == ruling || clan.IsEliminated) continue;

                GrievanceRegistry.Add(state, clan, ruling, GrievanceType.UnjustWar, weight,
                    "war on " + defender.Name + " at legitimacy " + legitimacy.ToString("0.00"));
            }
        }

        /// <summary>
        /// The two sources that are conditions rather than moments. Both are scanned weekly
        /// because both are *states the realm is in* - a tribute being paid, a relative still
        /// in a cell - and a state has no event to hang off. Renewal is idempotent
        /// (<see cref="Grievance.Renew"/> takes the larger weight), so a court stays
        /// humiliated for as long as the condition lasts and starts forgetting the week it
        /// ends. That is the intended behaviour, not an accident of the scan.
        /// </summary>
        public static void WeeklyScan(ModState state)
        {
            if (state == null) return;
            HumiliatingTribute(state);
            RelativesInCaptivity(state);
        }

        private static void HumiliatingTribute(ModState state)
        {
            for (var i = 0; i < state.Treaties.Count; i++)
            {
                var treaty = state.Treaties[i];
                if (!treaty.IsActive || treaty.TributeAmount <= 0 || treaty.TributePayer == null) continue;

                var payer = treaty.TributePayer;
                var ruling = payer.RulingClan;
                if (ruling == null) continue;

                for (var c = 0; c < payer.Clans.Count; c++)
                {
                    var clan = payer.Clans[c];
                    if (clan == ruling || clan.IsEliminated) continue;

                    GrievanceRegistry.Add(state, clan, ruling, GrievanceType.HumiliatingTribute,
                        reason: "the realm pays tribute");
                }
            }
        }

        private static void RelativesInCaptivity(ModState state)
        {
            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsPrisoner) continue;

                var clan = hero.Clan;
                var ruling = clan?.Kingdom?.RulingClan;
                if (ruling == null || clan == ruling) continue;

                if (hero.CaptivityStartTime.ElapsedYearsUntilNow < IntrigueConstants.CaptivityGrievanceYears)
                    continue;

                GrievanceRegistry.Add(state, clan, ruling, GrievanceType.RelativeInCaptivity,
                    reason: hero.Name + " has been a prisoner over a year");
            }
        }

    }
}
