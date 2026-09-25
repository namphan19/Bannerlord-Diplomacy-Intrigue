using DiplomacyIntrigue.Core;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>The terms of a realm's counter-intelligence, so every reader can explain it.</summary>
    public sealed class CounterIntelligenceTerms
    {
        public float Base;

        /// <summary>From the average security of the realm's towns and castles.</summary>
        public float FromSecurity;
        public float AverageSecurity;

        public float Total => Base + FromSecurity;

        public override string ToString()
            => Total.ToString("0.0") + " (base " + Base.ToString("0.0") + ", security "
               + FromSecurity.ToString("+0.0;-0.0;0.0") + " from an average of "
               + AverageSecurity.ToString("0") + ")";
    }

    /// <summary>
    /// A realm's defence against spies, 0-100 (design 03 §3). The one resolver: network growth
    /// reads it now, mission odds and exposure will at 3.2 and 3.4.
    ///
    /// **Only the part that needs no new state exists yet.** §3's formula also has a weekly
    /// counter-intelligence budget and a "security focus" policy term; both arrive at step 3.3,
    /// with the budget they need. Until then every realm defends at its base plus its towns'
    /// security, which is the floor §3 gives a realm that spends nothing.
    /// </summary>
    public static class CounterIntelligence
    {
        public static float Of(ModState state, Kingdom kingdom) => Explain(state, kingdom).Total;

        public static CounterIntelligenceTerms Explain(ModState state, Kingdom kingdom)
        {
            var t = new CounterIntelligenceTerms { Base = EspionageConstants.CounterIntelligenceBase };
            if (kingdom == null) return t;

            var sum = 0f;
            var count = 0;
            foreach (var fief in kingdom.Fiefs)
            {
                if (fief == null) continue;
                sum += fief.Security;
                count++;
            }
            t.AverageSecurity = count == 0 ? 0f : sum / count;
            t.FromSecurity = t.AverageSecurity * EspionageConstants.CounterIntelligencePerSecurity;
            return t;
        }
    }
}
