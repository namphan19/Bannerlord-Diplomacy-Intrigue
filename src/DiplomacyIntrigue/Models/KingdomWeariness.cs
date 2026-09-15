using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// How worn down a kingdom is by its recent wars, independent of any single war.
    ///
    /// War exhaustion lives on a <see cref="WarRecord"/> and dies with it. Weariness is
    /// what survives: it makes starting another war right after a hard one expensive,
    /// which is what stops the AI from cycling through wars forever.
    ///
    /// Stored as a list entry rather than a dictionary because the save system handles
    /// lists of simple classes far more predictably than keyed containers.
    /// </summary>
    public sealed class KingdomWeariness
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }
        [SaveableProperty(2)] public float Value { get; private set; }

        internal KingdomWeariness() { }

        internal KingdomWeariness(Kingdom kingdom, float value)
        {
            Kingdom = kingdom;
            Value = value;
        }

        internal void Add(float amount) => Value = Clamp(Value + amount);

        internal void Decay(float amount) => Value = Clamp(Value - amount);

        private static float Clamp(float v)
            => v < 0f ? 0f : (v > Diplomacy.DiplomacyConstants.WearinessMax
                ? Diplomacy.DiplomacyConstants.WearinessMax
                : v);

        public override string ToString()
            => (Kingdom == null ? "?" : Kingdom.Name.ToString()) + " weariness=" + Value.ToString("0.0");
    }
}
