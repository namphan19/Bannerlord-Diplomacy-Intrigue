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

        /// <summary>
        /// Baseline weariness from simply being at war. Both sides, every day.
        ///
        /// Raised from 0.08 after measuring the five wars still running in the run-03 save,
        /// and it corrects an error of mine. Run 02 gave 0.654 exhaustion per day for wars
        /// lasting 20 days or more, and I used that figure to predict wars would settle
        /// around day 92. That sample was **wars that had ended** - the fast ones - so it
        /// measured survivorship, not accrual.
        ///
        /// The live wars say otherwise:
        ///
        ///   Sturgia / Western Empire   486 days   exhaustion 58.4   = 0.12/day
        ///   Southern Empire / Aserai   432 days   exhaustion 42.5   = 0.10/day
        ///   Vlandia / Northern Empire  310 days   exhaustion 30.1   = 0.10/day
        ///
        /// At 0.10-0.12 a day, reaching the threshold of 60 takes 500-600 days. Two thirds of
        /// that came from the calendar and only a third from the fighting, even at 26
        /// casualties a day - because the casualty term divides by kingdom strength, and
        /// mature kingdoms field 13,000.
        ///
        /// Casualties per day swing by a factor of five between world states, so tuning the
        /// casualty term to hit a target length only works for one kind of war. The calendar
        /// does not swing. At 0.30 a day a war reaches the threshold at day 200 on the clock
        /// alone, and around day 170 once fighting is added - which is the band wars are
        /// meant to occupy, and it makes war length something a player can plan around
        /// rather than a function of battle luck.
        /// </summary>
        public const float ExhaustionPerDayAtWar = 0.30f;

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

        /// <summary>
        /// Weariness below this is not worth carrying at all. A war that ended with barely
        /// any exhaustion leaves no mark, which stops a flurry of short wars from adding up
        /// to a permanent brake.
        /// </summary>
        public const float WearinessCarryOverMinimum = 10f;

        // ---- Weariness: what a war leaves behind ----------------------------

        /// <summary>
        /// When a war ends, this fraction of the loser-side exhaustion carries into a
        /// per-kingdom pool. It raises the cost of starting a *new* war, so a realm that
        /// just fought a long one cannot immediately start another.
        /// </summary>
        public const float WearinessCarryOverFraction = 0.5f;

        /// <summary>
        /// Fraction of the remaining pool shed per day, in peace.
        ///
        /// Proportional, not flat, and that is the whole point. A flat drain cannot bound a
        /// pool that keeps receiving injections: measured at the end of run 01, every
        /// kingdom sat above 80 weariness because 247 wars had each added up to 30 while a
        /// flat 0.15/day removed far less. Proportional decay is self-limiting - at 80 this
        /// sheds 1.6 a day, at 20 only 0.4 - so a realm that fights constantly stays weary
        /// without ever pinning at the ceiling.
        /// </summary>
        public const float WearinessDecayFractionPerDay = 0.02f;

        /// <summary>
        /// Floor on the daily shed, so a nearly-clear pool still finishes clearing rather
        /// than trailing an asymptote forever.
        /// </summary>
        public const float WearinessDecayMinimumPerDay = 0.05f;

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

        /// <summary>
        /// How long a war must have run before indifference alone can end it. Two seasons:
        /// long enough that a slow campaign is not mistaken for no campaign.
        /// </summary>
        public const float DormantWarDays = 42f;

        /// <summary>
        /// Total casualties, both sides, under which a war counts as never really fought.
        /// A few hundred is a handful of skirmishes between parties that happened to meet.
        /// </summary>
        public const int DormantWarCasualties = 300;

        /// <summary>
        /// Casualties per day, both sides, below which a war is not being fought however long
        /// it has been going and however much has added up.
        ///
        /// Judging dormancy on a *total* casualty count has a blind spot: a war that creeps
        /// past 300 casualties over a year can never be closed by the absolute rule, however
        /// little is happening in it. A rate catches that; a total cannot.
        ///
        /// **A correction to my own earlier reasoning.** I first justified this by claiming
        /// the five long wars in the run-03 save were barely being fought - inferred from
        /// average exhaustion against the longest war's duration, which mixed two different
        /// wars together. Reading the actual records disproved it: those wars run at 11 to 53
        /// casualties a day, none of them is dormant under any threshold, and the reason they
        /// would not end was the exhaustion *rate*, now fixed at
        /// <see cref="ExhaustionPerDayAtWar"/>. This rule still earns its place for wars
        /// nobody fights at all - it closed four of them in run 03 - but it was not the fix
        /// for the long wars, and saying so was wrong.
        ///
        /// The four it did catch ran at 1.9, 5.8, 3.0 and 1.9 casualties per day.
        /// **Provisional** at 3/day.
        /// </summary>
        public const float DormantWarCasualtiesPerDay = 3f;

        /// <summary>
        /// The least a winning side will settle for, as a fraction of what the war earned.
        ///
        /// Exists because the concession ladder was unreachable: the tired side offered a
        /// white peace, and the only willingness check asked that same side whether it would
        /// sign - so it granted itself a free peace and the winner was never consulted. Run
        /// 02: `terms=white_peace` 13 times out of 13, across 13 in-game years.
        ///
        /// With the loser's tolerance at score x 1.25 and this at half the score, the window
        /// a package must land in is [0.5, 1.25] x score. Wide enough that the ladder's
        /// discrete rungs - prisoners 5, castle 25, town 45, tribute 60 - usually fit.
        /// </summary>
        public const float PeaceWinnerMinimumShare = 0.5f;

        /// <summary>
        /// A winning side abandons its demands and takes a white peace once its own
        /// exhaustion reaches this. The escape valve that stops a war deadlocking when the
        /// loser cannot afford anything the winner would accept.
        ///
        /// At the 0.65/day accrual measured in run 02 this arrives around day 108, which is
        /// inside the 100-200 day band wars are meant to occupy.
        /// </summary>
        public const float ExhaustionAcceptWhitePeaceWhenWinning = 70f;

        // ---- Submission and hegemony (1.9 / 1.10) ----------------------------
        //
        // A hegemon is not a title anyone claims: it is any kingdom holding at least one
        // active vassalage. Several coexist by construction. See docs/design/04-hegemony.md.

        /// <summary>
        /// War score a victor needs before it can demand submission - the top rung of the
        /// concession ladder, above a tributary pact at 60.
        ///
        /// Measured rather than guessed: run 03 produced two wars ending at war score
        /// **exactly 100** (`Battania / Northern Empire`, `Khuzait / Sturgia`), both of which
        /// settled for a tributary pact because this rung did not exist yet. Roughly 0.3
        /// such wars a year, so a hegemony forms about every three years.
        /// </summary>
        public const float PeaceCostVassalage = 90f;

        /// <summary>Where an imposed vassalage starts: submission at swordpoint holds poorly.</summary>
        public const float HoldOnCoercedSubmission = 35f;

        /// <summary>Where a voluntary submission starts. A volunteer is a far steadier vassal.</summary>
        public const float HoldOnVoluntarySubmission = 60f;

        /// <summary>
        /// Where a vassalage imposed at a peace table starts when the loser walked out of a
        /// vassalage to the same winner within <see cref="BrokenTreatyWindowYears"/> - a revolt
        /// crushed, or a vassal taken back by force from the rival it defected to.
        ///
        /// This constant existed for a long time with nothing reading it, so the behaviour it
        /// describes did not exist either. Wired in PeaceTable.ImposeSubmission.
        /// </summary>
        public const float HoldAfterFailedRevolt = 20f;

        /// <summary>Hold for a link loaded from a save that predates the field.</summary>
        public const float HoldDefault = 40f;

        /// <summary>
        /// How far Hold moves toward its target each day.
        ///
        /// Slow on purpose. A patron that loses a war does not lose its vassals that
        /// afternoon, and a vassal neglected for a season does not forgive it overnight.
        /// Everything in the target formula is a pressure, not an event.
        /// </summary>
        public const float HoldDriftPerDay = 1f;

        // Terms of the Hold target. The lead's source document (§16) as a weighted sum of
        // quantities the mod already tracks.
        public const float HoldBase = 40f;

        /// <summary>
        /// Fear: the balance of strength between patron and vassal, on a log scale clamped to
        /// +/-1 (Hegemony.PowerBalance) - so twice as strong reads +25 and half as strong -25.
        ///
        /// It was `ratio - 1` until the review of the run-04 world, which is lopsided: twice
        /// the vassal's strength earned the full +25, but half of it cost only -12.5, and the
        /// floor of -25 was reachable only by a patron with no army at all. That world was the
        /// case it hid: Northern Empire, weaker than six of its seven vassals, lost at most
        /// 13.6 Hold to any of them for it, and trust (+15) paid most of that back.
        /// </summary>
        public const float HoldStrengthWeight = 25f;
        /// <summary>Protection: wars of the vassal's the patron has joined, against those it ignored.</summary>
        public const float HoldProtectionWeight = 20f;
        public const float HoldTrustWeight = 15f;
        /// <summary>Tribute measured against the vassal's holdings, not in absolute denars.</summary>
        public const float HoldTributeBurdenWeight = 20f;
        /// <summary>Wars fought for someone else.</summary>
        public const float HoldWarBurdenWeight = 15f;
        /// <summary>Somebody stronger is available to submit to instead (§8.6, lack of alternatives).</summary>
        public const float HoldRivalWeight = 25f;
        public const float HoldCultureMismatchWeight = 10f;

        /// <summary>Denars of tribute per fief that counts as a full burden. Un-tuned.</summary>
        public const float HoldTributePerFiefForFullBurden = 200f;

        /// <summary>Obligation wars at once that count as a full war burden. Un-tuned.</summary>
        public const int HoldWarBurdenSaturation = 3;

        /// <summary>At or above this the vassal renews willingly when the term runs out.</summary>
        public const float HoldRenewThreshold = 70f;

        /// <summary>Below this a vassal refuses service and pays tribute late.</summary>
        public const float HoldPassiveResistanceThreshold = 40f;

        /// <summary>Below this a vassal will treat with outsiders despite the terms.</summary>
        public const float HoldDefianceThreshold = 30f;

        /// <summary>
        /// Below this, sustained, a vassal fights for its independence - for a vassal exactly
        /// as strong as its patron. The line moves with the balance of strength; see
        /// <see cref="SecessionCapabilityWeight"/>.
        /// </summary>
        public const float HoldSecessionThreshold = 15f;

        /// <summary>
        /// How far the secession line moves with the vassal's strength against its patron's:
        /// up to 30 for a vassal twice the patron's strength, down to 0 - never alone - for one
        /// half as strong (Hegemony.SecessionThreshold).
        ///
        /// A revolt is a war, and the decision to start one used to read resentment only. A
        /// vassal a fifth of its patron's size revolted at exactly the same Hold as one twice
        /// its size - so the weak ones marched to certain defeat, and the strong ones sat
        /// sullen under a patron they could have thrown off. Run 04's world ended with
        /// Northern Empire, weaker than six of its seven vassals, holding every one of them.
        /// Resentment is the motive; strength is whether acting on it is anything but
        /// suicide. Both have to be there. A weak vassal alone is not trapped: its link still
        /// lapses at the end of its term, and it can rise with a stronger sibling.
        ///
        /// **Un-tuned.** 15 keeps the band symmetric around the old fixed line.
        /// </summary>
        public const float SecessionCapabilityWeight = 15f;

        /// <summary>How long Hold must stay under the secession threshold before the revolt.</summary>
        public const float SecessionDaysBelowThreshold = 30f;

        /// <summary>Defiance marks inside a year that let the vassalage lapse at its term.</summary>
        public const int DefianceMarksToLapse = 2;

        /// <summary>A mark older than this is forgotten.</summary>
        public const float DefianceMarkMemoryDays = 84f;

        /// <summary>Hold lost by the patron's other vassals when one of them revolts.</summary>
        public const float SecessionContagionHold = 10f;

        /// <summary>
        /// A vassal whose Hold is below this once the contagion has landed joins a revolt that
        /// has just broken out, rather than waiting out its own thirty days.
        ///
        /// Added after the design review of run 04, which found a saturated hegemony with no
        /// way out: revolt was a decision each vassal took alone, against a patron that then
        /// called half its other vassals onto the rebel. Seven resentful vassals revolting
        /// one at a time lose seven times; the classic answer is that the first mover is the
        /// signal the others were waiting for. Below this line they were already defiant
        /// (treating with outsiders at 30), so joining is the next step, not a new one.
        ///
        /// **Un-tuned.** At 25, a sibling needs to have been under 35 before the news arrived.
        /// </summary>
        public const float RevoltJoinBelowHold = 25f;

        /// <summary>
        /// Least days between two defiance marks earned by withholding tribute.
        ///
        /// Withholding used to cost the vassal nothing at all: no mark, no trust, and the
        /// patron had no lever - so below Hold 40 keeping the money was simply free, and run
        /// 04 logged 193 withheld payments. It is defiance and now counts as such, but a
        /// payment falls due every 7 days and a mark every week would turn one sullen season
        /// into a broken treaty. Four weeks means a vassal that keeps withholding reaches two
        /// marks in about a month and will not renew at its term. **Un-tuned.**
        /// </summary>
        public const float TributeWithheldMarkIntervalDays = 28f;

        /// <summary>Trust the poacher loses with the patron whose vassal it took.</summary>
        public const float PoachingTrustCost = -30f;

        /// <summary>
        /// Relation the two rulers lose over a poached vassal, on top of the trust cost.
        ///
        /// Fifteen against the fabrication penalty's five, because this is done openly and
        /// to their face. Trust is the institutional memory between realms; relation is what
        /// the two people think of each other, and taking a man's vassal is personal.
        /// </summary>
        public const int PoachingRelationLoss = 15;

        /// <summary>A vassal will listen to a rival patron below this Hold.</summary>
        public const float PoachableBelowHold = 40f;

        /// <summary>Years of grace between ex-vassals when a hegemon is destroyed.</summary>
        public const int HegemonyCollapseGraceYears = 2;

        /// <summary>Value at which a cornered kingdom offers its submission.</summary>
        public const float AiSubmissionThreshold = 55f;

        // Weights of the submission valuation (design 04 §3.2).

        /// <summary>
        /// Weight on the threat term - the strength of everyone at war with the candidate,
        /// against its own, clamped at twice over.
        ///
        /// **Lowered from 70 after run 04, where submission stopped being a decision.** At 70
        /// the term reached 140 against a threshold of 55, so a kingdom outnumbered two to
        /// one knelt whatever else was true of it: nine submissions at values 55.5-166.9,
        /// four of them in the first ninety seconds of play, and one kingdom ended the run
        /// holding all seven others. Every other term in the valuation was decoration.
        ///
        /// The rule now, and the same one the war valuation's strength term was capped to:
        /// **no single term may clear the threshold on its own.** At 25 the term tops out at
        /// 50 against the bar of 55, so being surrounded is never by itself enough - a
        /// cornered kingdom also has to be within reach of the patron, worn down, or already
        /// trust it. Re-scored against run 04's nine submissions, three still happen.
        ///
        /// **Since scaled by the patron's cover** (Hegemony.SubmissionValue): the term is how
        /// much danger the patron can actually take off the candidate's hands, not how much
        /// danger there is. Before that, the valuation had no term that depended on the
        /// patron's strength at all, so a cornered kingdom knelt to its nearest same-culture
        /// neighbour whether or not that neighbour could protect it. That re-scoring of run 04
        /// predates the change and no longer applies.
        ///
        /// **Un-tuned beyond that arithmetic.** Run 05 is the measurement.
        /// </summary>
        public const float SubmissionThreatWeight = 25f;
        public const float SubmissionReachWeight = 40f;
        public const float SubmissionWearinessWeight = 30f;
        public const float SubmissionTrustWeight = 20f;
        public const float SubmissionPrideWeight = 50f;
        public const float SubmissionCultureWeight = 20f;

        /// <summary>
        /// A vassal past this exhaustion is excused from a call to arms even by its patron.
        /// Lower than the general bar: a patron that marches a dying vassal loses it.
        /// </summary>
        public const float VassalExcusedAboveExhaustion = 50f;

        // ---- Power: ambition, greed, and what they provoke (docs/design/06-power.md) --------
        //
        // The lead's design, 2026-09-16: strength makes a ruler hungry for war, the rest of the
        // map bands together against whoever is strongest, and a ruler grown too strong stops
        // wanting vassals and starts wanting provinces - which its vassals can see coming.
        // Every value below is un-tuned; run 06 is the first measurement.

        /// <summary>
        /// Days of the smoothed-strength average: the fraction of the gap closed each day is
        /// one over this. One in-game year (84 days).
        ///
        /// Longer than a war, which is the point: run 04's wars had a median of 63 days and
        /// run 05's 76, so the losses and recruiting of a single war move the average by about
        /// half rather than all the way. Greed and dread should describe a reign, not a campaign.
        /// </summary>
        public const float StrengthSmoothingDays = 84f;

        /// <summary>
        /// Dominance at which ambition is full. With eight kingdoms: 31% of the world's
        /// strength. Ambition is zero at an even split and rises linearly to here.
        /// </summary>
        public const float AmbitionFullAtDominance = 2.5f;

        /// <summary>
        /// War value added by full ambition. Under the war threshold of 18, so ambition alone
        /// never starts a war - the rule every term of the valuation follows since run 03.
        /// </summary>
        public const float WarValueAmbition = 15f;

        /// <summary>
        /// Pact value an ambitious ruler loses: a realm hungry for war has little use for a
        /// promise not to make one.
        /// </summary>
        public const float PactWeightAmbition = 20f;

        /// <summary>
        /// Live dominance at which a kingdom's own evaluation allows a second war of its choosing
        /// at once. With eight kingdoms: a quarter of the world's strength. Below it the
        /// restraint of <see cref="AiMaxConcurrentChosenWars"/> stands, for the reason run 02
        /// gave it.
        /// </summary>
        public const float AiDominanceForSecondWar = 2f;

        /// <summary>
        /// Smoothed dominance at which greed begins; it is full one even share above. With
        /// eight kingdoms: from 25% of the world's strength, full at 37.5%.
        /// </summary>
        public const float GreedStartsAtDominance = 2f;

        /// <summary>
        /// Greed at which a ruler takes no new vassals - no voluntary submission, no poaching,
        /// no vassalage at its peace table - and may make war on the ones it has.
        /// </summary>
        public const float GreedRefusesVassals = 0.5f;

        /// <summary>
        /// War value a greedy patron adds, at full greed, for turning on its own vassal: the
        /// vassal is near, weaker by construction, and already half-owned.
        /// </summary>
        public const float AnnexGreedWeight = 20f;

        /// <summary>
        /// War value taken off for tearing up the vassalage to do it. With the greed weight,
        /// annexation is 5 points less attractive than an ordinary war at the greed threshold
        /// and 5 more at full greed. The trust, the casus belli handed over and the influence
        /// of a Conquest war are charged on top, by the machinery that always charges them.
        /// </summary>
        public const float AnnexBreachPenalty = 15f;

        /// <summary>
        /// Hold a vassal loses to dread, at its patron's full greed. On a different axis from
        /// fear on purpose: fear reads the patron against this vassal, dread reads the patron
        /// against the world. A patron twice its vassal's strength at full greed nets zero -
        /// strong enough to hold them, too strong to be trusted with them.
        /// </summary>
        public const float HoldDreadWeight = 25f;

        /// <summary>
        /// How far the revolt line rises, at the patron's full greed. A vassal that expects to
        /// be swallowed has less to lose by fighting at poor odds - so capability matters less
        /// when the alternative is extinction.
        /// </summary>
        public const float SecessionDreadWeight = 15f;

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

        /// <summary>
        /// Weight on the balancing pull: how far the strongest sphere neither party belongs to
        /// outweighs the two of them together, clamped to 0..1.
        ///
        /// Added after the design review of run 04, where one sphere swallowed the map and
        /// nothing pushed back. Every other pact term reads the present - wars already being
        /// fought, borders, trust - so a rising power was never a reason to stand together
        /// until it was already at somebody's gates. Balancing against the dominant power is
        /// the oldest counterweight in the book and the one this evaluation lacked.
        ///
        /// It is zero across a balanced map (at campaign start no kingdom outweighs two
        /// others) and only bites once one sphere is more than any pair of outsiders combined.
        /// **Un-tuned**: 40 lets it carry two neighbours to a non-aggression pact on its own
        /// when a sphere is twice their weight, and not to a defensive pact. That breaks the
        /// "no single term clears the bar" rule the war and submission valuations follow, and
        /// knowingly: the pact valuation never followed it (shared threat is worth 60), a
        /// non-aggression pact is the cheapest commitment on the board, and two realms
        /// facing a power twice their combined size have reason enough.
        /// </summary>
        public const float PactWeightBalancing = 40f;

        /// <summary>
        /// Mutual value needed before each treaty type is worth signing.
        ///
        /// Raised after run 01. At 20, a non-aggression pact was worth signing with anyone a
        /// kingdom merely did not dislike, and eight kingdoms pacted themselves into a
        /// locked map: a standing web of 10 truces, 11 defensive pacts and 5 alliances, with
        /// almost no wars left possible. A pact should mean the two realms actively want one,
        /// not that they have no particular quarrel.
        /// </summary>
        public const float AiNonAggressionThreshold = 35f;
        public const float AiDefensivePactThreshold = 55f;
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

        /// <summary>
        /// Nor does one still carrying the last war.
        ///
        /// Raised from 30 after run 01. A war ending at exhaustion 60 carries 30 weariness,
        /// so the old cap blocked a kingdom immediately after *every* war it fought and only
        /// released it eight months later. 45 keeps the brake for realms that were genuinely
        /// hammered while letting an ordinary war be followed by another.
        /// </summary>
        public const float AiMaxWearinessToExpand = 45f;

        /// <summary>
        /// How many wars of its own choosing a kingdom will run at once. Wars it was dragged
        /// into by a treaty do not count - those were not its decision.
        ///
        /// Run 02 measured 148 chosen wars in 13.1 years across eight kingdoms: 1.4 per
        /// kingdom per year. That was survivable only because vanilla ended every war in six
        /// days. At the ~92 days the exhaustion model actually produces, 1.4 per year is 154%
        /// of the calendar - a kingdom permanently at war with everyone.
        ///
        /// This is restraint in the AI's own evaluation, not a rule of the world: the player
        /// may start as many wars as they like and pay for it in exhaustion. No hidden
        /// modifier sits in the shared machinery.
        /// </summary>
        public const int AiMaxConcurrentChosenWars = 1;

        /// <summary>
        /// Minimum strength advantage before war is even considered.
        ///
        /// Lowered from 1.2 after run 01: eight kingdoms sat within 6,000-7,100 strength of
        /// one another, so a 20% advantage almost never existed and the gate alone ruled out
        /// most of the map. Measuring the mature world afterwards showed even 1.05 was rarely
        /// met - the best ratio Aserai could find against any neighbour was 1.08.
        ///
        /// At 1.0 a kingdom may attack an equal. An earlier version of this comment added
        /// that the valuation "still discourages attacking upward, because the strength
        /// term goes negative below parity" - it does not, and cannot: this gate skips a
        /// stronger target before the valuation is ever reached, so that branch is dead.
        /// Attacking upward is forbidden outright, not priced.
        /// </summary>
        public const float AiWarStrengthRatio = 1.0f;

        /// <summary>Strength advantage at which submission can be demanded instead of war.</summary>
        public const float AiTributeDemandStrengthRatio = 2.0f;

        public const float WarValuePerStrengthRatio = 40f;

        /// <summary>
        /// Ceiling on the strength-advantage term, as a multiple of our own strength: at 1
        /// a kingdom twice as strong scores the full 40 and one four times as strong scores
        /// no more.
        ///
        /// Added after run 03, where this was the only unbounded term in the valuation and
        /// reached at least 138 in one declaration against the 85 that legitimacy,
        /// proximity and land hunger can reach together. The threshold is a floor, so an
        /// unbounded term does not create wars - it decides which target is chosen, and it
        /// made "whoever is weakest" outrank every other reason to fight.
        ///
        /// **Un-tuned.** The cap was chosen so a decisive advantage weighs about as much as
        /// a good claim across a shared border, not measured. Run 04 is the first data.
        /// </summary>
        public const float WarValueMaxStrengthAdvantage = 1f;

        public const float WarValueLegitimacy = 30f;
        public const float WarValueProximity = 20f;
        /// <summary>
        /// Weariness already gates expansion outright at AiMaxWearinessToExpand, so the
        /// value penalty only needs to make a tired realm pickier, not paralysed. At 0.5 a
        /// kingdom at the cap lost 22.5 points before anything else was weighed, and one
        /// above the cap - which every kingdom was, by the end of run 01 - lost 40.
        /// </summary>
        public const float WarValueWearinessPenalty = 0.15f;

        /// <summary>
        /// Weight on land hunger - strength share against fief share. This is what keeps
        /// the map moving once the opening wars have been settled; without it, evenly
        /// matched kingdoms have no reachable reason to fight and Calradia freezes.
        /// </summary>
        public const float WarValueLandHunger = 35f;

        /// <summary>
        /// War value needed before a kingdom acts on it, before the aggressiveness setting.
        ///
        /// Lowered to 18 now that this evaluation is the *only* source of wars between
        /// kingdoms. At 25 it declared 0.9 wars a year across eight kingdoms while vanilla
        /// produced 7.9; carrying that load alone needs a far lower bar. Players who want a
        /// quieter or bloodier world have the AiAggressiveness multiplier in the settings.
        /// </summary>
        public const float AiWarThreshold = 18f;

        /// <summary>
        /// Influence to declare war, before the legitimacy multiplier. A war with no case
        /// costs double, and weariness adds to the bill on top.
        ///
        /// Lowered from 100 after run 01, where this was the binding constraint: a war cost
        /// 180-240 influence while AI ruling clans held 170-230, so a kingdom could afford
        /// roughly one war ever. Vanilla's wars were free by comparison, which is why it won
        /// the initiative 9 to 1. At 40 the cost still bites - 72 for naked aggression
        /// against 52 for a reclaimed holding - without being the whole treasury.
        /// </summary>
        public const int WarDeclarationBaseInfluence = 40;

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
