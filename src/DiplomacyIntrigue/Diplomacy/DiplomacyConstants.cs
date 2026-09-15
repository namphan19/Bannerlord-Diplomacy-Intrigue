namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Every tunable number for the diplomacy pillar, in one place.
    ///
    /// Balance work means editing this file and nothing else. The values are first-cut
    /// defaults from docs/design/01-diplomacy.md, meant to be tuned against telemetry in
    /// the Phase 4 balance pass. The player-facing multipliers in ModSettings scale them
    /// at runtime; these are the baseline.
    /// </summary>
    public static class DiplomacyConstants
    {
        // ---- War exhaustion: accrual ----------------------------------------
        // Scale is 0-100 per side, per war.

        /// <summary>Baseline weariness from simply being at war. Both sides, every day.</summary>
        public const float ExhaustionPerDayAtWar = 0.08f;

        /// <summary>
        /// Casualties are divided by (kingdom strength / this), which is what makes
        /// exhaustion relative to size: the same 500 losses wreck a small realm and barely
        /// trouble an empire. Without this, big kingdoms sue for peace as readily as small
        /// ones, which is the vanilla behaviour we are replacing.
        /// </summary>
        public const float ExhaustionCasualtyStrengthDivisor = 100f;

        /// <summary>Floor on the divisor so a collapsing kingdom does not divide by ~0.</summary>
        public const float ExhaustionCasualtyMinDivisor = 1f;

        public const float ExhaustionPerTownLost = 6.0f;
        public const float ExhaustionPerCastleLost = 3.0f;
        public const float ExhaustionPerVillageRaided = 1.0f;

        /// <summary>Per day, for each of our own fortifications currently under siege.</summary>
        public const float ExhaustionPerDayUnderSiege = 0.15f;

        /// <summary>Per day, for each fief this war has cost us and the enemy still holds.</summary>
        public const float ExhaustionPerDayPerOccupiedFief = 0.02f;

        public const float ExhaustionMin = 0f;
        public const float ExhaustionMax = 100f;

        // ---- War exhaustion: thresholds -------------------------------------
        // Read by the court (Phase 2) and the AI peace logic (Phase 1.7).

        /// <summary>Doves start gaining support in the court.</summary>
        public const float ExhaustionCourtPressure = 40f;

        /// <summary>AI actively seeks peace and will accept a white peace.</summary>
        public const float ExhaustionSeekPeace = 60f;

        /// <summary>AI accepts unfavourable terms; fiefs start losing loyalty.</summary>
        public const float ExhaustionAcceptBadTerms = 80f;

        /// <summary>Daily fief loyalty penalty once past <see cref="ExhaustionAcceptBadTerms"/>.</summary>
        public const float LoyaltyPenaltyWhenBroken = -1.0f;

        // ---- Weariness: what a war leaves behind ----------------------------

        /// <summary>
        /// When a war ends, this fraction of the loser-side exhaustion carries into a
        /// per-kingdom pool. It raises the cost of starting a *new* war, so a realm that
        /// just fought a long one cannot immediately start another.
        /// </summary>
        public const float WearinessCarryOverFraction = 0.5f;

        /// <summary>Per day, in peace. Roughly a year to shed a hard-fought war.</summary>
        public const float WearinessDecayPerDay = 0.15f;

        public const float WearinessMax = 100f;

        // ---- War score ------------------------------------------------------
        // Range -100..100, positive means the war's aggressor is ahead.
        // Exhaustion says how tired a side is; war score says who is winning.
        // Peace *willingness* reads exhaustion, peace *terms* read war score.

        public const float WarScoreMin = -100f;
        public const float WarScoreMax = 100f;

        /// <summary>Scales the casualty differential of a field battle into war score.</summary>
        public const float WarScoreBattleFactor = 6f;

        /// <summary>Denominator floor, so a skirmish between scouts cannot swing a war.</summary>
        public const float WarScoreBattleMinTotal = 100f;

        public const float WarScoreBattleMin = 1f;
        public const float WarScoreBattleMax = 8f;

        public const float WarScorePerTownCaptured = 12f;
        public const float WarScorePerCastleCaptured = 6f;
        public const float WarScorePerVillageRaided = 1.5f;

        /// <summary>
        /// Daily pull toward zero. A stalemate should drift to a white peace rather than
        /// sit on a score earned by one battle two years ago.
        /// </summary>
        public const float WarScoreDriftPerDay = 0.05f;

        // ---- Casus belli ----------------------------------------------------

        /// <summary>A fief we held more recently than this supports an ancestral claim.</summary>
        public const int AncestralClaimMemoryYears = 20;

        /// <summary>A claim goes stale this long after it was acquired.</summary>
        public const int ClaimLifetimeYears = 2;

        /// <summary>Window in which an enemy raid on our village justifies a war.</summary>
        public const int AvengeRaidWindowDays = 60;

        /// <summary>Window in which a broken treaty still justifies a war.</summary>
        public const int BrokenTreatyWindowYears = 2;

        // ---- Treaties -------------------------------------------------------

        public const int NonAggressionPactYears = 2;
        public const int TruceYears = 1;
        public const int DefensivePactYears = 3;
        public const int AllianceYears = 3;
        public const int TributaryPactYears = 2;
        public const int VassalageYears = 5;

        public const int NonAggressionPactInfluence = 60;
        public const int DefensivePactInfluence = 100;
        public const int AllianceInfluence = 180;

        /// <summary>A truce is part of a peace settlement, so it is not bought separately.</summary>
        public const int TruceInfluence = 0;

        /// <summary>Tribute and vassalage terms are negotiated, so the pact itself is free.</summary>
        public const int NegotiatedTreatyInfluence = 0;

        /// <summary>Days between tribute payments under a tributary pact or vassalage.</summary>
        public const int TributePeriodDays = 7;

        /// <summary>
        /// Re-declaring war on a partner whose truce we just broke costs this multiple of
        /// the normal influence, on top of the trust damage.
        /// </summary>
        public const float TruceBreachWarCostMultiplier = 3f;

        // ---- Peace table ----------------------------------------------------
        // What a victory buys, priced in war-score points. The winner's war score is the
        // budget; the package has to fit inside it.
        //
        // The design doc originally described demand tiers. A budget reproduces the same
        // intent without exclusive-or branches, and adding a demand type is one constant
        // here rather than a rewritten table. Reference points from the original tiers:
        //   score 45 buys a castle, or money and prisoners
        //   score 90 buys two towns, or a tributary pact with change to spare

        public const float PeaceCostTown = 45f;
        public const float PeaceCostCastle = 25f;
        public const float PeaceCostTributaryPact = 60f;
        public const float PeaceCostPrisoners = 5f;
        public const float PeaceCostPerThousandIndemnity = 8f;

        /// <summary>At or below this war score nothing has been earned: white peace only.</summary>
        public const float PeaceWhitePeaceOnlyBelow = 20f;

        /// <summary>
        /// Margin on what a losing side will concede. Nobody signs away exactly the
        /// arithmetic, and it stops the AI refusing an offer over a rounding error.
        /// </summary>
        public const float PeaceAcceptanceGrace = 0.25f;

        // ---- Call to arms ----------------------------------------------------

        /// <summary>
        /// An ally past this exhaustion will not answer a call to arms. Somebody already
        /// fighting for their life cannot be dragged into another war.
        /// </summary>
        public const float CallToArmsRefuseAboveExhaustion = 70f;

        /// <summary>
        /// Trust below this and an ally does not answer at all. Alliances of convenience
        /// stop working exactly when they are needed.
        /// </summary>
        public const float CallToArmsTrustFloor = 0f;

        /// <summary>
        /// Hours the player has to answer a call to arms before it lapses as a refusal.
        /// </summary>
        public const float CallToArmsPlayerResponseHours = 24f;

        /// <summary>
        /// An ally refuses when the enemy outweighs the two of them by more than this.
        /// Joining a war that cannot be won is not loyalty.
        /// </summary>
        public const float CallToArmsHopelessRatio = 1.5f;

        // ---- Diplomatic trust ------------------------------------------------
        // One value per ORDERED pair: what A thinks of B is not what B thinks of A.

        public const float TrustMin = -100f;
        public const float TrustMax = 100f;

        public const float TrustTreatyHonoured = 12f;
        public const float TrustCallToArmsAnswered = 20f;
        public const float TrustCallToArmsRefused = -15f;
        public const float TrustTreatyBrokenVictim = -35f;
        public const float TrustTreatyBrokenObserver = -12f;
        public const float TrustUnjustWarObserver = -10f;
        public const float TrustPeaceHeld = 8f;
        public const float TrustSpyNetworkExposed = -25f;

        /// <summary>A war declared below this legitimacy offends every uninvolved court.</summary>
        public const float UnjustWarLegitimacyThreshold = 0.3f;

        /// <summary>Years of unbroken peace after a war before the trust dividend is paid.</summary>
        public const int PeaceDividendYears = 2;

        /// <summary>
        /// Below this, a kingdom will sign nothing but a truce with us. This is the lasting
        /// punishment for treachery: not a relation penalty that fades in a season, but a
        /// reputation that follows you for the rest of the campaign.
        /// </summary>
        public const float TrustFloorForPacts = -20f;

        // ---- Per-type lookups -----------------------------------------------

        public static int TreatyDurationYears(Models.TreatyType type)
        {
            switch (type)
            {
                case Models.TreatyType.NonAggressionPact: return NonAggressionPactYears;
                case Models.TreatyType.Truce: return TruceYears;
                case Models.TreatyType.DefensivePact: return DefensivePactYears;
                case Models.TreatyType.Alliance: return AllianceYears;
                case Models.TreatyType.TributaryPact: return TributaryPactYears;
                case Models.TreatyType.Vassalage: return VassalageYears;
                default: return NonAggressionPactYears;
            }
        }

        public static int TreatyInfluenceCost(Models.TreatyType type)
        {
            switch (type)
            {
                case Models.TreatyType.NonAggressionPact: return NonAggressionPactInfluence;
                case Models.TreatyType.Truce: return TruceInfluence;
                case Models.TreatyType.DefensivePact: return DefensivePactInfluence;
                case Models.TreatyType.Alliance: return AllianceInfluence;
                default: return NegotiatedTreatyInfluence;
            }
        }

        // ---- Claim fabrication ----------------------------------------------

        public const int FabricateClaimInfluenceCost = 150;
        public const int FabricateClaimGoldCost = 20000;
        public const int FabricateClaimDurationDays = 30;

        /// <summary>Chance the fabrication is exposed instead of succeeding.</summary>
        public const float FabricateClaimExposureChance = 0.20f;

        public const float FabricateExposedLegitimacyLoss = 15f;
        public const int FabricateExposedRelationLoss = 5;
    }
}
