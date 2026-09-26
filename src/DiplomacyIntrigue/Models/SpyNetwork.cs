using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One clan's network of agents inside another realm (design 03 §1).
    ///
    /// **Owned by a clan, not a kingdom** - the lead's call of 2026-09-25 (design 03 §9, decision
    /// 5). Keyed by kingdom pairs, as §1 first had it, a player who serves a king would have no
    /// espionage for most of a campaign. The target stays a kingdom: agents work a realm, not a
    /// house, and an exposure is a matter between realms (§5).
    ///
    /// **Stored**, unlike almost everything Phase 2 derives: strength is an asset built up week by
    /// week out of gold that was actually spent, and nothing in the world could rebuild it.
    ///
    /// Save ids are frozen. This type is definer class id 15; the next free ids are kept in CLAUDE.md §3 only.
    /// </summary>
    public sealed class SpyNetwork
    {
        /// <summary>The clan that pays for the network and whose hero runs it.</summary>
        [SaveableProperty(1)] public Clan Owner { get; private set; }

        /// <summary>The realm the agents work in.</summary>
        [SaveableProperty(2)] public Kingdom Target { get; private set; }

        /// <summary>0-100, and never above what the handler can hold (design 03 §1).</summary>
        [SaveableProperty(3)] public float Strength { get; private set; }

        /// <summary>
        /// The hero of the owning clan who runs it, stationed in the target realm. Null when the
        /// network has none: it then neither costs nor grows, and decays.
        /// </summary>
        [SaveableProperty(4)] public Hero Handler { get; private set; }

        /// <summary>Denars the owner means to spend on it each week.</summary>
        [SaveableProperty(5)] public int WeeklyBudget { get; private set; }

        [SaveableProperty(6)] public CampaignTime Established { get; private set; }

        /// <summary>
        /// What last week's upkeep did to strength, decay included, for the trend the network
        /// map shows (design 03 §7). Kept rather than recomputed because it depends on what was
        /// actually paid, which the purse no longer shows.
        /// </summary>
        [SaveableProperty(7)] public float LastWeekChange { get; private set; }

        /// <summary>Denars actually paid at the last weekly upkeep - less than the budget when the purse was short.</summary>
        [SaveableProperty(8)] public int LastWeekSpent { get; private set; }

        internal SpyNetwork() { }

        internal SpyNetwork(Clan owner, Kingdom target)
        {
            Owner = owner;
            Target = target;
            Established = CampaignTime.Now;
        }

        public bool Is(Clan owner, Kingdom target) => Owner == owner && Target == target;

        internal void SetHandler(Hero handler) => Handler = handler;

        internal void SetBudget(int weekly) => WeeklyBudget = weekly < 0 ? 0 : weekly;

        /// <summary>Moves strength, clamped to 0 and to <paramref name="ceiling"/>.</summary>
        internal void Change(float amount, float ceiling)
        {
            var next = Strength + amount;
            if (next > ceiling) next = ceiling;
            if (next < 0f) next = 0f;
            Strength = next;
        }

        internal void RecordWeek(float change, int spent)
        {
            LastWeekChange = change;
            LastWeekSpent = spent;
        }

        public override string ToString()
            => (Owner == null ? "?" : Owner.Name.ToString()) + " in "
               + (Target == null ? "?" : Target.Name.ToString()) + ": "
               + Strength.ToString("0.0") + ", handler "
               + (Handler == null ? "none" : Handler.Name.ToString())
               + ", " + WeeklyBudget + "/week";
    }
}
