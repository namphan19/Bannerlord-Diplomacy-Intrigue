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
            // One price for both faces of the top rung - see PeaceCostSubjugation. They cannot
            // both be set: a hegemon has no independence left to give, a free kingdom no sphere.
            if (terms.ImposeVassalage || terms.DissolveHegemony) cost += DiplomacyConstants.PeaceCostSubjugation;
            if (terms.ImposeTributaryPact) cost += DiplomacyConstants.PeaceCostTributaryPact;
            if (terms.ReleasePrisoners) cost += DiplomacyConstants.PeaceCostPrisoners;
            cost += IndemnityPoints(terms.IndemnityGold, terms.Loser);
            return cost;
        }

        // ================= What money is worth at the table =====================
        //
        // One rate, read by every price of an indemnity: the AI ladder (through CostOf and
        // LargestIndemnity), the negotiation screen, the Ctrl+D tables and the console. A
        // denar figure shown anywhere is priced by IndemnityPoints, and a points figure turned
        // into denars by IndemnityGoldFor, and both read IndemnityDenarsPerPoint.
        //
        // **When it is priced: at signing, from the treasury as it stands then.** The terms
        // carry denars, because denars are what was agreed and what is paid; the points those
        // denars cost are re-read whenever a check runs, and the check that decides is the one
        // Apply makes, with every route re-asking the signatures just before it. So a loser whose
        // treasury moved between the offer and the signature is priced at what it holds when it
        // pays. Three reasons that moment beat pricing at the offer:
        //  - the payment is taken at signing, so the share of the treasury it is, is the share
        //    at signing - a price frozen at the offer could pass 60% of what is left as 30%;
        //  - every other input to the table already works this way: war score decays daily,
        //    exhaustion moves, and the routes that hold an offer open (the AI's offer to the
        //    player, the Ctrl+D tables) re-ask the signatures when the answer arrives and refuse
        //    if the world moved under them;
        //  - a frozen price would have to live on PeaceTerms as a second number for the same
        //    thing, which is the mistake CasusBelli.Resolve exists to prevent.
        // If the treasury fell, the same denars cost more points and may break the budget or
        // the ceiling: the signing is refused with the reason, not executed at a price nobody
        // agreed to. If it rose, they cost fewer, and the winner's own re-check may find the
        // package no longer worth its while.

        /// <summary>
        /// The treasury an indemnity is measured against: the loser's ruling clan's purse, which
        /// is its ruler's gold - the same purse <see cref="PayIndemnity"/> draws on, so the price
        /// and the payment read one number.
        /// </summary>
        public static int IndemnityTreasury(Kingdom loser)
        {
            var gold = loser?.Leader?.Gold ?? 0;
            return gold > 0 ? gold : 0;
        }

        /// <summary>
        /// What one war-score point of indemnity takes from this loser, in denars: a share of its
        /// treasury (<see cref="DiplomacyConstants.PeaceIndemnityTreasurySharePerPoint"/>), never
        /// less than the old flat rate (<see cref="DiplomacyConstants.PeaceIndemnityFloorDenarsPerPoint"/>).
        /// The one rate - see the note above.
        /// </summary>
        public static float IndemnityDenarsPerPoint(Kingdom loser)
        {
            var byMeans = IndemnityTreasury(loser) * DiplomacyConstants.PeaceIndemnityTreasurySharePerPoint;
            return byMeans > DiplomacyConstants.PeaceIndemnityFloorDenarsPerPoint
                ? byMeans
                : DiplomacyConstants.PeaceIndemnityFloorDenarsPerPoint;
        }

        /// <summary>What an indemnity of <paramref name="gold"/> denars costs against the winner's budget.</summary>
        public static float IndemnityPoints(int gold, Kingdom loser)
            => gold <= 0 ? 0f : gold / IndemnityDenarsPerPoint(loser);

        /// <summary>
        /// The denars <paramref name="points"/> of indemnity take from this loser, before the
        /// ceiling. Truncated, so the result never costs more than the points it was sized from.
        /// </summary>
        public static int IndemnityGoldFor(float points, Kingdom loser)
            => points <= 0f ? 0 : (int)(points * IndemnityDenarsPerPoint(loser));

        /// <summary>
        /// The most any one peace may take from this loser
        /// (<see cref="DiplomacyConstants.PeaceIndemnityMaxTreasuryShare"/> of its treasury).
        /// </summary>
        public static int IndemnityCeiling(Kingdom loser)
            => (int)(IndemnityTreasury(loser) * DiplomacyConstants.PeaceIndemnityMaxTreasuryShare);

        /// <summary>
        /// An indemnity as a reader needs it: the denars, what they cost, and how much of the
        /// loser's treasury they are - "43000 denars, 30% of the 144000 Sturgia's ruler holds,
        /// 60 points". One description for the console and the tables.
        /// </summary>
        public static string DescribeIndemnity(int gold, Kingdom loser)
        {
            var treasury = IndemnityTreasury(loser);
            var share = treasury > 0 ? gold / (float)treasury * 100f : 0f;
            return gold + " denars, " + share.ToString("0") + "% of the " + treasury + " "
                   + (loser?.Name?.ToString() ?? "the loser") + "'s ruler holds, "
                   + IndemnityPoints(gold, loser).ToString("0") + " points";
        }

        /// <summary>The rate as a line of text, for the allowance readout.</summary>
        public static string DescribeIndemnityRate(Kingdom loser)
            => IndemnityDenarsPerPoint(loser).ToString("0") + " denars a point against a treasury of "
               + IndemnityTreasury(loser) + " ("
               + (DiplomacyConstants.PeaceIndemnityTreasurySharePerPoint * 100f).ToString("0.0")
               + "% a point, never under "
               + DiplomacyConstants.PeaceIndemnityFloorDenarsPerPoint.ToString("0")
               + "); no peace takes more than " + IndemnityCeiling(loser);

        /// <summary>
        /// Points the winner has earned in this war. Zero when they are not ahead, which is
        /// what makes a white peace the only option in a stalemate.
        ///
        /// Design 08 S-2: the score is what the winner earned, the budget is what its envoy can
        /// argue for against the loser's. The white-peace floor reads the raw score, so a
        /// stalemate stays a stalemate however good the envoys are.
        /// </summary>
        public static float BudgetFor(WarRecord war, Kingdom winner)
        {
            var score = WarScore.For(war, winner);
            return score <= DiplomacyConstants.PeaceWhitePeaceOnlyBelow
                ? 0f
                : score * Statecraft.StatecraftTerms.NegotiationFactor(winner, war.Other(winner));
        }

        /// <summary>The subjugation package: the top rung plus the prisoners it always carries.</summary>
        public static float SubjugationCost
            => DiplomacyConstants.PeaceCostSubjugation + DiplomacyConstants.PeaceCostPrisoners;

        /// <summary>
        /// The least a winner will settle for, in concession points.
        ///
        /// Two rules, in order:
        ///
        /// 1. **Subjugation is a cliff.** If this war can actually produce it, the winner wants
        ///    exactly that and nothing cheaper. A victory that can take the loser's standing
        ///    does not settle for the tribute half the score would have bought. The lead's call
        ///    (design/04 §12.9), and it also removes an asymmetry: the loser walking the ladder
        ///    up and the winner walking it down now stop on the same rung, so the outcome no
        ///    longer depends on which side reached the table first.
        /// 2. Otherwise half the war score, capped by **the dearest package this war could
        ///    actually produce**.
        ///
        /// That cap used to be the constant cost of the top rung, and it was wrong. Run 07,
        /// Spring 1099: Khuzait beat Sturgia to a war score of **225.6** and took a white
        /// peace. Sturgia already answered to Vlandia, so `CanSign` barred both subjugation and
        /// tribute; the demand stayed pinned at 95 with nothing on the table able to reach it,
        /// and the war only ended when the *winner* tired past
        /// <see cref="DiplomacyConstants.ExhaustionAcceptWhitePeaceWhenWinning"/>. A ceiling has
        /// to describe what is reachable, not what the ladder would cost in the abstract.
        ///
        /// One function rather than the expression inlined at each site: the console's
        /// allowance readout, the player's menu and the AI read the same figure, which is the
        /// rule the whole peace table follows.
        /// </summary>
        public static float MinimumAcceptable(ModState state, WarRecord war, Kingdom winner)
        {
            var budget = BudgetFor(war, winner);
            if (budget <= 0f) return 0f;

            var ceiling = DearestDemandable(state, war, winner);
            if (ceiling >= SubjugationCost) return SubjugationCost;

            var wanted = budget * DiplomacyConstants.PeaceWinnerMinimumShare;
            return wanted > ceiling ? ceiling : wanted;
        }

        /// <summary>
        /// The cost of the dearest package <see cref="IsDemandable"/> would actually allow in
        /// this war. Asked through the real resolver rather than re-deriving the rules, so a
        /// rung the table would refuse cannot inflate what the winner holds out for.
        ///
        /// Prisoners are the floor: they are always available and always affordable.
        /// </summary>
        private static float DearestDemandable(ModState state, WarRecord war, Kingdom winner)
        {
            var loser = war.Other(winner);
            if (loser == null) return DiplomacyConstants.PeaceCostPrisoners;

            var best = DiplomacyConstants.PeaceCostPrisoners;

            var subjugation = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
            if (Hegemony.IsHegemon(state, loser)) subjugation.DissolveHegemony = true;
            else
            {
                subjugation.ImposeVassalage = true;
                subjugation.TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod;
            }
            if (IsDemandable(state, war, subjugation, out _)) return CostOf(subjugation);

            var tribute = new PeaceTerms(winner, loser)
            {
                ReleasePrisoners = true,
                ImposeTributaryPact = true,
                TributePerPeriod = DiplomacyConstants.AiDefaultTributePerPeriod,
            };
            if (IsDemandable(state, war, tribute, out _)) best = Max(best, CostOf(tribute));

            var settlements = loser.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var fief = settlements[i];
                if (!fief.IsFortification) continue;
                var land = new PeaceTerms(winner, loser) { ReleasePrisoners = true };
                land.FiefsCeded.Add(fief);
                if (IsDemandable(state, war, land, out _)) best = Max(best, CostOf(land));
            }

            var indemnity = new PeaceTerms(winner, loser)
            {
                ReleasePrisoners = true,
                IndemnityGold = LargestIndemnity(war, winner, loser),
            };
            if (indemnity.IndemnityGold > 0 && IsDemandable(state, war, indemnity, out _))
                best = Max(best, CostOf(indemnity));

            return best;
        }

        private static float Max(float a, float b) => a > b ? a : b;

        /// <summary>
        /// The largest indemnity this war could actually charge, in denars, rounded down to
        /// whole thousands. Zero when there is nothing worth taking.
        ///
        /// Two questions, kept apart. **How many points** is the war's: the budget less the
        /// prisoners every package carries, capped at the tributary pact's cost so money stays
        /// a mid-ladder option - without that cap an indemnity sized to the whole budget would
        /// be the dearest thing available in almost every war and crowd out land and tribute.
        /// **What a point is worth** is the loser's: <see cref="IndemnityDenarsPerPoint"/>, a
        /// share of its treasury (2026-09-27). The result is capped at
        /// <see cref="IndemnityCeiling"/>, the same ceiling <see cref="IsDemandable"/> enforces,
        /// so the ladder never builds a package the table would refuse.
        ///
        /// History, because both halves were once wrong. Until 2026-09-20 the offer was half the
        /// ruler's gold, which run 07 showed was never once demandable: half of Sturgia's 740,000
        /// denars at the old flat 8 points per 1,000 came to roughly 2,950 concession points
        /// against a budget of 187, and zero of that run's 100 settlements carried an indemnity.
        /// Sizing the points from the war score revived the rung - run 08 used it in 35 of 123
        /// settlements - but the flat price made 60 points 7,500 denars, trivial to rulers
        /// holding 144,000-453,000. The treasury-share rate is the 2026-09-27 answer to that.
        /// </summary>
        public static int LargestIndemnity(WarRecord war, Kingdom winner, Kingdom loser)
        {
            if (IndemnityTreasury(loser) <= 0) return 0;

            var budget = BudgetFor(war, winner);
            var points = budget - DiplomacyConstants.PeaceCostPrisoners;
            if (points > DiplomacyConstants.PeaceCostTributaryPact)
                points = DiplomacyConstants.PeaceCostTributaryPact;
            if (points <= 0f) return 0;

            var byScore = IndemnityGoldFor(points, loser);
            var ceiling = IndemnityCeiling(loser);
            var gold = (byScore < ceiling ? byScore : ceiling) / 1000 * 1000;

            // Converting back can land a float's last bit above the points this was sized from
            // when the product falls exactly on a thousand, and IsDemandable would then refuse
            // the very package the ladder built. One thousand less is the cheap certainty.
            if (gold >= 1000 && IndemnityPoints(gold, loser) > points) gold -= 1000;
            return gold;
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

            // A package cannot contradict itself. The negotiation screen asks
            // PeaceTerms.AreExclusive to untick conflicting lines before they are ever
            // priced, but a hand-built package (the AI, a console command) reaches this
            // point directly and has to be refused here or the rule lives nowhere. Which
            // pairs conflict, and why, is AreExclusive's answer - not restated here.
            var kinds = (PeaceTermKind[])System.Enum.GetValues(typeof(PeaceTermKind));
            for (var i = 0; i < kinds.Length; i++)
            {
                if (!terms.Includes(kinds[i])) continue;
                for (var j = i + 1; j < kinds.Length; j++)
                {
                    if (terms.Includes(kinds[j]) && PeaceTerms.AreExclusive(kinds[i], kinds[j], out reason))
                        return false;
                }
            }

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

                // Winning a war is not the same as being able to hold the loser afterwards.
                // Every other route into vassalage asks this; the peace table did not, so a
                // kingdom that won on points could take a vassal it was weaker than, and the
                // link would begin with fear already working against it.
                if (!Hegemony.IsStrongEnoughToHold(terms.Winner, terms.Loser))
                {
                    reason = terms.Winner.Name + " is no stronger than " + terms.Loser.Name
                             + " and could not hold it as a vassal. Tribute or land is still on the table.";
                    return false;
                }
            }

            // Only a hegemon has a sphere to give up. Asked against the treaties rather than
            // any stored status, because that is the only place hegemony exists (design/04 §5.1).
            if (terms.DissolveHegemony && !Hegemony.IsHegemon(state, terms.Loser))
            {
                reason = terms.Loser.Name + " holds no vassals, so there is no sphere to break up.";
                return false;
            }

            // The demands that are treaties have to be signable once the war closes - asked
            // here, before the peace, because signing happens after it. Run 06 F4: a loser
            // already answering to another patron could not sign the tributary pact it was
            // being charged for, and the winner made peace and collected nothing it was
            // promised. CanSign does the asking so there is still only one resolver - as a
            // settlement term, so the trust floor does not apply (see CanSign's settlesWar).
            if (terms.ImposeTributaryPact
                && !TreatyRegistry.CanSign(state, terms.Winner, terms.Loser,
                    TreatyType.TributaryPact, out reason, settlesWar: true))
            {
                return false;
            }
            if (terms.ImposeVassalage
                && !TreatyRegistry.CanSign(state, terms.Winner, terms.Loser,
                    TreatyType.Vassalage, out reason, settlesWar: true))
            {
                return false;
            }

            // No peace bankrupts a realm, whoever wrote the package. A rule here rather than only
            // a limit on LargestIndemnity, because the player's own tables and the console build
            // packages by hand and reach this point without passing through the ladder's sizing.
            if (terms.IndemnityGold > 0)
            {
                var ceiling = IndemnityCeiling(terms.Loser);
                if (terms.IndemnityGold > ceiling)
                {
                    reason = "No peace may take more than "
                             + (DiplomacyConstants.PeaceIndemnityMaxTreasuryShare * 100f).ToString("0")
                             + "% of a realm's treasury. " + terms.Loser.Name + "'s ruler holds "
                             + IndemnityTreasury(terms.Loser) + " denars, so " + ceiling
                             + " is the most it can be asked for.";
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
            var scoreAgainstThem = -WarScore.For(war, loser);
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
            // The tolerance reads the same argument the budget does (design 08 S-2), so a loser
            // out-talked at the table concedes what the winner's budget can buy.
            var tolerance = scoreAgainstThem
                            * Statecraft.StatecraftTerms.NegotiationFactor(terms.Winner, loser)
                            * (1f + DiplomacyConstants.PeaceAcceptanceGrace);
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
        /// <see cref="MinimumAcceptable"/>.
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
            var wanted = MinimumAcceptable(state, war, winner);
            if (cost >= wanted) return true;

            reason = winner.Name + " is winning and will not settle for that: the package is worth "
                     + cost.ToString("0") + " against a war score of " + budget.ToString("0")
                     + ", so they want at least " + wanted.ToString("0")
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
            // Read before the peace closes the record: a dormant war ends by indifference, and
            // nobody's envoy earned anything by it (design 08 §6).
            var dormant = IsDormant(war);
            var packageCost = CostOf(terms);

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
            DissolveSphere(state, terms);
            ImposeSubmission(state, terms);

            if (!dormant) Statecraft.SkillXp.PeaceSigned(winner, loser, packageCost);

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

            // Read before the gold moves: afterwards the treasury is smaller and the same denars
            // would describe a larger share than the one that was priced.
            var treasury = IndemnityTreasury(terms.Loser);
            var points = IndemnityPoints(terms.IndemnityGold, terms.Loser);

            // Pay what they have if they cannot cover it. A peace that fails because the
            // treasury is short would just restart the war. IsDemandable's ceiling means this
            // should not happen; it stays as the guard for a purse that moved during the peace.
            var amount = terms.IndemnityGold < payer.Gold ? terms.IndemnityGold : payer.Gold;
            if (amount <= 0) return;
            GiveGoldAction.ApplyBetweenCharacters(payer, receiver, amount, true);

            // The next balance run tunes the treasury-share rate from this line: what share of
            // a treasury a settled war actually took, and at what price in points.
            Telemetry.Event("indemnity_paid", "payer", terms.Loser, "payee", terms.Winner,
                "gold", amount, "treasury", treasury, "points", points);
            Log.Info("Peace", terms.Loser.Name + " paid " + terms.Winner.Name + " an indemnity of "
                              + amount + " denars, "
                              + (treasury > 0 ? (amount / (float)treasury * 100f).ToString("0") : "0")
                              + "% of the " + treasury + " its ruler held (" + points.ToString("0.0")
                              + " points).");
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
        /// The beaten hegemon is made to free every kingdom that answers to it.
        ///
        /// **Dissolved, not broken.** The patron is not choosing this; the peace is imposing
        /// it. Closing the links as breaches would hand each freed vassal a grievance against
        /// a patron for an act it was compelled to, and cost it trust it did not spend - so
        /// `ClaimRegistry` is deliberately never reached here.
        ///
        /// The freed kingdoms **keep their own wars**. Each was called into them under its own
        /// `WarRecord`, and leaving those open is the point rather than an oversight: a cluster
        /// of newly independent kingdoms still at war with the strongest power on the map is
        /// exactly what the submission routes feed on. Nobody inherits the sphere at the table;
        /// a winner that wants these kingdoms has to earn each one separately, through the same
        /// valuation everybody else uses.
        /// </summary>
        private static void DissolveSphere(ModState state, PeaceTerms terms)
        {
            if (!terms.DissolveHegemony) return;

            var patron = terms.Loser;
            var links = new List<Treaty>();
            Hegemony.CollectVassalages(state, patron, links);
            if (links.Count == 0) return;

            for (var i = 0; i < links.Count; i++)
            {
                var vassal = links[i].SubordinateParty;
                TreatyRegistry.Dissolve(state, links[i]);
                Telemetry.Event("hegemony_dissolved_at_table", "patron", patron,
                    "vassal", vassal, "winner", terms.Winner);
            }

            Log.Info("Hegemony", patron.Name + " was made to release " + links.Count
                                 + " vassal(s) at the peace table with " + terms.Winner.Name
                                 + ". They are independent, and keep their own wars.");
        }

        /// <summary>
        /// The top rung: the loser becomes a vassal, and the winner becomes a hegemon by the
        /// only definition the mod has - holding one.
        ///
        /// Hold starts low (<see cref="DiplomacyConstants.HoldOnCoercedSubmission"/>), because
        /// submission at swordpoint is exactly the kind that comes apart - and lower still
        /// for a vassal brought back after walking out on this same winner
        /// (<see cref="Hegemony.StartingHoldWhenImposed"/>).
        /// </summary>
        private static void ImposeSubmission(ModState state, PeaceTerms terms)
        {
            if (!terms.ImposeVassalage) return;

            var startingHold = Hegemony.StartingHoldWhenImposed(state, terms.Winner, terms.Loser);
            var treaty = Hegemony.Submit(state, terms.Winner, terms.Loser,
                startingHold, terms.TributePerPeriod, out var reason, route: "imposed", settlesWar: true);

            if (treaty == null)
            {
                Log.Warn("Peace", "Could not impose submission: " + reason);
                return;
            }

            Log.Info("Hegemony", terms.Loser.Name + " submits to " + terms.Winner.Name
                                 + " as a vassal at hold "
                                 + startingHold.ToString("0")
                                 + (startingHold < DiplomacyConstants.HoldOnCoercedSubmission
                                     ? " (brought back by force)"
                                     : "")
                                 + ". " + terms.Winner.Name + " now holds "
                                 + Hegemony.VassalCount(state, terms.Winner) + " vassal(s).");
        }

        private static void ImposeTribute(ModState state, PeaceTerms terms)
        {
            if (!terms.ImposeTributaryPact) return;

            var treaty = TreatyRegistry.Sign(state, terms.Winner, terms.Loser,
                TreatyType.TributaryPact, out var reason,
                tributePayer: terms.Loser, tributeAmount: terms.TributePerPeriod, settlesWar: true);

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
                return "War score " + WarScore.For(war, winner).ToString("0.0")
                       + ": nothing has been earned. White peace only.\n  "
                       + WarScore.Describe(war, winner);

            var loser = war.Other(winner);
            var hasClaim = ClaimRegistry.HasTerritorialClaim(state, winner, loser);
            var largestIndemnity = LargestIndemnity(war, winner, loser);

            var lines = new List<string>
            {
                "War score " + WarScore.For(war, winner).ToString("0.0") + " gives a budget of " + budget.ToString("0")
                    + ", and we will not settle for less than "
                    + MinimumAcceptable(state, war, winner).ToString("0")
                    + " (dearest reachable: " + DearestDemandable(state, war, winner).ToString("0") + ").",
                "  " + WarScore.Describe(war, winner),
                "  " + Statecraft.StatecraftTerms.NegotiationLine(winner, loser),
                "  town              " + DiplomacyConstants.PeaceCostTown.ToString("0")
                    + (hasClaim ? "" : "   (blocked: no territorial claim)"),
                "  castle            " + DiplomacyConstants.PeaceCostCastle.ToString("0")
                    + (hasClaim ? "" : "   (blocked: no territorial claim)"),
                "  tributary pact    " + DiplomacyConstants.PeaceCostTributaryPact.ToString("0"),
                "  subjugation       " + DiplomacyConstants.PeaceCostSubjugation.ToString("0")
                    + (Hegemony.IsHegemon(state, loser)
                        ? "   (they free every vassal)"
                        : Hegemony.IsStrongEnoughToHold(winner, loser)
                            ? "   (they become our vassal)"
                            : "   (blocked: we are no stronger than them)"),
                "  release prisoners " + DiplomacyConstants.PeaceCostPrisoners.ToString("0"),
                "  indemnity         " + DescribeIndemnityRate(loser),
                "                    at most " + (largestIndemnity > 0
                    ? DescribeIndemnity(largestIndemnity, loser)
                    : "nothing") + " here",
                "Their exhaustion is " + war.ExhaustionOf(loser).ToString("0.0")
                    + "; they start listening at " + (DiplomacyConstants.ExhaustionSeekPeace
                        - (-WarScore.For(war, loser)) / 2f).ToString("0.0") + "."
            };
            return string.Join("\n", lines);
        }
    }
}
