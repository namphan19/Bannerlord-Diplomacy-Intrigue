namespace DiplomacyIntrigue.Espionage
{
    /// <summary>
    /// Every tunable number of the espionage pillar, in one file so a balance pass edits one
    /// place (CLAUDE.md §4). All of them are design 03's first numbers and **UN-TUNED**: none has
    /// been measured against a running campaign yet.
    /// </summary>
    public static class EspionageConstants
    {
        // ----- Networks (design 03 §1) ------------------------------------------

        /// <summary>Denars of weekly investment per point of growth, before the handler's roguery. UN-TUNED.</summary>
        public const float NetworkGoldPerPoint = 2000f;

        /// <summary>Roguery that doubles what the gold buys: growth x (1 + roguery / 200). UN-TUNED.</summary>
        public const float NetworkRogueryScale = 200f;

        /// <summary>Weekly growth lost per point of the target's counter-intelligence. UN-TUNED.</summary>
        public const float NetworkCounterIntelligenceDrag = 0.08f;

        /// <summary>Agents lost every week whatever is spent. UN-TUNED.</summary>
        public const float NetworkWeeklyAttrition = 0.7f;

        /// <summary>
        /// Lost every day on top of the weekly sum, always (design 03 §1). With the attrition and a
        /// starting counter-intelligence of about 14, a network needs roughly 2,900 denars a week
        /// under a handler of 100 roguery just to hold still. UN-TUNED.
        /// </summary>
        public const float NetworkDailyDecay = 0.1f;

        /// <summary>
        /// What the investment term is multiplied by while the owner's realm is at war with the
        /// target: borders are watched. The lead kept networks alive through a war on this rate
        /// rather than collapsing them (design 03 §9, decision 4). UN-TUNED.
        /// </summary>
        public const float NetworkWartimeGrowth = 0.5f;

        /// <summary>A handler's ceiling: 40 + roguery / 2 + charm / 4, capped at the scale's 100. UN-TUNED.</summary>
        public const float NetworkBaseCeiling = 40f;
        public const float NetworkCeilingPerRoguery = 0.5f;
        public const float NetworkCeilingPerCharm = 0.25f;
        public const float NetworkMaxStrength = 100f;

        // ----- Counter-intelligence (design 03 §3) --------------------------------

        /// <summary>What every realm has before it spends anything. UN-TUNED.</summary>
        public const float CounterIntelligenceBase = 10f;

        /// <summary>Per point of the realm's average town and castle security. UN-TUNED.</summary>
        public const float CounterIntelligencePerSecurity = 0.05f;
    }
}
