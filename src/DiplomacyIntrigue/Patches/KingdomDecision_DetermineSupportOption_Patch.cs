using System;
using DiplomacyIntrigue.Behaviors;
using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Intrigue;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;

namespace DiplomacyIntrigue.Patches
{
    /// <summary>
    /// WHAT: makes a court bloc vote as a unit. A clan that belongs to a bloc casts its vote
    /// for whatever its bloc leader would choose, instead of for whatever suits it alone.
    ///
    /// WHY A PATCH: there is no event and no GameModel anywhere near kingdom voting.
    /// `ModKingdomDecisionPermissionModel` governs whether a decision may *exist*, not how it
    /// is voted on, and nothing else in the campaign models touches support. The alternatives
    /// were examined with tools/CallSites before this file was written:
    ///
    ///   - `KingdomDecision.DetermineSupport(Clan, DecisionOutcome)` looks like the natural
    ///     target and is the wrong one. **Every** decision type overrides it -
    ///     DeclareWarDecision, MakePeaceKingdomDecision, KingdomPolicyDecision,
    ///     SettlementClaimantDecision and the rest - so a patch on the base method would
    ///     silently miss almost every vote in the game. That is the kind of patch that looks
    ///     applied, logs nothing, and produces a system that appears balanced while running on
    ///     a fraction of its inputs.
    ///   - `DetermineSupportOption` is declared once, on KingdomDecision, is not overridden
    ///     anywhere, and is the funnel every decision passes through. One patch, whole game.
    ///
    /// WHY A POSTFIX: the engine's own choice is computed first and then redirected. The bloc
    /// only ever moves a vote from one of the outcomes the engine already offered to another
    /// of them; it cannot invent an option or vote for something impossible.
    ///
    /// WHAT IS DELIBERATELY LEFT ALONE: a member whose loyalty is at or above the reliable
    /// band. Design 02 §3 is explicit - "a clan with loyalty >= 70 follows the ruler
    /// regardless of its bloc. Loyalty beats agenda." A bloc leader also votes its own mind,
    /// since it is the one being followed. Neither is an exception bolted on; both are the
    /// rule the spec states.
    ///
    /// TWO BEHAVIOURS FOUND BY RUNNING IT, recorded here because neither is obvious from the
    /// code and both are choices rather than accidents:
    ///
    ///   - **An abstaining member is left abstaining.** When the engine's own answer is null,
    ///     the clan declined to take a side, and this patch returns without touching it.
    ///     Turning an abstention into a vote would be manufacturing one, not redirecting one.
    ///   - **The bloc follows its leader's *preference*, not its leader's cast vote.**
    ///     `BestFor` asks `DetermineSupport`, which is what the leader wants; the leader's own
    ///     `DetermineSupportOption` can still come back null because it does not care enough to
    ///     spend influence. Observed live: Southern Empire's Autonomist leader abstained while
    ///     its bloc voted Yes. Following the cast vote instead would let an indifferent leader
    ///     silence its entire bloc, which makes blocs weaker rather than more united - the
    ///     opposite of what design 02 §3 is for. Worth revisiting if the balance pass shows
    ///     blocs carrying votes their leaders visibly did not want.
    ///
    /// WHY NOT ALSO REWRITE THE SUPPORT WEIGHT: `supportWeightOfSelectedOutcome` is the
    /// engine's own measure of how hard the clan pushes, and it feeds influence cost and
    /// relation changes. Redirecting which outcome a clan backs is the design; inflating how
    /// hard it pushes is not, and changing both would make the effect impossible to attribute
    /// when the balance pass reads it.
    ///
    /// VERIFIED AGAINST: Bannerlord v1.4.8 (TaleWorlds.CampaignSystem,
    /// TaleWorlds.CampaignSystem.Election.KingdomDecision::DetermineSupportOption; callers
    /// KingdomElection::DetermineSupport and KingdomDecision::ShouldBeCancelled).
    ///
    /// FAILURE MODE: on any exception the engine's own choice is left untouched, so the vote
    /// falls back to vanilla behaviour rather than breaking kingdom decisions.
    /// </summary>
    [HarmonyPatch(typeof(KingdomDecision), nameof(KingdomDecision.DetermineSupportOption))]
    public static class KingdomDecision_DetermineSupportOption_Patch
    {
        private static void Postfix(KingdomDecision __instance, Supporter supporter,
            MBReadOnlyList<DecisionOutcome> possibleOutcomes, ref DecisionOutcome __result)
        {
            try
            {
                if (!SubModule.Healthy || !Settings.Current.EnableIntrigue) return;
                if (__instance == null || supporter?.Clan == null || __result == null) return;
                if (possibleOutcomes == null || possibleOutcomes.Count < 2) return;

                var state = CoreBehavior.State;
                if (state == null) return;

                var clan = supporter.Clan;
                var bloc = BlocModel.BlocOf(state, clan);
                if (bloc?.Leader == null || bloc.Leader == clan) return;

                // Loyalty beats agenda (design 02 §3).
                if (LoyaltyModel.Of(state, clan) >= IntrigueConstants.LoyaltyReliable) return;

                var leaderChoice = BestFor(__instance, bloc.Leader, possibleOutcomes);
                if (leaderChoice == null || leaderChoice == __result) return;

                __result = leaderChoice;
            }
            catch (Exception ex)
            {
                // Never let a vote take the game down, and never let it half-apply: the
                // engine's own __result is still whatever it computed.
                Log.Error("Intrigue", "Bloc voting failed; the vanilla vote stands.", ex);
            }
        }

        /// <summary>
        /// Which outcome the bloc leader backs, by asking the decision itself.
        ///
        /// Calls `DetermineSupport` rather than `DetermineSupportOption`, for two reasons:
        /// it dispatches virtually to the decision's own override, so the leader is evaluated
        /// by the same rules as everyone else; and it does not re-enter this patch, so there
        /// is no recursion to guard against.
        /// </summary>
        private static DecisionOutcome BestFor(KingdomDecision decision, Clan leader,
            MBReadOnlyList<DecisionOutcome> outcomes)
        {
            DecisionOutcome best = null;
            var bestSupport = float.MinValue;

            for (var i = 0; i < outcomes.Count; i++)
            {
                var outcome = outcomes[i];
                if (outcome == null) continue;

                var support = decision.DetermineSupport(leader, outcome);
                if (support <= bestSupport) continue;

                bestSupport = support;
                best = outcome;
            }
            return best;
        }
    }
}
