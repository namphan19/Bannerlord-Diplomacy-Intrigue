using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A binding agreement between exactly two kingdoms. Lifecycle fields are mutated
    /// only through the treaty registry, never by callers.
    /// </summary>
    public sealed class Treaty
    {
        [SaveableProperty(1)] public int Id { get; private set; }
        [SaveableProperty(2)] public TreatyType Type { get; private set; }
        [SaveableProperty(3)] public Kingdom PartyA { get; private set; }
        [SaveableProperty(4)] public Kingdom PartyB { get; private set; }
        [SaveableProperty(5)] public CampaignTime SignedOn { get; private set; }
        [SaveableProperty(6)] public CampaignTime ExpiresOn { get; private set; }
        [SaveableProperty(7)] public TreatyStatus Status { get; private set; }

        /// <summary>Denars per tribute period. Zero for treaty types that carry no payment.</summary>
        [SaveableProperty(8)] public int TributeAmount { get; private set; }

        /// <summary>Which party pays. Null when TributeAmount is zero.</summary>
        [SaveableProperty(9)] public Kingdom TributePayer { get; private set; }

        [SaveableProperty(10)] public CampaignTime NextTributeDue { get; private set; }

        /// <summary>Set when the treaty ends, for post-mortem diplomacy (trust, grudges).</summary>
        [SaveableProperty(11)] public CampaignTime EndedOn { get; private set; }

        /// <summary>The party that broke it, when Status is Broken.</summary>
        [SaveableProperty(12)] public Kingdom BreachedBy { get; private set; }

        // The save system rehydrates instances without running a constructor.
        internal Treaty() { }

        internal Treaty(int id, TreatyType type, Kingdom a, Kingdom b, CampaignTime signedOn, CampaignTime expiresOn)
        {
            Id = id;
            Type = type;
            PartyA = a;
            PartyB = b;
            SignedOn = signedOn;
            ExpiresOn = expiresOn;
            Status = TreatyStatus.Active;
        }

        public bool IsActive => Status == TreatyStatus.Active;

        public bool Involves(Kingdom kingdom) => PartyA == kingdom || PartyB == kingdom;

        public bool IsBetween(Kingdom x, Kingdom y)
            => (PartyA == x && PartyB == y) || (PartyA == y && PartyB == x);

        /// <summary>Returns the signatory that is not the given kingdom, or null.</summary>
        public Kingdom Other(Kingdom kingdom)
        {
            if (PartyA == kingdom) return PartyB;
            if (PartyB == kingdom) return PartyA;
            return null;
        }

        /// <summary>True once the clock has run out. The registry sweeps these daily.</summary>
        public bool HasRunOut => ExpiresOn != CampaignTime.Never && ExpiresOn <= CampaignTime.Now;

        /// <summary>Alliances and defensive pacts drag signatories into partner wars.</summary>
        public bool CarriesCallToArms => Type == TreatyType.Alliance || Type == TreatyType.DefensivePact;

        /// <summary>Treaty types that make war between the parties illegal while active.</summary>
        public bool ForbidsWar =>
            Type == TreatyType.NonAggressionPact ||
            Type == TreatyType.Truce ||
            Type == TreatyType.DefensivePact ||
            Type == TreatyType.Alliance ||
            Type == TreatyType.TributaryPact ||
            Type == TreatyType.Vassalage;

        internal void SetTribute(Kingdom payer, int amountPerPeriod, CampaignTime firstDue)
        {
            TributePayer = payer;
            TributeAmount = amountPerPeriod;
            NextTributeDue = firstDue;
        }

        internal void AdvanceTributeDate(CampaignTime next) => NextTributeDue = next;

        internal void Close(TreatyStatus status, Kingdom breachedBy = null)
        {
            Status = status;
            EndedOn = CampaignTime.Now;
            BreachedBy = breachedBy;
        }

        public override string ToString()
            => Type + "(" + NameOf(PartyA) + " / " + NameOf(PartyB) + ", " + Status + ")";

        private static string NameOf(Kingdom k) => k == null ? "?" : k.Name.ToString();
    }
}
