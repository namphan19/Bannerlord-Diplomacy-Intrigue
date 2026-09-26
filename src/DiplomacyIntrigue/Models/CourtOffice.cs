using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One seat at a realm's court and the hero who holds it (design 09 C2). A seat is two things
    /// at once: the realm's voice in one political skill - its holder is the statecraft actor for
    /// that portfolio - and royal favour for the holder's house. One record, so the two cannot
    /// disagree about who sits where; design 08 §8 and design 09 C2 described the same seat.
    ///
    /// An empty seat has no record: the ruling house's best speaks, as before C2.
    ///
    /// Save ids are frozen. This type is definer class id 18; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class CourtOffice
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }

        /// <summary>Never <see cref="Portfolio.Ruler"/>: Leadership stays the ruler's own.</summary>
        [SaveableProperty(2)] public Portfolio Seat { get; private set; }

        [SaveableProperty(3)] public Hero Holder { get; private set; }

        /// <summary>When the holder was appointed, for the Court tab and telemetry.</summary>
        [SaveableProperty(4)] public CampaignTime Since { get; private set; }

        internal CourtOffice() { }

        internal CourtOffice(Kingdom kingdom, Portfolio seat, Hero holder)
        {
            Kingdom = kingdom;
            Seat = seat;
            Holder = holder;
            Since = CampaignTime.Now;
        }

        public override string ToString()
            => (Kingdom == null ? "?" : Kingdom.Name.ToString()) + " " + Seat + ": "
               + (Holder == null ? "?" : Holder.Name.ToString());
    }
}
