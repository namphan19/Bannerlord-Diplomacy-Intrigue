using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Diplomacy
{
    /// <summary>
    /// Every way the mod weighs a kingdom's strength, in one place. Design:
    /// docs/design/06-power.md.
    ///
    /// **Two readings of strength, and the rule for which is used where.** The engine's
    /// figure, <c>CurrentTotalStrength</c>, is a live military number that moves by a fifth
    /// after one large battle.
    ///
    ///   - **Live** strength answers questions about *now*: is this the moment to strike
    ///     (ambition), could this vassal win a revolt today, would an ally's help be enough
    ///     in this war. A kingdom whose armies were just destroyed genuinely cannot do those
    ///     things, and should not act as if it could.
    ///
    ///   - **Smoothed** strength answers questions about *what a kingdom is becoming*: has its
    ///     ruler grown greedy, should its vassals fear being swallowed, is it the rising
    ///     power the rest of the map ought to band against. Those are reputations. Read live,
    ///     they would flip with every battle - a vassal revolting in dread one week and
    ///     regretting it the next - and a player could disband an army before the weekly
    ///     evaluation to look harmless.
    ///
    /// **Dominance** is the common scale: a kingdom's share of the world's strength as a
    /// multiple of an even split. With eight kingdoms an even split is 12.5%, so a kingdom
    /// holding a quarter of Calradia's strength has dominance 2. It rises as rivals are
    /// eliminated only if the survivor's share rises faster than the split does.
    /// </summary>
    public static class Power
    {
        // ================= Live ================================================

        public static float Strength(Kingdom kingdom)
            => kingdom == null || kingdom.IsEliminated ? 0f : kingdom.CurrentTotalStrength;

        /// <summary>
        /// The balance of strength between two kingdoms, -1..+1: log2 of the ratio, clamped.
        /// +1 when <paramref name="a"/> is at least twice <paramref name="b"/>, -1 at half, 0
        /// at parity. Live.
        ///
        /// A log scale because the question is two-sided. `ratio - 1` - what Hold's fear term
        /// used - reaches +1 at twice as strong but only -0.5 at half as strong, so a weak
        /// patron was treated far more gently than a strong one was rewarded.
        /// </summary>
        public static float Balance(Kingdom a, Kingdom b)
        {
            var sa = Strength(a);
            var sb = Strength(b);
            if (sa <= 0f && sb <= 0f) return 0f;
            if (sb <= 0f) return 1f;
            if (sa <= 0f) return -1f;

            return Clamp((float)(System.Math.Log(sa / sb) / System.Math.Log(2.0)), -1f, 1f);
        }

        /// <summary>Live share of the world's strength, times the number of living kingdoms.</summary>
        public static float Dominance(Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated) return 0f;

            var total = 0f;
            var living = 0;
            foreach (var other in Kingdom.All)
            {
                if (!other.IsRealm()) continue;
                total += other.CurrentTotalStrength;
                living++;
            }
            return total <= 0f ? 0f : kingdom.CurrentTotalStrength / total * living;
        }

        /// <summary>
        /// How hungry for war a kingdom's strength makes its ruler, 0..1. Live - the lead's
        /// call: a ruler whose armies are full wants to use them.
        ///
        /// Zero at an even split, 1 at <see cref="DiplomacyConstants.AmbitionFullAtDominance"/>.
        /// Different from land hunger, which it sits beside in the war valuation: land hunger
        /// reads strength against the fiefs a kingdom already holds, so a strong kingdom with
        /// many fiefs feels none. Ambition reads strength against the world, and the strongest
        /// kingdom on the map feels it however much it already owns.
        /// </summary>
        public static float Ambition(Kingdom kingdom)
        {
            var span = DiplomacyConstants.AmbitionFullAtDominance - 1f;
            return span <= 0f ? 0f : Clamp((Dominance(kingdom) - 1f) / span, 0f, 1f);
        }

        // ================= Smoothed ============================================

        /// <summary>
        /// Moves every living kingdom's smoothed strength a day toward its live figure, and
        /// drops the records of kingdoms that no longer exist. Called once per campaign day.
        ///
        /// A kingdom with no record starts at its live figure rather than at zero, so a save
        /// that predates the field does not read every realm as weak for its first year.
        /// </summary>
        public static void DailySample(ModState state)
        {
            if (state == null) return;

            var fraction = 1f / DiplomacyConstants.StrengthSmoothingDays;

            for (var i = state.PowerRecords.Count - 1; i >= 0; i--)
            {
                var record = state.PowerRecords[i];
                if (record.Kingdom == null || record.Kingdom.IsEliminated)
                    state.PowerRecords.RemoveAt(i);
            }

            foreach (var kingdom in Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;

                var record = Find(state, kingdom);
                if (record == null)
                    state.PowerRecords.Add(new KingdomPower(kingdom, kingdom.CurrentTotalStrength));
                else
                    record.Sample(kingdom.CurrentTotalStrength, fraction);
            }
        }

        /// <summary>Smoothed strength, or the live figure for a kingdom not yet sampled.</summary>
        public static float Smoothed(ModState state, Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated) return 0f;
            var record = state == null ? null : Find(state, kingdom);
            return record == null ? kingdom.CurrentTotalStrength : record.Smoothed;
        }

        /// <summary>Smoothed dominance: the same scale as <see cref="Dominance"/>, read from averages.</summary>
        public static float SmoothedDominance(ModState state, Kingdom kingdom)
        {
            if (kingdom == null || kingdom.IsEliminated) return 0f;

            var total = 0f;
            var living = 0;
            foreach (var other in Kingdom.All)
            {
                if (!other.IsRealm()) continue;
                total += Smoothed(state, other);
                living++;
            }
            return total <= 0f ? 0f : Smoothed(state, kingdom) / total * living;
        }

        /// <summary>
        /// How far a ruler has moved from wanting vassals to wanting provinces, 0..1. Smoothed.
        ///
        /// Zero below <see cref="DiplomacyConstants.GreedStartsAtDominance"/> and full a whole
        /// even share above it. A greedy ruler stops taking vassals, may tear up a vassalage
        /// to conquer the vassal outright, and its vassals - who can see the same numbers -
        /// start to fear exactly that.
        /// </summary>
        public static float Greed(ModState state, Kingdom kingdom)
            => Clamp(SmoothedDominance(state, kingdom) - DiplomacyConstants.GreedStartsAtDominance, 0f, 1f);

        /// <summary>
        /// A sphere's smoothed strength: the head's plus every vassal's. What a coalition
        /// forming against a rising power actually reads - a power worth balancing against is
        /// one that has stayed strong, not one that happens to have its armies in the field.
        /// </summary>
        public static float SmoothedSphere(ModState state, Kingdom head)
        {
            if (head == null || head.IsEliminated) return 0f;

            var total = Smoothed(state, head);
            foreach (var treaty in state.ActiveTreatiesOf(head))
            {
                if (treaty.Type != TreatyType.Vassalage || treaty.DominantParty != head) continue;
                var vassal = treaty.SubordinateParty;
                if (vassal != null && !vassal.IsEliminated) total += Smoothed(state, vassal);
            }
            return total;
        }

        // ================= Reading it out ======================================

        /// <summary>
        /// What the rest of the map can see of a kingdom's power, in words. The same numbers
        /// the AI uses: a ruler's ambition and greed are visible to anyone watching its
        /// armies, so they are not something espionage needs to sell.
        /// </summary>
        public static string Describe(ModState state, Kingdom kingdom)
        {
            var dominance = Dominance(kingdom);
            var ambition = Ambition(kingdom);
            var greed = Greed(state, kingdom);

            var text = "holds " + (dominance / LivingCount() * 100f).ToString("0") + "% of Calradia's strength";

            if (greed >= DiplomacyConstants.GreedRefusesVassals)
                text += "; greedy - takes provinces, not vassals, and its vassals have reason to fear it";
            else if (greed > 0f)
                text += "; growing greedy - its vassals are uneasy";
            else if (ambition >= 0.5f)
                text += "; ambitious - hungry for war";
            else if (ambition > 0f)
                text += "; restless";

            return text;
        }

        private static int LivingCount()
        {
            var living = 0;
            foreach (var kingdom in Kingdom.All)
                if (kingdom.IsRealm()) living++;
            return living < 1 ? 1 : living;
        }

        private static KingdomPower Find(ModState state, Kingdom kingdom)
        {
            for (var i = 0; i < state.PowerRecords.Count; i++)
                if (state.PowerRecords[i].Kingdom == kingdom) return state.PowerRecords[i];
            return null;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
