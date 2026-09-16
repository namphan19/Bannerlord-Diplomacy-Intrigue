using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// A proposed peace settlement: who is conceding what.
    ///
    /// Deliberately **not** savable. A settlement is evaluated and applied within a single
    /// tick; nothing here needs to survive a save. Pending offers that sit in front of the
    /// player waiting for an answer arrive with the negotiation UI in 1.8, and will need
    /// their own savable type - keeping this one transient means the save format does not
    /// have to carry a half-finished negotiation today.
    /// </summary>
    public sealed class PeaceTerms
    {
        /// <summary>The side making demands. For a white peace either party will do.</summary>
        public Kingdom Winner { get; }

        /// <summary>The side conceding.</summary>
        public Kingdom Loser { get; }

        /// <summary>One-off indemnity in denars, paid by the loser on signing.</summary>
        public int IndemnityGold { get; set; }

        /// <summary>The loser frees every captured hero of the winner's kingdom.</summary>
        public bool ReleasePrisoners { get; set; }

        /// <summary>The loser becomes a tributary: recurring payment, enforced by treaty.</summary>
        public bool ImposeTributaryPact { get; set; }

        /// <summary>Per-period amount when <see cref="ImposeTributaryPact"/> is set.</summary>
        public int TributePerPeriod { get; set; }

        /// <summary>
        /// The loser submits: it becomes the winner's vassal, owing troops, tribute and its
        /// foreign policy. The top rung of the ladder, and the thing that makes the winner a
        /// hegemon.
        /// </summary>
        public bool ImposeVassalage { get; set; }

        /// <summary>Fiefs passing from loser to winner. Requires a territorial claim.</summary>
        public List<Settlement> FiefsCeded { get; } = new List<Settlement>();

        public PeaceTerms(Kingdom winner, Kingdom loser)
        {
            Winner = winner;
            Loser = loser;
        }

        /// <summary>Nothing changes hands. Always available to both sides.</summary>
        public bool IsWhitePeace
            => IndemnityGold <= 0 && !ReleasePrisoners && !ImposeTributaryPact
               && !ImposeVassalage && FiefsCeded.Count == 0;

        public override string ToString()
        {
            if (IsWhitePeace) return "white peace";

            var parts = new List<string>();
            if (FiefsCeded.Count > 0)
            {
                var names = new List<string>();
                for (var i = 0; i < FiefsCeded.Count; i++) names.Add(FiefsCeded[i].Name.ToString());
                parts.Add("cede " + string.Join(", ", names));
            }
            if (ImposeVassalage) parts.Add("submit as a vassal"
                                          + (TributePerPeriod > 0
                                              ? " paying " + TributePerPeriod + " per period"
                                              : ""));
            if (ImposeTributaryPact) parts.Add("tributary pact at " + TributePerPeriod + " per period");
            if (IndemnityGold > 0) parts.Add("indemnity of " + IndemnityGold);
            if (ReleasePrisoners) parts.Add("release prisoners");

            return string.Join("; ", parts);
        }
    }
}
