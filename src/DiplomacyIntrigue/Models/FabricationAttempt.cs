using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A claim being manufactured: heralds digging up genealogies, scribes producing
    /// documents. Takes time, costs influence and gold, and can be exposed instead of
    /// succeeding - so it reads as a gamble rather than a purchase.
    /// </summary>
    public sealed class FabricationAttempt
    {
        [SaveableProperty(1)] public Kingdom Claimant { get; private set; }
        [SaveableProperty(2)] public Settlement Target { get; private set; }
        [SaveableProperty(3)] public CampaignTime StartedOn { get; private set; }
        [SaveableProperty(4)] public CampaignTime ResolvesOn { get; private set; }

        internal FabricationAttempt() { }

        internal FabricationAttempt(Kingdom claimant, Settlement target, CampaignTime resolvesOn)
        {
            Claimant = claimant;
            Target = target;
            StartedOn = CampaignTime.Now;
            ResolvesOn = resolvesOn;
        }

        public bool IsDue => ResolvesOn <= CampaignTime.Now;

        public float DaysRemaining => (float)(ResolvesOn - CampaignTime.Now).ToDays;

        /// <summary>The kingdom the claim would be aimed at, read live from the fief.</summary>
        public Kingdom TargetKingdom => Target?.MapFaction as Kingdom;

        public override string ToString()
            => (Claimant == null ? "?" : Claimant.Name.ToString())
               + " fabricating a claim on " + (Target == null ? "?" : Target.Name.ToString())
               + " (" + DaysRemaining.ToString("0") + " days left)";
    }
}
