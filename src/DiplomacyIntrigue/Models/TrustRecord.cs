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
    /// Trust does not decay. That is the point of it - relation already exists as the value
    /// that fades within a season. Trust is reputation, and it follows a kingdom for the
    /// rest of the campaign unless it is deliberately rebuilt by honouring agreements.
    /// </summary>
    public sealed class TrustRecord
    {
        [SaveableProperty(1)] public Kingdom From { get; private set; }
        [SaveableProperty(2)] public Kingdom To { get; private set; }
        [SaveableProperty(3)] public float Value { get; private set; }

        /// <summary>Set whenever the value moves, so the UI can explain recent shifts.</summary>
        [SaveableProperty(4)] public CampaignTime LastChanged { get; private set; }

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
        }

        private static float Clamp(float v)
            => v < Diplomacy.DiplomacyConstants.TrustMin ? Diplomacy.DiplomacyConstants.TrustMin
                : (v > Diplomacy.DiplomacyConstants.TrustMax ? Diplomacy.DiplomacyConstants.TrustMax : v);

        public override string ToString()
            => (From == null ? "?" : From.Name.ToString())
               + " -> " + (To == null ? "?" : To.Name.ToString())
               + ": " + Value.ToString("0.0");
    }
}
