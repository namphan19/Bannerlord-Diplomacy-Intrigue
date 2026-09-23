using System.Collections.Generic;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// A faction of a kingdom's court: the clans that share an agenda, who speaks for them,
    /// and how much weight they carry.
    ///
    /// Built fresh by <see cref="BlocModel.BlocsOf"/> every time it is asked for, and never
    /// saved. A bloc is not a thing a clan joins and belongs to; it is the shape the court
    /// happens to have while the pressures stay as they are.
    /// </summary>
    public sealed class CourtBloc
    {
        public Kingdom Kingdom { get; }
        public CourtAgenda Agenda { get; }

        public List<Clan> Members { get; } = new List<Clan>();

        /// <summary>The sum of member influence - what the bloc can actually spend. Design 02 §3.</summary>
        public float Power { get; private set; }

        /// <summary>The most influential member; who the bloc speaks through. Design 02 §3.</summary>
        public Clan Leader { get; private set; }

        /// <summary>
        /// Members whose loyalty is at or above the reliable band. Design 02 §3: "a clan with
        /// loyalty >= 70 follows the ruler regardless of its bloc. Loyalty beats agenda."
        ///
        /// Counted at build time rather than recomputed on demand, so a bloc's real usable
        /// weight and its nominal weight can both be shown. The gap between them is the
        /// interesting number: a large bloc most of whose members will vote with the crown
        /// anyway is not the threat its size suggests.
        /// </summary>
        public int LoyalMembers { get; private set; }

        /// <summary>
        /// Influence held by members who will vote their agenda rather than follow the ruler.
        /// This is the figure that matters for whether a bloc can force anything through.
        /// </summary>
        public float EffectivePower { get; private set; }

        internal CourtBloc(Kingdom kingdom, CourtAgenda agenda)
        {
            Kingdom = kingdom;
            Agenda = agenda;
        }

        internal void Add(Clan clan, float loyalty)
        {
            if (clan == null) return;

            Members.Add(clan);

            var influence = clan.Influence > 0f ? clan.Influence : 0f;
            Power += influence;

            if (loyalty >= IntrigueConstants.LoyaltyReliable) LoyalMembers++;
            else EffectivePower += influence;

            if (Leader == null || clan.Influence > Leader.Influence) Leader = clan;
        }

        /// <summary>Share of the whole court's influence this bloc holds, 0-1.</summary>
        public float PowerShare(float kingdomTotalInfluence)
            => kingdomTotalInfluence <= 0f ? 0f : Power / kingdomTotalInfluence;

        public override string ToString()
            => Agenda + ": " + Members.Count + " clan(s), power " + Power.ToString("0")
               + (LoyalMembers > 0
                   ? " (effective " + EffectivePower.ToString("0") + "; " + LoyalMembers
                     + " follow the ruler anyway)"
                   : "")
               + ", led by " + (Leader == null ? "nobody" : Leader.Name.ToString());
    }
}
