using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// Whether a clan holds as much land as it thinks it deserves.
    ///
    /// One concept, one resolver. Two systems ask this question for opposite reasons -
    /// <see cref="GrievanceSources"/> wants to know how much a clan resents a fief going
    /// elsewhere, <see cref="LoyaltyModel"/> wants a term in the loyalty sum - and if each
    /// answered it privately they would eventually disagree about the same clan. CLAUDE.md §3
    /// is explicit about why that matters here.
    /// </summary>
    public static class FiefStanding
    {
        /// <summary>
        /// −1 to +1: how satisfied a clan is with its holdings. Negative is short of land,
        /// zero is exactly what it deserves, positive is well provided for.
        ///
        /// "Deserved by renown" (design 02 §2) is read through <see cref="Clan.Tier"/>, which
        /// *is* renown - the tiers are renown thresholds - and is stable across game versions
        /// in a way a raw renown number divided by a magic constant would not be. One fief per
        /// tier is the expectation.
        ///
        /// The positive half is deliberately compressed: a clan holding twice its due is
        /// content, not twice as content, so satisfaction saturates at +1 rather than growing
        /// with every extra town.
        /// </summary>
        public static float Satisfaction(Clan clan)
        {
            if (clan == null) return 0f;

            var deserved = clan.Tier;
            if (deserved <= 0) return 0f;   // a clan with no standing has no expectation to fail

            var held = clan.Fiefs == null ? 0 : clan.Fiefs.Count;
            var ratio = (held - deserved) / (float)deserved;

            return ratio < -1f ? -1f : (ratio > 1f ? 1f : ratio);
        }

        /// <summary>
        /// 0 to 1: how short of land a clan is. The negative half of
        /// <see cref="Satisfaction"/>, expressed as a positive appetite because that is how
        /// the grievance weighting reads it.
        /// </summary>
        public static float Hunger(Clan clan)
        {
            var satisfaction = Satisfaction(clan);
            return satisfaction < 0f ? -satisfaction : 0f;
        }
    }
}
