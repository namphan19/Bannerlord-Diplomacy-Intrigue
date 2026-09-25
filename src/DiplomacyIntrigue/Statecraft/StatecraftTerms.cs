using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace DiplomacyIntrigue.Statecraft
{
    /// <summary>
    /// Every skill term of design 08 §5, one function each, with the line the UI shows for it.
    /// The systems call these rather than reading a hero themselves, so the number a breakdown
    /// prints is the number the formula used (design 08 §3 rule 6), and each term has exactly one
    /// home. None of them takes an "is this the player" argument.
    /// </summary>
    public static class StatecraftTerms
    {
        // ----- S-1 Resolve: how fast a side tires ---------------------------------

        /// <summary>
        /// The multiplier on every exhaustion accrual of a side led by <paramref name="leader"/>:
        /// the ruler of a realm, or either leader of an internal war (the claimant for the rising).
        /// </summary>
        public static float ResolveFactor(Hero leader)
            => 1f - StatecraftConstants.ResolveWeight * StatecraftModel.Level(leader, DefaultSkills.Leadership);

        public static float ResolveFactor(Kingdom kingdom) => ResolveFactor(kingdom?.Leader);

        public static string ResolveLine(Hero leader)
            => "Resolve: " + StatecraftModel.Who(leader, DefaultSkills.Leadership) + ": exhaustion "
               + StatecraftModel.Factor(ResolveFactor(leader));

        // ----- S-2 Negotiation: what a victory buys at the table -----------------------

        /// <summary>The multiplier on a winner's peace budget: its envoy against the loser's.</summary>
        public static float NegotiationFactor(Kingdom winner, Kingdom loser)
        {
            if (!StatecraftModel.Enabled || winner == null || loser == null) return 1f;
            var contest = StatecraftModel.Contest(StatecraftModel.Level(winner, Portfolio.Envoy),
                StatecraftModel.Level(loser, Portfolio.Envoy));
            return 1f + StatecraftConstants.NegotiationWeight * contest;
        }

        public static string NegotiationLine(Kingdom winner, Kingdom loser)
            => "Negotiation: " + StatecraftModel.Who(StatecraftModel.Actor(winner, Portfolio.Envoy), DefaultSkills.Charm)
               + " against " + StatecraftModel.Who(StatecraftModel.Actor(loser, Portfolio.Envoy), DefaultSkills.Charm)
               + ": budget " + StatecraftModel.Factor(NegotiationFactor(winner, loser));

        // ----- S-3 Persuasion: what the other court makes of a pact -------------------

        /// <summary>Added to the value the asked side puts on a pact, from the proposer's envoy.</summary>
        public static float Persuasion(Kingdom proposer)
            => StatecraftConstants.PersuasionWeight * StatecraftModel.Level(proposer, Portfolio.Envoy);

        public static string PersuasionLine(Kingdom proposer)
            => "Envoy's persuasion, " + StatecraftModel.Who(StatecraftModel.Actor(proposer, Portfolio.Envoy), DefaultSkills.Charm)
               + ": " + StatecraftModel.Signed(Persuasion(proposer));

        // ----- S-4 Authority: how firmly a patron's vassals hold -------------------------

        public static float Authority(Kingdom patron)
            => StatecraftConstants.AuthorityWeight * StatecraftModel.Level(patron, Portfolio.Ruler);

        // ----- S-5 Subterfuge: whether a fabricated claim is caught ----------------------

        /// <summary>
        /// The chance a fabrication by <paramref name="fabricator"/> against <paramref name="target"/>
        /// comes to light: the base chance moved by its spymaster's Roguery against the target's
        /// watch's Scouting. Read when the prompt shows it and again when the claim resolves.
        /// </summary>
        public static float ExposureChance(Kingdom fabricator, Kingdom target)
        {
            var chance = DiplomacyConstants.FabricateClaimExposureChance;
            if (!StatecraftModel.Enabled || fabricator == null || target == null) return chance;
            var contest = StatecraftModel.Contest(StatecraftModel.Level(fabricator, Portfolio.Spymaster),
                StatecraftModel.Level(target, Portfolio.Watch));
            return chance * (1f - StatecraftConstants.SubterfugeWeight * contest);
        }

        public static string ExposureLine(Kingdom fabricator, Kingdom target)
            => (ExposureChance(fabricator, target) * 100f).ToString("0") + "% chance of being caught ("
               + StatecraftModel.Who(StatecraftModel.Actor(fabricator, Portfolio.Spymaster), DefaultSkills.Roguery)
               + " against their watch, "
               + StatecraftModel.Who(StatecraftModel.Actor(target, Portfolio.Watch), DefaultSkills.Scouting) + ")";

        // ----- S-6 Recovery: how fast a house forgets, and a crown recovers -----------------

        /// <summary>
        /// The multiplier on grievance fade against <paramref name="house"/> and on the legitimacy
        /// dividend of the realm it rules: that house's steward.
        /// </summary>
        public static float RecoveryFactor(Clan house)
            => 1f + StatecraftConstants.RecoveryWeight
               * StatecraftModel.Level(StatecraftModel.Actor(house, Portfolio.Steward), DefaultSkills.Steward);

        public static string RecoveryLine(Clan house)
            => "Recovery: " + StatecraftModel.Who(StatecraftModel.Actor(house, Portfolio.Steward), DefaultSkills.Steward)
               + ": " + StatecraftModel.Factor(RecoveryFactor(house));

        // ----- S-7 Presence: what a court thinks of its crown -----------------------------

        public static float Presence(Kingdom kingdom)
            => StatecraftConstants.PresenceWeight * StatecraftModel.Level(kingdom, Portfolio.Ruler);

        // ----- S-8 Backing: whom houses back for the throne -------------------------------

        /// <summary>A claimant's own Charm, added to every house's score for them. Personal: never delegated.</summary>
        public static float Backing(Hero claimant)
            => StatecraftConstants.BackingWeight * StatecraftModel.Level(claimant, DefaultSkills.Charm);

        // ----- S-9 Bloc voice: who speaks for a bloc ---------------------------------------

        /// <summary>
        /// A member's claim to speak for its bloc: influence, weighted by its head's own Charm. Bloc
        /// power stays the plain sum of influence, so the civil-war trigger does not move.
        /// </summary>
        public static float Voice(Clan member)
        {
            if (member == null) return 0f;
            // A house in debt to the court speaks for nobody however charming its head, and is
            // left in its plain order so that with the layer off the ranking is exactly design 02's.
            var influence = member.Influence;
            if (influence <= 0f) return influence;
            return influence * (1f + StatecraftConstants.BlocVoiceWeight
                                 * StatecraftModel.Level(member.Leader, DefaultSkills.Charm));
        }

        // ----- S-10 Haggling, and A-3 Silver Tongue: what a house costs to turn ----------

        /// <summary>The multiplier on a side-change price: the buyer's treasurer against the house's.</summary>
        public static float HagglingFactor(Hero buyer, Clan house)
        {
            if (!StatecraftModel.Enabled || buyer == null || house == null) return 1f;
            var contest = StatecraftModel.Contest(
                StatecraftModel.Level(StatecraftModel.Actor(buyer.Clan, Portfolio.Treasurer), DefaultSkills.Trade),
                StatecraftModel.Level(StatecraftModel.Actor(house, Portfolio.Treasurer), DefaultSkills.Trade));
            return 1f - StatecraftConstants.HagglingWeight * contest;
        }

        public static string HagglingLabel(Hero buyer, Clan house)
        {
            var theirs = StatecraftModel.Actor(buyer?.Clan, Portfolio.Treasurer);
            var ours = StatecraftModel.Actor(house, Portfolio.Treasurer);
            return "Haggling: " + StatecraftModel.NameOf(theirs) + " (Trade "
                   + (theirs == null ? 0 : theirs.GetSkillValue(DefaultSkills.Trade)) + ") against "
                   + StatecraftModel.NameOf(ours) + " (Trade "
                   + (ours == null ? 0 : ours.GetSkillValue(DefaultSkills.Trade)) + ")";
        }

        /// <summary>
        /// Who bargains for the buyer holds Silver Tongue. The treasurer, the hero doing the
        /// haggling, rather than the buyer: design 08 §3 rule 3, the hero who acts is the hero
        /// whose skill counts, and a perk is part of that hero's skill.
        /// </summary>
        public static Hero SilverTongueHolder(Hero buyer)
        {
            var treasurer = StatecraftModel.Actor(buyer?.Clan, Portfolio.Treasurer);
            return treasurer != null && treasurer.GetPerkValue(DefaultPerks.Trade.SilverTongue) ? treasurer : null;
        }

        // ----- A-2 Firebrand: what a proposal costs ---------------------------------------

        /// <summary>
        /// Vanilla's Firebrand discount on initiating a kingdom decision, applied to the mod's own
        /// war and treaty prices, which took those decisions over. Everyone's, AI rulers included.
        /// </summary>
        public static float FirebrandFactor(Hero proposer)
            => proposer != null && proposer.GetPerkValue(DefaultPerks.Charm.Firebrand)
                ? StatecraftConstants.FirebrandFactor
                : 1f;

        /// <summary>A treaty's influence price for the proposing realm's ruler.</summary>
        public static int TreatyInfluenceCost(Kingdom proposer, Models.TreatyType type)
            => (int)(DiplomacyConstants.TreatyInfluenceCost(type) * FirebrandFactor(proposer?.Leader));
    }
}
