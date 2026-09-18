using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A kingdom's strength as the world remembers it: an exponential moving average of the
    /// engine's live military figure, sampled once a day.
    ///
    /// The live figure (<c>Kingdom.CurrentTotalStrength</c>) swings by a fifth after a single
    /// large battle and recovers with a season of recruiting. That is the right number for
    /// decisions about *now* - whether to strike, whether a vassal could win its revolt - and
    /// the wrong one for decisions about *what a kingdom is becoming*: whether a ruler has grown
    /// greedy, whether its vassals should fear being swallowed. Read raw, those would flip on a
    /// battle and flip back a month later, and a player could disband an army before the weekly
    /// evaluation to look harmless. See Diplomacy/Power.cs for which reads which.
    ///
    /// A list entry rather than a dictionary, for the same reason as <see cref="KingdomWeariness"/>.
    /// </summary>
    public sealed class KingdomPower
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }
        [SaveableProperty(2)] public float Smoothed { get; private set; }

        internal KingdomPower() { }

        internal KingdomPower(Kingdom kingdom, float smoothed)
        {
            Kingdom = kingdom;
            Smoothed = smoothed;
        }

        /// <summary>Moves the average a fraction of the way toward today's figure.</summary>
        internal void Sample(float current, float fraction)
            => Smoothed += (current - Smoothed) * fraction;
    }
}
