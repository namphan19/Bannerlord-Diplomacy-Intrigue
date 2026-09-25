using System;
using DiplomacyIntrigue.Core;

namespace DiplomacyIntrigue.Espionage
{
    /// <summary>
    /// The espionage pillar's weekly upkeep, as one list. The campaign's weekly handler, `ai_week`
    /// and `test_network_week` all run this and nothing else, so no diagnostic can run part of a
    /// week and read it as the whole (CLAUDE.md §1, "a diagnostic that drives only part of a tick
    /// lies convincingly").
    /// </summary>
    public static class EspionageUpkeep
    {
        /// <summary>
        /// Counter-intelligence budgets first, then the networks: a network growing this week meets
        /// the defence paid for this week. Each step has its own try, so one failing does not stop
        /// the other - and none of them throws past here (CLAUDE.md §3).
        /// </summary>
        public static void Weekly(ModState state)
        {
            if (state == null) return;

            try
            {
                CounterIntelligence.WeeklyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Weekly counter-intelligence upkeep failed.", ex);
            }

            try
            {
                SpyNetworks.WeeklyTick(state);
            }
            catch (Exception ex)
            {
                Log.Error("Espionage", "Weekly network upkeep failed.", ex);
            }
        }
    }
}
