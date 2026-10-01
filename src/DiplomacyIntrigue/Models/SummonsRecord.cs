using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One live summons: a hegemon's ruler has some of a vassal's war parties marching under their
    /// command, inside one war, until a date (design 04 §5.2a, story 1.10c).
    ///
    /// **Stored**, because nothing in the world can rebuild it. The parties are in the patron's
    /// army - vanilla saves that itself, so it needs no help - but *why* they are there, which war
    /// they may fight in and the day they go home are decisions this record is the only account
    /// of. Releasing a party that came to the army by some other road is exactly the bug a
    /// derived rule would cause, so the parties are named by count against this record rather
    /// than by asking whose kingdom they belong to.
    ///
    /// **Dropped when it ends.** The cooldown lives on the link (<see cref="Treaty.LastSummonedOn"/>),
    /// so nothing needs a finished record kept, and the log lines plus the telemetry carry the
    /// history for a balance run.
    ///
    /// Save ids are frozen. This type is definer class id 19; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class SummonsRecord
    {
        /// <summary>The hegemon whose ruler gave the order.</summary>
        [SaveableProperty(1)] public Kingdom Patron { get; private set; }

        /// <summary>The kingdom whose war parties were taken.</summary>
        [SaveableProperty(2)] public Kingdom Vassal { get; private set; }

        /// <summary>The patron's ruler at the time of the order. Named so the diagnostic can say who gave it.</summary>
        [SaveableProperty(3)] public Hero Summoner { get; private set; }

        /// <summary>
        /// The other side of the obligation war this summons is for (R3). A summons names its war:
        /// the one war the vassal is serving its patron in. The enemy's own war record is the test
        /// for whether it is still running, so the war is named by its other party rather than by a
        /// reference that could outlive it.
        /// </summary>
        [SaveableProperty(4)] public Kingdom Enemy { get; private set; }

        [SaveableProperty(5)] public CampaignTime IssuedOn { get; private set; }

        [SaveableProperty(6)] public CampaignTime EndsOn { get; private set; }

        /// <summary>
        /// How many parties were taken. The release walks the army and lets go of up to this many
        /// of the vassal's own, which is what keeps a lord who joined the same army by another
        /// road from being marched home by somebody else's order.
        /// </summary>
        [SaveableProperty(7)] public int PartyCount { get; private set; }

        // The save system rehydrates instances without running a constructor.
        internal SummonsRecord() { }

        internal SummonsRecord(Kingdom patron, Kingdom vassal, Hero summoner, Kingdom enemy, int parties)
        {
            Patron = patron;
            Vassal = vassal;
            Summoner = summoner;
            Enemy = enemy;
            IssuedOn = CampaignTime.Now;
            PartyCount = parties;
        }

        /// <summary>Sets the day the parties go home. Separate from the constructor because the answer is priced first.</summary>
        internal void SetEnd(CampaignTime endsOn) => EndsOn = endsOn;

        public bool Is(Kingdom patron, Kingdom vassal) => Patron == patron && Vassal == vassal;

        public float DaysLeft => EndsOn <= CampaignTime.Now ? 0f : (float)(EndsOn - CampaignTime.Now).ToDays;

        public override string ToString()
            => (Patron == null ? "?" : Patron.Name.ToString()) + " calls up "
               + (Vassal == null ? "?" : Vassal.Name.ToString()) + " against "
               + (Enemy == null ? "?" : Enemy.Name.ToString()) + ": " + PartyCount + " party(ies), "
               + DaysLeft.ToString("0") + " days left";
    }
}