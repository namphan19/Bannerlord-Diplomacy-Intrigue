using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// How legitimate a kingdom's crown is, 0-100. The thing a ruler can lose without losing
    /// a battle.
    ///
    /// **Stored, unlike loyalty and blocs.** Legitimacy is an accumulated history - twelve
    /// points for a just war won, twenty lost for a treaty broken - and history cannot be
    /// re-derived from the state of the world. A kingdom that broke its word five years ago
    /// looks, today, exactly like one that never did.
    ///
    /// Kept apart from <see cref="KingdomPower"/> rather than folded into it, although both
    /// are per-kingdom rows. Power is a smoothed sample of an external fact and is rewritten
    /// every day; legitimacy is an internal pool that only moves when something happens to it.
    /// One record holding both would have two unrelated lifecycles and invite a daily sampler
    /// to touch a political number.
    ///
    /// Save ids are frozen. This type is definer class id 11; next free there is 12.
    /// </summary>
    public sealed class KingdomLegitimacy
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }

        [SaveableProperty(2)] public float Value { get; private set; }

        /// <summary>
        /// When the pool last moved, and why. The court screen has to be able to say what
        /// spent a crown's standing, not just that it is low - the player is being asked to
        /// manage this number, and an unexplained pool is unmanageable.
        /// </summary>
        [SaveableProperty(3)] public CampaignTime LastChanged { get; private set; }

        [SaveableProperty(4)] public string LastReason { get; private set; }

        /// <summary>
        /// When the peace dividend was last paid. Design 02 §4 grants legitimacy "per year of
        /// peace", which needs a mark: without one, either a daily tick pays a 365th of it and
        /// rounds away, or a yearly event pays a kingdom that was at war for 364 of those days.
        /// </summary>
        [SaveableProperty(5)] public CampaignTime LastPeaceDividend { get; private set; }

        internal KingdomLegitimacy() { }

        internal KingdomLegitimacy(Kingdom kingdom, float value)
        {
            Kingdom = kingdom;
            Value = value;
            LastChanged = CampaignTime.Now;
            LastPeaceDividend = CampaignTime.Now;
            LastReason = "founded";
        }

        internal void Adjust(float amount, string reason)
        {
            if (amount == 0f) return;

            Value += amount;
            if (Value < 0f) Value = 0f;
            else if (Value > 100f) Value = 100f;

            LastChanged = CampaignTime.Now;
            LastReason = reason;
        }

        internal void MarkPeaceDividend() => LastPeaceDividend = CampaignTime.Now;

        public override string ToString()
            => (Kingdom == null ? "?" : Kingdom.Name.ToString())
               + ": " + Value.ToString("0.0")
               + (string.IsNullOrEmpty(LastReason) ? "" : "  (last: " + LastReason + ")");
    }
}
