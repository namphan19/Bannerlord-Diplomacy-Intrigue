using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace DiplomacyIntrigue.Models
{
    /// <summary>One tickable line of a peace package, in the terms the model stores.</summary>
    public enum PeaceTermKind
    {
        Captives,
        Indemnity,
        Tribute,
        Land,
        Dissolution,
        Submission,
    }

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
        /// <summary>
        /// Demand kinds that cannot share one package. Submission *is* the tribute (the oath
        /// carries its own payment) and the top rung has two faces but only ever one at a
        /// time, so ticking one of a pair must untick the other. The rule lives here rather
        /// than in the negotiation screen so there is one answer to "what conflicts" for
        /// every caller, UI or AI.
        /// </summary>
        public static bool AreExclusive(PeaceTermKind a, PeaceTermKind b)
            => AreExclusive(a, b, out _);

        /// <summary><see cref="AreExclusive(PeaceTermKind, PeaceTermKind)"/>, with the reason a refusal gives.</summary>
        public static bool AreExclusive(PeaceTermKind a, PeaceTermKind b, out string reason)
        {
            reason = null;
            if (a == b) return false;
            if (Pair(a, b, PeaceTermKind.Submission, PeaceTermKind.Tribute))
                reason = "A vassalage carries its own tribute; a separate tributary pact would charge twice.";
            else if (Pair(a, b, PeaceTermKind.Submission, PeaceTermKind.Dissolution))
                reason = "One package cannot both submit a kingdom and break up its sphere.";
            return reason != null;
        }

        private static bool Pair(PeaceTermKind a, PeaceTermKind b, PeaceTermKind x, PeaceTermKind y)
            => (a == x && b == y) || (a == y && b == x);

        /// <summary>Whether this package carries a line of the given kind.</summary>
        public bool Includes(PeaceTermKind kind)
        {
            switch (kind)
            {
                case PeaceTermKind.Captives: return ReleasePrisoners;
                case PeaceTermKind.Indemnity: return IndemnityGold > 0;
                case PeaceTermKind.Tribute: return ImposeTributaryPact;
                case PeaceTermKind.Land: return FiefsCeded.Count > 0;
                case PeaceTermKind.Dissolution: return DissolveHegemony;
                case PeaceTermKind.Submission: return ImposeVassalage;
                default: return false;
            }
        }

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

        /// <summary>
        /// The loser keeps its throne and frees every kingdom that answers to it. Demandable
        /// only of a hegemon, and the only thing a hegemon has to give that is neither its
        /// territory nor itself - it cannot submit while it still holds vassals.
        ///
        /// Nobody inherits the sphere: the freed kingdoms become independent, not the winner's.
        /// They also stay in their own wars, which is what gives them a reason to kneel to
        /// somebody later. See docs/design/04-hegemony.md §12.4.3.
        /// </summary>
        public bool DissolveHegemony { get; set; }

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
               && !ImposeVassalage && !DissolveHegemony && FiefsCeded.Count == 0;

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
            if (DissolveHegemony) parts.Add("release every vassal");
            if (ImposeTributaryPact) parts.Add("tributary pact at " + TributePerPeriod + " per period");
            if (IndemnityGold > 0) parts.Add("indemnity of " + IndemnityGold);
            if (ReleasePrisoners) parts.Add("release prisoners");

            return string.Join("; ", parts);
        }
    }
}
