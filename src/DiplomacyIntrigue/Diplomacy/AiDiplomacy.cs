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
            /// <summary>Asked a stronger kingdom for protection, as its vassal.</summary>
            Submitted = 5,
            /// <summary>Took a neglected vassal off a rival patron.</summary>
            PoachedVassal = 6,
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
            // Submission ranks with peace rather than with the pacts, because it is the same
            // kind of decision: a realm that cannot survive alone is not shopping, it is
            // looking for a protector. Ahead of war for the same reason - a kingdom about to
            // kneel has no business starting a fight.
            if (TrySubmit(state, kingdom)) return Move.Submitted;

            if (TryDeclareWar(state, kingdom)) return Move.DeclaredWar;
            if (TryDemandTribute(state, kingdom)) return Move.DemandedTribute;

            // Courting somebody else's vassal comes before ordinary pacts: it is worth more
            // than a non-aggression pact and it is only ever available for a moment.
            if (Hegemony.TryPoach(state, kingdom)) return Move.PoachedVassal;

            if (TryOfferPact(state, kingdom)) return Move.OfferedPact;

            return Move.None;
        }

        // ================= 1. Get out of the worst war =========================

        /// <summary>
        /// Negotiates an exit from the war that is costing us most.
        ///
        /// Which side of the table we sit on is decided first, and that is the part this
        /// originally got wrong: a kingdom past its exhaustion threshold always *offered*,
        /// even when it was the one ahead on points, and always offered a white peace first.
        /// A realm that is winning but worn out should collect what the war earned, not hand
        /// it back.
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
            if (worstExhaustion < DiplomacyConstants.ExhaustionSeekPeace)
                return TryEndDormantWar(state, kingdom);

            var enemy = worst.Other(kingdom);
            if (enemy == null || enemy.IsEliminated) return false;

            return worst.ScoreFor(kingdom) > DiplomacyConstants.PeaceWhitePeaceOnlyBelow
                ? TryCollectPeace(state, worst, kingdom, enemy, worstExhaustion)
                : TryBuyPeace(state, worst, kingdom, enemy, worstExhaustion);
        }

        /// <summary>
        /// Closes a war nobody is fighting.
        ///
        /// Needed because taking peace from vanilla removed something vanilla was quietly
        /// doing for us. Exhaustion from elapsed time alone is 0.08/day, so a war between
        /// kingdoms that never meet would need 750 days to reach the negotiating threshold -
        /// and with no vanilla peace left, it would simply stay open. Run 03 would have
        /// measured a map slowly filling with wars nobody was fighting.
        ///
        /// Only ever a white peace: indifference concedes nothing, so there is nothing here
        /// for a patient winner to extract by waiting.
        /// </summary>
        private static bool TryEndDormantWar(ModState state, Kingdom kingdom)
        {
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                if (!PeaceTable.IsDormant(war)) continue;

                var enemy = war.Other(kingdom);
                if (enemy == null || enemy.IsEliminated) continue;

                var white = new PeaceTerms(enemy, kingdom);
                if (!PeaceTable.BothWouldSign(state, war, white, out _)) continue;
                if (!PeaceTable.Apply(state, war, white, out _, Telemetry.PeaceCause.Dormant)) continue;

                Log.Info("AI", kingdom.Name + " and " + enemy.Name + " let a dormant war lapse after "
                               + war.DaysElapsed.ToString("0") + " days and "
                               + war.TotalCasualties
                               + " casualties between them.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// We are behind or level: offer a white peace, and if the other side is winning
        /// enough to refuse it, concede upward - the cheapest package they will take, never
        /// past what their victory entitles them to.
        /// </summary>
        private static bool TryBuyPeace(ModState state, WarRecord war, Kingdom kingdom,
            Kingdom enemy, float exhaustion)
        {
            var white = new PeaceTerms(enemy, kingdom);
            if (PeaceTable.BothWouldSign(state, war, white, out _)
                && PeaceTable.Apply(state, war, white, out _))
            {
                Log.Info("AI", kingdom.Name + " sued for peace with " + enemy.Name
                               + " at exhaustion " + exhaustion.ToString("0.0") + ": white peace.");
                return true;
            }

            foreach (var terms in ConcessionLadder(state, war, enemy, kingdom))
            {
                if (!PeaceTable.IsDemandable(state, war, terms, out _)) continue;
                if (!PeaceTable.BothWouldSign(state, war, terms, out _)) continue;

                if (PeaceTable.Apply(state, war, terms, out _))
                {
                    Log.Info("AI", kingdom.Name + " bought peace from " + enemy.Name
                                   + " at exhaustion " + exhaustion.ToString("0.0") + ": " + terms + ".");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// We are ahead but worn out: take the most the war has earned that they will
        /// actually sign, and fall back to a white peace only when nothing is collectable.
        ///
        /// The ladder is walked in reverse - largest package first - because the same list
        /// serves both sides of the table. Offering, you want the cheapest thing they will
        /// accept; collecting, the dearest thing they will bear.
        /// </summary>
        private static bool TryCollectPeace(ModState state, WarRecord war, Kingdom kingdom,
            Kingdom enemy, float exhaustion)
        {
            var packages = new List<PeaceTerms>(ConcessionLadder(state, war, kingdom, enemy));
            for (var i = packages.Count - 1; i >= 0; i--)
            {
                var terms = packages[i];
                if (!PeaceTable.IsDemandable(state, war, terms, out _)) continue;
                if (!PeaceTable.BothWouldSign(state, war, terms, out _)) continue;

                if (PeaceTable.Apply(state, war, terms, out _))
                {
                    Log.Info("AI", kingdom.Name + " imposed terms on " + enemy.Name
                                   + " at exhaustion " + exhaustion.ToString("0.0")
                                   + " (war score " + war.ScoreFor(kingdom).ToString("0")
                                   + "): " + terms + ".");
                    return true;
                }
            }

            var white = new PeaceTerms(kingdom, enemy);
            if (PeaceTable.BothWouldSign(state, war, white, out _)
                && PeaceTable.Apply(state, war, white, out _))
            {
                Log.Info("AI", kingdom.Name + " let " + enemy.Name + " go at exhaustion "
                               + exhaustion.ToString("0.0")
                               + ": nothing collectable, white peace.");
                return true;
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

            // Submission: the dearest thing on the ladder at 90, and the only rung that
            // changes what the loser *is* rather than what it owns. Offered ahead of land
            // because a realm intact under a patron will usually prefer that to being
            // carved up - and because it is what makes the winner a hegemon.
            yield return new PeaceTerms(winner, loser)
            {
                ReleasePrisoners = true,
                ImposeVassalage = true,
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

        // ================= 1b. Submit, when there is no other way out ===========

        /// <summary>
        /// Asks a stronger kingdom for protection, as its vassal.
        ///
        /// Deliberately restricted: a kingdom that already answers to someone cannot submit
        /// twice, and one that holds vassals of its own cannot submit at all. The engine has
        /// two faction tiers and no parent-of-kingdom slot, so a chain of patrons is
        /// unrepresentable - hegemony stays flat, one patron per vassal (design 04 §1.2).
        /// </summary>
        private static bool TrySubmit(ModState state, Kingdom kingdom)
        {
            if (Hegemony.VassalageOf(state, kingdom) != null) return false;
            if (Hegemony.IsHegemon(state, kingdom)) return false;

            Kingdom best = null;
            var bestValue = 0f;
            string bestExplanation = null;

            foreach (var patron in Kingdom.All)
            {
                if (patron == kingdom || patron.IsEliminated) continue;
                if (patron.IsAtWarWith(kingdom)) continue;
                if (Hegemony.VassalageOf(state, patron) != null) continue;

                if (!TreatyRegistry.CanSign(state, patron, kingdom, TreatyType.Vassalage, out _)) continue;

                var value = Hegemony.SubmissionValue(state, kingdom, patron, out var explanation);
                if (value <= bestValue) continue;

                best = patron;
                bestValue = value;
                bestExplanation = explanation;
            }

            if (best == null) return false;
            if (bestValue < DiplomacyConstants.AiSubmissionThreshold) return false;

            // A player patron is asked rather than told. Everywhere else in this file the
            // player is treated exactly as the AI is; here the asymmetry is the point,
            // because accepting a vassal is a decision with consequences and the player
            // should get to refuse it.
            if (best.Leader == Hero.MainHero)
            {
                AskPlayerToAcceptSubmission(state, kingdom, best, bestValue);
                return true;
            }

            var treaty = Hegemony.Submit(state, best, kingdom,
                DiplomacyConstants.HoldOnVoluntarySubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason);

            if (treaty == null)
            {
                Log.Debug("AI", kingdom.Name + " could not submit to " + best.Name + ": " + reason);
                return false;
            }

            Log.Info("Hegemony", kingdom.Name + " submitted to " + best.Name
                                 + " as a vassal. " + bestExplanation);
            Announce(kingdom.Name + " submits to " + best.Name + " in exchange for protection.");
            return true;
        }

        /// <summary>
        /// Puts a submission offer in front of the player. Accepting makes them a hegemon.
        /// </summary>
        private static void AskPlayerToAcceptSubmission(ModState state, Kingdom candidate,
            Kingdom patron, float value)
        {
            var body = candidate.Name + " asks to become our vassal: tribute of "
                       + DiplomacyConstants.AiDefaultTributePerPeriod + " per period, troops in our wars, "
                       + "and their foreign policy answers to us." + System.Environment.NewLine
                       + System.Environment.NewLine
                       + "In exchange we are expected to defend them. A patron who does not"
                       + " loses the vassal, and a vassal that resents us will eventually revolt.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Offer of submission",
                    body,
                    true, true, "Accept", "Refuse",
                    () =>
                    {
                        var treaty = Hegemony.Submit(state, patron, candidate,
                            DiplomacyConstants.HoldOnVoluntarySubmission,
                            DiplomacyConstants.AiDefaultTributePerPeriod, out var reason);

                        if (treaty == null)
                        {
                            Log.Notify("Could not accept the submission: " + reason, Colors.Red);
                            return;
                        }

                        Log.Notify(candidate.Name + " is now our vassal.", Colors.Green);
                        Log.Info("Hegemony", candidate.Name + " submitted to the player's kingdom "
                                             + patron.Name + " (value " + value.ToString("0") + ").");
                    },
                    () =>
                    {
                        Log.Info("Hegemony", "The player refused " + candidate.Name + "'s submission.");
                        TrustRegistry.Adjust(state, candidate, patron, -5f, "refused our submission");
                    }), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("Hegemony", "Could not show the submission offer.", ex);
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

        /// <summary>
        /// 1 for neighbours, 0 for opposite ends of the map. Public because the hegemony
        /// cascade cap calls its vassals nearest the target first, and two measures of
        /// "nearby" that disagree would be one bug waiting to happen.
        /// </summary>
        public static float Proximity(Kingdom a, Kingdom b)
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
            // One war of our own at a time. Wars we were dragged into by a treaty do not
            // count against this - they were not our decision, and a kingdom already
            // honouring an obligation may still pursue its own quarrel.
            //
            // Without this the rate is set purely by the value threshold, and run 02 shows
            // where that lands: 1.4 chosen wars per kingdom per year, which only worked
            // because vanilla ended every war in six days. Fixing peace without fixing the
            // rate would trade one broken world for another.
            if (ChosenWarCount(state, kingdom) >= DiplomacyConstants.AiMaxConcurrentChosenWars) return false;

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

                var terms = EvaluateWar(state, kingdom, target);
                if (terms.Ratio < DiplomacyConstants.AiWarStrengthRatio) continue;

                if (terms.Total <= bestValue) continue;
                best = target;
                bestValue = terms.Total;
                bestCasus = terms.Casus;
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
        /// Every term of the war valuation for one pair, kept together so the decision and
        /// the diagnostic read the same numbers. <see cref="TryDeclareWar"/> acts on
        /// <see cref="Total"/>; <see cref="ExplainWarValue"/> prints these fields. They were
        /// two separate copies of the formula until 2026-09-16, which is the arrangement
        /// that let a term drift without the diagnostic noticing.
        /// </summary>
        public struct WarValueTerms
        {
            public float Ratio;
            public CasusBelliType Casus;
            public float Legitimacy;
            public float Proximity;
            public float Hunger;
            public float Weariness;

            public bool AdvantageCapped;
            public float FromRatio;
            public float FromLegitimacy;
            public float FromProximity;
            public float FromHunger;
            public float FromWeariness;

            /// <summary>Before the player's aggressiveness setting.</summary>
            public float Raw;
            /// <summary>What the threshold is compared against.</summary>
            public float Total;
        }

        /// <summary>
        /// What a war on <paramref name="them"/> is worth to <paramref name="us"/>. Gates are
        /// not applied here - the caller decides what to do with a value - but every term is.
        /// </summary>
        public static WarValueTerms EvaluateWar(ModState state, Kingdom us, Kingdom them)
        {
            var terms = new WarValueTerms();

            var theirs = them.CurrentTotalStrength;
            terms.Ratio = theirs <= 0f ? 0f : us.CurrentTotalStrength / theirs;

            var claim = ClaimRegistry.Best(state, us, them);
            terms.Casus = claim == null ? CasusBelliType.Conquest : claim.Type;
            terms.Legitimacy = CasusBelli.Legitimacy(terms.Casus);
            terms.Proximity = Proximity(us, them);
            terms.Hunger = LandHunger(us);
            terms.Weariness = state.WearinessOf(us);

            // The strength advantage is the only term with no natural ceiling, and run 03
            // shows what that costs: Vlandia valued a war on a beaten-down Northern Empire
            // at 199, of which at least 138 was this term, against 85 as the most that
            // legitimacy, proximity and land hunger can contribute together. An unbounded
            // term does not change *whether* a war happens - the threshold is a floor - but
            // it decides *which* target is chosen, so once any kingdom is weak every other
            // reason to fight becomes noise. That is the shape of run 03's Vlandia: four of
            // its five declarations were Conquest at legitimacy 0.20.
            //
            // Capped at twice our strength. Past that a war is already as one-sided as a
            // decision needs to know, and the cap leaves a decisive advantage worth about
            // as much as a good claim across a shared border rather than more than
            // everything else put together.
            var advantage = terms.Ratio - 1f;
            if (advantage > DiplomacyConstants.WarValueMaxStrengthAdvantage)
            {
                advantage = DiplomacyConstants.WarValueMaxStrengthAdvantage;
                terms.AdvantageCapped = true;
            }

            terms.FromRatio = advantage * DiplomacyConstants.WarValuePerStrengthRatio;
            terms.FromLegitimacy = terms.Legitimacy * DiplomacyConstants.WarValueLegitimacy;
            terms.FromProximity = terms.Proximity * DiplomacyConstants.WarValueProximity;
            terms.FromHunger = terms.Hunger * DiplomacyConstants.WarValueLandHunger;
            terms.FromWeariness = -terms.Weariness * DiplomacyConstants.WarValueWearinessPenalty;

            terms.Raw = terms.FromRatio + terms.FromLegitimacy + terms.FromProximity
                        + terms.FromHunger + terms.FromWeariness;
            terms.Total = terms.Raw * Settings.Current.AiAggressiveness;
            return terms;
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

            // Listed first among the gates because it is the one that will most often be the
            // answer, and a diagnostic that omits the binding constraint is worse than none.
            var chosen = ChosenWarCount(state, us);
            sb.AppendLine("  chosen wars:    " + chosen
                          + " (must be < " + DiplomacyConstants.AiMaxConcurrentChosenWars + ")"
                          + (chosen >= DiplomacyConstants.AiMaxConcurrentChosenWars ? "   BLOCKED" : ""));

            var worstExhaustion = WorstExhaustion(state, us);
            sb.AppendLine("  our exhaustion: " + worstExhaustion.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxExhaustionToExpand.ToString("0") + ")"
                          + (worstExhaustion > DiplomacyConstants.AiMaxExhaustionToExpand ? "   BLOCKED" : ""));

            var weariness = state.WearinessOf(us);
            sb.AppendLine("  our weariness:  " + weariness.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxWearinessToExpand.ToString("0") + ")"
                          + (weariness > DiplomacyConstants.AiMaxWearinessToExpand ? "   BLOCKED" : ""));

            // Same resolver the decision uses, so this cannot describe a formula the AI
            // does not run.
            var terms = EvaluateWar(state, us, them);

            sb.AppendLine("  strength ratio: " + terms.Ratio.ToString("0.00")
                          + " (must be >= " + DiplomacyConstants.AiWarStrengthRatio.ToString("0.00") + ")"
                          + (terms.Ratio < DiplomacyConstants.AiWarStrengthRatio ? "   BLOCKED" : ""));

            sb.AppendLine("  casus belli:    " + terms.Casus
                          + " (legitimacy " + terms.Legitimacy.ToString("0.00") + ")");
            sb.AppendLine("  value from strength advantage: " + terms.FromRatio.ToString("0.0")
                          + (terms.AdvantageCapped
                              ? "   (capped at ratio "
                                + (1f + DiplomacyConstants.WarValueMaxStrengthAdvantage).ToString("0.00") + ")"
                              : ""));
            sb.AppendLine("  value from legitimacy:         " + terms.FromLegitimacy.ToString("0.0"));
            sb.AppendLine("  value from proximity:          " + terms.FromProximity.ToString("0.0")
                          + "   (proximity " + terms.Proximity.ToString("0.00") + ")");
            sb.AppendLine("  value from land hunger:        " + terms.FromHunger.ToString("0.0")
                          + "   (hunger " + terms.Hunger.ToString("0.00") + ")");
            sb.AppendLine("  penalty from weariness:        " + terms.FromWeariness.ToString("0.0"));
            sb.AppendLine("  total: " + terms.Raw.ToString("0.0")
                          + " x aggressiveness " + Settings.Current.AiAggressiveness.ToString("0.00")
                          + " = " + terms.Total.ToString("0.0")
                          + " (needs " + DiplomacyConstants.AiWarThreshold.ToString("0") + ")"
                          + (terms.Total < DiplomacyConstants.AiWarThreshold ? "   BLOCKED" : ""));

            var cost = (int)(DiplomacyConstants.WarDeclarationBaseInfluence * (2f - terms.Legitimacy)
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

        /// <summary>
        /// Wars this kingdom chose, as opposed to ones a treaty dragged it into.
        /// </summary>
        private static int ChosenWarCount(ModState state, Kingdom kingdom)
        {
            var count = 0;
            foreach (var war in state.OngoingWarsOf(kingdom))
                if (!war.IsObligationWar) count++;
            return count;
        }

        private static void Announce(string text)
        {
            if (!Settings.Current.AnnounceAiDecisions) return;
            Log.Notify(text, Colors.Cyan);
        }
    }
}
