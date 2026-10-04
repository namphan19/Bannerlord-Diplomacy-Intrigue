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
    /// constants the AI reads also means the bar cannot drift away from the behaviour.
    ///
    /// Since R-1 (2026-09-27) the edges are the **base** bars, and a court moves the real
    /// one: a court of Doves sues and signs up to 30% below the Exhausted edge, one of Hawks
    /// up to 15% above it (<see cref="PeaceTable.SeekPeaceBar"/>). The band does not follow,
    /// deliberately - drawing it at the court-moved bar would show a rival's bloc shares,
    /// which the player sees only as bands (design 02 §9.1). "Exhausted" therefore reads
    /// "will accept a white peace unless its court holds it back"; an earlier version of
    /// this comment said the bar could never disagree with the behaviour, which stopped
    /// being true here.
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
                case ExhaustionBand.Breaking:
                    return Core.DiText.T("DI_EXHAUSTION_BREAKING", "Breaking");
                case ExhaustionBand.Exhausted:
                    return Core.DiText.T("DI_EXHAUSTION_EXHAUSTED", "Exhausted");
                case ExhaustionBand.Weary:
                    return Core.DiText.T("DI_EXHAUSTION_WEARY", "Weary");
                case ExhaustionBand.Strained:
                    return Core.DiText.T("DI_EXHAUSTION_STRAINED", "Strained");
                default:
                    return Core.DiText.T("DI_EXHAUSTION_FRESH", "Fresh");
            }
        }

        /// <summary>
        /// The exhaustion a band begins at. This is what a rival's bar in the Kingdom screen
        /// carries: "Weary" reads as 40 whether they are at 41 or 59, so the band stays a
        /// band even when it is drawn as a number.
        /// </summary>
        public static float Floor(ExhaustionBand band)
        {
            switch (band)
            {
                case ExhaustionBand.Breaking: return DiplomacyConstants.ExhaustionAcceptBadTerms;
                case ExhaustionBand.Exhausted: return DiplomacyConstants.ExhaustionSeekPeace;
                case ExhaustionBand.Weary: return DiplomacyConstants.ExhaustionCourtPressure;
                case ExhaustionBand.Strained: return DiplomacyConstants.PeaceWhitePeaceOnlyBelow;
                default: return 0f;
            }
        }

        /// <summary>What the band actually tells the player, in behavioural terms.</summary>
        public static string Meaning(ExhaustionBand band)
        {
            switch (band)
            {
                case ExhaustionBand.Breaking:
                    return Core.DiText.T("DI_EXHAUSTION_MEANING_BREAKING",
                        "will accept unfavourable terms; their fiefs are losing loyalty");
                case ExhaustionBand.Exhausted:
                    return Core.DiText.T("DI_EXHAUSTION_MEANING_EXHAUSTED", "will accept a white peace");
                case ExhaustionBand.Weary:
                    return Core.DiText.T("DI_EXHAUSTION_MEANING_WEARY", "their court is starting to press for peace");
                case ExhaustionBand.Strained:
                    return Core.DiText.T("DI_EXHAUSTION_MEANING_STRAINED", "feeling the cost, but not yet politically");
                default:
                    return Core.DiText.T("DI_EXHAUSTION_MEANING_FRESH", "nothing is pressing them");
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
            return Bar(Of(exhaustion)) + " " + Condition(exhaustion);
        }

        /// <summary>
        /// Band and meaning without the text meter - "Exhausted - will accept a white
        /// peace". For Gauntlet screens: the "[##...]" meter is drawn for the inquiry
        /// menus and reads as a rendering glitch anywhere else.
        /// </summary>
        public static string Condition(float exhaustion)
        {
            var band = Of(exhaustion);
            // The line is a key of its own, not the band's name glued to its meaning with " - " in
            // code: a language puts the two halves in whatever order it likes, and some does not
            // want a dash at all. Both halves are keyed words in their own right, so a translator
            // gets three things to place rather than one sentence with a hole in it.
            return Core.DiText.T("DI_EXHAUSTION_CONDITION", "{BAND} - {MEANING}",
                ("BAND", Name(band)),
                ("MEANING", Meaning(band)));
        }
    }
}
