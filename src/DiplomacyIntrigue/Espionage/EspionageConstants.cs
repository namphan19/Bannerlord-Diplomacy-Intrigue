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

        // ----- Missions (design 03 §2, §4) -----------------------------------------
        //
        // The requirement, price and duration of each mission are design 03 §2's table and live
        // in Missions.SpecOf, next to the type they describe. What is here is the arithmetic.

        /// <summary>Success chance before anything else: 0.15. UN-TUNED.</summary>
        public const float MissionBaseChance = 0.15f;

        /// <summary>Per point of network strength. UN-TUNED.</summary>
        public const float MissionChancePerNetwork = 0.005f;

        /// <summary>Per point of the handler's (roguery + charm) / 2. UN-TUNED.</summary>
        public const float MissionChancePerHandlerSkill = 0.004f;

        /// <summary>Per point of the target's counter-intelligence. UN-TUNED.</summary>
        public const float MissionChancePerCounterIntelligence = 0.006f;

        /// <summary>
        /// §4 names a per-mission difficulty and gives no values. Taken from the network each
        /// mission needs, as one rule rather than eight guesses: (requirement - 15) / 200, so
        /// ScoutArmies costs nothing and Assassinate 0.275. A mission that needs more of a network
        /// is also harder to bring off with it. UN-TUNED.
        /// </summary>
        public const float MissionDifficultyFloor = 15f;
        public const float MissionDifficultyPerRequirement = 1f / 200f;

        public const float MissionChanceMin = 0.05f;
        public const float MissionChanceMax = 0.95f;

        /// <summary>Exposure on failure: 0.25 + 0.008 x counter-intelligence - 0.003 x network, 0.05-0.90. UN-TUNED.</summary>
        public const float ExposureBase = 0.25f;
        public const float ExposurePerCounterIntelligence = 0.008f;
        public const float ExposurePerNetwork = 0.003f;
        public const float ExposureMin = 0.05f;
        public const float ExposureMax = 0.90f;

        /// <summary>Network strength a success uses up (assets get spent). UN-TUNED.</summary>
        public const float MissionSuccessNetworkCost = 5f;

        /// <summary>Network strength a failure costs. UN-TUNED.</summary>
        public const float MissionFailureNetworkCost = 10f;

        /// <summary>How long a successful ScoutArmies keeps the target's armies revealed. UN-TUNED.</summary>
        public const int ScoutArmiesRevealDays = 7;

        /// <summary>How long a successful ReadCourt keeps the target's court revealed exactly. UN-TUNED.</summary>
        public const int ReadCourtRevealDays = 14;

        /// <summary>Share of a garrison's regular troops a successful sabotage removes. UN-TUNED.</summary>
        public const float SabotageGarrisonShare = 0.25f;

        /// <summary>Loyalty a successful SpreadDissent takes from the town. UN-TUNED.</summary>
        public const float DissentLoyaltyLoss = 15f;

        /// <summary>StealTreasury takes min(20% of the ruler's gold, 50,000). UN-TUNED.</summary>
        public const float StealTreasuryShare = 0.2f;
        public const int StealTreasuryCap = 50000;

        /// <summary>
        /// Loyalty a bought house loses to its crown while the bribe holds (design 03 §2). A flat
        /// term in the loyalty sum, not a grievance: a bought lord has been paid, not wronged, and
        /// dressing gold up as resentment would put it in the court's ledger under a false name.
        /// UN-TUNED.
        /// </summary>
        public const float BribeLoyaltyLoss = 20f;

        /// <summary>
        /// How long a bribe holds, for both its effects: the loyalty loss, and the house taking the
        /// rising's side if an internal war starts in its realm (design 03 §2, "within 2 years").
        /// One window rather than a second duration for the loyalty term, the lead's call of
        /// 2026-09-25. UN-TUNED.
        /// </summary>
        public const int BribeWindowDays = 730;

        /// <summary>
        /// A resolved mission is kept this long, then dropped: longer than the longest reveal, so
        /// a reveal never outlives its record. A successful bribe is kept for its whole window
        /// instead (<see cref="BribeWindowDays"/>). UN-TUNED only in the sense that nothing reads it past 14.
        /// </summary>
        public const int ResolvedMissionKeepDays = 60;

        // ----- Exposure as a diplomatic event (design 03 §5) ------------------------

        /// <summary>Trust the victim loses in the owner's realm. UN-TUNED.</summary>
        public const float ExposureVictimTrust = -25f;

        /// <summary>Trust every other realm loses in the owner's realm after an exposed assassination. UN-TUNED.</summary>
        public const float ExposureAssassinationObserverTrust = -15f;

        /// <summary>How long the victim's EspionageExposed casus belli stands, as long as a broken treaty's. UN-TUNED.</summary>
        public const int ExposureClaimYears = 2;

        /// <summary>
        /// Relation the ruler loses with the head of a vassal house whose exposed operation handed
        /// a rival a casus belli against the whole realm. Through relation, so it reaches the
        /// house's loyalty the way the court already reads it (x0.5, so -7.5). UN-TUNED.
        /// </summary>
        public const int ExposureVassalRelationPenalty = -15;

        // ----- Counter-intelligence (design 03 §3) --------------------------------

        /// <summary>What every realm has before it spends anything. UN-TUNED.</summary>
        public const float CounterIntelligenceBase = 10f;

        /// <summary>Per point of the realm's average town and castle security. UN-TUNED.</summary>
        public const float CounterIntelligencePerSecurity = 0.05f;
    }
}
