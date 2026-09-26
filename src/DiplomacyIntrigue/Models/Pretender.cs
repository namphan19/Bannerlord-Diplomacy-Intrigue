using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// Someone who lost a throne and kept enough of the court to still be a problem.
    ///
    /// Design 02 §5: a claimant who finished a contested succession with more than 30% of the
    /// court behind them does not simply go away. They become a standing pretender, which is
    /// what gives a rival kingdom the `SupportClaimant` casus belli, what lets the Pretenders
    /// bloc form at all (design 02 §3), and what design 07's armed contest needs before it has
    /// anybody to put on the throne.
    ///
    /// **Stored**, for the same reason as crown legitimacy: it is a fact about what happened,
    /// not a state of the world. Nothing about a hero today says they were passed over for a
    /// crown eight years ago.
    ///
    /// Save ids are frozen. This type is definer class id 12; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class Pretender
    {
        /// <summary>The throne being claimed.</summary>
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }

        /// <summary>Who claims it.</summary>
        [SaveableProperty(2)] public Hero Claimant { get; private set; }

        /// <summary>
        /// The share of the court's influence that backed them at the succession, 0-1. Kept as
        /// it was on the day rather than recomputed, because it is the *claim* that persists -
        /// how much of the court would rally today is a live question the bloc answers.
        /// </summary>
        [SaveableProperty(3)] public float SupportAtSuccession { get; private set; }

        [SaveableProperty(4)] public CampaignTime Since { get; private set; }

        internal Pretender() { }

        internal Pretender(Kingdom kingdom, Hero claimant, float support)
        {
            Kingdom = kingdom;
            Claimant = claimant;
            SupportAtSuccession = support;
            Since = CampaignTime.Now;
        }

        /// <summary>
        /// A claim dies with the claimant, and ends if they take the throne after all or leave
        /// the kingdom. Checked rather than stored, so no event has to fire to keep it true -
        /// the same reasoning as `Hegemony.IsHegemon` being derived.
        /// </summary>
        public bool IsStillStanding
        {
            get
            {
                if (Kingdom == null || Kingdom.IsEliminated) return false;
                if (Claimant == null || !Claimant.IsAlive) return false;
                if (Claimant.Clan == null || Claimant.Clan.Kingdom != Kingdom) return false;
                return Kingdom.RulingClan != Claimant.Clan;
            }
        }

        public override string ToString()
            => (Claimant == null ? "?" : Claimant.Name.ToString())
               + " claims " + (Kingdom == null ? "?" : Kingdom.Name.ToString())
               + " (" + (SupportAtSuccession * 100f).ToString("0") + "% at the succession)";
    }
}
