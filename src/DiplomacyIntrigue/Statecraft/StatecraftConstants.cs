namespace DiplomacyIntrigue.Statecraft
{
    /// <summary>
    /// Every weight of the statecraft layer (design 08, Phase 2.8) in one file, so a balance pass
    /// edits one place. **Every value here is UN-TUNED**: the weights are design 08 §5's proposal,
    /// sized by the rule that no skill term decides alone (§3 rule 5), and the XP amounts are §6's,
    /// sized against one target - an active player ruler gains 2-4 levels a year in a portfolio's
    /// skill around 100-150. S3 (folded into balance run 08) is what measures them.
    /// </summary>
    public static class StatecraftConstants
    {
        // ---- How a skill becomes a number (design 08 §4.2) ----------------------

        /// <summary>
        /// Skill points per whole Level. Every 15 points from the realms' median moves a Level by
        /// 0.1. Chosen so that the template rulers (medians near 220) span about -1..+0.7 across
        /// the skills a player can realistically hold.
        /// </summary>
        public const float LevelSpan = 150f;

        /// <summary>The pivot when no realm has an actor at all, which only a broken world produces.</summary>
        public const float FallbackPivot = 200f;

        // ---- The terms (design 08 §5) --------------------------------------------

        /// <summary>S-1: exhaustion accrues x (1 - this x Level of the ruler's Leadership). ±15%.</summary>
        public const float ResolveWeight = 0.15f;

        /// <summary>S-2: the peace budget x (1 + this x Contest of the envoys' Charm). ±15%.</summary>
        public const float NegotiationWeight = 0.15f;

        /// <summary>S-3: added to the asked side's pact value, x Level of the proposer's envoy. ±10.</summary>
        public const float PersuasionWeight = 10f;

        /// <summary>S-4: added to every Hold target, x Level of the patron ruler's Leadership. ±10.</summary>
        public const float AuthorityWeight = 10f;

        /// <summary>
        /// S-5: exposure = base chance x (1 - this x Contest of spymaster Roguery against the
        /// target's watch Scouting). With the base at 20%, that is 10-30%.
        /// </summary>
        public const float SubterfugeWeight = 0.5f;

        /// <summary>S-6: grievance fade and the legitimacy dividend x (1 + this x Level of the steward).</summary>
        public const float RecoveryWeight = 0.5f;

        /// <summary>S-7: added to every court loyalty, x Level of the ruler's Leadership. ±5.</summary>
        public const float PresenceWeight = 5f;

        /// <summary>S-8: added to each claimant's backing score, x Level of the claimant's own Charm. ±15.</summary>
        public const float BackingWeight = 15f;

        /// <summary>S-9: a bloc member's claim to lead = influence x (1 + this x Level of its head's Charm).</summary>
        public const float BlocVoiceWeight = 0.5f;

        /// <summary>S-10: the side-change price x (1 - this x Contest of the two treasurers' Trade). ±15%.</summary>
        public const float HagglingWeight = 0.15f;

        // ---- Perks the takeover had cut off (design 08 §2, A-2 and A-3) --------

        /// <summary>A-2: vanilla's own Firebrand figure (-25% influence to initiate a decision).</summary>
        public const float FirebrandFactor = 0.75f;

        /// <summary>A-3: vanilla's own Silver Tongue figure (-15% gold to persuade lords to defect).</summary>
        public const float SilverTongueFactor = 0.85f;

        // ---- What an act costs follows the skills that do it (rule 10, the lead's, 2026-09-26) ----

        /// <summary>
        /// A price part is multiplied by this to the power of minus the level (or contest): x0.5 at
        /// +1, x2 at -1. The lead's call: skill should move a price strongly, not by rule 5's ±15%.
        /// UN-TUNED only in its reach, which the lead set.
        /// </summary>
        public const float PriceSkillSpread = 2f;

        // ---- Doing trains the skill (design 08 §6), raw XP before the learning rate ----

        public const float XpNonAggressionPact = 2000f;
        public const float XpDefensivePact = 3000f;
        public const float XpAlliance = 5000f;

        /// <summary>Each envoy, for a peace signed at the table: this plus <see cref="XpPeacePerPoint"/> x the package's cost.</summary>
        public const float XpPeaceBase = 2000f;
        public const float XpPeacePerPoint = 100f;

        public const float XpTributeDemandAccepted = 3000f;
        public const float XpSubmissionReceived = 8000f;

        /// <summary>Each ruler, per day a war lasted, capped at <see cref="XpWarEndedMax"/>.</summary>
        public const float XpWarEndedPerDay = 50f;
        public const float XpWarEndedMax = 8000f;

        public const float XpClaimFabricated = 5000f;
        public const float XpClaimExposed = 2000f;
        public const float XpFabricationCaught = 3000f;

        public const float XpInternalWarWon = 10000f;
        public const float XpPeaceDividend = 5000f;

        /// <summary>Each treasurer, per denar of tribute paid or received.</summary>
        public const float XpPerTributeDenar = 0.5f;

        /// <summary>Vanilla's bribe rate: the buyer's treasurer per denar paid for a house.</summary>
        public const float XpPerSideChangeDenarBuyer = 0.1f;

        /// <summary>The house's head per denar received for changing sides.</summary>
        public const float XpPerSideChangeDenarHouse = 0.05f;

        /// <summary>
        /// Design 09 C1: the envoy's Charm and the treasurer's Trade, each, per point of weight
        /// answered. A weight-8 wrong gives 2,000, a non-aggression pact's worth. UN-TUNED.
        /// </summary>
        public const float XpAmendsPerPoint = 250f;
    }
}
