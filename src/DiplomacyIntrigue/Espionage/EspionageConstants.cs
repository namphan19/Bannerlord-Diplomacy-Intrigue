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

        /// <summary>
        /// Denars of weekly budget, actually paid, per point of counter-intelligence (design 03 §3).
        /// 15,000 a week buys +10. Worked on design 03 §10's example (network 80, handler skill 62,
        /// counter-intelligence 12.6 -> 22.6), overall exposure goes from 3.1% to 6.4% for ScoutArmies
        /// and from 6.1% to 11.7% for Assassinate - by hand, not measured. UN-TUNED.
        /// </summary>
        public const float CounterIntelligenceGoldPerPoint = 1500f;

        /// <summary>The top of §3's 0-100 scale.</summary>
        public const float CounterIntelligenceMax = 100f;

        // ----- The AI (step 3.6) -----------------------------------------------------
        //
        // The lead's calls of 2026-09-25 (design 03 §9, decisions 11-13): an AI ruling house runs
        // at most one network, aimed at a clear rival, spends only a purse with room to spare, and
        // acts only when the chance of being caught is low. Every number below is a first guess.

        /// <summary>What an AI ruler keeps back before spending anything on espionage: the shared figure.</summary>
        public const int AiGoldReserve = Diplomacy.DiplomacyConstants.AiGoldReserve;

        /// <summary>Share of the purse above the reserve an AI ruler puts into its network each week. UN-TUNED.</summary>
        public const float AiNetworkBudgetShare = 0.04f;

        /// <summary>
        /// The most an AI ruler spends on its network in a week. UN-TUNED. By hand: at roguery 50
        /// against a counter-intelligence of 12.6, 6,000 buys 3.75, less 1.01 of counter-intelligence,
        /// 0.7 of attrition and 0.7 of daily decay - about +1.34 a week, so ~22 weeks to the 30 dissent
        /// needs and ~34 to the 45 a bribe needs. Slow on purpose (§1), but a balance run should say
        /// whether an AI ever gets there.
        /// </summary>
        public const int AiNetworkBudgetCap = 6000;

        /// <summary>Counter-intelligence ordered per point of threat (a war is 1, a caught intrusion 2). UN-TUNED.</summary>
        public const int AiCounterBudgetPerThreat = 1500;
        public const float AiCounterThreatPerWar = 1f;
        public const float AiCounterThreatPerExposure = 2f;

        /// <summary>Share of the purse above the reserve an AI ruler will put into counter-intelligence. UN-TUNED.</summary>
        public const float AiCounterBudgetShare = 0.03f;

        /// <summary>The most an AI ruler orders for counter-intelligence in a week. UN-TUNED.</summary>
        public const int AiCounterBudgetCap = 9000;

        /// <summary>
        /// The overall chance of exposure an AI accepts to launch - (1 - success) x exposure on
        /// failure, the figure the mission board shows. 10%, the lead's call (decision 13). UN-TUNED.
        /// </summary>
        public const float AiMaxExposure = 0.10f;

        /// <summary>Days an AI network waits after one operation resolves before the next. UN-TUNED.</summary>
        public const int AiMissionCooldownDays = 14;

        /// <summary>
        /// How much of a rival a realm is: at war 3, holding a territorial claim on them 2, claimed
        /// by them 1, a stronger neighbour 1. A realm bound to us by a pact, an alliance or vassalage
        /// is never a target. The best score of at least 1 gets the network. UN-TUNED.
        /// </summary>
        public const float AiTargetScoreWar = 3f;
        public const float AiTargetScoreClaim = 2f;
        public const float AiTargetScoreClaimedBy = 1f;
        public const float AiTargetScoreStrongerNeighbour = 1f;
        public const float AiTargetMinScore = 1f;

        /// <summary>Proximity (1 next door, 0 across the map) above which a stronger realm counts as a neighbour. UN-TUNED.</summary>
        public const float AiNeighbourProximity = 0.5f;

        /// <summary>
        /// A court worth subverting: crown legitimacy below this, or a standing pretender. Bribes and
        /// forgeries aim at a civil war, and a secure crown does not have one coming. UN-TUNED.
        /// </summary>
        public const float AiSubvertLegitimacy = 50f;

        /// <summary>A house head the AI will bribe: loyalty below this. UN-TUNED.</summary>
        public const float AiBribeMaxLoyalty = 40f;

        /// <summary>A house head the AI sends forged letters to: loyalty in this band, close enough to the defection line for 8 of grievance (x1.5) to push it over. UN-TUNED.</summary>
        public const float AiForgeMinLoyalty = 25f;
        public const float AiForgeMaxLoyalty = 45f;

        /// <summary>StealTreasury only when the take is at least this many times its price. UN-TUNED.</summary>
        public const float AiStealMinReturn = 3f;

        /// <summary>Assassinate only when the purse above the reserve holds this many times its price. UN-TUNED.</summary>
        public const float AiAssassinateGoldMultiple = 2f;
    }
}
