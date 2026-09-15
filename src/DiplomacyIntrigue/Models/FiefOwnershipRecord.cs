using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One span of time during which a kingdom held a settlement.
    ///
    /// The base game keeps no queryable history of settlement ownership, so an ancestral
    /// claim - "this town was ours within living memory" - cannot be expressed without
    /// recording it ourselves. This ledger is that record.
    ///
    /// It stays small: one row per actual transfer, a few hundred over a long campaign.
    /// </summary>
    public sealed class FiefOwnershipRecord
    {
        [SaveableProperty(1)] public Settlement Settlement { get; private set; }
        [SaveableProperty(2)] public Kingdom Kingdom { get; private set; }
        [SaveableProperty(3)] public CampaignTime From { get; private set; }

        /// <summary>CampaignTime.Never while the kingdom still holds it.</summary>
        [SaveableProperty(4)] public CampaignTime To { get; private set; }

        internal FiefOwnershipRecord() { }

        internal FiefOwnershipRecord(Settlement settlement, Kingdom kingdom, CampaignTime from)
        {
            Settlement = settlement;
            Kingdom = kingdom;
            From = from;
            To = CampaignTime.Never;
        }

        public bool IsCurrent => To == CampaignTime.Never;

        internal void Close(CampaignTime at) => To = at;

        /// <summary>
        /// How long ago this holding ended, in years. Zero while it is still held, which
        /// is what makes a current holding trivially "within memory".
        /// </summary>
        public float YearsSinceLost
            => IsCurrent ? 0f : (float)(CampaignTime.Now - To).ToYears;

        public override string ToString()
            => (Settlement == null ? "?" : Settlement.Name.ToString())
               + " held by " + (Kingdom == null ? "?" : Kingdom.Name.ToString())
               + " from " + From + (IsCurrent ? " (current)" : " to " + To);
    }
}
