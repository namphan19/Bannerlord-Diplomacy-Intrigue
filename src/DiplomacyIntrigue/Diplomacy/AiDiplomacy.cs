using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using DiplomacyIntrigue.Statecraft;
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
            /// <summary>A neglected vassal knelt to the kingdom attacking it.</summary>
            Defected = 7,
        }

        /// <summary>
        /// Runs one kingdom's weekly evaluation and returns what it did. Priority order
        /// matters: a realm that needs out of a war does not go shopping for allies.
        /// </summary>
        public static Move Evaluate(ModState state, Kingdom kingdom)
        {
            var move = EvaluateCore(state, kingdom);
            if (kingdom != null) LastMoves[kingdom] = move;
            return move;
        }

        /// <summary>
        /// The move each kingdom made at its most recent weekly evaluation, for the weekly
        /// telemetry. Session-scoped: it describes what the AI just did, not the campaign, and
        /// <see cref="ResetSession"/> empties it.
        /// </summary>
        private static readonly Dictionary<Kingdom, Move> LastMoves = new Dictionary<Kingdom, Move>();

        public static string LastMove(Kingdom kingdom)
            => kingdom != null && LastMoves.TryGetValue(kingdom, out var move) ? move.ToString() : "unevaluated";

        private static Move EvaluateCore(ModState state, Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated || kingdom.RulingClan == null) return Move.None;

            // Getting out of a losing war still comes first - nothing else matters while a
            // realm is being ground down.
            if (TrySeekPeace(state, kingdom)) return Move.SoughtPeace;

            // A vassal abandoned to its attacker defects to it - ahead of voluntary
            // submission, which it cannot take while it still has a patron.
            if (TryDefectToAttacker(state, kingdom)) return Move.Defected;

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

                // A signature the formula cannot make for the player is an offer
                // instead; one refused recently is not sent again, and the quiet war
                // carries on either way.
                if (IsPlayerRuled(enemy))
                {
                    if (TrustRegistry.RefusedOfferRecently(state, kingdom, enemy)) continue;
                    return OfferPeaceToPlayer(state, war, kingdom, enemy, white);
                }

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
            // A player who turned this kingdom's peace down recently is not asked again
            // yet; the war carries on and the week looks elsewhere for a move.
            if (IsPlayerRuled(enemy) && TrustRegistry.RefusedOfferRecently(state, kingdom, enemy))
                return false;

            var white = new PeaceTerms(enemy, kingdom);
            if (PeaceTable.BothWouldSign(state, war, white, out _))
            {
                if (IsPlayerRuled(enemy))
                    return OfferPeaceToPlayer(state, war, kingdom, enemy, white);

                if (PeaceTable.Apply(state, war, white, out _))
                {
                    Log.Info("AI", kingdom.Name + " sued for peace with " + enemy.Name
                                   + " at exhaustion " + exhaustion.ToString("0.0") + ": white peace.");
                    return true;
                }
            }

            foreach (var terms in ConcessionLadder(state, war, enemy, kingdom))
            {
                if (!PeaceTable.IsDemandable(state, war, terms, out _)) continue;
                if (!PeaceTable.BothWouldSign(state, war, terms, out _)) continue;

                if (IsPlayerRuled(enemy))
                    return OfferPeaceToPlayer(state, war, kingdom, enemy, terms);

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
            // Same refusal window as the buying path: a player who turned this
            // kingdom's offer down recently is left in their war, not asked again.
            if (IsPlayerRuled(enemy) && TrustRegistry.RefusedOfferRecently(state, kingdom, enemy))
                return false;

            var packages = new List<PeaceTerms>(ConcessionLadder(state, war, kingdom, enemy));
            for (var i = packages.Count - 1; i >= 0; i--)
            {
                var terms = packages[i];
                if (!PeaceTable.IsDemandable(state, war, terms, out _)) continue;
                if (!PeaceTable.BothWouldSign(state, war, terms, out _)) continue;

                if (IsPlayerRuled(enemy))
                    return OfferPeaceToPlayer(state, war, kingdom, enemy, terms);

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
            if (PeaceTable.BothWouldSign(state, war, white, out _))
            {
                if (IsPlayerRuled(enemy))
                    return OfferPeaceToPlayer(state, war, kingdom, enemy, white);

                if (PeaceTable.Apply(state, war, white, out _))
                {
                    Log.Info("AI", kingdom.Name + " let " + enemy.Name + " go at exhaustion "
                                   + exhaustion.ToString("0.0")
                                   + ": nothing collectable, white peace.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The one kingdom the formula may not sign for: a player-ruled realm is put the
        /// offer instead. Everything up to this point is identical to the AI-vs-AI path -
        /// same packages, same willingness math - only the signature itself is handed over.
        /// </summary>
        private static bool IsPlayerRuled(Kingdom kingdom)
            => Hero.MainHero != null && kingdom.Leader == Hero.MainHero;

        /// <summary>
        /// Puts the package the AI just picked in front of the player. BothWouldSign still
        /// decided which package was worth sending; the player's half of it is then asked
        /// rather than computed, so a refusal is a real choice with a cost - the war goes
        /// on and the refusal is remembered - not a rounding of the formula.
        ///
        /// The peace table screen (the mockup's board 3b) shows the package read-only;
        /// if that screen cannot come up the plain inquiry below still asks the same
        /// question, because a lost offer is a war nobody chose.
        /// </summary>
        private static bool OfferPeaceToPlayer(ModState state, WarRecord war, Kingdom offerer,
            Kingdom player, PeaceTerms terms)
        {
            try
            {
                UI.Negotiation.PeaceTablePopup.ShowIncoming(state, war, player, offerer, terms,
                    () => AcceptPeaceOffer(state, war, offerer, player, terms),
                    () => RefusePeaceOffer(state, war, offerer, player, terms));
            }
            catch (System.Exception ex)
            {
                Log.Error("AI", "The peace table could not open; falling back to the inquiry.", ex);
                OfferPeaceToPlayerInquiry(state, war, offerer, player, terms);
            }

            // The week's move was spent putting the offer on the table: the only
            // signature left is the player's.
            return true;
        }

        /// <summary>Accept half of the offer: re-checked, then applied.</summary>
        private static void AcceptPeaceOffer(ModState state, WarRecord war, Kingdom offerer,
            Kingdom player, PeaceTerms terms)
        {
            // Re-asked rather than trusted: the table sat open while the rest of the
            // week's evaluation ran, so both signatures are checked again before
            // anything is signed in our name.
            if (!PeaceTable.BothWouldSign(state, war, terms, out var lapsed))
            {
                Log.Notify("The moment has passed - " + lapsed, Colors.Red);
                return;
            }

            if (!PeaceTable.Apply(state, war, terms, out var failed))
            {
                Log.Notify("Could not make peace: " + failed, Colors.Red);
                return;
            }

            Log.Notify("Peace signed with " + offerer.Name + ": " + terms + ".",
                Colors.Green);
            Log.Info("AI", "The player accepted " + offerer.Name
                           + "'s peace offer: " + terms + ".");
        }

        /// <summary>Refuse half of the offer: the war goes on and the answer is remembered.</summary>
        private static void RefusePeaceOffer(ModState state, WarRecord war, Kingdom offerer,
            Kingdom player, PeaceTerms terms)
        {
            Log.Info("AI", "The player refused " + offerer.Name
                           + "'s peace offer (" + terms + ").");
            TrustRegistry.OnOfferRefused(state, offerer, player,
                "refused our peace offer");
            Log.Notify("We refused " + offerer.Name
                       + "'s terms. The war continues.", Colors.Red);
        }

        /// <summary>The pre-screen inquiry, kept as the fallback path.</summary>
        private static void OfferPeaceToPlayerInquiry(ModState state, WarRecord war,
            Kingdom offerer, Kingdom player, PeaceTerms terms)
        {
            var weAreLoser = terms.Loser == player;

            var body = "At war for " + war.DaysElapsed.ToString("0") + " days over "
                       + war.Justification + "." + System.Environment.NewLine
                       + "They are " + ExhaustionBands.Describe(war.ExhaustionOf(offerer)) + "."
                       + System.Environment.NewLine + System.Environment.NewLine
                       + (terms.IsWhitePeace
                           ? "They ask for a white peace: the war simply ends and nothing"
                             + " changes hands."
                           : weAreLoser
                               ? "Their terms, worth " + PeaceTable.CostOf(terms).ToString("0")
                                 + " against what this war has earned them: "
                                 + terms + "."
                               : "They offer, worth " + PeaceTable.CostOf(terms).ToString("0")
                                 + " against the " + PeaceTable.BudgetFor(war, player).ToString("0")
                                 + " this war has earned us: " + terms + ".")
                       + System.Environment.NewLine + System.Environment.NewLine
                       + "Refusing keeps the war going, and they will remember the answer.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    weAreLoser ? "Their terms for peace" : "Peace offer from " + offerer.Name,
                    body,
                    true, true,
                    terms.IsWhitePeace ? "Make peace" : "Accept these terms", "Refuse",
                    () =>
                    {
                        // Runs from the UI, outside the try below - a throw here would take the
                        // game down with it.
                        try
                        {
                            // Re-asked rather than trusted: the inquiry sat open while the
                            // rest of the week's evaluation ran, so both signatures are
                            // checked again before anything is signed in our name.
                            if (!PeaceTable.BothWouldSign(state, war, terms, out var lapsed))
                            {
                                Log.Notify("The moment has passed - " + lapsed, Colors.Red);
                                return;
                            }

                            if (!PeaceTable.Apply(state, war, terms, out var failed))
                            {
                                Log.Notify("Could not make peace: " + failed, Colors.Red);
                                return;
                            }

                            Log.Notify("Peace signed with " + offerer.Name + ": " + terms + ".",
                                Colors.Green);
                            Log.Info("AI", "The player accepted " + offerer.Name
                                           + "'s peace offer: " + terms + ".");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("AI", "Accepting the peace offer failed.", ex);
                        }
                    },
                    () =>
                    {
                        try
                        {
                            Log.Info("AI", "The player refused " + offerer.Name
                                           + "'s peace offer (" + terms + ").");
                            TrustRegistry.OnOfferRefused(state, offerer, player,
                                "refused our peace offer");
                            Log.Notify("We refused " + offerer.Name
                                       + "'s terms. The war continues.", Colors.Red);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("AI", "Refusing the peace offer failed.", ex);
                        }
                    }), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("AI", "Could not show the peace offer.", ex);
            }
        }

        /// <summary>
        /// Packages we would offer to end a war, cheapest first. Prisoners cost nothing
        /// strategically; submission costs everything and comes last, after the land.
        ///
        /// The order carries both directions of the table: the loser walks it forward and takes
        /// the first package the winner signs, the winner walks it backward and takes the
        /// dearest the loser will bear. So "cheapest first" is also "what a winner asks for
        /// last", and a rung in the wrong place breaks the direction nobody was reading.
        /// </summary>
        private static IEnumerable<PeaceTerms> ConcessionLadder(ModState state, WarRecord war,
            Kingdom winner, Kingdom loser)
        {
            var prisoners = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
            yield return prisoners;

            // Money, sized by what the war earned rather than by what the treasury holds. This
            // offered half the ruler's gold until 2026-09-20 and was therefore never once
            // demandable: run 07 settled 100 wars with **zero** indemnities, because half of a
            // late-game treasury prices out at thousands of concession points against a budget
            // that never passed 226. PeaceTable owns the sizing so the ladder and the
            // allowance readout quote the same figure.
            var indemnity = PeaceTable.LargestIndemnity(war, winner, loser);
            if (indemnity >= 1000)
                yield return new PeaceTerms(winner, loser)
                {
                    ReleasePrisoners = true,
                    IndemnityGold = indemnity,
                };

            yield return new PeaceTerms(winner, loser)
            {
                ReleasePrisoners = true,
                ImposeTributaryPact = true,
                TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod,
            };

            // Land, and only what they could take anyway. A castle before a town.
            foreach (var fief in CedeCandidates(loser, townsFirst: false))
            {
                var terms = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
                terms.FiefsCeded.Add(fief);
                yield return terms;
            }

            // Subjugation last: the dearest rung, and the only one that changes what the loser
            // *is* rather than what it owns. One rung with two faces, at one price
            // (DiplomacyConstants.PeaceCostSubjugation) - a hegemon gives up its sphere because
            // it cannot give up its independence, everybody else gives up the independence.
            //
            // Last in the list is first in the reverse walk, which is the direction a winner
            // collecting takes. Until 2026-09-20 submission sat ahead of the fiefs under a
            // comment claiming it was "offered ahead of land"; that was true only of the
            // loser's direction, and the loser could not reach it anyway. Since the demand is
            // now a cliff at this rung's cost (PeaceTable.MinimumAcceptable), both directions
            // stop here on the same score and the ordering no longer decides the outcome.
            if (Hegemony.IsHegemon(state, loser))
            {
                yield return new PeaceTerms(winner, loser)
                {
                    ReleasePrisoners = true,
                    DissolveHegemony = true,
                };
            }
            else if (WouldTakeVassals(state, winner))
            {
                // Not offered by a greedy winner, which wants the land itself. A greedy winner
                // facing a hegemon still takes the sphere apart: that costs it nothing to hold.
                yield return new PeaceTerms(winner, loser)
                {
                    ReleasePrisoners = true,
                    ImposeVassalage = true,
                    TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod,
                };
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
            var bestSettlesWar = false;

            foreach (var patron in Kingdom.All)
            {
                if (!patron.IsRealm()) continue;
                if (!CanSubmitTo(state, kingdom, patron, out _, out var settlesWar)) continue;

                var value = Hegemony.SubmissionValue(state, kingdom, patron, out var explanation);
                if (value <= bestValue) continue;

                best = patron;
                bestValue = value;
                bestExplanation = explanation;
                bestSettlesWar = settlesWar;
            }

            if (best == null) return false;
            if (bestValue < DiplomacyConstants.AiSubmissionThreshold) return false;

            // A player patron is asked rather than told. Everywhere else in this file the
            // player is treated exactly as the AI is; here the asymmetry is the point,
            // because accepting a vassal is a decision with consequences and the player
            // should get to refuse it.
            if (best.Leader == Hero.MainHero)
            {
                AskPlayerToAcceptSubmission(state, kingdom, best, bestValue, bestSettlesWar);
                return true;
            }

            // The oath ends the war it settles, and it has to end first: Hegemony.Submit calls
            // the new patron into the wars its vassal is defending the same day, and without
            // this one of those would be the war against the patron itself.
            if (bestSettlesWar) SettleBySubmission(kingdom, best);

            var treaty = Hegemony.Submit(state, best, kingdom,
                bestSettlesWar
                    ? DiplomacyConstants.HoldOnDesperateSubmission
                    : DiplomacyConstants.HoldOnVoluntarySubmission,
                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason,
                route: bestSettlesWar ? "submitted_to_attacker" : "voluntary",
                value: bestValue, detail: bestExplanation, settlesWar: bestSettlesWar);

            if (treaty == null)
            {
                // The peace is already made if it was going to be - CanSign agreed a moment
                // ago, so landing here means the two checks have drifted apart.
                Log.Warn("Hegemony", kingdom.Name + " could not submit to " + best.Name + ": " + reason);
                return false;
            }

            if (bestSettlesWar)
            {
                Log.Info("Hegemony", kingdom.Name + " had no patron to appeal to and knelt to its"
                                     + " attacker " + best.Name + ". " + bestExplanation);
                Announce(kingdom.Name + " kneels to " + best.Name + " to end the war.");
            }
            else
            {
                Log.Info("Hegemony", kingdom.Name + " submitted to " + best.Name
                                     + " as a vassal. " + bestExplanation);
                Announce(kingdom.Name + " submits to " + best.Name + " in exchange for protection.");
            }
            return true;
        }

        /// <summary>
        /// Whether <paramref name="candidate"/> could kneel to <paramref name="patron"/> right
        /// now: every structural gate a submission has to pass, without the valuation. The AI
        /// asks it of every kingdom and keeps the best-scoring survivor; the UI asks it of the
        /// one the player picked, and gets the reason when the answer is no.
        ///
        /// Kneeling to one of our own attackers is allowed, and for a kingdom whose every
        /// neighbour is already fighting it, it is the only door left open
        /// (design/04 §12.4.4). The oath *is* the peace, so it signs as a settlement
        /// (<paramref name="settlesWar"/>) and CanSign's war bar and trust floor step aside
        /// exactly as they do for a term agreed at the peace table.
        /// </summary>
        public static bool CanSubmitTo(ModState state, Kingdom candidate, Kingdom patron,
            out string reason, out bool settlesWar)
        {
            reason = null;
            settlesWar = false;

            if (candidate == null || patron == null || candidate == patron
                || candidate.IsEliminated || patron.IsEliminated)
            {
                reason = "Two living, different kingdoms are required.";
                return false;
            }
            if (Hegemony.VassalageOf(state, candidate) != null)
            {
                reason = candidate.Name + " already answers to a patron.";
                return false;
            }
            if (Hegemony.IsHegemon(state, candidate))
            {
                reason = candidate.Name + " holds vassals of its own and cannot kneel.";
                return false;
            }
            // Checked here rather than left to the valuation returning zero, so the player
            // is told *why* instead of being shown a barren number.
            if (!Hegemony.IsStrongEnoughToHold(patron, candidate))
            {
                reason = patron.Name + " is no stronger than " + candidate.Name
                         + " and has no protection to offer.";
                return false;
            }

            settlesWar = patron.IsAtWarWith(candidate);

            // CanSign also refuses a patron that is itself a vassal - hegemony is flat. It
            // is what makes the design's "kneel to the hegemon, never to its vassal" hold
            // without a rule of its own: an attacker that answers to somebody else simply
            // cannot take this, and the candidate looks past it to the hegemon.
            if (!TreatyRegistry.CanSign(state, patron, candidate, TreatyType.Vassalage, out reason,
                    settlesWar: settlesWar)) return false;

            // A player who turned us down recently is not asked again yet; we look elsewhere.
            if (patron.Leader == Hero.MainHero
                && TrustRegistry.RefusedOfferRecently(state, candidate, patron))
            {
                reason = patron.Name + " refused " + candidate.Name + "'s oath too recently to be asked again.";
                return false;
            }

            // A greedy AI ruler no longer wants vassals. A player patron is still asked -
            // taking a vassal is their decision, and the candidate's own dread of a greedy
            // patron is already in its valuation.
            if (patron.Leader != Hero.MainHero && !WouldTakeVassals(state, patron))
            {
                reason = patron.Name + " wants land, not vassals.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Ends the war the oath settles, tagged so a balance run can tell this ending from an
        /// ordinary one at the peace table. Nothing is conceded here beyond the submission
        /// itself - the vassalage *is* the term.
        /// </summary>
        internal static void SettleBySubmission(Kingdom candidate, Kingdom patron)
        {
            var previousCause = Telemetry.NotePeaceCause(Telemetry.PeaceCause.Submission, "vassalage");
            try
            {
                MakePeaceAction.Apply(patron, candidate);
            }
            finally
            {
                Telemetry.RestorePeaceCause(previousCause);
            }
        }

        /// <summary>
        /// Puts a submission offer in front of the player. Accepting makes them a hegemon.
        /// </summary>
        private static void AskPlayerToAcceptSubmission(ModState state, Kingdom candidate,
            Kingdom patron, float value, bool settlesWar)
        {
            var body = candidate.Name + " asks to become our vassal: tribute of "
                       + DiplomacyConstants.AiDefaultTributePerPeriod + " per period, troops in our wars, "
                       + "and their foreign policy answers to us." + System.Environment.NewLine
                       + System.Environment.NewLine
                       + (settlesWar
                           ? "They are asking the kingdom that is beating them. Accepting ends our"
                             + " war with them at once, on no other terms." + System.Environment.NewLine
                             + System.Environment.NewLine
                           : "")
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
                        // Runs from the UI, outside the try below - a throw here would take the
                        // game down with it.
                        try
                        {
                            // Re-asked rather than trusted: an inquiry sits open while the rest
                            // of the week's evaluation runs, so by now the war may be over, the
                            // candidate may have knelt elsewhere or a new treaty may forbid
                            // this. The defection inquiry learned that the expensive way.
                            var stillAtWar = patron.IsAtWarWith(candidate);
                            if (!TreatyRegistry.CanSign(state, patron, candidate,
                                    TreatyType.Vassalage, out var blocked, settlesWar: stillAtWar))
                            {
                                Log.Notify("Could not accept the submission: " + blocked, Colors.Red);
                                return;
                            }

                            if (stillAtWar) SettleBySubmission(candidate, patron);

                            var treaty = Hegemony.Submit(state, patron, candidate,
                                stillAtWar
                                    ? DiplomacyConstants.HoldOnDesperateSubmission
                                    : DiplomacyConstants.HoldOnVoluntarySubmission,
                                DiplomacyConstants.AiDefaultTributePerPeriod, out var reason,
                                route: stillAtWar ? "submitted_to_attacker_player" : "voluntary_to_player",
                                value: value, settlesWar: stillAtWar);

                            if (treaty == null)
                            {
                                Log.Notify("Could not accept the submission: " + reason, Colors.Red);
                                return;
                            }

                            Log.Notify(candidate.Name + " is now our vassal.", Colors.Green);
                            Log.Info("Hegemony", candidate.Name + " submitted to the player's kingdom "
                                                 + patron.Name + " (value " + value.ToString("0")
                                                 + (stillAtWar ? ", ending our war" : "") + ").");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Accepting the submission failed.", ex);
                        }
                    },
                    () =>
                    {
                        try
                        {
                            Log.Info("Hegemony", "The player refused " + candidate.Name + "'s submission.");
                            TrustRegistry.OnOfferRefused(state, candidate, patron, "refused our submission");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Refusing the submission failed.", ex);
                        }
                    }), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("Hegemony", "Could not show the submission offer.", ex);
            }
        }

        // ================= 1c. Defect to the attacker =========================

        /// <summary>
        /// A vassal left to fight alone kneels to the kingdom attacking it - the lead's F3
        /// answer to run 06, where a patron barred from defending (a truce with the
        /// aggressor, the one-step call-to-arms guard) simply watched its vassal be eaten
        /// and paid nothing for it.
        ///
        /// Runs after the ordinary peace routes on purpose: a vassal that can buy its way
        /// out of the war does that first; this is the exit for one whose patron will not
        /// come. The patron's *absence* is the precondition - a patron at war with the
        /// aggressor is defending, whatever the war is going like, and there is nothing to
        /// defect from.
        /// </summary>
        private static bool TryDefectToAttacker(ModState state, Kingdom kingdom)
        {
            var link = Hegemony.VassalageOf(state, kingdom);
            if (link == null) return false;
            var patron = link.DominantParty;
            if (patron == null || patron.IsEliminated) return false;

            // Only a link already slipping comes apart this way. The neglect that qualifies
            // is the same neglect Hold measures: a patron ignoring the war pulls the
            // protection term to -1 and Hold drifts down past this line on its own, so the
            // gate needs no bookkeeping of its own - and it hands the patron a grace of
            // however much Hold it had banked to join late and keep its vassal.
            if (Hegemony.HoldOf(link) >= DiplomacyConstants.HoldPassiveResistanceThreshold) return false;

            WarRecord worst = null;
            Kingdom aggressor = null;
            var worstScore = 0f;
            foreach (var war in state.OngoingWarsOf(kingdom))
            {
                if (!DefectionOptionQualifies(state, kingdom, patron, war,
                        out var candidate, out _)) continue;

                var score = war.ScoreFor(kingdom);
                if (worst != null && score >= worstScore) continue;

                worst = war;
                aggressor = candidate;
                worstScore = score;
            }

            if (worst == null) return false;
            if (!CanDefectTo(state, kingdom, link, aggressor, out _)) return false;

            // As with voluntary submission, a player is asked rather than told.
            if (aggressor.Leader == Hero.MainHero)
            {
                AskPlayerToAcceptDefection(state, kingdom, link, patron, aggressor);
                return true;
            }

            ExecuteDefection(state, kingdom, link, patron, aggressor);
            return true;
        }

        /// <summary>
        /// Whether this war gives <paramref name="vassal"/> grounds to kneel to its
        /// aggressor. The per-war gates of the defection scan, shared with the UI so the
        /// button greys out for exactly the reason the AI would not take it.
        ///
        /// Neglect means a war the vassal was *attacked* in - the same scope the patron's
        /// protection duty has (Hegemony.Protection). A war the vassal started itself is not
        /// the patron's to answer, so losing one is no grounds.
        /// </summary>
        private static bool DefectionOptionQualifies(ModState state, Kingdom vassal, Kingdom patron,
            WarRecord war, out Kingdom candidate, out string reason)
        {
            candidate = war?.Aggressor;
            reason = null;

            if (war == null || war.Defender != vassal)
            {
                candidate = null;
                reason = vassal.Name + " is not defending a war against them.";
                return false;
            }
            if (candidate == null || candidate.IsEliminated || candidate == patron)
            {
                candidate = null;
                reason = "The attacker cannot take us.";
                return false;
            }
            if (patron.IsAtWarWith(candidate))
            {
                reason = patron.Name + " has come to our defence - there is nothing to defect from.";
                return false;
            }

            // A player who turned this vassal down recently is not asked again yet, so
            // that war is no way out for now.
            if (candidate.Leader == Hero.MainHero
                && TrustRegistry.RefusedOfferRecently(state, vassal, candidate))
            {
                reason = candidate.Name + " refused our submission too recently to be asked again.";
                return false;
            }

            // And only when the war is actually going against it: a vassal holding its
            // own has no need to kneel to the enemy.
            if (-war.ScoreFor(vassal) < DiplomacyConstants.DefectionLosingScore)
            {
                reason = "the war is not going badly enough to justify it (score "
                         + war.ScoreFor(vassal).ToString("0") + ", needs -"
                         + DiplomacyConstants.DefectionLosingScore.ToString("0") + ").";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a vassal that <paramref name="aggressor"/> is beating could defect to it
        /// right now - the defection decision asked of one chosen pair instead of the whole
        /// map. The vassal's side of the table: a slipping bond, a defensive war being lost,
        /// a patron not fighting it - then <see cref="CanDefectTo"/> asks the attacker's side.
        /// </summary>
        public static bool CanDefectToAttacker(ModState state, Kingdom vassal, Kingdom aggressor,
            out string reason, out Treaty link)
        {
            link = Hegemony.VassalageOf(state, vassal);
            reason = null;

            if (link == null || !link.IsActive)
            {
                reason = vassal == null ? "No vassal was named." : vassal.Name + " has no patron to abandon.";
                return false;
            }
            var patron = link.DominantParty;
            if (patron == null || patron.IsEliminated)
            {
                reason = "The patron no longer exists.";
                return false;
            }
            if (Hegemony.HoldOf(link) >= DiplomacyConstants.HoldPassiveResistanceThreshold)
            {
                reason = patron.Name + " still holds our loyalty (hold " + Hegemony.HoldOf(link).ToString("0")
                         + "; defection needs it under "
                         + DiplomacyConstants.HoldPassiveResistanceThreshold.ToString("0") + ").";
                return false;
            }

            var war = state.OngoingWarBetween(vassal, aggressor);
            if (!DefectionOptionQualifies(state, vassal, patron, war, out _, out reason)) return false;

            return CanDefectTo(state, vassal, link, aggressor, out reason);
        }

        /// <summary>
        /// Whether <paramref name="aggressor"/> could take <paramref name="vassal"/> off its
        /// patron right now. The attacker's side of the table: a vassal is worth taking only
        /// if it can be held, a greedy attacker wants the land itself rather than a client on
        /// it, and the bond has to be one the two could actually sign - the same gates every
        /// other route into vassalage passes, with the old link set aside the way the
        /// poaching route sets it aside.
        ///
        /// Asked when the vassal decides and again when a player accepts. An inquiry stays
        /// open while the rest of the week's evaluation runs, so by the time the player
        /// answers the war may be over, the old bond gone or the patron in the field; the
        /// first version of the accept path made peace regardless.
        /// </summary>
        public static bool CanDefectTo(ModState state, Kingdom vassal, Treaty link, Kingdom aggressor,
            out string reason)
        {
            reason = null;
            var patron = link?.DominantParty;
            if (link == null || !link.IsActive || link.SubordinateParty != vassal || patron == null)
            {
                reason = vassal.Name + " no longer answers to the patron it was leaving.";
                return false;
            }
            if (aggressor == null || aggressor.IsEliminated || vassal.IsEliminated)
            {
                reason = "One of the kingdoms no longer exists.";
                return false;
            }
            if (!aggressor.IsAtWarWith(vassal))
            {
                reason = "The war it offered to end is already over.";
                return false;
            }
            if (patron.IsAtWarWith(aggressor))
            {
                reason = patron.Name + " has come to " + vassal.Name + "'s defence.";
                return false;
            }
            if (!Hegemony.IsStrongEnoughToHold(aggressor, vassal))
            {
                reason = aggressor.Name + " is not strong enough to hold " + vassal.Name + ".";
                return false;
            }
            if (aggressor.Leader != Hero.MainHero && !WouldTakeVassals(state, aggressor))
            {
                reason = aggressor.Name + " wants land, not vassals.";
                return false;
            }
            return TreatyRegistry.CanSign(state, aggressor, vassal, TreatyType.Vassalage, out reason,
                replacing: link, settlesWar: true);
        }

        /// <summary>
        /// Ends the war as a submission: peace first - the same order the peace table uses -
        /// then the old bond broken at the patron's expense and the new one signed
        /// (<see cref="Hegemony.Defect"/>).
        /// </summary>
        internal static void ExecuteDefection(ModState state, Kingdom vassal, Treaty link,
            Kingdom patron, Kingdom aggressor)
        {
            var previousCause = Telemetry.NotePeaceCause(Telemetry.PeaceCause.Defection, "vassalage");
            try
            {
                MakePeaceAction.Apply(aggressor, vassal);
            }
            finally
            {
                Telemetry.RestorePeaceCause(previousCause);
            }

            var treaty = Hegemony.Defect(state, link, aggressor, out var reason);
            if (treaty == null)
            {
                // The war is over and the old bond is gone either way - CanSign said the new
                // one would sign, so reaching this means the two checks drifted apart.
                Log.Warn("Hegemony", vassal.Name + " left " + patron.Name + " for " + aggressor.Name
                                     + " but the new vassalage could not be signed: " + reason);
                return;
            }

            Log.Info("Hegemony", vassal.Name + " abandoned " + patron.Name
                                 + ", which would not defend it, and submitted to its attacker "
                                 + aggressor.Name + ".");
            Announce(vassal.Name + " abandons " + patron.Name + " and kneels to " + aggressor.Name + ".");
        }

        /// <summary>
        /// The same offer, put to the player when theirs is the attacking kingdom. As with
        /// submission, accepting a vassal is a decision with consequences, so the player
        /// gets to refuse it.
        /// </summary>
        private static void AskPlayerToAcceptDefection(ModState state, Kingdom vassal, Treaty link,
            Kingdom patron, Kingdom aggressor)
        {
            var body = vassal.Name + ", a vassal of " + patron.Name + ", offers to end the war"
                       + " by submitting to us: tribute of " + DiplomacyConstants.AiDefaultTributePerPeriod
                       + " per period, troops in our wars, and their foreign policy answers to us."
                       + System.Environment.NewLine + System.Environment.NewLine
                       + "In exchange we are expected to defend them - the duty " + patron.Name
                       + " just failed, and every court will name it the oathbreaker here."
                       + " Refusing means the war goes on.";

            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    "Offer of submission",
                    body,
                    true, true, "Accept", "Refuse",
                    () =>
                    {
                        // Runs from the UI, outside the try below - a throw here would take the
                        // game down with it.
                        try
                        {
                            if (!CanDefectTo(state, vassal, link, aggressor, out var lapsed))
                            {
                                Log.Notify("The offer has lapsed: " + lapsed, Colors.Red);
                                Log.Info("Hegemony", vassal.Name + "'s offer to defect lapsed before the"
                                                     + " player answered: " + lapsed);
                                return;
                            }

                            ExecuteDefection(state, vassal, link, patron, aggressor);
                            if (Hegemony.PatronOf(state, vassal) == aggressor)
                                Log.Notify(vassal.Name + " is now our vassal.", Colors.Green);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Accepting the defection failed.", ex);
                        }
                    },
                    () =>
                    {
                        try
                        {
                            Log.Info("Hegemony", "The player refused " + vassal.Name + "'s defection.");
                            TrustRegistry.OnOfferRefused(state, vassal, aggressor, "refused our submission");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("Hegemony", "Refusing the defection failed.", ex);
                        }
                    }), true);
            }
            catch (System.Exception ex)
            {
                Log.Error("Hegemony", "Could not show the defection offer.", ex);
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
                if (other == kingdom || !other.IsRealm()) continue;
                if (kingdom.IsAtWarWith(other)) continue;

                var ourValue = PactValue(state, kingdom, other);
                var theirValue = PactValueWhenAsked(state, other, kingdom);
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
            var pactCost = StatecraftTerms.TreatyInfluenceCost(kingdom, bestType);
            if (!CanAffordInfluence(kingdom, pactCost)) return false;

            var treaty = TreatyRegistry.Sign(state, kingdom, best, bestType, out var reason);
            if (treaty == null)
            {
                Log.Debug("AI", kingdom.Name + " wanted a " + bestType + " with " + best.Name
                                + " but: " + reason);
                return false;
            }

            ChangeClanInfluenceAction.Apply(kingdom.RulingClan, -pactCost);
            SkillXp.PactSigned(kingdom, bestType);

            var pull = BalancingPull(state, kingdom, best, out var against);
            Telemetry.Event("ai_pact_signed", "kingdom", kingdom, "partner", best, "type", bestType,
                "mutualValue", bestValue, "balancingPull", pull * DiplomacyConstants.PactWeightBalancing,
                "against", against, "ambition", Power.Ambition(kingdom), "partnerAmbition", Power.Ambition(best));
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
            var balancing = BalancingPull(state, us, them, out _);
            var ambition = Power.Ambition(us);

            return DiplomacyConstants.PactWeightSharedThreat * sharedThreat
                   + DiplomacyConstants.PactWeightProximity * proximity
                   + DiplomacyConstants.PactWeightTrust * trust
                   - DiplomacyConstants.PactWeightAggression * aggression
                   + DiplomacyConstants.PactWeightRelation * relation
                   + DiplomacyConstants.PactWeightBalancing * balancing
                   - DiplomacyConstants.PactWeightAmbition * ambition;
        }

        /// <summary>
        /// What <paramref name="asked"/> makes of a pact <paramref name="proposer"/> puts to it:
        /// its own valuation, moved by the proposer's envoy (design 08 S-3). Only the side being
        /// asked is persuaded - the proposer already wants the pact. Every place a court decides on
        /// an offer reads this: the AI's weekly scan, the player's chooser and its button.
        /// </summary>
        public static float PactValueWhenAsked(ModState state, Kingdom asked, Kingdom proposer)
            => PactValue(state, asked, proposer) + StatecraftTerms.Persuasion(proposer);

        /// <summary>
        /// How much the strongest sphere neither kingdom belongs to outweighs the two of them
        /// together, 0..1, and which sphere that is.
        ///
        /// Symmetric in the pair, so both sides of a pact read the same pull. A vassal's
        /// sphere is its patron's, so a vassal is never drawn to balance against its own
        /// patron by this term - that is what Hold and revolt are for.
        /// </summary>
        public static float BalancingPull(ModState state, Kingdom us, Kingdom them, out Kingdom against)
        {
            against = null;
            if (state == null || us == null || them == null) return 0f;

            var ourHead = Hegemony.SphereHead(state, us);
            var theirHead = Hegemony.SphereHead(state, them);

            // Smoothed: whether a power is worth banding against is a judgment about what it
            // has become, not about where its armies happen to be this week (Power.cs).
            //
            // Both sides read the same measure. This counted two bare kingdoms while the threat
            // below was already a whole sphere, so a hegemon weighing a rival left its own
            // vassals off its own side and overstated the pull - two answers to "how strong is
            // this side", which is the bug rather than the symptom (CLAUDE.md §3). Two vassals
            // of one patron share a head, and it is counted once.
            var pair = ourHead == theirHead
                ? Power.SmoothedSphere(state, ourHead)
                : Power.SmoothedSphere(state, ourHead) + Power.SmoothedSphere(state, theirHead);
            if (pair <= 0f) return 0f;

            var strongest = 0f;
            foreach (var head in Kingdom.All)
            {
                if (!head.IsRealm() || head == ourHead || head == theirHead) continue;
                if (Hegemony.PatronOf(state, head) != null) continue;

                var strength = Power.SmoothedSphere(state, head);
                if (strength <= strongest) continue;
                strongest = strength;
                against = head;
            }

            var pull = strongest / pair - 1f;
            if (pull <= 0f) { against = null; return 0f; }
            return pull > 1f ? 1f : pull;
        }

        /// <summary>Fraction of our enemies that are also theirs. Common enemies bind.</summary>
        private static float SharedThreat(Kingdom us, Kingdom them)
        {
            var ours = 0;
            var shared = 0;
            foreach (var kingdom in Kingdom.All)
            {
                if (kingdom == us || !kingdom.IsRealm()) continue;
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
            foreach (var target in Kingdom.All)
            {
                if (!target.IsRealm()) continue;
                if (DemandTributeOf(state, kingdom, target)) return true;
            }

            return false;
        }

        /// <summary>
        /// One demand on one target: the body the weekly scan runs for each candidate, and
        /// what <c>diplomacy.test_demand_tribute</c> runs for a chosen pair, so a test of the
        /// player's inquiry goes through the code the AI actually runs.
        /// </summary>
        public static bool DemandTributeOf(ModState state, Kingdom kingdom, Kingdom target)
        {
            var terms = EvaluateTribute(state, kingdom, target);
            if (!terms.Allowed)
            {
                // Only the court's refusals are worth a line: every other gate is the ordinary
                // shape of the map, but a demand that would otherwise have been made and was
                // stopped by the target's own houses is what a balance run needs to count.
                if (terms.CourtBlocked)
                    Log.Info("AI", kingdom.Name + " would demand tribute of " + target.Name
                                   + ", but its court would not bear it: " + terms.CourtDetail + ".");
                return false;
            }

            if (IsPlayerRuled(target))
            {
                if (WhyPlayerNotAsked(state, kingdom, target) != null) return false;
                AskPlayerForTribute(state, kingdom, target, terms);
                // The week's move was spent making the demand: the only answer left is the
                // player's, the same as a peace offer.
                return true;
            }

            var treaty = TreatyRegistry.Sign(state, kingdom, target, TreatyType.TributaryPact,
                out var reason, tributePayer: target,
                tributeAmount: DiplomacyConstants.AiDefaultTributePerPeriod);

            if (treaty == null)
            {
                Log.Debug("AI", kingdom.Name + " could not impose tribute on " + target.Name + ": " + reason);
                return false;
            }

            SkillXp.TributeDemandAccepted(kingdom);
            Log.Info("AI", kingdom.Name + " imposed a tributary pact on " + target.Name + ".");
            Announce(target.Name + " agrees to pay tribute to " + kingdom.Name + ".");
            return true;
        }

        /// <summary>
        /// Whether <paramref name="kingdom"/> could impose tributary status on
        /// <paramref name="target"/> right now. The demand is coercion, not negotiation: a
        /// claim gives it a pretext, overwhelming strength makes refusal suicidal, and the
        /// target's trust in us and its own court are what the AI consults instead of an
        /// answer. The same gate the weekly scan runs, asked of the one pair the player picked.
        /// </summary>
        public static bool CanDemandTribute(ModState state, Kingdom kingdom, Kingdom target,
            out string reason)
        {
            var terms = EvaluateTribute(state, kingdom, target);
            reason = terms.Blocked;
            return terms.Allowed;
        }

        /// <summary>
        /// Every gate of a tribute demand, and the first that refused. One resolver:
        /// <see cref="CanDemandTribute"/>, the weekly scan, the player's inquiry and
        /// <c>diplomacy.tribute_value</c> all read this, so the diagnostic cannot describe a
        /// rule the AI does not run - the reason <see cref="EvaluateWar"/> exists.
        ///
        /// <paramref name="full"/> evaluates every gate even after one has refused, for the
        /// diagnostic. The decision stops at the first, because the court is the one gate that
        /// costs something to read.
        /// </summary>
        public static TributeDemandTerms EvaluateTribute(ModState state, Kingdom kingdom, Kingdom target,
            bool full = false)
        {
            var t = new TributeDemandTerms { Demander = kingdom, Target = target };

            if (state == null || kingdom == null || target == null || kingdom == target
                || kingdom.IsEliminated || target.IsEliminated)
            {
                t.Block("Two living, different kingdoms are required.");
                return t;
            }

            t.AtWar = kingdom.IsAtWarWith(target);
            if (t.AtWar)
                t.Block("The war with " + target.Name + " is the demand - end it at the peace table.");

            t.Exhaustion = WarExhaustion.Worst(state, kingdom);
            if (t.Exhaustion > DiplomacyConstants.AiMaxExhaustionToExpand)
                t.Block(kingdom.Name + " is too exhausted to press anyone (exhaustion "
                        + t.Exhaustion.ToString("0") + ").");

            t.HasClaim = ClaimRegistry.HasTerritorialClaim(state, kingdom, target);
            if (!t.HasClaim)
                t.Block(kingdom.Name + " holds no territorial claim on " + target.Name
                        + " - without one the demand is bare extortion.");

            t.TargetPatron = TreatyRegistry.PatronOf(state, target);
            if (t.TargetPatron != null)
                t.Block(target.Name + " already answers to another kingdom.");

            var theirs = target.CurrentTotalStrength;
            t.Ratio = theirs > 0f ? kingdom.CurrentTotalStrength / theirs : 0f;
            if (t.Ratio < DiplomacyConstants.AiTributeDemandStrengthRatio)
                t.Block(kingdom.Name + " is not strong enough to cow " + target.Name
                        + " (needs x" + DiplomacyConstants.AiTributeDemandStrengthRatio.ToString("0.0")
                        + " their strength).");

            // They submit only if the alternative looks worse than paying.
            t.Trust = TrustRegistry.Get(state, target, kingdom);
            if (t.Trust < DiplomacyConstants.TrustFloorForPacts)
                t.Block(target.Name + " does not trust " + kingdom.Name
                        + " enough to accept terms (trust " + t.Trust.ToString("0") + ").");

            if (t.Allowed || full)
            {
                // The court stands in for an AI crown's answer. A player crown answers for
                // itself, so there it only informs the inquiry - the verdict is still read, so
                // the player is shown the number an AI crown in the same seat would act on.
                t.Court = Intrigue.TributeCourt.Assess(state, target);
                t.CourtAnswers = !IsPlayerRuled(target);
                if (t.CourtAnswers && t.Court.Refuses)
                {
                    t.CourtDetail = t.Court.BreakingNames() + " would be left a defection risk, "
                                    + (t.Court.ShareBreaking * 100f).ToString("0") + "% of the court (refuses at "
                                    + (DiplomacyConstants.AiTributeCourtRefusalShare * 100f).ToString("0") + "%)";
                    // Bands, not figures: this reason reaches the player's button, and a rival
                    // court is shown only as bands (design 02 §9.1). "Ready to break" is the
                    // Encyclopedia's name for the same band (CourtBands).
                    if (t.Block(target.Name + "'s court would not bear it - paying would leave too many"
                                + " of its houses ready to break."))
                        t.CourtBlocked = true;
                }
            }

            // The pact itself still has to be signable - an existing tribute between us, or a
            // patron's claim on either side's foreign policy, refuses here. Checked inside the
            // gate rather than left to Sign, so the player's button carries the real reason
            // and the AI's scan skips the same target it would have failed on.
            if (t.Allowed || full)
            {
                t.Signable = TreatyRegistry.CanSign(state, kingdom, target, TreatyType.TributaryPact,
                    out var unsignable);
                t.SignReason = unsignable;
                if (!t.Signable) t.Block(unsignable);
            }

            return t;
        }

        /// <summary>
        /// A tribute demand for one pair, gate by gate, with the target's court house by house.
        /// Figures throughout: this is a diagnostic, not the rival court the player is shown.
        /// </summary>
        public static string ExplainTributeValue(ModState state, Kingdom us, Kingdom them)
        {
            var t = EvaluateTribute(state, us, them, full: true);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(us.Name + " considering a demand for tribute from " + them.Name);
            sb.AppendLine("  at war:         " + (t.AtWar ? "yes   BLOCKED" : "no"));
            sb.AppendLine("  our exhaustion: " + t.Exhaustion.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxExhaustionToExpand.ToString("0") + ")"
                          + (t.Exhaustion > DiplomacyConstants.AiMaxExhaustionToExpand ? "   BLOCKED" : ""));
            sb.AppendLine("  our claim:      " + (t.HasClaim ? "yes" : "none   BLOCKED"));
            sb.AppendLine("  their patron:   " + (t.TargetPatron == null ? "none" : t.TargetPatron.Name + "   BLOCKED"));
            sb.AppendLine("  strength ratio: " + t.Ratio.ToString("0.00")
                          + " (must be >= " + DiplomacyConstants.AiTributeDemandStrengthRatio.ToString("0.00") + ")"
                          + (t.Ratio < DiplomacyConstants.AiTributeDemandStrengthRatio ? "   BLOCKED" : ""));
            sb.AppendLine("  their trust:    " + t.Trust.ToString("0.0")
                          + " (must be >= " + DiplomacyConstants.TrustFloorForPacts.ToString("0") + ")"
                          + (t.Trust < DiplomacyConstants.TrustFloorForPacts ? "   BLOCKED" : ""));

            var court = t.Court;
            if (court == null)
                sb.AppendLine("  their court:    not read");
            else if (!court.Applies)
                sb.AppendLine("  their court:    silent - intrigue is switched off");
            else
            {
                sb.AppendLine("  their court:    " + (court.ShareBreaking * 100f).ToString("0.0")
                              + "% would be left a defection risk (refuses at "
                              + (DiplomacyConstants.AiTributeCourtRefusalShare * 100f).ToString("0") + "%"
                              + (court.CountedHouses ? ", counted by heads - no house holds influence" : ", by influence")
                              + ")"
                              + (!court.Refuses ? ""
                                  : t.CourtAnswers ? "   BLOCKED"
                                  : "   (a player crown answers for itself: shown, not applied)"));
                sb.AppendLine("      house                      loyalty -> if paying   weight   breaks");
                for (var i = 0; i < court.Houses.Count; i++)
                {
                    var h = court.Houses[i];
                    sb.AppendLine("      " + h.Clan.Name.ToString().PadRight(26)
                                  + h.Loyalty.ToString("0.0").PadLeft(7) + " -> "
                                  + h.Projected.ToString("0.0").PadLeft(6)
                                  + h.Weight.ToString("0").PadLeft(12)
                                  + (h.Breaks ? "   yes" : ""));
                }
                if (court.Houses.Count == 0) sb.AppendLine("      (no sworn house but the crown's own)");
            }

            sb.AppendLine("  pact signable:  " + (t.Signable ? "yes" : "no - " + t.SignReason));
            var notAsked = WhyPlayerNotAsked(state, us, them);
            sb.AppendLine(!t.Allowed
                ? "  verdict: refused - " + t.Blocked
                : !IsPlayerRuled(them)
                    ? "  verdict: the demand stands"
                    : notAsked == null
                        ? "  verdict: the demand stands - it would be put to the player"
                        : "  verdict: the demand stands, but it would not be sent now - " + notAsked);
            return sb.ToString();
        }

        /// <summary>
        /// Why a demand that clears every gate would still not be put to a player crown right
        /// now; null when it would be, or when the target is not player-ruled. The scan and
        /// <c>diplomacy.tribute_value</c> both read this - the diagnostic once reported "it
        /// would be put to the player" for a demand the scan was silently skipping.
        ///
        /// The refusal window is the one every offer to the player shares
        /// (<see cref="TrustRegistry.RefusedOfferRecently"/>): a kingdom whose peace, protection
        /// or tribute the player just turned down does not come back with another request
        /// the next week, whichever kind it was.
        /// </summary>
        public static string WhyPlayerNotAsked(ModState state, Kingdom kingdom, Kingdom target)
        {
            if (target == null || !IsPlayerRuled(target)) return null;
            if (_tributeAskPending) return "another demand for tribute is already in front of the player";
            if (TrustRegistry.RefusedOfferRecently(state, kingdom, target))
                return target.Name + " turned down an offer from " + kingdom.Name + " within the last "
                       + DiplomacyConstants.PlayerOfferRefusalCooldownDays.ToString("0") + " days";
            return null;
        }

        /// <summary>
        /// True while a tribute demand sits in front of the player, so a second kingdom
        /// evaluated in the same week does not stack another inquiry over it. Session state:
        /// cleared by <see cref="ResetSession"/>, since an answer lost with a closed campaign
        /// must not silence every future demand.
        /// </summary>
        private static bool _tributeAskPending;

        /// <summary>Drops what the last session left open. Called at session launch.</summary>
        public static void ResetSession()
        {
            _tributeAskPending = false;

            // Keyed on the last campaign's Kingdom objects. They never match the new ones
            // (MBObjectBase does not override Equals), so leaving them only held the previous
            // world in memory after every load.
            LastMoves.Clear();
        }

        /// <summary>
        /// The demand put to a player-ruled target. Before this, the scan signed the pact in the
        /// player's name: every other AI path that proposes something to a player-ruled realm
        /// hands the signature over (<see cref="IsPlayerRuled"/>), and this one had been missed.
        ///
        /// The player's own court is shown in full - it is their court (design 02 §9.1) - with
        /// the verdict an AI crown in the same seat would act on.
        /// </summary>
        private static void AskPlayerForTribute(ModState state, Kingdom demander, Kingdom player,
            TributeDemandTerms terms)
        {
            var nl = System.Environment.NewLine;
            var amount = DiplomacyConstants.AiDefaultTributePerPeriod;
            var body = demander.Name + " demands tribute: " + amount + " denars every "
                       + DiplomacyConstants.TributePeriodDays + " days for "
                       + DiplomacyConstants.TributaryPactYears + " years, and in return no war between us."
                       + nl + "They hold a claim on our land and field " + terms.Ratio.ToString("0.0")
                       + " times our strength." + nl + nl
                       + "Every sworn house of ours will resent paying for as long as the pact lasts.";

            var court = terms.Court;
            if (court != null && court.Applies && court.Houses.Count > 0)
            {
                var share = (court.ShareBreaking * 100f).ToString("0");
                var line = (DiplomacyConstants.AiTributeCourtRefusalShare * 100f).ToString("0");
                body += nl + (court.BreakingCount == 0
                    ? "No house would be left ready to break."
                    : "Paying would leave " + court.BreakingNames() + " ready to break: " + share
                      + "% of our court.")
                        + " " + (court.Refuses
                            ? "A crown that heeded its court would refuse - it does at " + line + "%."
                            : "A crown that heeded its court would pay - it refuses only at " + line + "%.");
            }

            body += nl + nl + "If we refuse, " + demander.Name + " will trust us less for it.";

            _tributeAskPending = true;
            try
            {
                InformationManager.ShowInquiry(new InquiryData(
                    demander.Name + " demands tribute",
                    body,
                    true, true,
                    "Pay the tribute", "Refuse",
                    () =>
                    {
                        // Runs from the UI, outside any campaign handler's try.
                        try
                        {
                            _tributeAskPending = false;
                            // Re-asked rather than trusted: the answer can come a while later,
                            // and the demand may no longer stand.
                            var fresh = EvaluateTribute(state, demander, player);
                            if (!fresh.Allowed)
                            {
                                Log.Notify("The moment has passed - " + fresh.Blocked, Colors.Red);
                                return;
                            }

                            var treaty = TreatyRegistry.Sign(state, demander, player, TreatyType.TributaryPact,
                                out var failed, tributePayer: player, tributeAmount: amount);
                            if (treaty == null)
                            {
                                Log.Notify("Could not agree: " + failed, Colors.Red);
                                return;
                            }

                            SkillXp.TributeDemandAccepted(demander);
                            Log.Info("AI", "The player accepted " + demander.Name + "'s demand for tribute: "
                                           + player.Name + " pays " + amount + " per period.");
                            Log.Notify("We pay tribute to " + demander.Name + ".", Colors.Red);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("AI", "Accepting the demand for tribute failed.", ex);
                        }
                    },
                    () =>
                    {
                        try
                        {
                            _tributeAskPending = false;
                            Log.Info("AI", "The player refused " + demander.Name + "'s demand for tribute.");
                            TrustRegistry.OnOfferRefused(state, demander, player, "refused our demand for tribute");
                            Log.Notify("We refused " + demander.Name + "'s demand.", Colors.Red);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("AI", "Refusing the demand for tribute failed.", ex);
                        }
                    }), true);
                Log.Info("AI", demander.Name + " demanded tribute of the player's realm, " + player.Name + ".");
            }
            catch (System.Exception ex)
            {
                _tributeAskPending = false;
                Log.Error("AI", "Could not show the demand for tribute.", ex);
            }
        }

        /// <summary>
        /// Every gate of one tribute demand, read once. <see cref="EvaluateTribute"/> fills it;
        /// the decision reads <see cref="Allowed"/> and <see cref="Blocked"/>, the diagnostic
        /// prints the rest. Same arrangement as <see cref="WarValueTerms"/>, for the same reason.
        /// </summary>
        public sealed class TributeDemandTerms
        {
            public Kingdom Demander;
            public Kingdom Target;

            /// <summary>The first gate that refused, worded for the player's button. Null when the demand stands.</summary>
            public string Blocked;

            public bool Allowed => Blocked == null;

            public bool AtWar;
            public float Exhaustion;
            public bool HasClaim;
            public Kingdom TargetPatron;
            public float Ratio;
            public float Trust;

            /// <summary>Null when an earlier gate refused and the evaluation was not asked for in full.</summary>
            public Intrigue.TributeCourtVerdict Court;

            /// <summary>False for a player-ruled target: the player answers, the court only informs.</summary>
            public bool CourtAnswers;

            /// <summary>The court was the first gate to refuse.</summary>
            public bool CourtBlocked;

            /// <summary>The court's refusal with its figures, for the log - never shown for a rival court.</summary>
            public string CourtDetail;

            public bool Signable;

            /// <summary>Why <c>TreatyRegistry.CanSign</c> refused, when it did.</summary>
            public string SignReason;

            /// <summary>Records a refusal; true when it is the first, so the caller can tell whose it was.</summary>
            internal bool Block(string reason)
            {
                if (Blocked != null) return false;
                Blocked = reason ?? "Not possible now.";
                return true;
            }
        }

        // ================= 4. War, as a last resort =============================

        /// <summary>
        /// The three gates of restraint every deliberate new war has to clear, whatever
        /// starts it. One resolver, because poaching a rival's vassal now means war too
        /// (design 04, the lead's call after run 04), and a kingdom that may not declare a
        /// war may not walk into one through the back door either.
        ///
        /// One war of our own at a time. Wars a treaty dragged us into do not count: they
        /// were not our decision, and a kingdom already honouring an obligation may still
        /// pursue its own quarrel. Without this the rate is set purely by the value
        /// threshold, and run 02 shows where that lands - 1.4 chosen wars per kingdom per
        /// year, which only worked because vanilla ended every war in six days.
        /// </summary>
        public static bool CanTakeOnAnotherWar(ModState state, Kingdom kingdom)
        {
            if (state == null || kingdom == null) return false;
            if (ChosenWarCount(state, kingdom) >= MaxChosenWars(kingdom)) return false;
            if (WarExhaustion.Worst(state, kingdom) > DiplomacyConstants.AiMaxExhaustionToExpand) return false;
            if (state.WearinessOf(kingdom) > DiplomacyConstants.AiMaxWearinessToExpand) return false;
            return true;
        }

        /// <summary>
        /// How many wars of its own choosing a kingdom's evaluation will run at once: one, or
        /// two for a kingdom holding <see cref="DiplomacyConstants.AiDominanceForSecondWar"/>
        /// even shares of the world's strength. Live - the lead's design: a ruler whose armies
        /// are full wants to use them.
        /// </summary>
        public static int MaxChosenWars(Kingdom kingdom)
            => DiplomacyConstants.AiMaxConcurrentChosenWars
               + (Power.Dominance(kingdom) >= DiplomacyConstants.AiDominanceForSecondWar ? 1 : 0);

        private static bool TryDeclareWar(ModState state, Kingdom kingdom)
        {
            if (!CanTakeOnAnotherWar(state, kingdom)) return false;

            Kingdom best = null;
            var bestValue = 0f;
            var bestAnnexation = false;
            var bestTerms = new WarValueTerms();

            foreach (var target in Kingdom.All)
            {
                if (target == kingdom || !target.IsRealm()) continue;
                if (kingdom.IsAtWarWith(target)) continue;

                // A war a treaty forbids is off the table - unless the treaty is our own
                // vassal's oath and we have grown greedy enough to tear it up. That is the only
                // way this evaluation ever breaks a treaty on purpose, and it is priced below.
                var annexation = false;
                if (!TreatyEnforcement.IsWarAllowed(state, kingdom, target))
                {
                    if (!Hegemony.CouldAnnex(state, kingdom, target)) continue;
                    annexation = true;
                }

                var terms = EvaluateWar(state, kingdom, target, annexation);
                if (terms.Ratio < DiplomacyConstants.AiWarStrengthRatio) continue;

                if (terms.Total <= bestValue) continue;
                best = target;
                bestValue = terms.Total;
                bestAnnexation = annexation;
                bestTerms = terms;
            }

            if (best == null) return false;
            if (bestValue < DiplomacyConstants.AiWarThreshold) return false;

            return ExecuteWarDeclaration(state, kingdom, best, bestTerms, bestAnnexation, bestValue);
        }

        /// <summary>
        /// What declaring this war costs the ruling clan: naked aggression pays double,
        /// legitimacy discounts it, and weariness inflates the bill. One formula, shared by
        /// the decision, its diagnostic and the player's buttons.
        ///
        /// <paramref name="proposer"/> is whoever puts the war to the realm - the ruler for the AI,
        /// the player's own hero when the player proposes it as a vassal - and only matters for
        /// Firebrand (design 08 A-2), vanilla's discount on initiating a decision, which the
        /// takeover had left reading nothing.
        /// </summary>
        public static int WarDeclarationCost(ModState state, Kingdom kingdom, float legitimacy, Hero proposer = null)
            => (int)(DiplomacyConstants.WarDeclarationBaseInfluence * (2f - legitimacy)
                     * (1f + (state == null ? 0f : state.WearinessOf(kingdom)) / 100f)
                     * Statecraft.StatecraftTerms.FirebrandFactor(proposer ?? kingdom?.Leader));

        /// <summary>
        /// The price of declaring war on <paramref name="them"/>, with the casus belli
        /// <see cref="EvaluateWar"/> would fight it on: the best claim held, or conquest. The
        /// player's Declare war button is charged this, so it pays what the AI pays and what
        /// <c>diplomacy.war_value</c> prints (design 08 A-1).
        /// </summary>
        public static int WarDeclarationCostAgainst(ModState state, Kingdom us, Kingdom them, Hero proposer = null)
        {
            var claim = state == null ? null : ClaimRegistry.Best(state, us, them);
            var casus = claim == null ? CasusBelliType.Conquest : claim.Type;
            return WarDeclarationCost(state, us, CasusBelli.Legitimacy(casus), proposer);
        }

        /// <summary>
        /// Pays the influence, tears up the oath first when the war is an annexation (or the
        /// declaration would be vetoed), and declares through the same sanctioned call every
        /// war in this mod goes through. Returns whether the war actually opened - a refused
        /// declaration must never be logged as one that happened (run 04).
        ///
        /// Shared with the player's annexation action so a greedy player-patron pays the same
        /// price and walks the same path the AI does.
        /// </summary>
        internal static bool ExecuteWarDeclaration(ModState state, Kingdom kingdom, Kingdom target,
            WarValueTerms terms, bool annexation, float value)
        {
            var legit = CasusBelli.Legitimacy(terms.Casus);
            var cost = WarDeclarationCost(state, kingdom, legit);
            if (!CanAffordInfluence(kingdom, cost)) return false;

            ChangeClanInfluenceAction.Apply(kingdom.RulingClan, -cost);

            // The oath goes first, with the full price of a breach, or the war would be vetoed.
            if (annexation) Hegemony.TurnOnVassal(state, kingdom, target);

            TreatyEnforcement.BeginSanctionedWar();
            try
            {
                DeclareWarAction.ApplyByKingdomDecision(kingdom, target);
            }
            finally
            {
                TreatyEnforcement.EndSanctionedWar();
            }

            // Read back rather than assumed, for the reason run 04 gave: a refused declaration
            // must never be logged as a war.
            var opened = kingdom.IsAtWarWith(target);
            Telemetry.Event("ai_war_declared", "kingdom", kingdom, "target", target, "opened", opened,
                "annexation", annexation, "casusBelli", terms.Casus, "legitimacy", legit,
                "value", value, "cost", cost, "sideRatio", terms.Ratio, "ownRatio", terms.OwnRatio,
                "ourSupport", terms.OurSupport, "theirSupport", terms.TheirSupport,
                "fromRatio", terms.FromRatio, "fromLegitimacy", terms.FromLegitimacy,
                "fromProximity", terms.FromProximity, "fromHunger", terms.FromHunger,
                "fromWeariness", terms.FromWeariness, "fromAmbition", terms.FromAmbition,
                "fromAnnexation", terms.FromAnnexation);
            Log.Info("AI", kingdom.Name + (opened ? " declared war on " : " tried and failed to declare war on ")
                           + target.Name
                           + (annexation ? " to annex its former vassal" : "")
                           + " (" + terms.Casus + ", legitimacy " + legit.ToString("0.00")
                           + ", value " + value.ToString("0") + ", cost " + cost + " influence).");
            if (opened)
                Announce(annexation
                    ? kingdom.Name + " turns on its vassal " + target.Name + " to annex it."
                    : kingdom.Name + " declares war on " + target.Name + ".");
            return opened;
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
            /// <summary>
            /// Our side against theirs, each kingdom plus the support it can expect
            /// (CallToArms.ExpectedSupport). What the gate and the strength term read.
            /// </summary>
            public float Ratio;
            /// <summary>Us against them alone - what Ratio was before alliances counted.</summary>
            public float OwnRatio;
            public float OurSupport;
            public float TheirSupport;
            public float Ambition;
            /// <summary>True when the target is our own vassal and this war means breaking its oath.</summary>
            public bool Annexation;
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
            public float FromAmbition;
            public float FromAnnexation;

            /// <summary>Before the player's aggressiveness setting.</summary>
            public float Raw;
            /// <summary>What the threshold is compared against.</summary>
            public float Total;
        }

        /// <summary>
        /// What a war on <paramref name="them"/> is worth to <paramref name="us"/>. Gates are
        /// not applied here - the caller decides what to do with a value - but every term is.
        /// </summary>
        public static WarValueTerms EvaluateWar(ModState state, Kingdom us, Kingdom them, bool asAnnexation = false)
        {
            var terms = new WarValueTerms { Annexation = asAnnexation };

            var ours = Power.Strength(us);
            var theirs = Power.Strength(them);
            terms.OwnRatio = theirs <= 0f ? 0f : ours / theirs;

            // Sides, not kingdoms. This used to weigh the target alone, so an alliance never
            // deterred anyone: a kingdom picked its victim as if the victim stood by itself and
            // discovered the coalition only when the call to arms went out. Counting the
            // support each side can expect is what makes a lone kingdom the natural target and
            // gives the weak a reason to stand together - the counterweight to ambition.
            terms.OurSupport = CallToArms.ExpectedSupport(state, us, them, principalWasAttacked: false);
            terms.TheirSupport = CallToArms.ExpectedSupport(state, them, us, principalWasAttacked: true);
            var theirSide = theirs + terms.TheirSupport;
            terms.Ratio = theirSide <= 0f ? 0f : (ours + terms.OurSupport) / theirSide;

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

            terms.Ambition = Power.Ambition(us);
            terms.FromAmbition = terms.Ambition * DiplomacyConstants.WarValueAmbition;

            if (asAnnexation)
                terms.FromAnnexation = Power.Greed(state, us) * DiplomacyConstants.AnnexGreedWeight
                                       - DiplomacyConstants.AnnexBreachPenalty;

            terms.Raw = terms.FromRatio + terms.FromLegitimacy + terms.FromProximity
                        + terms.FromHunger + terms.FromWeariness + terms.FromAmbition + terms.FromAnnexation;
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
                if (!other.IsRealm()) continue;
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
            var annexation = block != TreatyEnforcement.Block.None && Hegemony.CouldAnnex(state, us, them);
            sb.AppendLine("  enforcement:   " + (block == TreatyEnforcement.Block.None
                ? "allowed"
                : annexation
                    ? "our own vassal - greedy enough to tear up its oath and annex it"
                    : "BLOCKED - " + TreatyEnforcement.Explain(state, us, them, block)));

            // Listed first among the gates because it is the one that will most often be the
            // answer, and a diagnostic that omits the binding constraint is worse than none.
            var chosen = ChosenWarCount(state, us);
            var allowed = MaxChosenWars(us);
            sb.AppendLine("  chosen wars:    " + chosen
                          + " (must be < " + allowed + ", dominance " + Power.Dominance(us).ToString("0.00") + ")"
                          + (chosen >= allowed ? "   BLOCKED" : ""));

            var worstExhaustion = WarExhaustion.Worst(state, us);
            sb.AppendLine("  our exhaustion: " + worstExhaustion.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxExhaustionToExpand.ToString("0") + ")"
                          + (worstExhaustion > DiplomacyConstants.AiMaxExhaustionToExpand ? "   BLOCKED" : ""));

            var weariness = state.WearinessOf(us);
            sb.AppendLine("  our weariness:  " + weariness.ToString("0.0")
                          + " (must be <= " + DiplomacyConstants.AiMaxWearinessToExpand.ToString("0") + ")"
                          + (weariness > DiplomacyConstants.AiMaxWearinessToExpand ? "   BLOCKED" : ""));

            // Same resolver the decision uses, so this cannot describe a formula the AI
            // does not run.
            var terms = EvaluateWar(state, us, them, annexation);

            sb.AppendLine("  strength ratio: " + terms.Ratio.ToString("0.00")
                          + " sides (must be >= " + DiplomacyConstants.AiWarStrengthRatio.ToString("0.00") + ")"
                          + (terms.Ratio < DiplomacyConstants.AiWarStrengthRatio ? "   BLOCKED" : ""));
            sb.AppendLine("      alone " + terms.OwnRatio.ToString("0.00")
                          + "; support we expect " + terms.OurSupport.ToString("0")
                          + ", support they expect " + terms.TheirSupport.ToString("0"));

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
            sb.AppendLine("  value from ambition:           " + terms.FromAmbition.ToString("0.0")
                          + "   (ambition " + terms.Ambition.ToString("0.00") + ")");
            if (terms.Annexation)
                sb.AppendLine("  annexation (greed - breach):   " + terms.FromAnnexation.ToString("0.0")
                              + "   (greed " + Power.Greed(state, us).ToString("0.00") + ")");
            sb.AppendLine("  total: " + terms.Raw.ToString("0.0")
                          + " x aggressiveness " + Settings.Current.AiAggressiveness.ToString("0.00")
                          + " = " + terms.Total.ToString("0.0")
                          + " (needs " + DiplomacyConstants.AiWarThreshold.ToString("0") + ")"
                          + (terms.Total < DiplomacyConstants.AiWarThreshold ? "   BLOCKED" : ""));

            var cost = WarDeclarationCost(state, us, terms.Legitimacy);
            var available = us.RulingClan == null ? 0f : us.RulingClan.Influence;
            sb.AppendLine("  influence cost: " + cost
                          + (StatecraftTerms.FirebrandFactor(us.Leader) < 1f ? " (Firebrand, x0.75)" : "")
                          + ", available " + available.ToString("0")
                          + (available < cost ? "   BLOCKED" : ""));

            return sb.ToString();
        }

        // ================= shared helpers =======================================

        /// <summary>
        /// Whether a ruler still wants vassals at all. Below
        /// <see cref="DiplomacyConstants.GreedRefusesVassals"/> it does; above it, it wants
        /// provinces - no voluntary submissions, no poaching, no vassalage at its peace table.
        /// </summary>
        public static bool WouldTakeVassals(ModState state, Kingdom ruler)
            => Power.Greed(state, ruler) < DiplomacyConstants.GreedRefusesVassals;

        private static bool CanAffordInfluence(Kingdom kingdom, int cost)
            => cost <= 0 || (kingdom.RulingClan != null && kingdom.RulingClan.Influence >= cost);


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
