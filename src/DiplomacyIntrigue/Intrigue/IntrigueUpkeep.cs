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
        /// The conditions that renew grievances, then the civil wars' leaders buying houses, then
        /// the AI rulers making amends (design 09 C1) - after the scan, so a ruler answers the court
        /// as it stands this week. Each step has its own try: none throws past here (CLAUDE.md §3).
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

            try
            {
                Amends.AiWeekly(state);
            }
            catch (Exception ex)
            {
                Log.Error("Intrigue", "Weekly amends failed.", ex);
            }
        }
    }
}
