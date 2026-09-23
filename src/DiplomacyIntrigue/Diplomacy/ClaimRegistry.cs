using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Grants, renews, expires and queries casus belli.
    ///
    /// The AI and the player go through exactly the same functions here. That is a
    /// deliberate project decision: the AI plays by the same rules, so there are no hidden
    /// modifiers and the player cannot out-cheese it by learning where the seams are.
    /// </summary>
    public static class ClaimRegistry
    {
        // ----- Granting -------------------------------------------------------

        /// <summary>
        /// Adds a claim, or renews the matching one if it already exists. Returns the live
        /// claim either way, or null when the arguments do not describe a real dispute.
        /// </summary>
        public static Claim Grant(ModState state, Kingdom claimant, Kingdom target, CasusBelliType type,
            CampaignTime expiresOn, Settlement settlement = null, bool fabricated = false)
        {
            if (claimant == null || target == null || claimant == target) return null;
            if (type == CasusBelliType.None) return null;

            for (var i = 0; i < state.Claims.Count; i++)
            {
                var existing = state.Claims[i];
                if (!existing.IsLive || !existing.Matches(claimant, target, type, settlement)) continue;

                // A fresh triggering event renews rather than stacks, so repeated raids do
                // not inflate a kingdom's claim list.
                existing.ExtendTo(expiresOn);
                return existing;
            }

            var claim = new Claim(claimant, target, type, settlement, expiresOn, fabricated);
            state.Claims.Add(claim);
            Log.Debug("Claims", "Granted: " + claim);
            return claim;
        }

        /// <summary>
        /// Someone took one of our fiefs, so we have a claim to take it back. Granted to
        /// the former holder, against whoever holds it now. Lives as long as the fief is
        /// within living memory - this is the one claim type that is meant to outlast a
        /// generation.
        /// </summary>
        public static Claim GrantAncestralClaim(ModState state, Kingdom formerHolder, Settlement settlement)
        {
            var currentHolder = settlement?.MapFaction as Kingdom;
            if (currentHolder == null || formerHolder == null || currentHolder == formerHolder) return null;

            return Grant(state, formerHolder, currentHolder, CasusBelliType.ReclaimAncestralLand,
                CampaignTime.YearsFromNow(DiplomacyConstants.AncestralClaimMemoryYears), settlement);
        }

        /// <summary>A raid justifies retaliation, but not forever.</summary>
        public static Claim GrantRaidClaim(ModState state, Kingdom victim, Kingdom raider)
            => Grant(state, victim, raider, CasusBelliType.AvengeRaid,
                CampaignTime.DaysFromNow(DiplomacyConstants.AvengeRaidWindowDays));

        public static Claim GrantBrokenTreatyClaim(ModState state, Kingdom victim, Kingdom breaker)
            => Grant(state, victim, breaker, CasusBelliType.BrokenTreaty,
                CampaignTime.YearsFromNow(DiplomacyConstants.BrokenTreatyWindowYears));

        /// <summary>
        /// Settles every live BrokenTreaty claim either kingdom holds against the other, and
        /// returns how many. Called when one submits to the other: a kingdom that kneels has
        /// answered for the oath it broke, and a patron that takes it back has accepted that.
        ///
        /// Found by driving an annexation on the run-04 world. Northern Empire turned on
        /// Sturgia and the war was filed as BrokenTreaty at legitimacy 0.95, at the price of a
        /// just war - because Sturgia's revolt, from before it knelt again, was still a live
        /// claim. A patron tearing up an oath must not be able to cite a breach it forgave.
        /// Land claims are left alone: a submission settles a betrayal, not a border.
        /// </summary>
        public static int SettleBreaches(ModState state, Kingdom a, Kingdom b, CampaignTime? acquiredBefore = null)
        {
            var settled = 0;
            for (var i = 0; i < state.Claims.Count; i++)
            {
                var claim = state.Claims[i];
                if (!claim.IsLive || claim.Type != CasusBelliType.BrokenTreaty) continue;
                if (acquiredBefore.HasValue && claim.AcquiredOn > acquiredBefore.Value) continue;
                if (!((claim.Claimant == a && claim.Target == b) || (claim.Claimant == b && claim.Target == a))) continue;
                claim.Settle();
                settled++;
            }
            return settled;
        }

        // ----- Queries --------------------------------------------------------

        public static IEnumerable<Claim> LiveClaims(ModState state, Kingdom claimant, Kingdom target)
        {
            for (var i = 0; i < state.Claims.Count; i++)
            {
                var claim = state.Claims[i];
                if (claim.IsLive && claim.Claimant == claimant && claim.Target == target)
                    yield return claim;
            }
        }

        /// <summary>
        /// The most defensible claim the kingdom holds against the target, or null. This is
        /// what a war declaration should be justified with.
        /// </summary>
        public static Claim Best(ModState state, Kingdom claimant, Kingdom target)
        {
            Claim best = null;
            foreach (var claim in LiveClaims(state, claimant, target))
                if (best == null || claim.Legitimacy > best.Legitimacy) best = claim;
            return best;
        }

        /// <summary>
        /// Legitimacy of the best available justification, 0 when there is none. Naked
        /// aggression stays possible - it is just expensive and isolating.
        /// </summary>
        public static float BestLegitimacy(ModState state, Kingdom claimant, Kingdom target)
        {
            var best = Best(state, claimant, target);
            return best == null ? CasusBelli.Legitimacy(CasusBelliType.None) : best.Legitimacy;
        }

        public static bool HasTerritorialClaim(ModState state, Kingdom claimant, Kingdom target)
        {
            foreach (var claim in LiveClaims(state, claimant, target))
                if (claim.AllowsFiefDemands) return true;
            return false;
        }

        // ----- Upkeep ---------------------------------------------------------

        /// <summary>
        /// Drops claims that have run out, and any whose kingdoms no longer exist. Called
        /// daily.
        /// </summary>
        public static int ExpireStale(ModState state)
        {
            var removed = 0;
            for (var i = state.Claims.Count - 1; i >= 0; i--)
            {
                var claim = state.Claims[i];
                if (claim.IsLive && claim.Claimant != null && claim.Target != null) continue;

                state.Claims.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        // ----- Fabrication ----------------------------------------------------

        /// <summary>
        /// Starts manufacturing a claim on a fief. Charges the cost up front; the outcome
        /// lands after <see cref="DiplomacyConstants.FabricateClaimDurationDays"/> days.
        /// Returns null and sets <paramref name="reason"/> when it cannot be started.
        /// </summary>
        public static FabricationAttempt StartFabrication(ModState state, Kingdom claimant,
            Settlement settlement, out string reason)
        {
            reason = null;

            if (claimant == null || settlement == null) { reason = "No kingdom or no target."; return null; }
            if (!settlement.IsFortification) { reason = "Only towns and castles can be claimed."; return null; }

            var holder = settlement.MapFaction as Kingdom;
            if (holder == null) { reason = "That fief is not held by a kingdom."; return null; }
            if (holder == claimant) { reason = "You already hold that fief."; return null; }

            for (var i = 0; i < state.Fabrications.Count; i++)
            {
                if (state.Fabrications[i].Claimant == claimant && state.Fabrications[i].Target == settlement)
                {
                    reason = "A claim on that fief is already being fabricated.";
                    return null;
                }
            }

            var ruling = claimant.RulingClan;
            if (ruling == null) { reason = "That kingdom has no ruling clan."; return null; }
            if (ruling.Influence < DiplomacyConstants.FabricateClaimInfluenceCost)
            {
                reason = "Not enough influence (needs " + DiplomacyConstants.FabricateClaimInfluenceCost + ").";
                return null;
            }

            var leader = claimant.Leader;
            if (leader == null || leader.Gold < DiplomacyConstants.FabricateClaimGoldCost)
            {
                reason = "Not enough gold (needs " + DiplomacyConstants.FabricateClaimGoldCost + ").";
                return null;
            }

            ChangeClanInfluenceAction.Apply(ruling, -DiplomacyConstants.FabricateClaimInfluenceCost);
            leader.ChangeHeroGold(-DiplomacyConstants.FabricateClaimGoldCost);

            var attempt = new FabricationAttempt(claimant, settlement,
                CampaignTime.DaysFromNow(DiplomacyConstants.FabricateClaimDurationDays));
            state.Fabrications.Add(attempt);

            Log.Info("Claims", "Fabrication started: " + attempt);
            return attempt;
        }

        /// <summary>Resolves every fabrication whose time has come. Called daily.</summary>
        public static void ResolveFabrications(ModState state)
        {
            for (var i = state.Fabrications.Count - 1; i >= 0; i--)
            {
                var attempt = state.Fabrications[i];

                if (attempt.Claimant == null || attempt.Target == null)
                {
                    state.Fabrications.RemoveAt(i);
                    continue;
                }
                if (!attempt.IsDue) continue;

                state.Fabrications.RemoveAt(i);

                var target = attempt.TargetKingdom;
                if (target == null || target == attempt.Claimant)
                {
                    // The fief changed hands while the heralds were working. The effort is
                    // wasted rather than quietly retargeted at the new holder.
                    Log.Info("Claims", "Fabrication void - " + attempt.Target.Name + " changed hands.");
                    continue;
                }

                if (MBRandom.RandomFloat < DiplomacyConstants.FabricateClaimExposureChance)
                    Expose(state, attempt, target);
                else
                    Succeed(state, attempt, target);
            }
        }

        private static void Succeed(ModState state, FabricationAttempt attempt, Kingdom target)
        {
            Grant(state, attempt.Claimant, target, CasusBelliType.ReclaimAncestralLand,
                CampaignTime.YearsFromNow(DiplomacyConstants.ClaimLifetimeYears),
                attempt.Target, fabricated: true);

            Log.Info("Claims", attempt.Claimant.Name + " fabricated a claim on " + attempt.Target.Name + ".");
        }

        /// <summary>
        /// The fabrication came to light. The target gains a grievance and every other
        /// court trusts the fabricator less - the penalty for being caught has to be
        /// diplomatic rather than a refund, or fabricating is always worth trying.
        /// </summary>
        private static void Expose(ModState state, FabricationAttempt attempt, Kingdom target)
        {
            var fabricator = attempt.Claimant;

            // Being lied about is itself a justification for war.
            GrantBrokenTreatyClaim(state, target, fabricator);

            var fabricatorLeader = fabricator.Leader;
            if (fabricatorLeader != null)
            {
                foreach (var kingdom in Kingdom.All)
                {
                    if (kingdom == fabricator || !kingdom.IsRealm()) continue;

                    var leader = kingdom.Leader;
                    if (leader == null) continue;

                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                        fabricatorLeader, leader,
                        -DiplomacyConstants.FabricateExposedRelationLoss, false);
                }
            }

            // The pool this was waiting for exists as of 2.4, so the penalty is paid rather
            // than logged. The constant stays here, where the exposure rules live.
            if (Core.Settings.Current.EnableIntrigue)
                Intrigue.LegitimacyRegistry.OnCaughtFabricating(state, fabricator);

            Log.Info("Claims", fabricator.Name + " was caught fabricating a claim on "
                               + attempt.Target.Name + " - relations damaged with every kingdom.");
        }
    }
}
