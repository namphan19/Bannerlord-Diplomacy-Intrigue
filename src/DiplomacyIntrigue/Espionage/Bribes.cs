using DiplomacyIntrigue.Core;
using DiplomacyIntrigue.Models;
using TaleWorlds.CampaignSystem;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>
    /// Whether a house has taken foreign gold (design 03 §2 BribeLord, §6; step 3.5). The one
    /// resolver for it: loyalty reads it for its "foreign gold" term, and the internal war reads
    /// it to put the house on the rising's side. Two readers asking the same question of two
    /// different rules would be the mistake CLAUDE.md §3 names.
    ///
    /// **Derived from the mission record, never stored.** A bribe is a successful BribeLord no
    /// older than <see cref="EspionageConstants.BribeWindowDays"/>, the same way a reveal is read
    /// from the ScoutArmies or ReadCourt that bought it. There is no "bought" flag to fall out of
    /// step with the operation, and no new save data: the missions are kept as long as this reads
    /// them (<see cref="Missions.KeepDays"/>).
    ///
    /// **The gold binds the head who took it.** A house stays bought while the lord who was paid
    /// still leads it and it still sits in the realm the bribe was aimed at. The heir took nothing,
    /// and a house that has left for another crown owes the first crown nothing to betray. The
    /// alternative, a bribe that follows the house whoever leads it, was not taken (a call made in
    /// building, not by the lead): it would let a
    /// payment outlive everybody who knew about it.
    ///
    /// Several bribes on one house do not add up. It is bought or it is not; a second purse buys
    /// only a longer window.
    /// </summary>
    public static class Bribes
    {
        /// <summary>The live bribe on <paramref name="clan"/>, the most recent if there are several; null if none.</summary>
        public static SpyMission On(ModState state, Clan clan)
        {
            if (state == null || clan?.Leader == null || clan.Kingdom == null) return null;

            SpyMission latest = null;
            for (var i = 0; i < state.SpyMissions.Count; i++)
            {
                var m = state.SpyMissions[i];
                if (m.Type != SpyMissionType.BribeLord || m.Outcome != MissionOutcome.Success) continue;
                if (m.TargetHero != clan.Leader || !m.TargetHero.IsAlive) continue;
                if (m.Target != clan.Kingdom) continue;
                if ((CampaignTime.Now - m.ResolvedOn).ToDays >= EspionageConstants.BribeWindowDays) continue;
                if (latest == null || m.ResolvedOn > latest.ResolvedOn) latest = m;
            }
            return latest;
        }

        public static bool IsBought(ModState state, Clan clan) => On(state, clan) != null;

        /// <summary>Who paid, for the owner's own report and the log. Never shown to the victim.</summary>
        public static Clan BuyerOf(ModState state, Clan clan) => On(state, clan)?.Owner;

        /// <summary>Days left before the bribe runs out; 0 when the house is not bought.</summary>
        public static float DaysLeft(ModState state, Clan clan)
        {
            var m = On(state, clan);
            if (m == null) return 0f;
            var left = EspionageConstants.BribeWindowDays - (float)(CampaignTime.Now - m.ResolvedOn).ToDays;
            return left < 0f ? 0f : left;
        }
    }
}
