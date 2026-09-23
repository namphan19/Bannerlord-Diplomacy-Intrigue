using DiplomacyIntrigue.Models;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// Every tunable number for the court-intrigue pillar, in one file so a balance pass
    /// edits one place. Same rule as Diplomacy/DiplomacyConstants.cs.
    ///
    /// **Everything here is un-tuned.** These are the figures from design 02, which were
    /// written before any of it ran. Phase 1 learned this the hard way: run 01 found war
    /// exhaustion accruing about thirty times faster than the design assumed, and two
    /// constants were once justified in comments as "measured" when the run behind the claim
    /// was confounded. So no number in this file claims to be measured until a run says so,
    /// and the marker below is the authority on which ones have moved.
    /// </summary>
    public static class IntrigueConstants
    {
        // ----- Grievances (design 02 §1) --------------------------------------

        /// <summary>
        /// Weight lost per day. UN-TUNED: design 02 §1's figure, which puts an uncompounded
        /// grievance of weight 8 at zero in ~400 days - "a grievance nobody compounds fades in
        /// roughly a year and a half".
        /// </summary>
        public const float GrievanceDecayPerDay = 0.02f;

        /// <summary>
        /// A fief they bid for went to a rival. UN-TUNED. The heaviest single slight in the
        /// design, and the one that makes the player's own patronage decisions cost something.
        /// </summary>
        public const float GrievanceFiefToRival = 8f;

        /// <summary>
        /// Ceiling for a war the clan's bloc opposed; the actual weight is this scaled by
        /// (1 - legitimacy), so a fully justified war offends nobody. UN-TUNED. This is the
        /// hinge where Phase 1's casus belli work starts paying into Phase 2.
        /// </summary>
        public const float GrievanceUnjustWarMax = 8f;

        /// <summary>A relative left in enemy captivity over a year. UN-TUNED. Renews if still held.</summary>
        public const float GrievanceRelativeInCaptivity = 6f;

        /// <summary>
        /// How long a relative must have been held before the clan blames its own crown for
        /// leaving them there. UN-TUNED: design 02 §1's "more than 1 year".
        /// </summary>
        public const float CaptivityGrievanceYears = 1f;

        /// <summary>The realm bought peace with tribute. UN-TUNED. Paying side only.</summary>
        public const float GrievanceHumiliatingTribute = 5f;

        /// <summary>A fief of theirs fell to the enemy; the crown failed to defend it. UN-TUNED.</summary>
        public const float GrievanceFiefLostToEnemy = 4f;

        /// <summary>A policy passed against their agenda. UN-TUNED.</summary>
        public const float GrievancePolicyAgainstAgenda = 3f;

        /// <summary>Peace signed while they were winning. UN-TUNED. Hawks specifically.</summary>
        public const float GrievancePeaceWhileWinning = 3f;

        /// <summary>The ruler turned down a request. UN-TUNED, and deliberately the cheapest.</summary>
        public const float GrievanceRequestRefused = 2f;

        /// <summary>Backed a losing claimant at a contested succession. UN-TUNED: design 02 §5.</summary>
        public const float GrievanceSuccessionPassedOver = 6f;

        // ----- Crown legitimacy (design 02 §4) --------------------------------

        /// <summary>Where every crown starts, on a 0-100 pool. UN-TUNED: design 02 §4.</summary>
        public const float LegitimacyStart = 60f;

        /// <summary>Won a war whose cause was solid. UN-TUNED.</summary>
        public const float LegitimacyWonJustWar = 12f;

        /// <summary>
        /// Won a war in the middle of the justification range. UN-TUNED, and **not in design
        /// 02 §4** - the spec gives a figure for a just win and for an unjust win and nothing
        /// between, which would make a war at justification 0.5 pay the same as naked
        /// aggression. The midpoint is interpolated rather than left as a cliff.
        /// </summary>
        public const float LegitimacyWonOrdinaryWar = 7f;

        /// <summary>Won a war nobody could justify. A win is a win, barely. UN-TUNED.</summary>
        public const float LegitimacyWonUnjustWar = 2f;

        /// <summary>Lost a war. UN-TUNED.</summary>
        public const float LegitimacyLostWar = 10f;

        /// <summary>Declared a war with nothing to point at. UN-TUNED.</summary>
        public const float LegitimacyNoCasusBelli = 8f;

        /// <summary>
        /// Broke a treaty. The heaviest single entry in design 02 §4, deliberately: a crown
        /// that breaks its word is treated as worse than a crown that loses. UN-TUNED.
        /// </summary>
        public const float LegitimacyBrokeTreaty = 20f;

        /// <summary>Lost a fief to an outside power. UN-TUNED.</summary>
        public const float LegitimacyLostFief = 3f;

        /// <summary>Granted for each full year without a war. UN-TUNED.</summary>
        public const float LegitimacyPeaceDividend = 3f;

        /// <summary>How long that year is. UN-TUNED, and a year by definition.</summary>
        public const float LegitimacyPeaceDividendYears = 1f;

        /// <summary>At or above this war justification, a victory counts as a just one. UN-TUNED.</summary>
        public const float LegitimacyJustWar = 0.7f;

        /// <summary>Below this war justification, a victory buys almost nothing. UN-TUNED.</summary>
        public const float LegitimacyUnjustWar = 0.3f;

        /// <summary>
        /// War score below which a settlement is a white peace with no victor, so neither
        /// crown's standing moves. UN-TUNED. Without it both sides of every stalemate would
        /// file a claim to victory.
        /// </summary>
        public const float LegitimacyDecisiveScore = 10f;

        /// <summary>
        /// Below this the crown is weak enough for a pretender to speak openly (design 02 §3,
        /// §5). UN-TUNED. The claimant half of that condition is 2.5's.
        /// </summary>
        public const float LegitimacyPretenderThreshold = 40f;

        // ----- Succession (design 02 §5) --------------------------------------

        /// <summary>
        /// Share of the court the new ruler needs for the succession to pass off quietly.
        /// UN-TUNED: design 02 §5's "clear majority (> 60%)".
        /// </summary>
        public const float SuccessionClearMajority = 0.6f;

        /// <summary>What a contested succession costs the new crown. UN-TUNED: design 02 §5.</summary>
        public const float SuccessionContestedLegitimacy = 15f;

        /// <summary>
        /// Share a losing claimant must keep to remain a standing pretender. UN-TUNED:
        /// design 02 §5's "more than 30% support".
        /// </summary>
        public const float SuccessionPretenderShare = 0.3f;

        /// <summary>
        /// How strongly a clan leader backs their own claim, on the same scale as relation
        /// (-100..100). UN-TUNED, and set high: a claimant who would rather see somebody else
        /// crowned is not a claimant.
        /// </summary>
        public const float SuccessionSelfBacking = 100f;

        /// <summary>
        /// How much loyalty to the crown counts as backing for whoever now wears it. UN-TUNED.
        /// At 0.5 a fully loyal clan brings the equivalent of +50 relation to the incumbent,
        /// which is what makes a well-run realm inherit smoothly.
        /// </summary>
        public const float SuccessionLoyaltyWeight = 0.5f;

        // ----- Court blocs (design 02 §3) -------------------------------------
        //
        // These are pressures, not probabilities: only their order within one clan matters,
        // because a clan joins whichever agenda pulls hardest. Comparing them between clans
        // means nothing. All UN-TUNED.

        /// <summary>Dove pull per point of war exhaustion above `ExhaustionCourtPressure`. UN-TUNED.</summary>
        public const float DovePressurePerExhaustion = 1f;

        /// <summary>Hawk pull at full land hunger, when a weaker neighbour exists. UN-TUNED.</summary>
        public const float HawkPressureFromHunger = 40f;

        /// <summary>
        /// How much weaker a neighbour must be before the hawks think it is worth taking.
        /// UN-TUNED. Reads the same smoothed strength the Phase 1 war valuation reads, so the
        /// hawks push for wars the kingdom's own AI would also consider.
        /// </summary>
        public const float HawkWeakNeighbourRatio = 0.9f;

        /// <summary>
        /// Autonomist pull at full crown authority for a clan holding the entire court's
        /// influence. UN-TUNED, and scaled by influence share, so in practice it is small.
        /// </summary>
        public const float AutonomistPressure = 120f;

        /// <summary>Centralist pull for a clan holding more land than its standing demands. UN-TUNED.</summary>
        public const float CentralistPressureFromPatronage = 30f;

        /// <summary>
        /// Pull on a clan whose own leader holds a claim to the throne. UN-TUNED, and set
        /// above every other agenda on purpose: a clan with a crown within reach is not
        /// weighing tax policy.
        /// </summary>
        public const float PretenderPressureOwnClaim = 200f;

        /// <summary>
        /// Pull per point by which a clan prefers a claimant to the sitting ruler. UN-TUNED.
        /// At 0.5 a clan that likes a pretender 60 points more than its king is pulled harder
        /// than any other agenda can manage.
        /// </summary>
        public const float PretenderPressurePerRelationPoint = 0.5f;

        // ----- Loyalty (design 02 §2) -----------------------------------------

        /// <summary>Where a clan with no feelings either way sits. UN-TUNED.</summary>
        public const float LoyaltyBase = 50f;

        /// <summary>
        /// Relation contributes half its value, so vanilla's -100..+100 becomes -50..+50.
        /// UN-TUNED. Design 02 §2 writes it as "relation / 2".
        /// </summary>
        public const float LoyaltyRelationFactor = 0.5f;

        /// <summary>
        /// What one point of accumulated grievance costs in loyalty. UN-TUNED, and the most
        /// load-bearing number in the pillar: at 1.5, a court holding two maximum grievances
        /// (16 weight) is already 24 points down, which takes a neutral clan from
        /// transactional to disaffected on its own.
        /// </summary>
        public const float LoyaltyGrievanceFactor = 1.5f;

        /// <summary>Full satisfaction with holdings is worth this much either way. UN-TUNED.</summary>
        public const float LoyaltyFiefFactor = 10f;

        /// <summary>
        /// How much the realm's worst ongoing war drags on loyalty, per point of exhaustion.
        /// UN-TUNED. At 0.2 a war at exhaustion 100 costs 20 loyalty across the whole court.
        /// </summary>
        public const float LoyaltyWarExhaustionFactor = 0.2f;

        /// <summary>Per point of crown legitimacy away from neutral. UN-TUNED. Inert until 2.4.</summary>
        public const float LoyaltyLegitimacyFactor = 0.2f;

        /// <summary>
        /// The midpoint of the crown-legitimacy pool (design 02 §4 starts every kingdom at 60,
        /// on a 0-100 scale whose neutral point is 50). Until 2.4 exists every crown reads as
        /// exactly neutral, so the legitimacy term contributes nothing rather than guessing.
        /// </summary>
        public const float LegitimacyNeutral = 50f;

        // Band edges. These are not cosmetic: each one is a behavioural threshold, which is
        // why the court UI shows a band rather than a number for rival kingdoms (design 02
        // §9.1) - a band says which side of a threshold a court sits on without saying how far.

        /// <summary>At or above: votes with the ruler regardless of agenda. UN-TUNED.</summary>
        public const float LoyaltyReliable = 70f;

        /// <summary>At or above: votes its own interest. UN-TUNED.</summary>
        public const float LoyaltyTransactional = 40f;

        /// <summary>At or above: votes against the ruler but stays. Below: defection risk. UN-TUNED.</summary>
        public const float LoyaltyDisaffected = 25f;

        /// <summary>
        /// The starting weight for a type. One place, so a source cannot disagree with the
        /// ledger about what a slight is worth.
        /// </summary>
        public static float WeightOf(GrievanceType type)
        {
            switch (type)
            {
                case GrievanceType.FiefToRival: return GrievanceFiefToRival;
                case GrievanceType.UnjustWar: return GrievanceUnjustWarMax;
                case GrievanceType.RelativeInCaptivity: return GrievanceRelativeInCaptivity;
                case GrievanceType.HumiliatingTribute: return GrievanceHumiliatingTribute;
                case GrievanceType.FiefLostToEnemy: return GrievanceFiefLostToEnemy;
                case GrievanceType.PolicyAgainstAgenda: return GrievancePolicyAgainstAgenda;
                case GrievanceType.PeaceWhileWinning: return GrievancePeaceWhileWinning;
                case GrievanceType.RequestRefused: return GrievanceRequestRefused;
                case GrievanceType.SuccessionPassedOver: return GrievanceSuccessionPassedOver;
                default: return 0f;
            }
        }
    }
}
