using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// What an AI kingdom decides to do about its neighbours, once a week.
    ///
    /// Two rules shape this more than any formula:
    ///
    ///   - **At most one action per kingdom per week, and usually none.** Rate limiting is
    ///     a design feature, not an optimisation. Diplomacy that fires daily reads as
    ///     random noise; weekly, with a reason attached to each move, reads as intent.
    ///
    ///   - **The same functions the player uses.** Every decision here goes through
    ///     <see cref="TreatyRegistry"/>, <see cref="PeaceTable"/> and
    ///     <see cref="ClaimRegistry"/>. There is no AI-only path and no hidden modifier,
    ///     so a number shown to the player is the number the AI used.
    /// </summary>
    public static class AiDiplomacy
    {
        /// <summary>The move a kingdom made, for logging and for the UI to explain itself.</summary>
        public enum Move
        {
            None = 0,
            SoughtPeace = 1,
            OfferedPact = 2,
            DemandedTribute = 3,
            DeclaredWar = 4,
        }

        /// <summary>
        /// Runs one kingdom's weekly evaluation and returns what it did. Priority order
        /// matters: a realm that needs out of a war does not go shopping for allies.
        /// </summary>
        public static Move Evaluate(ModState state, Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated || kingdom.RulingClan == null) return Move.None;

            // Getting out of a losing war still comes first - nothing else matters while a
            // realm is being ground down.
            if (TrySeekPeace(state, kingdom)) return Move.SoughtPeace;

            // War before pacts, reversed from the original order. Vanilla no longer starts
            // wars, so if this evaluation prefers a cheap non-aggression pact whenever one
            // is available, the map signs itself into permanent peace - which is exactly
            // what run 01 produced, with a standing web of truces, pacts and alliances and
            // almost nothing happening. A kingdom with a good war available takes it.
            if (TryDeclareWar(state, kingdom)) return Move.DeclaredWar;
            if (TryDemandTribute(state, kingdom)) return Move.DemandedTribute;
            if (TryOfferPact(state, kingdom)) return Move.OfferedPact;

            return Move.None;
        }

        // ================= 1. Get out of the worst war =========================

        /// <summary>
        /// Negotiates an exit from the war that is costing us most. Tries a white peace
        /// first, then concedes upward - the cheapest package the other side will take -
        /// and stops when the price exceeds what their victory entitles them to or what we
        /// are willing to bear.
        /// </summary>
        private static bool TrySeekPeace(ModState state, Kingdom kingdom)
        {
            WarRecord worst = null;
            var worstExhaustion = 0f;
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                var exhaustion = war.ExhaustionOf(kingdom);
                if (exhaustion <= worstExhaustion) continue;
                worst = war;
                worstExhaustion = exhaustion;
            }

            if (worst == null) return false;
            if (worstExhaustion < DiplomacyConstants.ExhaustionSeekPeace) return false;

            var enemy = worst.Other(kingdom);
            if (enemy == null || enemy.IsEliminated) return false;

            // A white peace costs nothing, so it is always the first offer.
            var white = new PeaceTerms(enemy, kingdom);
            if (PeaceTable.WouldAccept(state, worst, white, out _)
                && PeaceTable.Apply(state, worst, white, out _))
            {
                Log.Info("AI", kingdom.Name + " sued for peace with " + enemy.Name
                               + " at exhaustion " + worstExhaustion.ToString("0.0") + ": white peace.");
                return true;
            }

            // They want something. Concede in increasing order of pain, and never past
            // what their war score entitles them to.
            foreach (var terms in ConcessionLadder(state, worst, enemy, kingdom))
            {
                if (!PeaceTable.IsDemandable(state, worst, terms, out _)) continue;
                if (!PeaceTable.WouldAccept(state, worst, terms, out _)) continue;

                if (PeaceTable.Apply(state, worst, terms, out _))
                {
                    Log.Info("AI", kingdom.Name + " bought peace from " + enemy.Name
                                   + " at exhaustion " + worstExhaustion.ToString("0.0") + ": " + terms + ".");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Packages we would offer to end a war, cheapest first. Prisoners cost nothing
        /// strategically; land costs the most and comes last.
        /// </summary>
        private static IEnumerable<PeaceTerms> ConcessionLadder(ModState state, WarRecord war,
            Kingdom winner, Kingdom loser)
        {
            var prisoners = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
            yield return prisoners;

            var leader = loser.Leader;
            if (leader != null && leader.Gold > 0)
            {
                var affordable = leader.Gold / 2;
                if (affordable >= 1000)
                    yield return new PeaceTerms(winner, loser)
                    {
                        ReleasePrisoners = true,
                        IndemnityGold = affordable / 1000 * 1000,
                    };
            }

            yield return new PeaceTerms(winner, loser)
            {
                ReleasePrisoners = true,
                ImposeTributaryPact = true,
                TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod,
            };

            // Land last, and only what they could take anyway. A castle before a town.
            foreach (var fief in CedeCandidates(loser, townsFirst: false))
            {
                var terms = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
                terms.FiefsCeded.Add(fief);
                yield return terms;
            }
        }

        private static IEnumerable<Settlement> CedeCandidates(Kingdom loser, bool townsFirst)
        {
            var settlements = loser.Settlements;
            for (var pass = 0; pass < 2; pass++)
            {
                var wantTowns = townsFirst ? pass == 0 : pass == 1;
                for (var i = 0; i < settlements.Count; i++)
                {
                    var settlement = settlements[i];
                    if (!settlement.IsFortification) continue;
                    if (settlement.IsTown != wantTowns) continue;
                    yield return settlement;
                }
            }
        }

        // ================= 2. Find a partner ====================================

        private static bool TryOfferPact(ModState state, Kingdom kingdom)
        {
            Kingdom best = null;
            var bestValue = 0f;
            var bestType = TreatyType.NonAggressionPact;

            foreach (var other in Kingdom.All)
            {
                if (other == kingdom || other.IsEliminated) continue;
                if (kingdom.IsAtWarWith(other)) continue;

                var ourValue = PactValue(state, kingdom, other);
                var theirValue = PactValue(state, other, kingdom);
                var mutual = ourValue < theirValue ? ourValue : theirValue;

                // Ambition scales with how much both sides want it. A pact neither side is
                // enthusiastic about is a non-aggression pact, not an alliance.
                var type = mutual >= DiplomacyConstants.AiAllianceThreshold ? TreatyType.Alliance
                    : mutual >= DiplomacyConstants.AiDefensivePactThreshold ? TreatyType.DefensivePact
                    : TreatyType.NonAggressionPact;

                if (mutual < DiplomacyConstants.AiNonAggressionThreshold) continue;
                if (mutual <= bestValue) continue;

                best = other;
                bestValue = mutual;
                bestType = type;
            }

            if (best == null) return false;
            if (!CanAffordInfluence(kingdom, DiplomacyConstants.TreatyInfluenceCost(bestType))) return false;

            var treaty = TreatyRegistry.Sign(state, kingdom, best, bestType, out var reason);
            if (treaty == null)
            {
                Log.Debug("AI", kingdom.Name + " wanted a " + bestType + " with " + best.Name
                                + " but: " + reason);
                return false;
            }

            ChangeClanInfluenceAction.Apply(kingdom.RulingClan,
                -DiplomacyConstants.TreatyInfluenceCost(bestType));

            Log.Info("AI", kingdom.Name + " signed a " + bestType + " with " + best.Name
                           + " (mutual value " + bestValue.ToString("0") + ").");
            Announce(kingdom.Name + " and " + best.Name + " sign a " + bestType + ".");
            return true;
        }

        /// <summary>
        /// How much <paramref name="us"/> wants an agreement with <paramref name="them"/>.
        ///
        /// Two terms in the design doc had no cheap data behind them and are approximated,
        /// deliberately and visibly: trade exposure is proxied by **proximity**, and border
        /// security is folded into **aggression** - a weak neighbour sharing a long border
        /// is a temptation, not a partner, which is the same thing said from the other side.
        /// </summary>
        public static float PactValue(ModState state, Kingdom us, Kingdom them)
        {
            var sharedThreat = SharedThreat(us, them);
            var proximity = Proximity(us, them);
            var trust = TrustRegistry.Get(state, us, them) / 100f;
            var aggression = Aggression(state, us, them);
            var relation = FactionManager.GetRelationBetweenClans(us.RulingClan, them.RulingClan) / 100f;

            return DiplomacyConstants.PactWeightSharedThreat * sharedThreat
                   + DiplomacyConstants.PactWeightProximity * proximity
                   + DiplomacyConstants.PactWeightTrust * trust
                   - DiplomacyConstants.PactWeightAggression * aggression
                   + DiplomacyConstants.PactWeightRelation * relation;
        }

        /// <summary>Fraction of our enemies that are also theirs. Common enemies bind.</summary>
        private static float SharedThreat(Kingdom us, Kingdom them)
        {
            var ours = 0;
            var shared = 0;
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == us || kingdom.IsEliminated) continue;
                if (!us.IsAtWarWith(kingdom)) continue;
                ours++;
                if (kingdom != them && them.IsAtWarWith(kingdom)) shared++;
            }
            return ours == 0 ? 0f : (float)shared / ours;
        }

        private static float Proximity(Kingdom a, Kingdom b)
        {
            var from = a.FactionMidSettlement;
            var to = b.FactionMidSettlement;
            if (from == null || to == null) return 0f;

            var distance = from.GetPosition2D.Distance(to.GetPosition2D);
            var normalised = distance / DiplomacyConstants.MapDistanceNormaliser;
            if (normalised >= 1f) return 0f;
            return 1f - normalised;
        }

        /// <summary>
        /// How much we want what they have: a standing territorial claim plus a strength
        /// advantage. Zero when we are weaker and have no claim.
        /// </summary>
        private static float Aggression(ModState state, Kingdom us, Kingdom them)
        {
            var value = ClaimRegistry.HasTerritorialClaim(state, us, them)
                ? DiplomacyConstants.AggressionFromClaim
                : 0f;

            var theirs = them.CurrentTotalStrength;
            if (theirs > 0f)
            {
                var ratio = us.CurrentTotalStrength / theirs;
                if (ratio > 1f) value += (ratio - 1f) * DiplomacyConstants.AggressionPerStrengthRatio;
            }

            return value > 1f ? 1f : value;
        }

        // ================= 3. Demand submission instead of war ==================

        /// <summary>
        /// A much stronger kingdom with a claim can demand tributary status rather than
        /// invade. This is the expansion path that does not need an army, and it gives the
        /// weaker side a choice between paying and fighting.
        /// </summary>
        private static bool TryDemandTribute(ModState state, Kingdom kingdom)
        {
            if (WorstExhaustion(state, kingdom) > DiplomacyConstants.AiMaxExhaustionToExpand) return false;

            foreach (var target in Kingdom.All)
            {
                if (target == kingdom || target.IsEliminated) continue;
                if (kingdom.IsAtWarWith(target)) continue;
                if (!ClaimRegistry.HasTerritorialClaim(state, kingdom, target)) continue;
                if (TreatyRegistry.PatronOf(state, target) != null) continue;

                var theirs = target.CurrentTotalStrength;
                if (theirs <= 0f) continue;
                if (kingdom.CurrentTotalStrength / theirs < DiplomacyConstants.AiTributeDemandStrengthRatio) continue;

                // They submit only if the alternative looks worse than paying.
                if (TrustRegistry.Get(state, target, kingdom) < DiplomacyConstants.TrustFloorForPacts) continue;

                var treaty = TreatyRegistry.Sign(state, kingdom, target, TreatyType.TributaryPact,
                    out var reason, tributePayer: target,
                    tributeAmount: DiplomacyConstants.AiDefaultTributePerPeriod);

                if (treaty == null)
                {
                    Log.Debug("AI", kingdom.Name + " could not impose tribute on " + target.Name + ": " + reason);
                    continue;
                }

                Log.Info("AI", kingdom.Name + " imposed a tributary pact on " + target.Name + ".");
                Announce(target.Name + " agrees to pay tribute to " + kingdom.Name + ".");
                return true;
            }

            return false;
        }

        // ================= 4. War, as a last resort =============================

        private static bool TryDeclareWar(ModState state, Kingdom kingdom)
        {
            if (WorstExhaustion(state, kingdom) > DiplomacyConstants.AiMaxExhaustionToExpand) return false;

            var weariness = state.WearinessOf(kingdom);
            if (weariness > DiplomacyConstants.AiMaxWearinessToExpand) return false;

            Kingdom best = null;
            var bestValue = 0f;
            CasusBelliType bestCasus = CasusBelliType.Conquest;

            foreach (var target in Kingdom.All)
            {
                if (target == kingdom || target.IsEliminated) continue;
                if (kingdom.IsAtWarWith(target)) continue;
                if (!TreatyEnforcement.IsWarAllowed(state, kingdom, target)) continue;

                var theirs = target.CurrentTotalStrength;
                if (theirs <= 0f) continue;
                var ratio = kingdom.CurrentTotalStrength / theirs;
                if (ratio < DiplomacyConstants.AiWarStrengthRatio) continue;

                var claim = ClaimRegistry.Best(state, kingdom, target);
                var casus = claim == null ? CasusBelliType.Conquest : claim.Type;
                var legitimacy = CasusBelli.Legitimacy(casus);

                var value = (ratio - 1f) * DiplomacyConstants.WarValuePerStrengthRatio
                            + legitimacy * DiplomacyConstants.WarValueLegitimacy
                            + Proximity(kingdom, target) * DiplomacyConstants.WarValueProximity
                            + LandHunger(kingdom) * DiplomacyConstants.WarValueLandHunger
                            - weariness * DiplomacyConstants.WarValueWearinessPenalty;

                value *= Settings.Current.AiAggressiveness;

                if (value <= bestValue) continue;
                best = target;
                bestValue = value;
                bestCasus = casus;
            }

            if (best == null) return false;
            if (bestValue < DiplomacyConstants.AiWarThreshold) return false;

            // Naked aggression costs double, and weariness adds to the bill.
            var legit = CasusBelli.Legitimacy(bestCasus);
            var cost = (int)(DiplomacyConstants.WarDeclarationBaseInfluence * (2f - legit)
                             * (1f + weariness / 100f));
            if (!CanAffordInfluence(kingdom, cost)) return false;

            ChangeClanInfluenceAction.Apply(kingdom.RulingClan, -cost);

            TreatyEnforcement.BeginSanctionedWar();
            try
            {
                DeclareWarAction.ApplyByKingdomDecision(kingdom, best);
            }
            finally
            {
                TreatyEnforcement.EndSanctionedWar();
            }

            Log.Info("AI", kingdom.Name + " declared war on " + best.Name
                           + " (" + bestCasus + ", legitimacy " + legit.ToString("0.00")
                           + ", value " + bestValue.ToString("0") + ", cost " + cost + " influence).");
            Announce(kingdom.Name + " declares war on " + best.Name + ".");
            return true;
        }

        /// <summary>
        /// How badly a kingdom wants land, 0..1: its share of the world's strength measured
        /// against its share of the world's fiefs. A realm that fields a third of Calradia's
        /// armies while holding a fifth of its towns is the one that starts the next war.
        ///
        /// This is the design's "ownAggressionToward" term, which the war valuation was
        /// missing: without it a kingdom's only reasons to fight were a claim it might not
        /// have and a strength advantage it might not have either.
        ///
        /// Note on its size: at campaign start Calradia is deliberately balanced, so every
        /// kingdom's strength share roughly equals its fief share and this term contributes
        /// almost nothing. It only bites once the map has skewed - which is the right time
        /// for it to bite, but it also means it cannot be tuned from a fresh save.
        /// </summary>
        public static float LandHunger(Kingdom kingdom)
        {
            var totalStrength = 0f;
            var totalFiefs = 0;
            foreach (var other in Kingdom.All)
            {
                if (other.IsEliminated) continue;
                totalStrength += other.CurrentTotalStrength;
                totalFiefs += CountFortifications(other);
            }
            if (totalStrength <= 0f || totalFiefs <= 0) return 0f;

            var strengthShare = kingdom.CurrentTotalStrength / totalStrength;
            var fiefShare = (float)CountFortifications(kingdom) / totalFiefs;

            var hunger = strengthShare - fiefShare;
            if (hunger <= 0f) return 0f;

            // Scale so a realm carrying twice the armies its holdings warrant reads as 1.
            var scaled = hunger / (fiefShare <= 0f ? strengthShare : fiefShare);
            return scaled > 1f ? 1f : scaled;
        }

        private static int CountFortifications(Kingdom kingdom)
        {
            var count = 0;
            var settlements = kingdom.Settlements;
            for (var i = 0; i < settlements.Count; i++)
                if (settlements[i].IsFortification) count++;
            return count;
        }

        /// <summary>
        /// The war valuation for one pair, term by term, with every gate it has to clear.
        /// Built for diagnosis: when a kingdom will not act, this says which line stopped it
        /// rather than leaving it to be inferred from an absence of behaviour.
        /// </summary>
        public static string ExplainWarValue(ModState state, Kingdom us, Kingdom them)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(us.Name + " considering war on " + them.Name);

            if (us.IsAtWarWith(them)) { sb.AppendLine("  already at war."); return sb.ToString(); }

            var block = TreatyEnforcement.WhyWarBlocked(state, us, them);
            sb.AppendLine("  enforcement:   " + (block == TreatyEnforcement.Block.None
                ? "allowed"
                : "BLOCKED - " + TreatyEnforcement.Explain(state, us, them, block)));

            var worstExhaustion = WorstExhaustion(state, us);
            sb.AppendLine("  our exhaustion: " + worstExhaustion.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxExhaustionToExpand.ToString("0") + ")"
                          + (worstExhaustion > DiplomacyConstants.AiMaxExhaustionToExpand ? "   BLOCKED" : ""));

            var weariness = state.WearinessOf(us);
            sb.AppendLine("  our weariness:  " + weariness.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxWearinessToExpand.ToString("0") + ")"
                          + (weariness > DiplomacyConstants.AiMaxWearinessToExpand ? "   BLOCKED" : ""));

            var theirs = them.CurrentTotalStrength;
            var ratio = theirs <= 0f ? 0f : us.CurrentTotalStrength / theirs;
            sb.AppendLine("  strength ratio: " + ratio.ToString("0.00")
                          + " (must be >= " + DiplomacyConstants.AiWarStrengthRatio.ToString("0.00") + ")"
                          + (ratio < DiplomacyConstants.AiWarStrengthRatio ? "   BLOCKED" : ""));

            var claim = ClaimRegistry.Best(state, us, them);
            var casus = claim == null ? CasusBelliType.Conquest : claim.Type;
            var legitimacy = CasusBelli.Legitimacy(casus);
            var proximity = Proximity(us, them);
            var hunger = LandHunger(us);

            var fromRatio = (ratio - 1f) * DiplomacyConstants.WarValuePerStrengthRatio;
            var fromLegit = legitimacy * DiplomacyConstants.WarValueLegitimacy;
            var fromProx = proximity * DiplomacyConstants.WarValueProximity;
            var fromHunger = hunger * DiplomacyConstants.WarValueLandHunger;
            var fromWeary = -weariness * DiplomacyConstants.WarValueWearinessPenalty;

            var raw = fromRatio + fromLegit + fromProx + fromHunger + fromWeary;
            var scaled = raw * Settings.Current.AiAggressiveness;

            sb.AppendLine("  casus belli:    " + casus + " (legitimacy " + legitimacy.ToString("0.00") + ")");
            sb.AppendLine("  value from strength advantage: " + fromRatio.ToString("0.0"));
            sb.AppendLine("  value from legitimacy:         " + fromLegit.ToString("0.0"));
            sb.AppendLine("  value from proximity:          " + fromProx.ToString("0.0")
                          + "   (proximity " + proximity.ToString("0.00") + ")");
            sb.AppendLine("  value from land hunger:        " + fromHunger.ToString("0.0")
                          + "   (hunger " + hunger.ToString("0.00") + ")");
            sb.AppendLine("  penalty from weariness:        " + fromWeary.ToString("0.0"));
            sb.AppendLine("  total: " + raw.ToString("0.0")
                          + " x aggressiveness " + Settings.Current.AiAggressiveness.ToString("0.00")
                          + " = " + scaled.ToString("0.0")
                          + " (needs " + DiplomacyConstants.AiWarThreshold.ToString("0") + ")"
                          + (scaled < DiplomacyConstants.AiWarThreshold ? "   BLOCKED" : ""));

            var cost = (int)(DiplomacyConstants.WarDeclarationBaseInfluence * (2f - legitimacy)
                             * (1f + weariness / 100f));
            var available = us.RulingClan == null ? 0f : us.RulingClan.Influence;
            sb.AppendLine("  influence cost: " + cost + ", available " + available.ToString("0")
                          + (available < cost ? "   BLOCKED" : ""));

            return sb.ToString();
        }

        // ================= shared helpers =======================================

        private static bool CanAffordInfluence(Kingdom kingdom, int cost)
            => cost <= 0 || (kingdom.RulingClan != null && kingdom.RulingClan.Influence >= cost);

        private static float WorstExhaustion(ModState state, Kingdom kingdom)
        {
            var worst = 0f;
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                var value = war.ExhaustionOf(kingdom);
                if (value > worst) worst = value;
            }
            return worst;
        }

        private static void Announce(string text)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, Colors.Cyan);
        }
    }
}
