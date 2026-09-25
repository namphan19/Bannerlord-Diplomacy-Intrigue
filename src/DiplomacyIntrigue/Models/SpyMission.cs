using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace DiplomacyIntrigue.Models
{
    /// <summary>
    /// One covert operation, from the day it is paid for to the day it resolves and a while after
    /// (design 03 §2, §4).
    ///
    /// Kept after it resolves, for two readers. A successful ScoutArmies or ReadCourt goes on
    /// revealing for days, and that is read from the mission itself - "revealed until" is its
    /// resolution date plus a duration, never a second record that could disagree with it. And the
    /// operations list (design 03 §7) shows what happened recently. Resolved missions are dropped
    /// once neither reader needs them (<c>Missions.DailyTick</c>).
    ///
    /// Save ids are frozen. This type is definer class id 16; next free there is 17.
    /// </summary>
    public sealed class SpyMission
    {
        /// <summary>The clan whose network runs it.</summary>
        [SaveableProperty(1)] public Clan Owner { get; private set; }

        /// <summary>The realm it works against.</summary>
        [SaveableProperty(2)] public Kingdom Target { get; private set; }

        [SaveableProperty(3)] public SpyMissionType Type { get; private set; }

        /// <summary>The lord a BribeLord or Assassinate is aimed at; null for the others.</summary>
        [SaveableProperty(4)] public Hero TargetHero { get; private set; }

        /// <summary>The town or castle a SabotageGarrison or SpreadDissent is aimed at; null for the others.</summary>
        [SaveableProperty(5)] public Settlement TargetSettlement { get; private set; }

        [SaveableProperty(6)] public CampaignTime Launched { get; private set; }

        [SaveableProperty(7)] public CampaignTime ResolvesOn { get; private set; }

        /// <summary>Paid up front, and never refunded (design 03 §2, §7).</summary>
        [SaveableProperty(8)] public int GoldPaid { get; private set; }

        /// <summary>Pending until it resolves.</summary>
        [SaveableProperty(9)] public MissionOutcome Outcome { get; private set; }

        [SaveableProperty(10)] public CampaignTime ResolvedOn { get; private set; }

        /// <summary>
        /// The handler who ran it, recorded at launch: an exposure captures this hero, and the
        /// operations list names them even after the network changes hands.
        /// </summary>
        [SaveableProperty(11)] public Hero Handler { get; private set; }

        internal SpyMission() { }

        internal SpyMission(Clan owner, Kingdom target, SpyMissionType type, Hero handler,
                            Hero targetHero, Settlement targetSettlement, int gold, int days)
        {
            Owner = owner;
            Target = target;
            Type = type;
            Handler = handler;
            TargetHero = targetHero;
            TargetSettlement = targetSettlement;
            GoldPaid = gold;
            Launched = CampaignTime.Now;
            ResolvesOn = CampaignTime.DaysFromNow(days);
            Outcome = MissionOutcome.Pending;
        }

        public bool IsPending => Outcome == MissionOutcome.Pending;

        internal void Resolve(MissionOutcome outcome)
        {
            Outcome = outcome;
            ResolvedOn = CampaignTime.Now;
        }

        public override string ToString()
            => Type + " by " + (Owner == null ? "?" : Owner.Name.ToString())
               + " in " + (Target == null ? "?" : Target.Name.ToString())
               + (TargetHero != null ? " on " + TargetHero.Name : "")
               + (TargetSettlement != null ? " at " + TargetSettlement.Name : "")
               + ", " + Outcome;
    }
}
