using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One thing a clan holds against another clan. The court's memory, and the only part of
    /// Phase 2 that is stored rather than derived.
    ///
    /// A grievance is **event-sourced**: it records that something happened, on a date, with a
    /// weight. Nothing about it is recomputed from world state, which is what separates it
    /// from relation - relation is how a clan feels now, a grievance is what it has not
    /// forgotten. Loyalty (Intrigue/LoyaltyModel) reads these and is itself derived, so the
    /// save stays small and a balance change takes effect on campaigns already in progress.
    ///
    /// **Clan to clan, not clan to kingdom**, which design 02 §1 originally implied. The
    /// kingdom version was rejected because the lead's 2026-09-23 brief needs clans to hold
    /// things against *each other*, not only against the crown, and a grievance against the
    /// crown is simply one whose <see cref="Target"/> is the ruling clan. One shape covers
    /// both; two would have meant two ledgers that could disagree about the same slight,
    /// which is the mistake CasusBelli.Resolve exists to prevent.
    ///
    /// Save ids are frozen. This type is definer class id 10; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class Grievance
    {
        /// <summary>The clan that feels wronged.</summary>
        [SaveableProperty(1)] public Clan Holder { get; private set; }

        /// <summary>The clan they blame. The ruling clan, for anything aimed at the crown.</summary>
        [SaveableProperty(2)] public Clan Target { get; private set; }

        [SaveableProperty(3)] public GrievanceType Type { get; private set; }

        /// <summary>
        /// What it still weighs today, after decay. Starts at the type's weight in
        /// <see cref="Intrigue.IntrigueConstants"/> and is removed from the ledger at zero.
        /// </summary>
        [SaveableProperty(4)] public float Weight { get; private set; }

        /// <summary>
        /// When it happened. Kept even though decay already encodes age, because the court UI
        /// has to be able to say *when* - a ledger that only shows a decayed number cannot
        /// explain itself, and the player is being asked to manage these.
        /// </summary>
        [SaveableProperty(5)] public CampaignTime Created { get; private set; }

        /// <summary>
        /// When the crown last made amends for it (design 09 C1). Meaningless while
        /// <see cref="Answers"/> is 0, which is how a record from before C1 loads: never answered,
        /// the correct reading, so no schema bump.
        /// </summary>
        [SaveableProperty(6)] public CampaignTime AnsweredOn { get; private set; }

        /// <summary>How many times amends were made for it. 0 for a grievance never answered.</summary>
        [SaveableProperty(7)] public int Answers { get; private set; }

        internal Grievance() { }

        internal Grievance(Clan holder, Clan target, GrievanceType type, float weight)
        {
            Holder = holder;
            Target = target;
            Type = type;
            Weight = weight;
            Created = CampaignTime.Now;
        }

        /// <summary>
        /// Nothing left to feel. The registry drops it on the next daily tick unless it is still
        /// remembered as answered (<see cref="WasAnsweredWithin"/>): a repeat of the wrong, and a
        /// second apology, are priced from that memory (design 09 §1).
        /// </summary>
        public bool IsSpent => Weight <= 0f;

        /// <summary>
        /// Answered at least once, less than <paramref name="years"/> ago. The window is the
        /// registry's constant, passed in, so this layer reads nothing above it.
        /// </summary>
        public bool WasAnsweredWithin(float years) => Answers > 0 && AnsweredOn.ElapsedYearsUntilNow < years;

        /// <summary>
        /// The crown made amends: the weight goes, the memory of having answered stays. The
        /// registry decides whether it may; this only records that it happened.
        /// </summary>
        internal void Answer()
        {
            Weight = 0f;
            AnsweredOn = CampaignTime.Now;
            Answers++;
        }

        public bool Is(Clan holder, Clan target) => Holder == holder && Target == target;

        /// <summary>
        /// Upkeep only. A grievance nobody compounds fades; one that keeps being renewed
        /// never clears, which is the whole point of a court that remembers.
        /// </summary>
        internal void Decay(float amount)
        {
            if (amount <= 0f) return;
            Weight -= amount;
            if (Weight < 0f) Weight = 0f;
        }

        /// <summary>
        /// A repeat of the same slight renews rather than stacks - the same rule claims use
        /// (ClaimRegistry). Ten small refusals should not add up to a civil war on their own;
        /// a court that is slighted repeatedly stays angry, it does not grow infinitely angry.
        /// </summary>
        internal void Renew(float weight)
        {
            if (weight > Weight) Weight = weight;
            Created = CampaignTime.Now;
        }

        public override string ToString()
            => (Holder == null ? "?" : Holder.Name.ToString())
               + " vs " + (Target == null ? "?" : Target.Name.ToString())
               + ": " + Type + " " + Weight.ToString("0.0");
    }
}
