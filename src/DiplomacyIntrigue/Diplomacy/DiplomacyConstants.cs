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
        ///
        /// Tuned from balance run 01 (28 in-game years, 247 wars): at 100 a chosen war
        /// reached exhaustion 51.6 in 23.6 days - 2.19 per day, of which elapsed time
        /// contributed 0.08. Casualties were ~96% of the accrual and wars lasted three
        /// weeks instead of years. Lowering this to 20 makes casualty exhaustion 5x weaker,
        /// aiming chosen wars at roughly 1.5 years.
        ///
        /// Note the direction: a SMALLER value here means a LARGER divisor and therefore
        /// LESS exhaustion per casualty. Still an estimate - casualties do not scale
        /// linearly with war length - so it wants a second run to confirm.
        /// </summary>
        public const float ExhaustionCasualtyStrengthDivisor = 20f;

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

        /// <summary>
        /// A fief we held more recently than this supports an ancestral claim.
        ///
        /// Tuned from balance run 01: at 20 years, live claims settled at 83-93 across
        /// eight kingdoms - effectively everyone holding a claim on everyone. That made
        /// ReclaimAncestralLand (legitimacy 0.70) the reason behind 39.7% of wars and left
        /// Conquest (0.20), the expensive fallback that is supposed to price naked
        /// aggression, behind only 18.6%. Cheap justification everywhere makes war cheap
        /// everywhere, so the window is shorter now: a claim should outlive a grudge, not a
        /// generation of diplomacy.
        /// </summary>
        public const int AncestralClaimMemoryYears = 12;

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

        // ---- AI diplomacy ---------------------------------------------------
        // One evaluation per kingdom per week, at most one action, usually none.

        /// <summary>
        /// Weights for how much a kingdom wants an agreement. Two of the design doc terms
        /// had no cheap data behind them: trade exposure is proxied by proximity, and
        /// border security is folded into aggression - a weak neighbour on a long border is
        /// a temptation rather than a partner, which is the same statement inverted.
        /// </summary>
        public const float PactWeightSharedThreat = 60f;
        public const float PactWeightProximity = 40f;
        public const float PactWeightTrust = 30f;
        public const float PactWeightAggression = 50f;
        public const float PactWeightRelation = 25f;

        /// <summary>Mutual value needed before each treaty type is worth signing.</summary>
        public const float AiNonAggressionThreshold = 20f;
        public const float AiDefensivePactThreshold = 45f;
        public const float AiAllianceThreshold = 70f;

        /// <summary>A standing territorial claim is most of what makes a neighbour a target.</summary>
        public const float AggressionFromClaim = 0.6f;

        /// <summary>Added aggression per unit of strength advantage over a neighbour.</summary>
        public const float AggressionPerStrengthRatio = 0.4f;

        /// <summary>
        /// Rough width of the campaign map, used to turn a distance into a 0-1 proximity.
        /// Approximate on purpose: it only has to rank neighbours against distant realms.
        /// </summary>
        public const float MapDistanceNormaliser = 900f;

        /// <summary>A realm this worn out does not start anything new.</summary>
        public const float AiMaxExhaustionToExpand = 40f;

        /// <summary>Nor does one still carrying the last war.</summary>
        public const float AiMaxWearinessToExpand = 30f;

        /// <summary>Minimum strength advantage before war is even considered.</summary>
        public const float AiWarStrengthRatio = 1.2f;

        /// <summary>Strength advantage at which submission can be demanded instead of war.</summary>
        public const float AiTributeDemandStrengthRatio = 2.0f;

        public const float WarValuePerStrengthRatio = 40f;
        public const float WarValueLegitimacy = 30f;
        public const float WarValueProximity = 20f;
        public const float WarValueWearinessPenalty = 0.5f;

        /// <summary>
        /// Weight on land hunger - strength share against fief share. This is what keeps
        /// the map moving once the opening wars have been settled; without it, evenly
        /// matched kingdoms have no reachable reason to fight and Calradia freezes.
        /// </summary>
        public const float WarValueLandHunger = 35f;

        /// <summary>
        /// War value needed before a kingdom acts on it, before the aggressiveness setting.
        ///
        /// UNVALIDATED. Set to 25 so that a clearly attractive war can clear it - a realm
        /// 26% stronger than an adjacent neighbour scores 28.7 - but this number has not
        /// been confirmed against play. It cannot be: see the note on tooling limits in
        /// ROADMAP 1.7. Validating it needs a real ten-year campaign, which is the Phase 4
        /// balance task.
        /// </summary>
        public const float AiWarThreshold = 25f;

        /// <summary>
        /// Influence to declare war, before the legitimacy multiplier. A war with no case
        /// costs double, and weariness adds to the bill on top.
        /// </summary>
        public const int WarDeclarationBaseInfluence = 100;

        public const int AiDefaultTributePerPeriod = 500;

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
