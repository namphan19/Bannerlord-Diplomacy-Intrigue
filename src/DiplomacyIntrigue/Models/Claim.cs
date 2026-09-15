using DiplomacyIntrigue.Diplomacy;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A standing justification one kingdom holds for war against another.
    ///
    /// This is the difference between "Vlandia attacked Battania" and "Vlandia attacked
    /// Battania to retake Pravend". The type decides how legitimate the war looks, which
    /// in turn decides what it costs, who is offended by it, and what may be demanded at
    /// the peace table.
    ///
    /// Claims expire. A grievance nobody acts on stops being a casus belli.
    /// </summary>
    public sealed class Claim
    {
        [SaveableProperty(1)] public Kingdom Claimant { get; private set; }
        [SaveableProperty(2)] public Kingdom Target { get; private set; }
        [SaveableProperty(3)] public CasusBelliType Type { get; private set; }

        /// <summary>The disputed fief for territorial claims; null otherwise.</summary>
        [SaveableProperty(4)] public Settlement Settlement { get; private set; }

        [SaveableProperty(5)] public CampaignTime AcquiredOn { get; private set; }
        [SaveableProperty(6)] public CampaignTime ExpiresOn { get; private set; }

        /// <summary>
        /// True when the claim was manufactured rather than earned. Fabrication is a
        /// gamble - see <see cref="DiplomacyConstants.FabricateClaimExposureChance"/> -
        /// and a fabricated claim that later comes to light is a scandal, so we remember.
        /// </summary>
        [SaveableProperty(7)] public bool IsFabricated { get; private set; }

        internal Claim() { }

        internal Claim(Kingdom claimant, Kingdom target, CasusBelliType type,
            Settlement settlement, CampaignTime expiresOn, bool isFabricated)
        {
            Claimant = claimant;
            Target = target;
            Type = type;
            Settlement = settlement;
            AcquiredOn = CampaignTime.Now;
            ExpiresOn = expiresOn;
            IsFabricated = isFabricated;
        }

        public bool IsExpired => ExpiresOn != CampaignTime.Never && ExpiresOn <= CampaignTime.Now;

        public bool IsLive => !IsExpired;

        /// <summary>Range 0..1. See <see cref="CasusBelli.Legitimacy"/>.</summary>
        public float Legitimacy => CasusBelli.Legitimacy(Type);

        /// <summary>
        /// Only a territorial claim lets a victor take land. A war fought to avenge a raid
        /// can extract tribute and prisoners, not provinces - that is what makes the choice
        /// of casus belli matter beyond its cost.
        /// </summary>
        public bool AllowsFiefDemands
            => Type == CasusBelliType.Conquest || Type == CasusBelliType.ReclaimAncestralLand;

        internal void ExtendTo(CampaignTime expiresOn)
        {
            // A fresh triggering event renews an existing claim rather than stacking a
            // duplicate, so repeated raids do not inflate a kingdom's claim list.
            if (expiresOn > ExpiresOn) ExpiresOn = expiresOn;
        }

        public bool Matches(Kingdom claimant, Kingdom target, CasusBelliType type, Settlement settlement)
            => Claimant == claimant && Target == target && Type == type && Settlement == settlement;

        public override string ToString()
        {
            var name = Claimant == null ? "?" : Claimant.Name.ToString();
            var target = Target == null ? "?" : Target.Name.ToString();
            var where = Settlement == null ? "" : " over " + Settlement.Name;
            return name + " vs " + target + ": " + Type + where
                   + " (legitimacy " + Legitimacy.ToString("0.00")
                   + (IsFabricated ? ", fabricated" : "")
                   + ", expires " + ExpiresOn + ")";
        }
    }
}
