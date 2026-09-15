namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// How worn down a kingdom looks to an outsider.
    ///
    /// Decision from the design review: the player sees their own exhaustion exactly and a
    /// rival's only as a band. Espionage in Phase 3 buys the exact figure back.
    ///
    /// The band edges are not arbitrary fifths - each one is a real behavioural threshold
    /// taken from <see cref="DiplomacyConstants"/>. That is the whole point: "Exhausted"
    /// literally means "will now accept a white peace", so a band is a statement about what
    /// the enemy will do, not a decorative label. Deriving the edges from the same
    /// constants the AI reads also means the bar can never disagree with the behaviour.
    /// </summary>
    public enum ExhaustionBand
    {
        Fresh = 0,
        Strained = 1,
        Weary = 2,
        Exhausted = 3,
        Breaking = 4,
    }

    public static class ExhaustionBands
    {
        public static ExhaustionBand Of(float exhaustion)
        {
            if (exhaustion >= DiplomacyConstants.ExhaustionAcceptBadTerms) return ExhaustionBand.Breaking;
            if (exhaustion >= DiplomacyConstants.ExhaustionSeekPeace) return ExhaustionBand.Exhausted;
            if (exhaustion >= DiplomacyConstants.ExhaustionCourtPressure) return ExhaustionBand.Weary;
            if (exhaustion >= DiplomacyConstants.PeaceWhitePeaceOnlyBelow) return ExhaustionBand.Strained;
            return ExhaustionBand.Fresh;
        }

        public static string Name(ExhaustionBand band)
        {
            switch (band)
            {
                case ExhaustionBand.Breaking: return "Breaking";
                case ExhaustionBand.Exhausted: return "Exhausted";
                case ExhaustionBand.Weary: return "Weary";
                case ExhaustionBand.Strained: return "Strained";
                default: return "Fresh";
            }
        }

        /// <summary>What the band actually tells the player, in behavioural terms.</summary>
        public static string Meaning(ExhaustionBand band)
        {
            switch (band)
            {
                case ExhaustionBand.Breaking:
                    return "will accept unfavourable terms; their fiefs are losing loyalty";
                case ExhaustionBand.Exhausted:
                    return "will accept a white peace";
                case ExhaustionBand.Weary:
                    return "their court is starting to press for peace";
                case ExhaustionBand.Strained:
                    return "feeling the cost, but not yet politically";
                default:
                    return "nothing is pressing them";
            }
        }

        /// <summary>A five-step bar, so the band reads at a glance without a number.</summary>
        public static string Bar(ExhaustionBand band)
        {
            var filled = (int)band + 1;
            var sb = new System.Text.StringBuilder(7);
            sb.Append('[');
            for (var i = 0; i < 5; i++) sb.Append(i < filled ? '#' : '.');
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Band, bar and meaning together - what a rival's war looks like to us.</summary>
        public static string Describe(float exhaustion)
        {
            var band = Of(exhaustion);
            return Bar(band) + " " + Name(band) + " - " + Meaning(band);
        }
    }
}
