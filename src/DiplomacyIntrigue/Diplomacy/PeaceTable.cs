using System.Collections.Generic;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Turns peace from a yes/no into a negotiation.
    ///
    /// Two numbers decide different halves of it, and keeping them separate is the point:
    ///   war score  -> how much the winner may demand   (what is on the table)
    ///   exhaustion -> whether the loser will sign       (whether they take it)
    ///
    /// A side that is winning but worn out will accept a white peace. A side that is losing
    /// but fresh will refuse to be dismembered and fight on. Those two cases are the reason
    /// the mod has two numbers instead of one.
    ///
    /// **Design note - budget, not tiers.** The design doc originally listed demand tiers
    /// ("45-70: one castle OR tribute and prisoners"). That was replaced with a point
    /// budget: every demand costs war-score points and the package must fit inside what the
    /// war earned. It reproduces the same intent, removes the exclusive-or branches, and
    /// means a new demand type is one constant rather than a rewritten table.
    /// </summary>
    public static class PeaceTable
    {
        /// <summary>
        /// A war nobody is actually fighting: old enough to have gone somewhere, and almost no
        /// blood spilled.
        ///
        /// This exists because taking peace from vanilla exposed a gap vanilla had been
        /// quietly covering. Exhaustion accrues 0.08/day from elapsed time alone, so a war
        /// between kingdoms that never meet needs **750 days** to reach the threshold at which
        /// either side will negotiate - every real war in run 02 got there in about 90 days,
        /// but only because casualties did the work. With no vanilla peace left, distant wars
        /// would simply stay open, and run 03 would have measured a map filling up with wars
        /// nobody was fighting.
        ///
        /// It lives here rather than on <see cref="WarRecord"/> because it reads two
        /// constants, and the dependency rule is that Models may not reach up into the
        /// systems layer.
        /// </summary>
        /// <remarks>
        /// Two readings of "not being fought", because run 03 showed one was not enough. An
        /// absolute cap catches the war that never started; a rate catches the war that has
        /// been technically ongoing for sixteen months at a casualty a day. The second is the
        /// one that matters more: those wars cannot end, and a kingdom in one cannot sign
        /// anything, so they quietly turn the map into permanent war.
        /// </remarks>
        public static bool IsDormant(WarRecord war)
        {
            if (war == null) return false;

            var days = war.DaysElapsed;
            if (days < DiplomacyConstants.DormantWarDays) return false;

            if (war.TotalCasualties <= DiplomacyConstants.DormantWarCasualties) return true;

            return war.TotalCasualties / days < DiplomacyConstants.DormantWarCasualtiesPerDay;
        }

        /// <summary>What a given package costs against the winner's war-score budget.</summary>
        public static float CostOf(PeaceTerms terms)
        {
            if (terms == null) return 0f;

            var cost = 0f;
            for (var i = 0; i < terms.FiefsCeded.Count; i++)
            {
                var fief = terms.FiefsCeded[i];
                cost += fief.IsTown ? DiplomacyConstants.PeaceCostTown : DiplomacyConstants.PeaceCostCastle;
            }
            if (terms.ImposeVassalage) cost += DiplomacyConstants.PeaceCostVassalage;
            if (terms.ImposeTributaryPact) cost += DiplomacyConstants.PeaceCostTributaryPact;
            if (terms.ReleasePrisoners) cost += DiplomacyConstants.PeaceCostPrisoners;
            cost += terms.IndemnityGold / 1000f * DiplomacyConstants.PeaceCostPerThousandIndemnity;
            return cost;
        }

        /// <summary>
        /// Points the winner has earned in this war. Zero when they are not ahead, which is
        /// what makes a white peace the only option in a stalemate.
        /// </summary>
        public static float BudgetFor(WarRecord war, Kingdom winner)
        {
            var score = war.ScoreFor(winner);
            return score <= DiplomacyConstants.PeaceWhitePeaceOnlyBelow ? 0f : score;
        }

        /// <summary>
        /// Whether the winner is *allowed* to ask for this, independent of whether the
        /// loser would agree. Returns false with a player-facing reason.
        /// </summary>
        public static bool IsDemandable(ModState state, WarRecord war, PeaceTerms terms, out string reason)
        {
            reason = null;
            if (terms == null) { reason = "No terms."; return false; }
            if (terms.IsWhitePeace) return true;

            if (!war.Involves(terms.Winner) || !war.Involves(terms.Loser))
            {
                reason = "Those kingdoms are not in this war.";
                return false;
            }

            // Land changes hands only on a territorial claim. A war fought to avenge a raid
            // can extract money and prisoners but not provinces - this is what makes the
            // choice of casus belli matter beyond its influence cost.
            if (terms.FiefsCeded.Count > 0 && !ClaimRegistry.HasTerritorialClaim(state, terms.Winner, terms.Loser))
            {
                reason = terms.Winner.Name + " has no territorial claim against " + terms.Loser.Name
                         + ", so no land can be demanded. A war of conquest or a reclaimed"
                         + " ancestral holding would allow it.";
                return false;
            }

            for (var i = 0; i < terms.FiefsCeded.Count; i++)
            {
                var fief = terms.FiefsCeded[i];
                if (fief.MapFaction != terms.Loser)
                {
                    reason = fief.Name + " does not belong to " + terms.Loser.Name + ".";
                    return false;
                }
                if (!fief.IsFortification)
                {
                    reason = fief.Name + " is not a town or castle.";
                    return false;
                }
            }

            // Submission is the one demand that can be structurally impossible rather than
            // merely unaffordable: a kingdom has one patron or none.
            if (terms.ImposeVassalage)
            {
                var existingPatron = TreatyRegistry.PatronOf(state, terms.Loser);
                if (existingPatron != null && existingPatron != terms.Winner)
                {
                    reason = terms.Loser.Name + " already answers to " + existingPatron.Name
                             + ". That bond would have to be broken before ours could be made.";
                    return false;
                }
            }

            var budget = BudgetFor(war, terms.Winner);
            var cost = CostOf(terms);
            if (cost > budget)
            {
                reason = "This war has not earned that. Demanded " + cost.ToString("0")
                         + " against a war score of " + budget.ToString("0") + ".";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether the loser would sign. Willingness comes from exhaustion, moderated by
        /// how badly they are losing: a side being dismantled settles sooner than a side
        /// merely tired.
        /// </summary>
        public static bool WouldAccept(ModState state, WarRecord war, PeaceTerms terms, out string reason)
        {
            var loser = terms.Loser;
            var scoreAgainstThem = -war.ScoreFor(loser);
            var exhaustion = war.ExhaustionOf(loser);

            // Nobody defends a war they are not fighting. A dormant war (see
            // IsDormant) ends by mutual indifference, and only ever on white terms
            // - indifference concedes nothing, so there is nothing here for a winner to
            // extract by simply waiting.
            if (terms.IsWhitePeace && IsDormant(war)) { reason = null; return true; }

            var threshold = DiplomacyConstants.ExhaustionSeekPeace - scoreAgainstThem / 2f;
            if (exhaustion < threshold)
            {
                reason = loser.Name + " is not worn down enough: exhaustion "
                         + exhaustion.ToString("0.0") + " against a threshold of "
                         + threshold.ToString("0.0") + ".";
                return false;
            }

            if (terms.IsWhitePeace) { reason = null; return true; }

            // They concede roughly what their defeat justifies, plus a margin - nobody
            // signs away exactly the arithmetic, and the grace keeps the AI from rejecting
            // an offer over a rounding error.
            var tolerance = scoreAgainstThem * (1f + DiplomacyConstants.PeaceAcceptanceGrace);
            var cost = CostOf(terms);
            if (cost > tolerance)
            {
                reason = loser.Name + " will not concede that much: the package is worth "
                         + cost.ToString("0") + " against a tolerance of " + tolerance.ToString("0") + ".";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Whether the **winner** would sign. This half was missing, and its absence made the
        /// concession ladder unreachable.
        ///
        /// The bug, precisely: a kingdom past its exhaustion threshold offered a white peace,
        /// and the only willingness check - <see cref="WouldAccept"/> - asks whether
        /// <c>terms.Loser</c> would sign. In a white-peace offer the offering side *is* the
        /// loser, so it was asking itself, agreeing with itself, and taking a free peace. The
        /// winner never got a say. Run 02: 13 peace-table settlements, 13 white peaces, zero
        /// concessions in 13 in-game years.
        ///
        /// Three ways a winner signs: it earned nothing (a stalemate has nothing to collect),
        /// it is worn out itself, or the package is worth at least
        /// <see cref="DiplomacyConstants.PeaceWinnerMinimumShare"/> of what the war earned.
        /// </summary>
        public static bool WinnerWouldAccept(ModState state, WarRecord war, PeaceTerms terms, out string reason)
        {
            reason = null;
            if (terms == null) { reason = "No terms."; return false; }

            var winner = terms.Winner;
            var budget = BudgetFor(war, winner);

            // A stalemate entitles them to nothing, so a white peace is the whole of what is
            // on the table and refusing it would just prolong a war neither side is winning.
            if (budget <= 0f) return true;

            var exhaustion = war.ExhaustionOf(winner);
            if (exhaustion >= DiplomacyConstants.ExhaustionAcceptWhitePeaceWhenWinning) return true;

            var cost = CostOf(terms);
            var wanted = budget * DiplomacyConstants.PeaceWinnerMinimumShare;
            if (cost >= wanted) return true;

            reason = winner.Name + " is winning and will not settle for that: the package is worth "
                     + cost.ToString("0") + " against a war score of " + budget.ToString("0")
                     + ", and they are only at exhaustion " + exhaustion.ToString("0.0")
                     + " of the " + DiplomacyConstants.ExhaustionAcceptWhitePeaceWhenWinning.ToString("0")
                     + " that would make them stop caring.";
            return false;
        }

        /// <summary>
        /// Both signatures. Every route to peace - the AI's, the player's demand, the player's
        /// offer - goes through this, so no path can accidentally consult only one side again.
        /// </summary>
        public static bool BothWouldSign(ModState state, WarRecord war, PeaceTerms terms, out string reason)
            => WinnerWouldAccept(state, war, terms, out reason)
               && WouldAccept(state, war, terms, out reason);

        /// <summary>
        /// Signs the peace and executes the terms. Reads war score and exhaustion first,
        /// because making peace closes the war record.
        /// </summary>
        public static bool Apply(ModState state, WarRecord war, PeaceTerms terms, out string reason,
            Telemetry.PeaceCause cause = Telemetry.PeaceCause.PeaceTable)
        {
            if (!IsDemandable(state, war, terms, out reason)) return false;

            var winner = terms.Winner;
            var loser = terms.Loser;
            var summary = terms.ToString();

            // Peace first: it closes the war record, carries exhaustion into weariness, and
            // records the truce. The terms are then executed between kingdoms at peace,
            // which is what a ceded fief actually is.
            var previousCause = Telemetry.NotePeaceCause(cause, summary);
            try
            {
                MakePeaceAction.Apply(winner, loser);
            }
            finally
            {
                Telemetry.RestorePeaceCause(previousCause);
            }

            CedeFiefs(state, terms);
            PayIndemnity(terms);
            ReleaseHeroes(terms);
            ImposeTribute(state, terms);
            ImposeSubmission(state, terms);

            Log.Info("Peace", winner.Name + " and " + loser.Name + " made peace: " + summary + ".");
            return true;
        }

        private static void CedeFiefs(ModState state, PeaceTerms terms)
        {
            var newOwner = terms.Winner.Leader;
            if (newOwner == null) return;

            for (var i = 0; i < terms.FiefsCeded.Count; i++)
            {
                var fief = terms.FiefsCeded[i];
                ChangeOwnerOfSettlementAction.ApplyByDefault(newOwner, fief);

                // Land lost at a peace table is the oldest grievance there is. The ledger
                // already records the transfer; this makes the claim explicit so the loser
                // has something to want back rather than having to wait for a later war.
                ClaimRegistry.GrantAncestralClaim(state, terms.Loser, fief);
            }
        }

        private static void PayIndemnity(PeaceTerms terms)
        {
            if (terms.IndemnityGold <= 0) return;

            var payer = terms.Loser.Leader;
            var receiver = terms.Winner.Leader;
            if (payer == null || receiver == null) return;

            // Pay what they have if they cannot cover it. A peace that fails because the
            // treasury is short would just restart the war.
            var amount = terms.IndemnityGold < payer.Gold ? terms.IndemnityGold : payer.Gold;
            if (amount > 0) GiveGoldAction.ApplyBetweenCharacters(payer, receiver, amount, true);
        }

        private static void ReleaseHeroes(PeaceTerms terms)
        {
            if (!terms.ReleasePrisoners) return;

            var freed = 0;
            var winnerHeroes = terms.Winner.Heroes;
            for (var i = 0; i < winnerHeroes.Count; i++)
            {
                var hero = winnerHeroes[i];
                if (!hero.IsPrisoner) continue;

                var captorFaction = hero.PartyBelongedToAsPrisoner?.MapFaction;
                if (captorFaction != terms.Loser) continue;

                EndCaptivityAction.ApplyByPeace(hero, terms.Loser.Leader);
                freed++;
            }
            if (freed > 0) Log.Info("Peace", terms.Loser.Name + " released " + freed + " captive hero(es).");
        }

        /// <summary>
        /// The top rung: the loser becomes a vassal, and the winner becomes a hegemon by the
        /// only definition the mod has - holding one.
        ///
        /// Hold starts low (<see cref="DiplomacyConstants.HoldOnCoercedSubmission"/>), because
        /// submission at swordpoint is exactly the kind that comes apart.
        /// </summary>
        private static void ImposeSubmission(ModState state, PeaceTerms terms)
        {
            if (!terms.ImposeVassalage) return;

            var treaty = Hegemony.Submit(state, terms.Winner, terms.Loser,
                DiplomacyConstants.HoldOnCoercedSubmission, terms.TributePerPeriod, out var reason);

            if (treaty == null)
            {
                Log.Warn("Peace", "Could not impose submission: " + reason);
                return;
            }

            Log.Info("Hegemony", terms.Loser.Name + " submits to " + terms.Winner.Name
                                 + " as a vassal at hold "
                                 + DiplomacyConstants.HoldOnCoercedSubmission.ToString("0")
                                 + ". " + terms.Winner.Name + " now holds "
                                 + Hegemony.VassalCount(state, terms.Winner) + " vassal(s).");
        }

        private static void ImposeTribute(ModState state, PeaceTerms terms)
        {
            if (!terms.ImposeTributaryPact) return;

            var treaty = TreatyRegistry.Sign(state, terms.Winner, terms.Loser,
                TreatyType.TributaryPact, out var reason,
                tributePayer: terms.Loser, tributeAmount: terms.TributePerPeriod);

            if (treaty == null)
                Log.Warn("Peace", "Could not impose the tributary pact: " + reason);
        }

        /// <summary>
        /// The most the winner could ask for right now, as text. Used by the console today
        /// and by the negotiation screen in 1.8 - both read the same budget the AI uses.
        /// </summary>
        public static string DescribeAllowance(ModState state, WarRecord war, Kingdom winner)
        {
            var budget = BudgetFor(war, winner);
            if (budget <= 0f)
                return "War score " + war.ScoreFor(winner).ToString("0.0")
                       + ": nothing has been earned. White peace only.";

            var loser = war.Other(winner);
            var hasClaim = ClaimRegistry.HasTerritorialClaim(state, winner, loser);

            var lines = new List<string>
            {
                "War score " + war.ScoreFor(winner).ToString("0.0") + " gives a budget of " + budget.ToString("0") + ".",
                "  town              " + DiplomacyConstants.PeaceCostTown.ToString("0")
                    + (hasClaim ? "" : "   (blocked: no territorial claim)"),
                "  castle            " + DiplomacyConstants.PeaceCostCastle.ToString("0")
                    + (hasClaim ? "" : "   (blocked: no territorial claim)"),
                "  tributary pact    " + DiplomacyConstants.PeaceCostTributaryPact.ToString("0"),
                "  submission        " + DiplomacyConstants.PeaceCostVassalage.ToString("0")
                    + "   (they become our vassal)",
                "  release prisoners " + DiplomacyConstants.PeaceCostPrisoners.ToString("0"),
                "  indemnity         " + DiplomacyConstants.PeaceCostPerThousandIndemnity.ToString("0") + " per 1000 denars",
                "Their exhaustion is " + war.ExhaustionOf(loser).ToString("0.0")
                    + "; they start listening at " + (DiplomacyConstants.ExhaustionSeekPeace
                        - (-war.ScoreFor(loser)) / 2f).ToString("0.0") + "."
            };
            return string.Join("\n", lines);
        }
    }
}
