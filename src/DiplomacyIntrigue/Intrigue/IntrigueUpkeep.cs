using System;
using DiplomacyIntrigue.Core;

namespace DiplomacyIntrigue.Intrigue
{
    /// <summary>
    /// The court pillar's weekly upkeep, as one list. The campaign's weekly handler and
    /// <c>diplomacy.ai_week</c> both run this and nothing else - until 2026-09-26 `ai_week` kept its
    /// own copy of the list, which is how a pass gets left out of a diagnostic and a frozen value
    /// gets blamed on the system (CLAUDE.md §1). The same shape as <c>EspionageUpkeep</c>.
    /// </summary>
    public static class IntrigueUpkeep
    {
        /// <summary>
        /// The AI rulers' court acts for the day, before the internal-war check (design 09 D17): seats
        /// whose holders fell away are emptied first, then each AI realm under threat takes one act -
        /// amends if it has one to make, otherwise a seat (C2). One act a day per realm, so a crown in
        /// crisis spends at the pace a player clicking through the Court tab would, not all at once.
        /// The campaign's daily handler and <c>diplomacy.tick_days</c> both run this.
        /// </summary>
        public static void AiCourtDaily(ModState state)
        {
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                Offices.DailyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Emptying fallen court seats failed.", ex);
            }

            foreach (var kingdom in TaleWorlds.CampaignSystem.Kingdom.All)
            {
                if (!kingdom.IsRealm()) continue;
                if (kingdom.Leader == null || kingdom.Leader == TaleWorlds.CampaignSystem.Hero.MainHero) continue;

                try
                {
                    if (!Amends.TryAi(state, kingdom)) Offices.TryAi(state, kingdom);
                }
                catch (Exception ex)
                {
                    Log.Error("Intrigue", "The court acts of " + kingdom.Name + " failed.", ex);
                }
            }
        }

        /// <summary>
        /// The conditions that renew grievances, then the civil wars' leaders buying houses. Each
        /// step has its own try: none throws past here (CLAUDE.md §3). The AI rulers' amends were
        /// here until the lead moved them to the daily upkeep (design 09 §7, D17).
        /// </summary>
        public static void Weekly(ModState state)
        {
            if (state == null || !Settings.Current.EnableIntrigue) return;

            try
            {
                GrievanceSources.WeeklyScan(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Weekly grievance scan failed.", ex);
            }

            try
            {
                SideChange.WeeklyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Weekly side changes failed.", ex);
            }
        }
    }
}
