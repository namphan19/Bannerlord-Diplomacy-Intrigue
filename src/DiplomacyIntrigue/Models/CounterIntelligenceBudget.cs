using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// What a realm spends each week on hunting foreign agents (design 03 §3, step 3.3).
    ///
    /// **One per realm, paid by whoever rules it** - the lead's call of 2026-09-25 (design 03 §9,
    /// decision 9). It belongs to the kingdom, not to the ruler: a new ruler inherits the standing
    /// order and pays it from their own purse, the purse that also pays the realm's troops, so the
    /// trade-off §3 asks for is between the same denars.
    ///
    /// **Stored**, because what was actually paid last week cannot be derived from anything in the
    /// world. The counter-intelligence it buys is derived from <see cref="LastWeekSpent"/> - money
    /// paid, not money promised - so a ruler with an empty purse and a large budget defends at
    /// what the purse could cover.
    ///
    /// Save ids are frozen. This type is definer class id 17; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class CounterIntelligenceBudget
    {
        [SaveableProperty(1)] public Kingdom Kingdom { get; private set; }

        /// <summary>Denars the realm means to spend each week.</summary>
        [SaveableProperty(2)] public int WeeklyBudget { get; private set; }

        /// <summary>What the ruler actually paid at the last weekly upkeep. What §3's gold term reads.</summary>
        [SaveableProperty(3)] public int LastWeekSpent { get; private set; }

        internal CounterIntelligenceBudget() { }

        internal CounterIntelligenceBudget(Kingdom kingdom)
        {
            Kingdom = kingdom;
        }

        internal void SetBudget(int weekly) => WeeklyBudget = weekly < 0 ? 0 : weekly;

        internal void RecordWeek(int spent) => LastWeekSpent = spent < 0 ? 0 : spent;

        /// <summary>Nothing ordered and nothing paid: the record carries no information and can go.</summary>
        public bool IsEmpty => WeeklyBudget <= 0 && LastWeekSpent <= 0;

        public override string ToString()
            => (Kingdom == null ? "?" : Kingdom.Name.ToString()) + ": " + WeeklyBudget + "/week, paid "
               + LastWeekSpent + " last week";
    }
}
