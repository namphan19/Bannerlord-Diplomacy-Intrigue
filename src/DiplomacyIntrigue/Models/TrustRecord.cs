using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// What one kingdom thinks of another, -100..100.
    ///
    /// The pair is **ordered** on purpose: "Vlandia trusts Battania" and "Battania trusts
    /// Vlandia" are different facts, and after a betrayal they are very different numbers.
    /// Symmetric trust would let the betrayer forgive themselves.
    ///
    /// Trust decays toward zero with neglect - the lead's decision after run 06, where a
    /// world that kept honouring treaties saturated near +100 and reputation had become a
    /// ratchet. A relationship has to be maintained to be kept; a grudge fades too, which
    /// is also the ledger's first route back from the bottom
    /// (<see cref="Diplomacy.TrustRegistry.DailyTick"/>).
    /// </summary>
    public sealed class TrustRecord
    {
        [SaveableProperty(1)] public Kingdom From { get; private set; }
        [SaveableProperty(2)] public Kingdom To { get; private set; }
        [SaveableProperty(3)] public float Value { get; private set; }

        /// <summary>Set whenever the value moves, so the UI can explain recent shifts.</summary>
        [SaveableProperty(4)] public CampaignTime LastChanged { get; private set; }

        /// <summary>
        /// The last time something *good* passed between the pair - the timestamp the decay
        /// grace window reads (<see cref="Diplomacy.DiplomacyConstants.TrustDecayGraceDays"/>).
        /// Kept separate from <see cref="LastChanged"/>, which moves on every change including
        /// the decay itself and so could never say when the pair last did each other a good
        /// turn. Loads as <c>CampaignTime.Zero</c> on pre-F2 saves - long before any campaign
        /// began - which simply means those records decay normally.
        /// </summary>
        [SaveableProperty(5)] public CampaignTime LastPositiveChange { get; private set; }

        /// <summary>
        /// When <see cref="To"/> - always the player's realm - last turned down an offer from
        /// <see cref="From"/> to kneel to it, read by the re-offer cooldown
        /// (<see cref="Diplomacy.DiplomacyConstants.PlayerOfferRefusalCooldownDays"/>).
        ///
        /// Kept here rather than in a list of its own because the refusal already lands on
        /// this record as a trust change; a separate saved type would need its own definer
        /// entries for one timestamp. Loads as <c>CampaignTime.Zero</c> on older saves, so no
        /// cooldown is running - at worst one offer repeated after loading.
        /// </summary>
        [SaveableProperty(6)] public CampaignTime LastOfferRefused { get; private set; }

        internal TrustRecord() { }

        internal TrustRecord(Kingdom from, Kingdom to, float value)
        {
            From = from;
            To = to;
            Value = value;
            LastChanged = CampaignTime.Now;
        }

        public bool Is(Kingdom from, Kingdom to) => From == from && To == to;

        internal void Add(float amount)
        {
            if (amount == 0f) return;
            Value = Clamp(Value + amount);
            LastChanged = CampaignTime.Now;
            if (amount > 0f) LastPositiveChange = CampaignTime.Now;
        }

        /// <summary>
        /// Upkeep only, for <see cref="Diplomacy.TrustRegistry.DailyTick"/>. A decay step on
        /// a negative record is a *positive* amount - routed through <see cref="Add"/> it
        /// would stamp <see cref="LastPositiveChange"/> and suspend its own decay inside the
        /// grace window, which is exactly what a live test showed: a -51 grudge moved once
        /// and then froze. Decay is not a good turn, so it must not feed the grace clock.
        /// </summary>
        internal void Decay(float amount)
        {
            if (amount == 0f) return;
            Value = Clamp(Value + amount);
            LastChanged = CampaignTime.Now;
        }

        internal void MarkOfferRefused() => LastOfferRefused = CampaignTime.Now;

        private static float Clamp(float v)
            => v < Diplomacy.DiplomacyConstants.TrustMin ? Diplomacy.DiplomacyConstants.TrustMin
                : (v > Diplomacy.DiplomacyConstants.TrustMax ? Diplomacy.DiplomacyConstants.TrustMax : v);

        public override string ToString()
            => (From == null ? "?" : From.Name.ToString())
               + " -> " + (To == null ? "?" : To.Name.ToString())
               + ": " + Value.ToString("0.0");
    }
}
