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
                default: return 0f;
            }
        }
    }
}
